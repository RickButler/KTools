using System.Threading.Tasks;
using LoginProxy.Packets;

namespace LoginProxy.Pipeline;

/// <summary>
///     Represents the next middleware in the UDP login proxy pipeline.
///     Responsible for forwarding the PacketContext on to the next component.
/// </summary>
/// <param name="context">The current packet processing context.</param>
public delegate Task UdpProxyDelegate(PacketContext context, UdpPacket packet);