namespace NanoTransport;

/// <summary>
/// 可丢弃消息被丢弃的事件参数。
/// </summary>
public sealed class PackageDroppedEventArgs : EventArgs
{
    public PackageDroppedEventArgs(uint serviceId, int bodyLength)
    {
        ServiceId = serviceId;
        BodyLength = bodyLength;
    }

    public uint ServiceId { get; }

    public int BodyLength { get; }
}
