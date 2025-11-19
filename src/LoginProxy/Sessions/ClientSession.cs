using System.Net;

namespace LoginProxy.Sessions;

public class ClientSession
{
    public ClientSession(IPEndPoint clientEndpoint)
    {
        ClientEndpoint = clientEndpoint;
    }

    public IPEndPoint ClientEndpoint { get; }
    public IPEndPoint? ServerEndpoint { get; private set; }

    public void UpdateServerEndpoint(IPEndPoint serverEndpoint)
    {
        ServerEndpoint = serverEndpoint;
    }
}