using TouchSocket.Core;
using TouchSocket.Sockets;

namespace NanoTransport;

/// <summary>
/// 设备侧 TCP 客户端传输，继承 TouchSocket 并内置协议头、队列与重连。
/// </summary>
public class NanoTcpClient : TcpClient, INanoTransport
{
    private readonly NanoTransportOptions _options;
    private readonly NanoTransportCore _core;

    public NanoTcpClient(NanoTransportOptions? options = null)
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

    public bool SendRaw(uint serviceId, ReadOnlySpan<byte> body, bool allowDrop)
    {
        return _core.TryEnqueue(serviceId, body, allowDrop);
    }

    public bool SendRaw(string sessionId, uint serviceId, ReadOnlySpan<byte> body, bool allowDrop)
    {
        throw new NotSupportedException("客户端传输不支持指定会话发送。");
    }

    /// <summary>
    /// 配置并连接到服务端。
    /// </summary>
    public virtual async Task ConnectAsync(IPHost remoteHost, CancellationToken cancellationToken = default)
    {
        if (remoteHost is null)
        {
            throw new ArgumentNullException(nameof(remoteHost));
        }
        var config = BuildConfig();
        config.SetRemoteIPHost(remoteHost);
        await SetupAsync(config).ConfigureAwait(false);
        await base.ConnectAsync(cancellationToken).ConfigureAwait(false);
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

            if (_options.EnableReconnection)
            {
                plugins.UseReconnection<TcpClient>(option =>
                {
                    option.UseSimple(TimeSpan.FromSeconds(1), 10);
                });
            }
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

    private async Task SendPacketAsync(OutgoingPacket packet)
    {
        var bytes = NanoProtocol.EncodePacket(packet.ServiceId, packet.Body);
        if (Online)
        {
            await SendAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string GetSessionId(ITcpSession session)
    {
        return session is IIdClient idClient
            ? idClient.Id
            : session.RemoteEndPoint?.ToString() ?? session.GetHashCode().ToString();
    }
}
