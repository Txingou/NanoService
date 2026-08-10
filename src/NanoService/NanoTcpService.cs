using NanoTransport;
using TouchSocket.Sockets;

namespace NanoService;

/// <summary>
/// 统一 TCP 服务端，内置 NanoServiceClient 与 NanoServiceHost，业务直接 Send 即可。
/// </summary>
/// <typeparam name="TClient">自定义 TcpSessionClient 子类。</typeparam>
public class NanoTcpService<TClient> : NanoTransport.NanoTcpService<TClient>
    where TClient : TcpSessionClient
{
    private readonly NanoServiceClient _client;
    private readonly NanoServiceHost _host;
    private readonly object _initLock = new();
    private bool _initialized;

    public NanoTcpService(NanoTransportOptions? options = null)
        : base(options)
    {
        _client = new NanoServiceClient(this);
        _host = new NanoServiceHost();
        _host.Attach(this);
    }

    /// <summary>
    /// 注册入站业务处理器。
    /// </summary>
    public void RegisterService<TRequest, TConverter>(NanoServiceBase<TRequest, TConverter> service)
        where TRequest : class
        where TConverter : NanoBinaryConverter<TRequest>, new()
    {
        _host.Register(service);
    }

    /// <summary>
    /// 注册出站序列化器与发送策略。
    /// </summary>
    public void RegisterService<TRequest>(NanoBinaryConverter<TRequest> converter, SendPolicy policy = SendPolicy.Reliable)
        where TRequest : class
    {
        _client.Register(converter, policy);
    }

    /// <summary>
    /// 向所有在线会话广播单向请求。
    /// </summary>
    public bool Send<TRequest>(TRequest request)
        where TRequest : class
    {
        EnsureInitialized();
        return _client.Send(request);
    }

    /// <summary>
    /// 向指定会话标识发送单向请求。
    /// </summary>
    public bool Send<TRequest>(TRequest request, string sessionId)
        where TRequest : class
    {
        EnsureInitialized();
        return _client.Send(request, sessionId);
    }

    /// <summary>
    /// 向指定会话对象发送单向请求。
    /// </summary>
    public bool Send<TRequest>(TRequest request, TClient session)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(session);
        return Send(request, session.Id);
    }

    /// <summary>
    /// 派生类在此集中注册入站处理器与出站序列化器。
    /// </summary>
    protected virtual void RegisterServices()
    {
    }

    public override async Task StartAsync(IPHost listenHost, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await base.StartAsync(listenHost, cancellationToken).ConfigureAwait(false);
    }

    protected override void SafetyDispose(bool disposing)
    {
        if (disposing)
        {
            _host.Dispose();
        }

        base.SafetyDispose(disposing);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            RegisterServices();
            _initialized = true;
        }
    }
}

/// <summary>
/// 使用默认 TcpSessionClient 的统一 TCP 服务端。
/// </summary>
public class NanoTcpService : NanoTcpService<NanoTransport.NanoTcpSessionClient>
{
    public NanoTcpService(NanoTransportOptions? options = null)
        : base(options)
    {
    }
}
