using System.Collections.Concurrent;
using NanoTransport;

namespace NanoService;

/// <summary>
/// 服务端路由器，按 ServiceId 将收到的业务包分发给业务处理器。
/// </summary>
public class NanoServiceHost : IDisposable
{
    private readonly ConcurrentDictionary<uint, INanoService> _services = new();
    private INanoTransport? _transport;

    /// <summary>
    /// 注册业务处理器。
    /// </summary>
    public void Register<TRequest, TConverter>(NanoServiceBase<TRequest, TConverter> service)
        where TRequest : class
        where TConverter : NanoBinaryConverter<TRequest>, new()
    {
        if (service is null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        var serviceId = ServiceIdHelper.Compute<TRequest>();
        if (!_services.TryAdd(serviceId, service))
        {
            throw new InvalidOperationException($"ServiceId 冲突：{typeof(TRequest).FullName} 已注册。");
        }
    }

    /// <summary>
    /// 绑定传输层，自动订阅收到的业务包。
    /// </summary>
    public void Attach(INanoTransport transport)
    {
        if (transport is null)
        {
            throw new ArgumentNullException(nameof(transport));
        }
        if (_transport is not null)
        {
            _transport.PackageReceived -= OnPackageReceived;
        }

        _transport = transport;
        _transport.PackageReceived += OnPackageReceived;
    }

    /// <summary>
    /// 手动分发一个业务包。
    /// </summary>
    public void Dispatch(uint serviceId, byte[] body, INanoCallContext context)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (_services.TryGetValue(serviceId, out var service))
        {
            service.Dispatch(body, context);
        }
    }

    public void Dispose()
    {
        if (_transport is not null)
        {
            _transport.PackageReceived -= OnPackageReceived;
            _transport = null;
        }
    }

    private void OnPackageReceived(object? sender, PackageReceivedEventArgs e)
    {
        Dispatch(e.ServiceId, e.Body, new NanoCallContext(e.SessionId, e.RemoteEndPoint, e.Session));
    }
}
