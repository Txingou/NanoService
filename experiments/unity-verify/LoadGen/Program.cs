using System.Diagnostics;
using System.Text.Json;
using NanoTransport;
using TouchSocket.Sockets;
using UnityVerify;

var options = LoadGenOptions.Parse(args);
Console.WriteLine(
    $"[LoadGen] {options.Clients} clients x {options.RatePerSecond} msg/s for {options.DurationSeconds}s -> {options.Host}:{options.Port}");

var payload = new string('a', options.PayloadLength);
var counters = new AggregateCounters();
var sendTimestamps = new SendTimestamps();

var clients = new List<VerifyClient>(options.Clients);
for (var i = 0; i < options.Clients; i++)
{
    clients.Add(new VerifyClient(
        onEchoReply: null,
        onCommand: _ => Interlocked.Increment(ref counters.CommandsReceived),
        options: new NanoTransportOptions { EnableReconnection = false }));
}

var connected = 0;
var connectTasks = clients.Select(client =>
{
    var task = client.ConnectAsync(new IPHost($"{options.Host}:{options.Port}"));
    _ = task.ContinueWith(t =>
    {
        if (t.IsCompletedSuccessfully)
        {
            Interlocked.Increment(ref connected);
        }
    }, TaskScheduler.Default);
    return task;
}).ToArray();

try
{
    await Task.WhenAll(connectTasks);
}
catch
{
    // 个别连接失败不终止压测，统计里会体现。
}

Console.WriteLine($"[LoadGen] connected={connected}/{options.Clients}");
if (connected == 0)
{
    Console.Error.WriteLine("[LoadGen] no client connected, abort.");
    Environment.ExitCode = 1;
    return;
}

var stopwatch = Stopwatch.StartNew();
var sendTasks = clients.Select(client => RunClientLoopAsync(client, payload, options, counters, sendTimestamps)).ToArray();
await Task.WhenAll(sendTasks);
stopwatch.Stop();

var firstSend = Volatile.Read(ref sendTimestamps.FirstSendTimestamp);
var lastSend = Volatile.Read(ref sendTimestamps.LastSendTimestamp);
var activeSeconds = (lastSend - firstSend) / (double)Stopwatch.Frequency;
var sent = Volatile.Read(ref counters.TelemetrySent);
var dropped = Volatile.Read(ref counters.TelemetryDropped);
var result = new
{
    host = options.Host,
    port = options.Port,
    clients = options.Clients,
    connected,
    ratePerSecond = options.RatePerSecond,
    durationSeconds = options.DurationSeconds,
    payloadChars = payload.Length,
    telemetrySent = sent,
    telemetryDropped = dropped,
    sendSuccessRate = sent + dropped == 0 ? 0 : (double)sent / (sent + dropped),
    commandsReceived = Volatile.Read(ref counters.CommandsReceived),
    elapsedSeconds = activeSeconds,
    throughputPerSecond = activeSeconds <= 0 ? 0 : sent / activeSeconds
};

var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
File.WriteAllText(options.OutputPath, json);
Console.WriteLine(json);

foreach (var client in clients)
{
    client.Dispose();
}

static async Task RunClientLoopAsync(
    VerifyClient client,
    string payload,
    LoadGenOptions options,
    AggregateCounters counters,
    SendTimestamps sendTimestamps)
{
    var stopwatch = Stopwatch.StartNew();
    var intervalTicks = (long)(Stopwatch.Frequency / options.RatePerSecond);
    var nextSend = Stopwatch.GetTimestamp() + intervalTicks;
    var sequence = 0;
    var targetMessages = (int)(options.RatePerSecond * options.DurationSeconds);

    for (var i = 0; i < targetMessages; i++)
    {
        var sentTimestamp = Stopwatch.GetTimestamp();
        if (Volatile.Read(ref sendTimestamps.FirstSendTimestamp) == 0)
        {
            Interlocked.CompareExchange(ref sendTimestamps.FirstSendTimestamp, sentTimestamp, 0);
        }

        Interlocked.Exchange(ref sendTimestamps.LastSendTimestamp, sentTimestamp);

        var sent = client.Send(new TelemetryMessage
        {
            Sequence = sequence++,
            SentUtcTicks = DateTime.UtcNow.Ticks,
            Payload = payload
        });

        if (sent)
        {
            Interlocked.Increment(ref counters.TelemetrySent);
        }
        else
        {
            Interlocked.Increment(ref counters.TelemetryDropped);
        }

        nextSend += intervalTicks;
        var now = Stopwatch.GetTimestamp();
        var delayMs = (nextSend - now) * 1000.0 / Stopwatch.Frequency;
        if (delayMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
        }
        else
        {
            await Task.Yield();
        }
    }
}

internal sealed class SendTimestamps
{
    public long FirstSendTimestamp;
    public long LastSendTimestamp;
}

internal sealed class AggregateCounters
{
    public long TelemetrySent;
    public long TelemetryDropped;
    public long CommandsReceived;
}

internal sealed class LoadGenOptions
{
    public string Host { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 7788;
    public int Clients { get; private set; } = 1000;
    public double RatePerSecond { get; private set; } = 20;
    public double DurationSeconds { get; private set; } = 30;
    public int PayloadLength { get; private set; } = 220;
    public string OutputPath { get; private set; } = "output/loadgen-client-stats.json";

    public static LoadGenOptions Parse(string[] args)
    {
        var options = new LoadGenOptions();
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--host":
                    options.Host = args[i + 1];
                    break;
                case "--port":
                    {
                        var port = 0;
                        _ = int.TryParse(args[i + 1], out port);
                        options.Port = port;
                    }
                    break;
                case "--clients":
                    {
                        var clients = 0;
                        _ = int.TryParse(args[i + 1], out clients);
                        options.Clients = clients;
                    }
                    break;
                case "--rate":
                    {
                        var rate = 0.0;
                        _ = double.TryParse(args[i + 1], out rate);
                        options.RatePerSecond = rate;
                    }
                    break;
                case "--duration":
                    {
                        var duration = 0.0;
                        _ = double.TryParse(args[i + 1], out duration);
                        options.DurationSeconds = duration;
                    }
                    break;
                case "--payload":
                    {
                        var payload = 0;
                        _ = int.TryParse(args[i + 1], out payload);
                        options.PayloadLength = payload;
                    }
                    break;
                case "--output":
                    options.OutputPath = args[i + 1];
                    break;
            }
        }

        return options;
    }
}
