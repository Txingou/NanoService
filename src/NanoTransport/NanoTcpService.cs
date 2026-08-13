using TouchSocket.Core;
using TouchSocket.Sockets;

namespace NanoTransport;

/// <summary>
/// 云端 TCP 服务端传输基类，继承 TouchSocket 泛型 TcpService 并内置协议头与队列。
/// </summary>
/// <typeparam name="TClient">会话类型。</typeparam>
public class NanoTcpService<TClient> : TcpService<TClient>, INanoTransport
    where TClient : TcpSessionClient
{
    private readonly NanoTransportOptions _options;
    private readonly NanoTransportCore _core;

    public NanoTcpService(NanoTransportOptions? options = null)
    {
        _options = options ?? new NanoTransportOptions();
        _options.Validate();
        _core = new NanoTransportCore(_options, SendPacketAsync);
        _core.PackageDropped += (_, e) => PackageDropped?.Invoke(this, e);
    }

    public event EventHandler<PackageReceivedEventArgs>? PackageReceived;

    public event EventHandler<SessionConnectedEventArgs>? SessionConnected;

    public event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

    public event EventHandler<PackageDroppedEventArgs>? PackageDropped;

    public long DroppedCount => _core.DroppedCount;

    /// <inheritdoc />
    public bool Online => ServerState == ServerState.Running;

    public bool SendRaw(uint serviceId, ReadOnlySpan<byte> body, bool allowDrop)
    {
        return _core.TryEnqueue(serviceId, body, allowDrop);
    }

    public bool SendRaw(string sessionId, uint serviceId, ReadOnlySpan<byte> body, bool allowDrop)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            throw new ArgumentException("会话标识不能为空。", nameof(sessionId));
        }
        return _core.TryEnqueue(serviceId, body, allowDrop, sessionId);
    }

    /// <summary>
    /// 配置并启动监听。
    /// </summary>
    public virtual async Task StartAsync(IPHost listenHost, CancellationToken cancellationToken = default)
    {
        if (listenHost is null)
        {
            throw new ArgumentNullException(nameof(listenHost));
        }
        var config = BuildConfig();
        config.SetListenIPHosts([listenHost]);
        await SetupAsync(config).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 子类可重写以追加 TouchSocket 配置。
    /// </summary>
    protected virtual TouchSocketConfig BuildConfig()
    {
        var config = new TouchSocketConfig();
        config.SetSingleStreamDataHandlingAdapter(() => new NanoDataHandlingAdapter(_options.MaxPackageSize));
        config.ConfigurePlugins(plugins =>
        {
            plugins.AddTcpReceivedPlugin((ITcpSession session, ReceivedDataEventArgs e) => OnTcpReceived(session, e));
            plugins.AddTcpConnectedPlugin((ITcpSession session) => OnTcpConnected(session));
            plugins.AddTcpClosedPlugin((ITcpSession session) => OnTcpClosed(session));
        });
        return config;
    }

    protected override void SafetyDispose(bool disposing)
    {
        if (disposing)
        {
            _core.Dispose();
        }

        base.SafetyDispose(disposing);
    }

    protected override TClient NewClient()
    {
        return (TClient)Activator.CreateInstance(typeof(TClient), nonPublic: true)!;
    }

    private Task OnTcpReceived(ITcpSession session, ReceivedDataEventArgs e)
    {
        if (e.RequestInfo is NanoRequestInfo info)
        {
            PackageReceived?.Invoke(this, new PackageReceivedEventArgs(info.ServiceId, info.Body, GetSessionId(session), session.RemoteEndPoint, session));
        }

        return Task.CompletedTask;
    }

    private Task OnTcpConnected(ITcpSession session)
    {
        SessionConnected?.Invoke(this, new SessionConnectedEventArgs(GetSessionId(session), session.RemoteEndPoint));
        return Task.CompletedTask;
    }

    private Task OnTcpClosed(ITcpSession session)
    {
        SessionDisconnected?.Invoke(this, new SessionDisconnectedEventArgs(GetSessionId(session)));
        return Task.CompletedTask;
    }

    private static string GetSessionId(ITcpSession session)
    {
        return session is IIdClient idClient
            ? idClient.Id
            : session.RemoteEndPoint?.ToString() ?? session.GetHashCode().ToString();
    }

    private async Task SendPacketAsync(OutgoingPacket packet)
    {
        var bytes = NanoProtocol.EncodePacket(packet.ServiceId, packet.Body);
        if (packet.SessionId is null)
        {
            foreach (var client in GetClients().OfType<TClient>())
            {
                if (client.Online)
                {
                    await client.SendAsync(bytes, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return;
        }

        await SendAsync(packet.SessionId, bytes, CancellationToken.None).ConfigureAwait(false);
    }
}

/// <summary>
/// 使用默认 TcpSessionClient 的云端 TCP 服务端传输。
/// </summary>
public class NanoTcpService : NanoTcpService<NanoTcpSessionClient>
{
    public NanoTcpService(NanoTransportOptions? options = null)
        : base(options)
    {
    }
}

/// <summary>
/// NanoTransport 默认会话类型。
/// </summary>
public class NanoTcpSessionClient : TcpSessionClient
{
    public NanoTcpSessionClient()
    {
    }
}
