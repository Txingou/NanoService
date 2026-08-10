namespace NanoTransport;

/// <summary>
/// 待发送的原始数据包。
/// </summary>
internal readonly struct OutgoingPacket
{
    public OutgoingPacket(string? sessionId, uint serviceId, byte[] body)
    {
        SessionId = sessionId;
        ServiceId = serviceId;
        Body = body;
    }

    public string? SessionId { get; }

    public uint ServiceId { get; }

    public byte[] Body { get; }
}
