using System.Net;
using LoginProxy.Packets;

namespace LoginProxy.Tests;

public static class PacketFactory
{
    public static UdpPacket MakeClientPacket(byte[] data)
    {
        return new UdpPacket(data, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345));
    }

    public static UdpPacket MakeServerPacket(byte[] data)
    {
        return new UdpPacket(data, new IPEndPoint(IPAddress.Parse("1.2.3.4"), 5998));
    }
}