namespace UnityVerify
{
    public sealed class TelemetryMessage
    {
        public int Sequence { get; set; }

        public long SentUtcTicks { get; set; }

        public string Payload { get; set; } = string.Empty;
    }

    public sealed class ServerCommand
    {
        public string Name { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }

    public sealed class EchoRequest
    {
        public long SentUtcTicks { get; set; }
    }

    public sealed class EchoReply
    {
        public long SentUtcTicks { get; set; }
    }
}
