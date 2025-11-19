// File: Pipeline/Middlewares/DesLoginDeserializeMiddleware.cs

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Packets.Models;
using LoginProxy.Pipeline;
using Microsoft.Extensions.Options;

namespace LoginProxy.Middleware
{
    public sealed class DesLoginDeserializeMiddleware : IUdpProxyMiddleware
    {
        private readonly ProxySettings _settings;

        public DesLoginDeserializeMiddleware(IOptions<ProxySettings> settings)
        {
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
        {
            var data = packet.Data ?? Array.Empty<byte>();
            var offset = Math.Max(0, _settings.LoginPayloadOffset);
            if (data.Length <= offset)
                return next(context, packet);

            var cipherLen = data.Length - offset;
            var fullLen = (cipherLen / 8) * 8;
            if (fullLen <= 0)
                return next(context, packet);

            var cipher = new byte[fullLen];
            Buffer.BlockCopy(data, offset, cipher, 0, fullLen);

            try
            {
                using var des = DES.Create();
                des.Mode = CipherMode.CBC;
                des.Padding = PaddingMode.None;
                des.Key = HexTo8(_settings.DesKey);
                des.IV  = HexTo8(_settings.DesIV);

                var plain = des.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);

                // Parse ASCII "user\0pass\0"
                var (user, pass) = ParseUserPass(plain);

                // Only set if we parsed anything
                if (!string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(pass))
                {
                    context.LoginInfo = new LoginInfo
                    {
                        Username = user ?? string.Empty,
                        Password = pass ?? string.Empty
                    };
                }
            }
            catch
            {
                // leave packet as-is
            }

            return next(context, packet);
        }

        private static (string user, string pass) ParseUserPass(byte[] plain)
        {
            int idx = 0;
            int FindZero(int start)
            {
                for (int i = start; i < plain.Length; i++)
                    if (plain[i] == 0) return i;
                return plain.Length;
            }

            var z1 = FindZero(idx);
            var user = Encoding.ASCII.GetString(plain, idx, Math.Max(0, z1 - idx));
            idx = Math.Min(z1 + 1, plain.Length);

            var z2 = FindZero(idx);
            var pass = Encoding.ASCII.GetString(plain, idx, Math.Max(0, z2 - idx));

            return (user, pass);
        }

        private static byte[] HexTo8(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length < 16)
                throw new ArgumentException("DES key/IV must be 16 hex chars.", nameof(hex));

            var b = new byte[8];
            for (int i = 0; i < 8; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
