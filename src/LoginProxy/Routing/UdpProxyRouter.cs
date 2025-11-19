using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using LoginProxy.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoginProxy.Routing
{
    public class UdpProxyRouter : IUdpProxyRouter, IDisposable
    {
        private readonly ILogger<UdpProxyRouter> _logger;
        private readonly IUdpProxyPipeline _pipeline;
        private readonly ProxySettings _settings;
        private readonly ClientSessionStore _sessions;

        private readonly Socket _socket;

        public IPEndPoint ListeningEndPoint { get; }
        public IPEndPoint LoginServerEndPoint { get; }

        private bool _disposed;

        public UdpProxyRouter(
            ILogger<UdpProxyRouter> logger,
            IUdpProxyPipeline pipeline,
            IOptions<ProxySettings> settings,
            ClientSessionStore sessions)
        {
            _logger = logger;
            _pipeline = pipeline;
            _settings = settings.Value;
            _sessions = sessions;

            ListeningEndPoint = new IPEndPoint(
                IPAddress.Parse(_settings.ListenHost),
                _settings.ListenPort);

            LoginServerEndPoint = new IPEndPoint(
                IPAddress.Parse(_settings.LoginServerHost),
                _settings.LoginServerPort);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(ListeningEndPoint);
        }

        public async Task RouteAsync(PacketContext context, UdpPacket packet)
        {
            var remote = packet.RemoteEndpoint;
            var fromLoginServer =
                remote.Address.Equals(LoginServerEndPoint.Address) &&
                remote.Port == LoginServerEndPoint.Port;

            if (!fromLoginServer)
            {
                // Packet from client → login server
                var session = _sessions.GetOrCreate(remote);

                // Eagerly assign server endpoint so tests & routing have it
                if (session.ServerEndpoint == null)
                {
                    session.UpdateServerEndpoint(LoginServerEndPoint);
                }

                await _pipeline.InvokeAsync(context, packet);

                await SendToAsync(packet.Data, session.ServerEndpoint!);
            }
            else
            {
                // Packet from login server → client
                var session = _sessions.GetByServerEndpoint(LoginServerEndPoint);
                if (session == null)
                {
                    _logger.LogWarning(
                        "Dropping server packet from {Remote} – no matching client session.",
                        remote);
                    return;
                }

                await _pipeline.InvokeAsync(context, packet);

                await SendToAsync(packet.Data, session.ClientEndpoint);
            }
        }

        private async Task SendToAsync(byte[] data, IPEndPoint endpoint)
        {
            try
            {
                await _socket.SendToAsync(
                    new ArraySegment<byte>(data),
                    SocketFlags.None,
                    endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UDP send failure to {Endpoint}", endpoint);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _socket.Dispose(); } catch { /* ignore */ }
        }
    }
}
