using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace LoginProxy.Sessions;

public class ClientSessionStore
{
    private readonly ConcurrentDictionary<IPEndPoint, ClientSession> _sessions = new();

    public ClientSession GetOrCreate(IPEndPoint clientEndpoint)
    {
        return _sessions.GetOrAdd(clientEndpoint, ep =>
            new ClientSession(clientEndpoint));
    }

    public ClientSession? GetByClientEndpoint(IPEndPoint endpoint)
    {
        _sessions.TryGetValue(endpoint, out var session);
        return session;
    }

    // NEW: Used when routing server -> client
    public ClientSession? GetByServerEndpoint(IPEndPoint serverEndpoint)
    {
        return _sessions.Values.FirstOrDefault(s =>
            s.ServerEndpoint != null &&
            s.ServerEndpoint.Equals(serverEndpoint));
    }

    public ClientSession[] GetAllSessions()
    {
        return _sessions.Values.ToArray();
    }
}