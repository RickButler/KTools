// File: LoginProxy.Tests/Pipeline/DesLoginSerializeMiddlewareTests.cs

using System.Net;
using System.Security.Cryptography;
using System.Text;
using LoginProxy.Config;
using LoginProxy.Middleware;
using LoginProxy.Packets;
using LoginProxy.Packets.Models;
using LoginProxy.Pipeline;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoginProxy.Tests.Middleware
{
    public class DesLoginSerializeMiddlewareTests
    {
        private static byte[] Hex(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
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

        [Fact]
        public async Task Encrypts_LoginInfo_Into_PacketData_With_Offset()
        {
            // Arrange
            var settings = new ProxySettings
            {
                DesKey = "6919379AC61BBE27",
                DesIV  = "0000000000000000",
                LoginPayloadOffset = 4
            };
            var middleware = new DesLoginSerializeMiddleware(Options.Create(settings));

            var ctx = new PacketContext
            {
                LoginInfo = new LoginInfo { Username = "rick", Password = "secret" }
            };

            var originalPrefix = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var pkt = new UdpPacket((byte[])originalPrefix.Clone(), new IPEndPoint(IPAddress.Loopback, 10001));

            var expectedCipher = DesEncrypt("rick", "secret", settings.DesKey, settings.DesIV);

            // Act
            UdpProxyDelegate next = (c, p) => Task.CompletedTask;
            await middleware.InvokeAsync(ctx, pkt, next);

            // Assert: prefix untouched
            Assert.Equal(originalPrefix, pkt.Data.Take(settings.LoginPayloadOffset).ToArray());

            // Assert: encrypted payload written at offset
            var slice = pkt.Data.Skip(settings.LoginPayloadOffset).Take(expectedCipher.Length).ToArray();
            Assert.Equal(expectedCipher, slice);

            // Assert: total length >= offset + cipher len
            Assert.True(pkt.Data.Length >= settings.LoginPayloadOffset + expectedCipher.Length);
        }

        [Fact]
        public async Task Does_Nothing_When_LoginInfo_Absent()
        {
            // Arrange
            var settings = new ProxySettings
            {
                DesKey = "6919379AC61BBE27",
                DesIV  = "0000000000000000",
                LoginPayloadOffset = 0
            };
            var middleware = new DesLoginSerializeMiddleware(Options.Create(settings));

            var ctx = new PacketContext();
            var original = new byte[] { 1, 2, 3, 4, 5 };
            var pkt = new UdpPacket((byte[])original.Clone(), new IPEndPoint(IPAddress.Loopback, 10002));

            // Act
            UdpProxyDelegate next = (c, p) => Task.CompletedTask;
            await middleware.InvokeAsync(ctx, pkt, next);

            // Assert: data unchanged
            Assert.Equal(original, pkt.Data);
        }

        [Fact]
        public async Task Expands_Buffer_When_Too_Small()
        {
            // Arrange
            var settings = new ProxySettings
            {
                DesKey = "6919379AC61BBE27",
                DesIV  = "0000000000000000",
                LoginPayloadOffset = 2
            };
            var middleware = new DesLoginSerializeMiddleware(Options.Create(settings));

            var ctx = new PacketContext
            {
                LoginInfo = new LoginInfo { Username = "u", Password = "p" }
            };

            // Start with a tiny buffer so middleware must grow it
            var pkt = new UdpPacket(new byte[] { 0xAA, 0xBB }, new IPEndPoint(IPAddress.Loopback, 10003));

            var expectedCipher = DesEncrypt("u", "p", settings.DesKey, settings.DesIV);

            // Act
            UdpProxyDelegate next = (c, p) => Task.CompletedTask;
            await middleware.InvokeAsync(ctx, pkt, next);

            // Assert: prefix preserved
            Assert.Equal(new byte[] { 0xAA, 0xBB }, pkt.Data.Take(2).ToArray());

            // Assert: cipher written
            var slice = pkt.Data.Skip(2).Take(expectedCipher.Length).ToArray();
            Assert.Equal(expectedCipher, slice);

            // Assert: grew to fit
            Assert.True(pkt.Data.Length >= 2 + expectedCipher.Length);
        }
    }
}
