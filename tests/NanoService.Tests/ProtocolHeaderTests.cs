using System.Buffers.Binary;
using NanoTransport;
using Xunit;

namespace NanoService.Tests;

public sealed class ProtocolHeaderTests
{
    [Fact]
    public void EncodePacket_WritesBigEndianHeader()
    {
        var body = new byte[] { 1, 2, 3 };
        var packet = NanoProtocol.EncodePacket(0x12345678, body);

        Assert.Equal(NanoProtocol.HeaderLength + body.Length, packet.Length);
        Assert.Equal(NanoProtocol.Magic, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(0, 4)));
        Assert.Equal((uint)packet.Length, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(4, 4)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(8, 4)));
        Assert.Equal(body, packet.AsSpan(NanoProtocol.HeaderLength).ToArray());
    }

    [Fact]
    public void NanoRequestInfo_ParsesValidPacket()
    {
        var body = new byte[] { 9, 8, 7 };
        var packet = NanoProtocol.EncodePacket(0xABCDEF01, body);
        var info = new NanoRequestInfo();

        Assert.True(info.OnParsingHeader(packet.AsSpan(0, NanoProtocol.HeaderLength)));
        Assert.Equal(body.Length, info.BodyLength);
        Assert.Equal(0xABCDEF01u, info.ServiceId);
        Assert.True(info.OnParsingBody(packet.AsSpan(NanoProtocol.HeaderLength)));
        Assert.Equal(body, info.Body);
    }

    [Fact]
    public void NanoRequestInfo_RejectsInvalidMagic()
    {
        var packet = NanoProtocol.EncodePacket(1, [1]);
        packet[0] = 0;
        var info = new NanoRequestInfo();

        Assert.False(info.OnParsingHeader(packet.AsSpan(0, NanoProtocol.HeaderLength)));
    }
}
