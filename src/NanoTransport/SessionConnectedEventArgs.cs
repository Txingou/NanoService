using System.Net;

namespace NanoTransport;

/// <summary>
/// 会话建立的事件参数。
/// </summary>
public sealed class SessionConnectedEventArgs : EventArgs
{
    public SessionConnectedEventArgs(string sessionId, EndPoint? remoteEndPoint)
    {
        SessionId = sessionId;
        RemoteEndPoint = remoteEndPoint;
    }

    public string SessionId { get; }

    public EndPoint? RemoteEndPoint { get; }
}
