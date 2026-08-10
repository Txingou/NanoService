using System.Net;
using TouchSocket.Sockets;

namespace NanoService;

/// <summary>
/// 远程调用上下文，向业务处理器提供会话身份信息与真实会话对象。
/// </summary>
public interface INanoCallContext
{
    /// <summary>
    /// 发送方会话标识。
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// 发送方远端地址。
    /// </summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// 发送方会话对象，客户端传输为 TcpClient，服务端传输为 TcpSessionClient 子类。
    /// </summary>
    ITcpSession Session { get; }

    /// <summary>
    /// 按具体类型获取会话对象。
    /// </summary>
    TClient? GetSession<TClient>()
        where TClient : class;
}
