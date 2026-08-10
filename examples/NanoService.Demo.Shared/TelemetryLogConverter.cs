using TouchSocket.Core;

namespace NanoService.Demo.Shared;

/// <summary>
/// TelemetryLog 的二进制序列化器。
/// </summary>
public sealed class TelemetryLogConverter : NanoBinaryConverter<TelemetryLog>
{
    protected override TelemetryLog Read<TReader>(ref TReader reader, Type type)
    {
        return new TelemetryLog
        {
            LogLevel = (LogLevel)ReaderExtension.ReadValue<TReader, int>(ref reader),
            Message = ReaderExtension.ReadVarString<TReader>(ref reader)
        };
    }

    protected override void Write<TWriter>(ref TWriter writer, in TelemetryLog obj)
    {
        WriterExtension.WriteValue(ref writer, (int)obj.LogLevel);
        WriterExtension.WriteVarString(ref writer, obj.Message);
    }
}
