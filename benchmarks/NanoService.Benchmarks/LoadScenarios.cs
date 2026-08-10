using System.Diagnostics;
using NanoService;
using NanoTransport;
using TouchSocket.Sockets;

namespace NanoService.Benchmarks;

internal static class LoadScenarios
{
    public static async Task RunAsync(string[] args)
    {
        var payload = ParseInt(args, "--payload", 256);
        var duration = ParseInt(args, "--duration", 5);

        await RunLoadAsync(payload, duration);
        RunOverloadAsync();
    }

    private static async Task RunLoadAsync(int payload, int duration)
    {
        var port = EndToEndHelpers.GetFreePort();
        long received = 0;
        var server = new CountingServer(() => Interlocked.Increment(ref received));
        await server.StartAsync(new IPHost($"127.0.0.1:{port}"));

        var client = new ThroughputClient();
        await client.ConnectAsync(new IPHost($"127.0.0.1:{port}"));

        var message = new string('x', payload);
        long sentOk = 0;
        long attempts = 0;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(duration))
        {
            attempts++;
            if (client.Send(new EchoRequest { Message = message }))
            {
                sentOk++;
            }
        }

        stopwatch.Stop();
        await Task.Delay(200);

        var seconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        Console.WriteLine();
        Console.WriteLine($"负载测试 payload={payload}B duration={duration}s");
        Console.WriteLine($"客户端尝试={attempts} 入队成功={sentOk} 服务端送达={received} 丢弃={client.DroppedCount}");
        Console.WriteLine($"入队速率={sentOk / seconds:0} msg/s，送达速率={received / seconds:0} msg/s");
        Console.WriteLine($"送达带宽={received * payload / seconds / 1024.0 / 1024.0:0.00} MB/s");

        client.Dispose();
        server.Dispose();
    }

    private static void RunOverloadAsync()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new NanoTransportOptions
        {
            DroppableCapacity = 8,
            ReliableCapacity = 1,
            EnableReconnection = false
        };

        using var core = new NanoTransportCore(options, _ => Task.CompletedTask, () => gate.Task);
        var body = new byte[256];
        long dropped = 0;
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 10000; i++)
        {
            if (!core.TryEnqueue(1, body, allowDrop: true))
            {
                dropped++;
            }
        }

        stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine($"过载测试 10000 次入队，可丢弃容量={options.DroppableCapacity}");
        Console.WriteLine($"丢弃={dropped} 丢弃率={dropped / 100.0:0.00}% 总耗时={stopwatch.ElapsedMilliseconds}ms");

        var firstReliable = core.TryEnqueue(2, body, allowDrop: false);
        var blockingTask = Task.Run(() => core.TryEnqueue(3, body, allowDrop: false));
        Thread.Sleep(100);
        Console.WriteLine($"可靠通道：首个入队={firstReliable}，第二次入队阻塞={!blockingTask.IsCompleted}");

        gate.SetResult();
        var completed = blockingTask.GetAwaiter().GetResult();
        Console.WriteLine($"释放发送循环后，第二次入队完成={completed}");
    }

    private static int ParseInt(string[] args, string name, int defaultValue)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }

            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i][(name.Length + 1)..], out var inlineValue))
            {
                return inlineValue;
            }
        }

        return defaultValue;
    }
}
