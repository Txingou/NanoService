using System.Net;
using TouchSocket.Sockets;

namespace NanoService;

internal sealed class NanoCallContext : INanoCallContext
{
    public NanoCallContext(string sessionId, EndPoint? remoteEndPoint, ITcpSession session)
    {
        SessionId = sessionId;
        RemoteEndPoint = remoteEndPoint;
        Session = session;
    }

    public string SessionId { get; }

    public EndPoint? RemoteEndPoint { get; }

    public ITcpSession Session { get; }

    public TClient? GetSession<TClient>()
        where TClient : class
    {
        return Session as TClient;
    }
}
