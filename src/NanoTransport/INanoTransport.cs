using System.Net;

namespace NanoTransport;

/// <summary>
/// 传输层统一接口，客户端与服务端均实现该接口。
/// </summary>
public interface INanoTransport : IDisposable
{
    /// <summary>
    /// 发送原始业务包。
    /// </summary>
    bool SendRaw(uint serviceId, ReadOnlySpan<byte> body, bool allowDrop);

    /// <summary>
    /// 向指定会话发送原始业务包；客户端传输不支持时抛出异常。
    /// </summary>
    bool SendRaw(string sessionId, uint serviceId, ReadOnlySpan<byte> body, bool allowDrop);

    /// <summary>
    /// 收到完整业务包后触发。
    /// </summary>
    event EventHandler<PackageReceivedEventArgs>? PackageReceived;

    /// <summary>
    /// 会话建立后触发。
    /// </summary>
    event EventHandler<SessionConnectedEventArgs>? SessionConnected;

    /// <summary>
    /// 会话断开后触发。
    /// </summary>
    event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

    /// <summary>
    /// 可丢弃消息因队列满而被丢弃时触发。
    /// </summary>
    event EventHandler<PackageDroppedEventArgs>? PackageDropped;

    /// <summary>
    /// 累计丢弃包数。
    /// </summary>
    long DroppedCount { get; }

    /// <summary>
    /// 当前是否在线。
    /// </summary>
    bool Online { get; }
}
