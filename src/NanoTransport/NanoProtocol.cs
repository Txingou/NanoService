using System.Buffers.Binary;

namespace NanoTransport;

/// <summary>
/// 12 字节协议头定义与编码。
/// </summary>
internal static class NanoProtocol
{
    public const uint Magic = 0x5A6C8F01;

    public const int HeaderLength = 12;

    public static byte[] EncodePacket(uint serviceId, ReadOnlySpan<byte> body)
    {
        var packet = new byte[HeaderLength + body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0, 4), Magic);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), (uint)packet.Length);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8, 4), serviceId);
        body.CopyTo(packet.AsSpan(HeaderLength));
        return packet;
    }
}
