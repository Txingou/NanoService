using TouchSocket.Core;

namespace NanoService.Demo.Shared;

/// <summary>
/// DeviceCommand 的二进制序列化器。
/// </summary>
public sealed class DeviceCommandConverter : NanoBinaryConverter<DeviceCommand>
{
    protected override DeviceCommand Read<TReader>(ref TReader reader, Type type)
    {
        return new DeviceCommand
        {
            Name = ReaderExtension.ReadVarString<TReader>(ref reader),
            Payload = ReaderExtension.ReadVarString<TReader>(ref reader)
        };
    }

    protected override void Write<TWriter>(ref TWriter writer, in DeviceCommand obj)
    {
        WriterExtension.WriteVarString(ref writer, obj.Name);
        WriterExtension.WriteVarString(ref writer, obj.Payload);
    }
}
