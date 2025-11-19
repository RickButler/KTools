// File: Pipeline/Middlewares/LoginRewriteMiddleware.cs

using System;
using System.Threading.Tasks;
using LoginProxy.Config;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using Microsoft.Extensions.Options;

namespace LoginProxy.Middleware
{
    public sealed class LoginRewriteMiddleware : IUdpProxyMiddleware
    {
        private readonly ProxySettings _settings;

        public LoginRewriteMiddleware(IOptions<ProxySettings> settings)
        {
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
        {
            var li = context.LoginInfo;
            if (li != null &&
                _settings.AccountRewrites.TryGetValue(li.Username, out var rw))
            {
                li.Username = rw.Username;
                li.Password = rw.Password;
            }

            return next(context, packet);
        }
    }
}