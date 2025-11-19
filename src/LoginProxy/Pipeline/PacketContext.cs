// File: Pipeline/PacketContext.cs
using System.Collections.Generic;
using System.Threading;
using LoginProxy.Packets.Models;
using LoginProxy.Sessions;

namespace LoginProxy.Pipeline
{
    public sealed class PacketContext
    {
        public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public ClientSession? Session { get; set; }
        public LoginInfo? LoginInfo { get; set; }
        public CancellationToken CancellationToken { get; init; } = default;
    }
}