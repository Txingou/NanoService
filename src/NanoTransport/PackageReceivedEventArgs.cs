using System.Net;
using TouchSocket.Sockets;

namespace NanoTransport;

/// <summary>
/// 收到完整业务包的事件参数。
/// </summary>
public sealed class PackageReceivedEventArgs : EventArgs
{
    public PackageReceivedEventArgs(uint serviceId, byte[] body, string sessionId, EndPoint? remoteEndPoint, ITcpSession session)
    {
        ServiceId = serviceId;
        Body = body;
        SessionId = sessionId;
        RemoteEndPoint = remoteEndPoint;
        Session = session;
    }

    public uint ServiceId { get; }

    public byte[] Body { get; }

    public string SessionId { get; }

    public EndPoint? RemoteEndPoint { get; }

    public ITcpSession Session { get; }
}
