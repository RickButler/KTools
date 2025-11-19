using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LoginProxy.Packets;
using LoginProxy.Pipeline;
using LoginProxy.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoginProxy;

/// <summary>
///     Background hosted service that runs the P99 login proxy loop.
///     Uses IUdpProxyRouter to forward packets to/from EQ login server.
/// </summary>
public class LoginProxyService : BackgroundService
{
    private readonly ILogger<LoginProxyService> _logger;
    private readonly UdpProxyPipeline _pipeline;
    private readonly IUdpProxyRouter _router;
    private readonly UdpClient _udpClient = new();

    public LoginProxyService(
        ILogger<LoginProxyService> logger,
        IUdpProxyRouter router,
        UdpProxyPipeline pipeline)
    {
        _logger = logger;
        _router = router;
        _pipeline = pipeline;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LoginProxyService starting UDP receive loop...");

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var result = await _udpClient.ReceiveAsync(stoppingToken);

                var packet = new UdpPacket(result.Buffer, result.RemoteEndPoint);
                var context = new PacketContext();

                await _pipeline.InvokeAsync(context, packet); // Packet mutation done inside pipeline
                await _router.RouteAsync(context, packet); // Forward using router logic
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoginProxyService loop");
            }

        _logger.LogInformation("LoginProxyService is stopping...");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Bind local listener before processing
        var local = _router.GetListenEndpoint();
        _udpClient.Client.Bind(local);

        _logger.LogInformation("UDP bound to {Endpoint}", local);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LoginProxyService stopping...");
        _udpClient.Close();
        await base.StopAsync(cancellationToken);
    }
}

internal static class RouterExtensions
{
    public static IPEndPoint GetListenEndpoint(this IUdpProxyRouter router)
    {
        // Extension helper — router already knows its listen host/port
        return router.ListeningEndPoint;
    }
}