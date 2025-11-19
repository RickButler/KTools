// File: LoginProxy.E2E.Tests/UdpProxyRouterE2ETests.cs

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LoginProxy.Config;
using LoginProxy.Middleware;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using LoginProxy.Routing;
using LoginProxy.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoginProxy.Tests.Routing;

public class UdpProxyRouterE2ETests
{
    // Minimal executable pipeline: Deserialize → Rewrite → Serialize
    [Trait("Category", "E2E")]
    private sealed class Pipeline : IUdpProxyPipeline
    {
        private readonly IUdpProxyMiddleware[] _mw;
        public Pipeline(params IUdpProxyMiddleware[] mw) => _mw = mw;

        public Task InvokeAsync(PacketContext ctx, UdpPacket pkt)
        {
            UdpProxyDelegate term = static (c, p) => Task.CompletedTask;
            for (int i = _mw.Length - 1; i >= 0; i--)
            {
                var next = term;
                var m = _mw[i];
                term = (c, p) => m.InvokeAsync(c, p, next);
            }
            return term(ctx, pkt);
        }
    }

    [Fact]
    public async Task EndToEnd_ClientToServer_Rewrite_Then_ServerToClient_Reply()
    {
        // Fake login server socket (bind to 0 to get a free port)
        using var fakeServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEp = (IPEndPoint)fakeServer.Client.LocalEndPoint!;

        // Client socket (fixed endpoint to receive final reply)
        var clientEp = new IPEndPoint(IPAddress.Loopback, 40081);
        using var client = new UdpClient(clientEp);

        // Proxy settings: listen on 0 (ephemeral), point to fake server
        var settings = new ProxySettings
        {
            ListenHost = "127.0.0.1",
            ListenPort = 0,
            LoginServerHost = "127.0.0.1",
            LoginServerPort = serverEp.Port,
            DesKey = "6919379AC61BBE27",
            DesIV  = "0000000000000000",
            LoginPayloadOffset = 0,
            AccountRewrites =
            {
                ["olduser"] = new AccountRewrite { Username = "newuser", Password = "newpass" }
            }
        };

        // Real middlewares
        var pipeline = new Pipeline(
            new DesLoginDeserializeMiddleware(Options.Create(settings)),
            new LoginRewriteMiddleware(Options.Create(settings)),
            new DesLoginSerializeMiddleware(Options.Create(settings))
        );

        var store = new ClientSessionStore();
        using var router = new UdpProxyRouter(
            Mock.Of<ILogger<UdpProxyRouter>>(),
            pipeline,
            Options.Create(settings),
            store);

        // 1) CLIENT → PROXY → SERVER: send encrypted(olduser/oldpass)
        var originalCipher = DesEncrypt("olduser", "oldpass", settings.DesKey, settings.DesIV);
        var clientToProxyPacket = new UdpPacket(originalCipher, clientEp);

        await router.RouteAsync(new PacketContext(), clientToProxyPacket);

        // Server should receive a datagram from the proxy; its contents should be encrypted(newuser/newpass)
        var recvServer = await ReceiveAsync(fakeServer, msTimeout: 1500);
        Assert.NotNull(recvServer);

        var decryptedAtServer = DesDecrypt(recvServer!.Value.data, settings.DesKey, settings.DesIV);
        Assert.Equal(("newuser", "newpass"), decryptedAtServer);

        // 2) SERVER → PROXY → CLIENT: fake server replies "OK"
        var serverReply = Encoding.ASCII.GetBytes("OK");
        // Simulate proxy receiving from the real server endpoint
        var serverToProxyPacket = new UdpPacket(serverReply, serverEp);

        await router.RouteAsync(new PacketContext(), serverToProxyPacket);

        // Client should receive "OK"
        var recvClient = await ReceiveAsync(client, msTimeout: 1500);
        Assert.NotNull(recvClient);
        Assert.Equal("OK", Encoding.ASCII.GetString(recvClient!.Value.data));
    }

    // --------- helpers ---------

    private static async Task<(byte[] data, IPEndPoint remote)?> ReceiveAsync(UdpClient sock, int msTimeout)
    {
        using var cts = new CancellationTokenSource(msTimeout);
        try
        {
#if NET8_0_OR_GREATER
            var res = await sock.ReceiveAsync(cts.Token);
            return (res.Buffer, res.RemoteEndPoint);
#else
            var task = sock.ReceiveAsync();
            using (cts.Token.Register(() => task.TrySetCanceled()))
            {
                var res = await task;
                return (res.Buffer, res.RemoteEndPoint);
            }
#endif
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DesEncrypt(string user, string pass, string hexKey, string hexIv)
    {
        var payload = Encoding.ASCII.GetBytes(user + "\0" + pass + "\0");
        int pad = (8 - (payload.Length % 8)) % 8;
        if (pad > 0)
        {
            var tmp = new byte[payload.Length + pad];
            Buffer.BlockCopy(payload, 0, tmp, 0, payload.Length);
            payload = tmp;
        }

        using var des = DES.Create();
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.None;
        des.Key = Hex(hexKey);
        des.IV  = Hex(hexIv);
        return des.CreateEncryptor().TransformFinalBlock(payload, 0, payload.Length);
    }

    private static (string user, string pass) DesDecrypt(byte[] cipher, string hexKey, string hexIv)
    {
        using var des = DES.Create();
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.None;
        des.Key = Hex(hexKey);
        des.IV  = Hex(hexIv);

        var plain = des.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);
        int z1 = Array.IndexOf(plain, (byte)0); if (z1 < 0) z1 = plain.Length;
        var user = Encoding.ASCII.GetString(plain, 0, z1);
        int start = Math.Min(z1 + 1, plain.Length);
        int z2 = Array.IndexOf(plain, (byte)0, start); if (z2 < 0) z2 = plain.Length;
        var pass = Encoding.ASCII.GetString(plain, start, Math.Max(0, z2 - start));
        return (user, pass);
    }

    private static byte[] Hex(string hex)
    {
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }
}