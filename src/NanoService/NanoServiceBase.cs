using TouchSocket.Core;

namespace NanoService;

/// <summary>
/// 业务处理器基类，绑定请求类型与序列化器。
/// </summary>
/// <typeparam name="TRequest">请求类型。</typeparam>
/// <typeparam name="TConverter">请求类型的序列化器。</typeparam>
public abstract class NanoServiceBase<TRequest, TConverter> : INanoService
    where TRequest : class
    where TConverter : NanoBinaryConverter<TRequest>, new()
{
    private readonly NanoBinaryConverter<TRequest> _converter;
    private readonly Type _requestType;

    protected NanoServiceBase()
    {
        _converter = new TConverter();
        _requestType = typeof(TRequest);
    }

    /// <summary>
    /// 处理反序列化后的请求。
    /// </summary>
    protected abstract void Handle(TRequest request, INanoCallContext context);

    void INanoService.Dispatch(byte[] body, INanoCallContext context)
    {
        var byteBlock = new ByteBlock(body);
        try
        {
            var request = _converter.Deserialize(ref byteBlock, _requestType);
            Handle(request, context);
        }
        finally
        {
            byteBlock.Dispose();
        }
    }
}
