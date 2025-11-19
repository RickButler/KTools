using LoginProxy.Packets;
using LoginProxy.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoginProxy.Tests.Pipeline;

public class UdpProxyPipelineTests
{
    [Fact]
    public async Task Pipeline_ExecutesMiddleware()
    {
        var services = new ServiceCollection();
        var middleware = new TestMiddleware();

        services.AddSingleton<IUdpProxyMiddleware>(middleware);

        var provider = services.BuildServiceProvider();
        var pipeline = new UdpProxyPipeline(provider);

        var context = new PacketContext();
        var packet  = PacketFactory.MakeClientPacket(new byte[] { 0x00 });

        await pipeline.InvokeAsync(context, packet);

        Assert.True(middleware.Called);
    }

    private sealed class TestMiddleware : IUdpProxyMiddleware
    {
        public bool Called { get; private set; }

        public Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next)
        {
            Called = true;
            return next(context, packet);
        }
    }
}