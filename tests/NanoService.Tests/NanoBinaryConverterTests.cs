using NanoService;
using NanoService.Demo.Shared;
using TouchSocket.Core;
using Xunit;

namespace NanoService.Tests;

public sealed class NanoBinaryConverterTests
{
    [Fact]
    public void TelemetryLog_RoundTrip()
    {
        var converter = new TelemetryLogConverter();
        var source = new TelemetryLog
        {
            LogLevel = LogLevel.Warning,
            Message = "测试日志"
        };

        var writer = new ByteBlock(64);
        TelemetryLog result;
        try
        {
            converter.Serialize(ref writer, source);

            writer.SeekToStart();
            result = converter.Deserialize(ref writer, typeof(TelemetryLog));
        }
        finally
        {
            writer.Dispose();
        }

        Assert.Equal(source.LogLevel, result.LogLevel);
        Assert.Equal(source.Message, result.Message);
    }

    [Fact]
    public void DeviceCommand_RoundTrip()
    {
        var converter = new DeviceCommandConverter();
        var source = new DeviceCommand
        {
            Name = "reboot",
            Payload = "now"
        };

        var writer = new ByteBlock(64);
        DeviceCommand result;
        try
        {
            converter.Serialize(ref writer, source);

            writer.SeekToStart();
            result = converter.Deserialize(ref writer, typeof(DeviceCommand));
        }
        finally
        {
            writer.Dispose();
        }

        Assert.Equal(source.Name, result.Name);
        Assert.Equal(source.Payload, result.Payload);
    }
}
