using System.Collections.Concurrent;
using NanoTransport;
using TouchSocket.Core;

namespace NanoService;

/// <summary>
/// 客户端发送器，持有类型到序列化器的映射并发起单向调用。
/// </summary>
public class NanoServiceClient
{
    private readonly INanoTransport _transport;
    private readonly ConcurrentDictionary<Type, Registration> _registrations = new();

    public NanoServiceClient(INanoTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>
    /// 注册请求类型的序列化器与发送策略。
    /// </summary>
    public void Register<TRequest>(NanoBinaryConverter<TRequest> converter, SendPolicy policy = SendPolicy.Reliable)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(converter);

        var serviceId = ServiceIdHelper.Compute<TRequest>();
        if (!_registrations.TryAdd(typeof(TRequest), new Registration((IFastBinaryConverter)converter, serviceId, policy)))
        {
            throw new InvalidOperationException($"请求类型重复注册：{typeof(TRequest).FullName}。");
        }
    }

    /// <summary>
    /// 发送单向请求。
    /// </summary>
    public bool Send<TRequest>(TRequest request)
        where TRequest : class
    {
        return SendCore(request, sessionId: null);
    }

    /// <summary>
    /// 向指定会话发送单向请求，用于服务端主动下发。
    /// </summary>
    public bool Send<TRequest>(TRequest request, string sessionId)
        where TRequest : class
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return SendCore(request, sessionId);
    }

    private bool SendCore<TRequest>(TRequest request, string? sessionId)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_registrations.TryGetValue(typeof(TRequest), out var registration))
        {
            throw new InvalidOperationException($"未注册请求类型的序列化器：{typeof(TRequest).FullName}。");
        }

        var byteBlock = new ByteBlock(256);
        byte[] body;
        try
        {
            registration.Converter.Write(ref byteBlock, request);
            body = byteBlock.Memory.Slice(0, byteBlock.Length).ToArray();
        }
        finally
        {
            byteBlock.Dispose();
        }

        var allowDrop = registration.Policy == SendPolicy.Droppable;
        return sessionId is null
            ? _transport.SendRaw(registration.ServiceId, body, allowDrop)
            : _transport.SendRaw(sessionId, registration.ServiceId, body, allowDrop);
    }

    private sealed record Registration(
        IFastBinaryConverter Converter,
        uint ServiceId,
        SendPolicy Policy);
}
