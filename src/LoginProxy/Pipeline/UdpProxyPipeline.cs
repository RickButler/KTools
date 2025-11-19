using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoginProxy.Packets;
using Microsoft.Extensions.DependencyInjection;

namespace LoginProxy.Pipeline
{
    public sealed class UdpProxyPipeline : IUdpProxyPipeline
    {
        private readonly IList<IUdpProxyMiddleware> _middlewares;
        private UdpProxyDelegate _pipeline = static (ctx, pkt) => Task.CompletedTask;

        public UdpProxyPipeline(IServiceProvider services)
        {
            _middlewares = services.GetServices<IUdpProxyMiddleware>().ToList();
            Build();
        }

        public Task InvokeAsync(PacketContext context, UdpPacket packet)
            => _pipeline(context, packet);

        private void Build()
        {
            UdpProxyDelegate terminal = static (ctx, pkt) => Task.CompletedTask;

            // Build in reverse so execution order matches registration order
            _pipeline = _middlewares
                .Reverse()
                .Aggregate(terminal, (next, mw) =>
                    (ctx, pkt) => mw.InvokeAsync(ctx, pkt, next));
        }
    }
}