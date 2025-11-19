// File: Pipeline/Middlewares/DesLoginSerializeMiddleware.cs

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using Microsoft.Extensions.Options;

namespace LoginProxy.Middleware
{
    public sealed class DesLoginSerializeMiddleware : IUdpProxyMiddleware
    {
        private readonly ProxySettings _settings;

        public DesLoginSerializeMiddleware(IOptions<ProxySettings> settings)
        {
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
        {
            var li = context.LoginInfo;
            if (li == null)
                return next(context, packet);

            var offset = Math.Max(0, _settings.LoginPayloadOffset);
            var plaintext = BuildPlain(li.Username, li.Password);

            try
            {
                using var des = DES.Create();
                des.Mode = CipherMode.CBC;
                des.Padding = PaddingMode.None;
                des.Key = HexTo8(_settings.DesKey);
                des.IV  = HexTo8(_settings.DesIV);

                var cipher = des.CreateEncryptor().TransformFinalBlock(plaintext, 0, plaintext.Length);

                var data = packet.Data ?? Array.Empty<byte>();
                var required = offset + cipher.Length;
                if (data.Length < required)
                {
                    var newBuf = new byte[required];
                    if (data.Length > 0)
                        Buffer.BlockCopy(data, 0, newBuf, 0, data.Length);
                    data = newBuf;
                }

                Buffer.BlockCopy(cipher, 0, data, offset, cipher.Length);
                packet.Data = data;
            }
            catch
            {
                // leave packet as-is
            }

            return next(context, packet);
        }

        private static byte[] BuildPlain(string user, string pass)
        {
            var core = Encoding.ASCII.GetBytes((user ?? string.Empty) + "\0" + (pass ?? string.Empty) + "\0");
            int pad = core.Length % 8;
            if (pad == 0) return core;

            var len = core.Length + (8 - pad);
            var outBuf = new byte[len];
            Buffer.BlockCopy(core, 0, outBuf, 0, core.Length);
            // trailing zeros already present
            return outBuf;
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
