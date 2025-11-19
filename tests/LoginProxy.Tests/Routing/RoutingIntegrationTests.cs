using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Packets.Models;
using LoginProxy.Pipeline;
using LoginProxy.Routing;
using LoginProxy.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoginProxy.Tests.Routing
{
    /// <summary>
    /// Full integration test: Router + Pipeline + (Deserialize → Rewrite → Serialize).
    /// The packet's encrypted login payload is rewritten, stays encrypted, and the session gets created.
    /// </summary>
    [Trait("Category", "Integration")]
    public class UdpProxyRouterIntegrationTests
    {
        // ---------- Minimal concrete pipeline that executes middlewares in order ----------
        private sealed class TestPipeline : IUdpProxyPipeline
        {
            private readonly IUdpProxyMiddleware[] _middlewares;

            public TestPipeline(params IUdpProxyMiddleware[] middlewares)
            {
                _middlewares = middlewares;
            }

            public Task InvokeAsync(PacketContext context, UdpPacket packet)
            {
                UdpProxyDelegate terminal = static (ctx, pkt) => Task.CompletedTask;

                // Build the chain in reverse
                for (int i = _middlewares.Length - 1; i >= 0; i--)
                {
                    var current = _middlewares[i];
                    var next = terminal;
                    terminal = (ctx, pkt) => current.InvokeAsync(ctx, pkt, next);
                }

                return terminal(context, packet);
            }
        }

        // Context key (if you prefer Items bag); alternatively you can use context.LoginInfo
        private static readonly object LoginInfoKey = new();

        // ---------- Middleware: DES decrypt into context.Items ----------
        private sealed class DesDeserializeMiddleware : IUdpProxyMiddleware
        {
            private readonly ProxySettings _settings;
            public DesDeserializeMiddleware(IOptions<ProxySettings> opt) => _settings = opt.Value;

            public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
            {
                var (user, pass) = DesDecryptCredentials(packet.Data, _settings.DesKey, _settings.DesIV);
                context.Items[LoginInfoKey] = new LoginInfo { Username = user, Password = pass };
                return next(context, packet);
            }
        }

        // ---------- Middleware: rewrite from settings.AccountRewrites (if present) ----------
        private sealed class RewriteMiddleware : IUdpProxyMiddleware
        {
            private readonly ProxySettings _settings;
            public RewriteMiddleware(IOptions<ProxySettings> opt) => _settings = opt.Value;

            public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
            {
                if (context.Items.TryGetValue(LoginInfoKey, out var obj) && obj is LoginInfo li)
                {
                    if (_settings.AccountRewrites.TryGetValue(li.Username, out var rw))
                    {
                        li.Username = rw.Username;
                        li.Password = rw.Password;
                    }
                }
                return next(context, packet);
            }
        }

        // ---------- Middleware: serialize (DES encrypt) back to packet.Data ----------
        private sealed class DesSerializeMiddleware : IUdpProxyMiddleware
        {
            private readonly ProxySettings _settings;
            public DesSerializeMiddleware(IOptions<ProxySettings> opt) => _settings = opt.Value;

            public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
            {
                if (context.Items.TryGetValue(LoginInfoKey, out var obj) && obj is LoginInfo li)
                {
                    packet.Data = DesEncryptCredentials(li.Username, li.Password, _settings.DesKey, _settings.DesIV);
                }
                return next(context, packet);
            }
        }

        // ---------- Helpers: DES CBC NoPadding w/ zero padding on our side ----------
        private static (string user, string pass) DesDecryptCredentials(byte[] ciphertext, string hexKey, string hexIv)
        {
            using var des = DES.Create();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.None;
            des.Key = HexToBytes(hexKey);
            des.IV = HexToBytes(hexIv);

            var plain = des.CreateDecryptor().TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            // Parse user\0pass\0 (ASCII), ignore trailing zeros
            int firstZero = Array.IndexOf(plain, (byte)0);
            if (firstZero < 0) firstZero = plain.Length;
            var user = Encoding.ASCII.GetString(plain, 0, firstZero);

            int startPass = firstZero + 1;
            int secondZero = Array.IndexOf(plain, (byte)0, startPass);
            if (secondZero < 0) secondZero = plain.Length;
            var pass = startPass <= plain.Length
                ? Encoding.ASCII.GetString(plain, startPass, Math.Max(0, secondZero - startPass))
                : "";

            return (user, pass);
        }

        private static byte[] DesEncryptCredentials(string user, string pass, string hexKey, string hexIv)
        {
            var payload = Encoding.ASCII.GetBytes(user + "\0" + pass + "\0");
            // zero pad to block size
            int pad = (8 - (payload.Length % 8)) % 8;
            if (pad > 0)
            {
                var padded = new byte[payload.Length + pad];
                Buffer.BlockCopy(payload, 0, padded, 0, payload.Length);
                payload = padded;
            }

            using var des = DES.Create();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.None;
            des.Key = HexToBytes(hexKey);
            des.IV = HexToBytes(hexIv);

            return des.CreateEncryptor().TransformFinalBlock(payload, 0, payload.Length);
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // ---------- Test proper ----------

        [Fact]
        public async Task Router_Pipeline_Rewrites_Encrypted_Login_And_Creates_Session()
        {
            // Arrange settings: bind listener to 0 (free port), use known DES, and rewrite rule
            var settings = new ProxySettings
            {
                ListenHost = "127.0.0.1",
                ListenPort = 0, // so tests don’t collide
                LoginServerHost = "127.0.0.1",
                LoginServerPort = 5998,
                DesKey = "6919379AC61BBE27",
                DesIV  = "0000000000000000",
                AccountRewrites =
                {
                    // old -> new
                    ["olduser"] = new AccountRewrite { Username = "newuser", Password = "newpass" }
                }
            };
            var opts = Options.Create(settings);

            // Real pipeline with our three middlewares
            var pipeline = new TestPipeline(
                new DesDeserializeMiddleware(opts),
                new RewriteMiddleware(opts),
                new DesSerializeMiddleware(opts));

            var logger = Mock.Of<ILogger<UdpProxyRouter>>();
            var store  = new ClientSessionStore();

            using var router = new UdpProxyRouter(
                logger,
                pipeline,
                Options.Create(settings),
                store);

            // Client packet contains ONLY the encrypted login payload as data
            var originalCipher = DesEncryptCredentials("olduser", "oldpass", settings.DesKey, settings.DesIV);
            var client = new IPEndPoint(IPAddress.Loopback, 40001);
            var packet = new UdpPacket(originalCipher, client);

            var ctx = new PacketContext();

            // Act
            await router.RouteAsync(ctx, packet);

            // Assert: session created
            var session = store.GetAllSessions().Single();
            Assert.Equal(client.Address, session.ClientEndpoint.Address);
            Assert.Equal(client.Port,    session.ClientEndpoint.Port);

            Assert.NotNull(session.ServerEndpoint);
            Assert.Equal(IPAddress.Loopback, session.ServerEndpoint!.Address);
            Assert.InRange(session.ServerEndpoint.Port, 1, 65535);

            // Packet now contains encrypted(newuser/newpass)
            var expectedCipher = DesEncryptCredentials("newuser", "newpass", settings.DesKey, settings.DesIV);
            Assert.Equal(expectedCipher, packet.Data);

            // Sanity: decrypt and confirm
            var (u2, p2) = DesDecryptCredentials(packet.Data, settings.DesKey, settings.DesIV);
            Assert.Equal("newuser", u2);
            Assert.Equal("newpass", p2);
        }
    }
}
