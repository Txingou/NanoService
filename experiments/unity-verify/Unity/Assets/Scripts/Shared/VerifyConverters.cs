using System;
using NanoService;
using TouchSocket.Core;

namespace UnityVerify
{
    public sealed class TelemetryMessageConverter : NanoBinaryConverter<TelemetryMessage>
    {
        protected override TelemetryMessage Read<TReader>(ref TReader reader, Type type)
        {
            return new TelemetryMessage
            {
                Sequence = ReaderExtension.ReadValue<TReader, int>(ref reader),
                SentUtcTicks = ReaderExtension.ReadValue<TReader, long>(ref reader),
                Payload = ReaderExtension.ReadVarString<TReader>(ref reader)
            };
        }

        protected override void Write<TWriter>(ref TWriter writer, in TelemetryMessage obj)
        {
            WriterExtension.WriteValue(ref writer, obj.Sequence);
            WriterExtension.WriteValue(ref writer, obj.SentUtcTicks);
            WriterExtension.WriteVarString(ref writer, obj.Payload);
        }
    }

    public sealed class ServerCommandConverter : NanoBinaryConverter<ServerCommand>
    {
        protected override ServerCommand Read<TReader>(ref TReader reader, Type type)
        {
            return new ServerCommand
            {
                Name = ReaderExtension.ReadVarString<TReader>(ref reader),
                Payload = ReaderExtension.ReadVarString<TReader>(ref reader)
            };
        }

        protected override void Write<TWriter>(ref TWriter writer, in ServerCommand obj)
        {
            WriterExtension.WriteVarString(ref writer, obj.Name);
            WriterExtension.WriteVarString(ref writer, obj.Payload);
        }
    }

    public sealed class EchoRequestConverter : NanoBinaryConverter<EchoRequest>
    {
        protected override EchoRequest Read<TReader>(ref TReader reader, Type type)
        {
            return new EchoRequest
            {
                SentUtcTicks = ReaderExtension.ReadValue<TReader, long>(ref reader)
            };
        }

        protected override void Write<TWriter>(ref TWriter writer, in EchoRequest obj)
        {
            WriterExtension.WriteValue(ref writer, obj.SentUtcTicks);
        }
    }

    public sealed class EchoReplyConverter : NanoBinaryConverter<EchoReply>
    {
        protected override EchoReply Read<TReader>(ref TReader reader, Type type)
        {
            return new EchoReply
            {
                SentUtcTicks = ReaderExtension.ReadValue<TReader, long>(ref reader)
            };
        }

        protected override void Write<TWriter>(ref TWriter writer, in EchoReply obj)
        {
            WriterExtension.WriteValue(ref writer, obj.SentUtcTicks);
        }
    }
}
