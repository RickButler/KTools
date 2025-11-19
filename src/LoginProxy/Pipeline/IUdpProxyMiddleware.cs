using System.Threading.Tasks;
using LoginProxy.Packets;

namespace LoginProxy.Pipeline;

public interface IUdpProxyMiddleware
{
    Task InvokeAsync(PacketContext context, UdpPacket packet, UdpProxyDelegate next);
}