using TouchSocket.Core;

namespace NanoTransport;

/// <summary>
/// NanoTransport 使用的固定包头拆包适配器。
/// </summary>
internal sealed class NanoDataHandlingAdapter : CustomFixedHeaderDataHandlingAdapter<NanoRequestInfo>
{
    public NanoDataHandlingAdapter(int maxPackageSize)
    {
        MaxPackageSize = maxPackageSize;
    }

    public override int HeaderLength => NanoProtocol.HeaderLength;

    protected override NanoRequestInfo GetInstance()
    {
        return new NanoRequestInfo();
    }
}
