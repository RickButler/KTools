using System;
using System.Net;

namespace LoginProxy.Packets;

public class UdpPacket
{
    public UdpPacket(byte[] data, IPEndPoint remoteEndpoint)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        RemoteEndpoint = remoteEndpoint ?? throw new ArgumentNullException(nameof(remoteEndpoint));
    }

    public byte[] Data { get; set; }
    public IPEndPoint RemoteEndpoint { get; private set; }

    /// <summary>
    ///     Used by router to retarget this packet to a new remote endpoint
    ///     before LoginProxyService sends it out.
    /// </summary>
    public void UpdateRemote(IPEndPoint newEndpoint)
    {
        RemoteEndpoint = newEndpoint
                         ?? throw new ArgumentNullException(nameof(newEndpoint));
    }

    /// <summary>
    ///     Clone packet with a new endpoint (if needed for pipeline behaviors)
    /// </summary>
    public UdpPacket CloneFor(IPEndPoint newEndpoint)
    {
        return new UdpPacket(Data, newEndpoint);
    }
}