using NanoService;
using TouchSocket.Core;

namespace NanoService.Benchmarks;

internal sealed class EchoRequest
{
    public string Message { get; set; } = string.Empty;
}

internal sealed class EchoRequestConverter : NanoBinaryConverter<EchoRequest>
{
    protected override EchoRequest Read<TReader>(ref TReader reader, Type type)
    {
        return new EchoRequest
        {
            Message = ReaderExtension.ReadVarString<TReader>(ref reader)
        };
    }

    protected override void Write<TWriter>(ref TWriter writer, in EchoRequest obj)
    {
        WriterExtension.WriteVarString(ref writer, obj.Message);
    }
}

internal sealed class EchoReply
{
    public string Message { get; set; } = string.Empty;
}

internal sealed class EchoReplyConverter : NanoBinaryConverter<EchoReply>
{
    protected override EchoReply Read<TReader>(ref TReader reader, Type type)
    {
        return new EchoReply
        {
            Message = ReaderExtension.ReadVarString<TReader>(ref reader)
        };
    }

    protected override void Write<TWriter>(ref TWriter writer, in EchoReply obj)
    {
        WriterExtension.WriteVarString(ref writer, obj.Message);
    }
}
