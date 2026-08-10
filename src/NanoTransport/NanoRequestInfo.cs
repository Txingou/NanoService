using System.Buffers.Binary;
using TouchSocket.Core;

namespace NanoTransport;

/// <summary>
/// 自定义固定包头请求信息，负责 Magic 校验、TotalLength 与 ServiceId 解析。
/// </summary>
internal sealed class NanoRequestInfo : IFixedHeaderRequestInfo
{
    private byte[] _buffer = Array.Empty<byte>();
    private int _written;

    public int BodyLength { get; set; }

    public uint ServiceId { get; private set; }

    public byte[] Body { get; private set; } = Array.Empty<byte>();

    public bool OnParsingHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < NanoProtocol.HeaderLength)
        {
            return false;
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(header) != NanoProtocol.Magic)
        {
            return false;
        }

        var totalLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4));
        if (totalLength < NanoProtocol.HeaderLength || totalLength > int.MaxValue)
        {
            return false;
        }

        ServiceId = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(8));
        BodyLength = (int)totalLength - NanoProtocol.HeaderLength;
        _buffer = new byte[BodyLength];
        _written = 0;
        return true;
    }

    public bool OnParsingBody(ReadOnlySpan<byte> body)
    {
        if (_written + body.Length > _buffer.Length)
        {
            return false;
        }

        body.CopyTo(_buffer.AsSpan(_written));
        _written += body.Length;
        Body = _buffer.AsSpan(0, _written).ToArray();
        return true;
    }
}
