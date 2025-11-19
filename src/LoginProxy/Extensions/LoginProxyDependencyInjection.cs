
using LoginProxy.Config;
using LoginProxy.Middleware;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using LoginProxy.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoginProxy.Extensions;

public static class LoginProxyDependencyInjection
{
    public static IServiceCollection AddLoginProxy(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Bind settings
        services
            .AddOptions<ProxySettings>()
            .Bind(config.GetSection("LoginProxy"))
            .ValidateDataAnnotations();

        // Pipeline + cipher
        services.AddSingleton<DesCredentialCipher>();
        services.AddSingleton<IUdpProxyPipeline, UdpProxyPipeline>();

        // Middlewares (order matters)
        services.AddUdpProxyMiddleware<DesLoginDeserializeMiddleware>();
        services.AddUdpProxyMiddleware<LoginRewriteMiddleware>();
        services.AddUdpProxyMiddleware<DesLoginSerializeMiddleware>();

        // Routing (listener + forwarder)
        services.AddSingleton<IUdpProxyRouter, UdpProxyRouter>();

        // Hosted service to run proxy
        services.AddHostedService<LoginProxyService>();

        return services;
    }
}

public static class UdpProxyMiddlewareServiceCollectionExtensions
{
    public static IServiceCollection AddUdpProxyMiddleware<T>(
        this IServiceCollection services) where T : class, IUdpProxyMiddleware
    {
        return services.AddSingleton<IUdpProxyMiddleware, T>();
    }
}