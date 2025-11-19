using System.Collections.Generic;
using System.Net;

namespace LoginProxy.Sessions;

public interface IClientSessionStore
{
    ClientSession GetOrCreate(IPEndPoint endpoint);
    void Remove(ClientSession session);
    IEnumerable<ClientSession> GetAll();
}