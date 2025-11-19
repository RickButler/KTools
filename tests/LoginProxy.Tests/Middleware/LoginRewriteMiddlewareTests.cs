using System.Net;
using LoginProxy.Config;
using LoginProxy.Middleware;
using LoginProxy.Packets;
using LoginProxy.Packets.Models;
using LoginProxy.Pipeline;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoginProxy.Tests.Middleware;

public class LoginRewriteMiddlewareTests
{
    [Fact]
    public async Task Rewrite_ChangesUsernameAndPassword()
    {
        // Arrange
        var settings = Options.Create(new ProxySettings
        {
            AccountRewrites = new Dictionary<string, AccountRewrite>
            {
                ["rick"] = new AccountRewrite
                {
                    Username = "secure",
                    Password = "pass123"
                }
            }
        });

        var middleware = new LoginRewriteMiddleware(settings);
        var context = new PacketContext
        {
            LoginInfo = new LoginInfo { Username = "rick", Password = "wrong" }
        };
        var packet = new UdpPacket(new byte[0], new IPEndPoint(IPAddress.Loopback, 12345));

        // Act
        UdpProxyDelegate next = (ctx, pkt) => Task.CompletedTask;
        await middleware.InvokeAsync(context, packet, next);

        // Assert
        Assert.NotNull(context.LoginInfo);
        Assert.Equal("secure", context.LoginInfo!.Username);
        Assert.Equal("pass123", context.LoginInfo!.Password);
    }
}