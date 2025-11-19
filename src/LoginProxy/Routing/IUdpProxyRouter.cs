using System.Net;
using System.Threading.Tasks;
using LoginProxy.Packets;
using LoginProxy.Pipeline;

namespace LoginProxy.Routing;

/// <summary>
///     Routes UDP packets between EQ login clients and the login server,
///     ensuring response packets are forwarded to the correct originating client.
/// </summary>
public interface IUdpProxyRouter
{
    /// <summary>
    ///     Local endpoint the router listens on.
    /// </summary>
    IPEndPoint ListeningEndPoint { get; }

    /// <summary>
    ///     Routes a single received UDP packet through the middleware pipeline
    ///     and forwards it to the appropriate remote endpoint.
    /// </summary>
    Task RouteAsync(PacketContext context, UdpPacket packet);
}