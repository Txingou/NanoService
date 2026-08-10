namespace NanoTransport;

/// <summary>
/// NanoTransport 运行参数。
/// </summary>
public sealed class NanoTransportOptions
{
    /// <summary>
    /// 可丢弃队列容量，默认 1024。
    /// </summary>
    public int DroppableCapacity { get; set; } = 1024;

    /// <summary>
    /// 可靠队列容量，默认 64。
    /// </summary>
    public int ReliableCapacity { get; set; } = 64;

    /// <summary>
    /// 最大数据包大小，默认 1MB。
    /// </summary>
    public int MaxPackageSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// 客户端是否启用自动重连，默认启用。
    /// </summary>
    public bool EnableReconnection { get; set; } = true;

    internal void Validate()
    {
        if (DroppableCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DroppableCapacity), "可丢弃队列容量必须大于 0。");
        }

        if (ReliableCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReliableCapacity), "可靠队列容量必须大于 0。");
        }

        if (MaxPackageSize < NanoProtocol.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPackageSize), "最大包大小不能小于协议头长度。");
        }
    }
}
