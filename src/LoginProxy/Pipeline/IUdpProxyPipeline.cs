using System.Threading.Tasks;
using LoginProxy.Packets;

namespace LoginProxy.Pipeline;

public interface IUdpProxyPipeline
{
    Task InvokeAsync(PacketContext context, UdpPacket packet);
}