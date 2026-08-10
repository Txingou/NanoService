using NanoTransport;
using Xunit;

namespace NanoService.Tests;

public sealed class OutgoingQueueTests
{
    [Fact]
    public void DroppableQueue_DropsNewPacket_WhenFull()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new NanoTransportOptions
        {
            DroppableCapacity = 1,
            ReliableCapacity = 1,
            EnableReconnection = false
        };

        using var core = new NanoTransportCore(options, _ => Task.CompletedTask, () => gate.Task);
        var dropped = 0;
        core.PackageDropped += (_, _) => Interlocked.Increment(ref dropped);

        Assert.True(core.TryEnqueue(1, [1, 2, 3], allowDrop: true));
        Assert.False(core.TryEnqueue(2, [4, 5], allowDrop: true));
        Assert.Equal(1, dropped);
        Assert.Equal(1, core.DroppedCount);

        gate.SetResult();
    }

    [Fact]
    public void ReliableQueue_Enqueues_WhenSpaceAvailable()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new NanoTransportOptions
        {
            DroppableCapacity = 1,
            ReliableCapacity = 1,
            EnableReconnection = false
        };

        using var core = new NanoTransportCore(options, _ => gate.Task);

        Assert.True(core.TryEnqueue(1, [1], allowDrop: false));

        gate.SetResult();
    }
}
