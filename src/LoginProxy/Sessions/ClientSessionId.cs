using System;
using System.Net;

namespace LoginProxy.Sessions;

/// <summary>
///     Represents a stable, comparable identity for a client session,
///     based on the client's IP address + port.
/// </summary>
public sealed class ClientSessionId : IEquatable<ClientSessionId>
{
    public ClientSessionId(string address, int port)
    {
        Address = address;
        Port = port;
    }

    public ClientSessionId(IPEndPoint endpoint)
    {
        Address = endpoint.Address.ToString();
        Port = endpoint.Port;
    }

    public string Address { get; }
    public int Port { get; }

    public bool Equals(ClientSessionId? other)
    {
        return other is not null &&
               other.Address == Address &&
               other.Port == Port;
    }

    public override string ToString()
    {
        return $"{Address}:{Port}";
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientSessionId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Address, Port);
    }
}