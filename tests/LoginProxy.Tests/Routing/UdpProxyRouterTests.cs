using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using LoginProxy.Routing;
using LoginProxy.Sessions;
using System.Linq;
using System.Net;

namespace LoginProxy.Tests.Routing
{
    public class UdpProxyRouterTests
    {
        private static UdpProxyRouter MakeRouter(
            out ClientSessionStore store,
            out Mock<IUdpProxyPipeline> pipeline)
        {
            var logger = Mock.Of<ILogger<UdpProxyRouter>>();

            pipeline = new Mock<IUdpProxyPipeline>();
            pipeline
                .Setup(p => p.InvokeAsync(
                    It.IsAny<PacketContext>(),
                    It.IsAny<UdpPacket>()))
                .Returns(Task.CompletedTask);

            store = new ClientSessionStore();

            // Bind to port 0 so the OS assigns a free ephemeral port for the listener.
            var settings = Options.Create(new ProxySettings
            {
                ListenHost = "127.0.0.1",
                ListenPort = 0,                  // <-- avoid "Address already in use"
                LoginServerHost = "127.0.0.1",
                LoginServerPort = 5998
            });

            return new UdpProxyRouter(
                logger,
                pipeline.Object,
                settings,
                store);
        }

        [Fact]
        public async Task Creates_Session_On_First_Client_Packet()
        {
            using var router = MakeRouter(out var sessions, out var pipeline);
            var packet = PacketFactory.MakeClientPacket(new byte[] { 0x01 });

            await router.RouteAsync(new PacketContext(), packet);

            var s = sessions.GetAllSessions().Single();

            // Client endpoint present (address must match the sender)
            Assert.Equal(packet.RemoteEndpoint.Address, s.ClientEndpoint.Address);
            Assert.InRange(s.ClientEndpoint.Port, 1, 65535);

            // Server endpoint must be assigned eagerly by router (and use an ephemeral port)
            Assert.NotNull(s.ServerEndpoint);
            Assert.Equal(IPAddress.Parse("127.0.0.1"), s.ServerEndpoint!.Address);
            Assert.InRange(s.ServerEndpoint.Port, 1, 65535);
            Assert.NotEqual(0, s.ServerEndpoint.Port);

            // Pipeline invoked exactly once
            pipeline.Verify(p => p.InvokeAsync(It.IsAny<PacketContext>(), It.IsAny<UdpPacket>()), Times.Once);
        }

        [Fact]
        public async Task Reuses_Session_On_Subsequent_Client_Packets()
        {
            using var router = MakeRouter(out var sessions, out var pipeline);

            var first = PacketFactory.MakeClientPacket(new byte[] { 0xAA });
            var second = new UdpPacket(new byte[] { 0xBB }, first.RemoteEndpoint); // same client endpoint

            await router.RouteAsync(new PacketContext(), first);
            await router.RouteAsync(new PacketContext(), second);

            // Still exactly one session
            Assert.Single(sessions.GetAllSessions());

            // Pipeline invoked for both packets
            pipeline.Verify(p => p.InvokeAsync(It.IsAny<PacketContext>(), It.IsAny<UdpPacket>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Server_Origin_Packet_Does_Not_Create_New_Session()
        {
            using var router = MakeRouter(out var sessions, out var pipeline);

            // Seed a session
            var clientPkt = PacketFactory.MakeClientPacket(new byte[] { 0x01 });
            await router.RouteAsync(new PacketContext(), clientPkt);
            Assert.Single(sessions.GetAllSessions());

            var existing = sessions.GetAllSessions().Single();

            // Simulate a packet coming from the login server
            var serverPkt = new UdpPacket(new byte[] { 0x02 }, router.LoginServerEndPoint);

            await router.RouteAsync(new PacketContext(), serverPkt);

            // Session count unchanged; pipeline called twice total
            Assert.Single(sessions.GetAllSessions());
            pipeline.Verify(p => p.InvokeAsync(It.IsAny<PacketContext>(), It.IsAny<UdpPacket>()), Times.Exactly(2));

            // Existing session still holds both endpoints
            Assert.NotNull(existing.ClientEndpoint);
            Assert.NotNull(existing.ServerEndpoint);
        }
    }
}
