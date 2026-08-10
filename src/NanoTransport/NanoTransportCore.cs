using System.Threading.Channels;

namespace NanoTransport;

/// <summary>
/// 双通道发送队列：可靠通道满时等待，可丢弃通道满时丢新包。
/// </summary>
internal sealed class NanoTransportCore : IDisposable
{
    private readonly Channel<OutgoingPacket> _reliableChannel;
    private readonly Channel<OutgoingPacket> _droppableChannel;
    private readonly Func<OutgoingPacket, Task> _sendPacket;
    private readonly Func<Task>? _sendLoopGate;
    private readonly CancellationTokenSource _cts = new();
    private long _droppedCount;

    public NanoTransportCore(NanoTransportOptions options, Func<OutgoingPacket, Task> sendPacket, Func<Task>? sendLoopGate = null)
    {
        _reliableChannel = Channel.CreateBounded<OutgoingPacket>(
            new BoundedChannelOptions(options.ReliableCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });
        _droppableChannel = Channel.CreateBounded<OutgoingPacket>(
            new BoundedChannelOptions(options.DroppableCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });
        _sendPacket = sendPacket;
        _sendLoopGate = sendLoopGate;
        _ = Task.Run(SendLoopAsync);
    }

    public event EventHandler<PackageDroppedEventArgs>? PackageDropped;

    public long DroppedCount => Volatile.Read(ref _droppedCount);

    public bool TryEnqueue(uint serviceId, ReadOnlySpan<byte> body, bool allowDrop, string? sessionId = null)
    {
        var packet = new OutgoingPacket(sessionId, serviceId, body.ToArray());

        if (!allowDrop)
        {
            try
            {
                _reliableChannel.Writer.WriteAsync(packet, _cts.Token).AsTask().GetAwaiter().GetResult();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        if (_droppableChannel.Writer.TryWrite(packet))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        PackageDropped?.Invoke(this, new PackageDroppedEventArgs(packet.ServiceId, packet.Body.Length));
        return false;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _reliableChannel.Writer.TryComplete();
        _droppableChannel.Writer.TryComplete();
        _cts.Dispose();
    }

    private async Task SendLoopAsync()
    {
        try
        {
            if (_sendLoopGate is not null)
            {
                await _sendLoopGate().ConfigureAwait(false);
            }

            while (!_cts.IsCancellationRequested)
            {
                var reliableWait = _reliableChannel.Reader.WaitToReadAsync(_cts.Token).AsTask();
                var droppableWait = _droppableChannel.Reader.WaitToReadAsync(_cts.Token).AsTask();
                await Task.WhenAny(reliableWait, droppableWait).ConfigureAwait(false);

                while (_reliableChannel.Reader.TryRead(out var packet))
                {
                    await SendAsync(packet).ConfigureAwait(false);
                }

                while (_droppableChannel.Reader.TryRead(out var packet))
                {
                    await SendAsync(packet).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
        }
        catch (Exception)
        {
            // 网络异常由 TouchSocket 重连机制处理，发送循环继续运行。
        }
    }

    private async Task SendAsync(OutgoingPacket packet)
    {
        try
        {
            await _sendPacket(packet).ConfigureAwait(false);
        }
        catch
        {
            // 单个包发送失败不影响后续包。
        }
    }
}
