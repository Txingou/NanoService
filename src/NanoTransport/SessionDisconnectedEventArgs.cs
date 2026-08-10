namespace NanoTransport;

/// <summary>
/// 会话断开的事件参数。
/// </summary>
public sealed class SessionDisconnectedEventArgs : EventArgs
{
    public SessionDisconnectedEventArgs(string sessionId)
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
}
