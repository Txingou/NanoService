using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;
using NanoService;
using NanoTransport;
using TouchSocket.Sockets;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityVerify
{
    public sealed class VerifyLauncher : MonoBehaviour
    {
        public string mode = "server";
        public string host = "127.0.0.1";
        public int port = 7788;
        public int telemetryPerSecond = 20;
        public int telemetryPayloadChars = 220;
        public int echoEveryMilliseconds = 1000;
        public int runSeconds;
        public string outputPath = string.Empty;

        private VerifyClient? _client;
        private VerifyServer? _server;
        private bool _statsWritten;
        private bool _running = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<VerifyLauncher>() == null)
            {
                new GameObject("VerifyLauncher").AddComponent<VerifyLauncher>();
            }
        }

        private void Awake()
        {
            mode = PlayerPrefs.GetString("UnityVerifyMode", mode);

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-mode":
                        mode = args[i + 1];
                        break;
                    case "-host":
                        host = args[i + 1];
                        break;
                    case "-port":
                        _ = int.TryParse(args[i + 1], out port);
                        break;
                    case "-runSeconds":
                        _ = int.TryParse(args[i + 1], out runSeconds);
                        break;
                    case "-outputPath":
                        outputPath = args[i + 1];
                        break;
                }
            }
        }

        private IEnumerator Start()
        {
            yield return null;

            if (mode.Equals("client", StringComparison.OrdinalIgnoreCase))
            {
                yield return RunClient();
            }
            else
            {
                yield return RunServer();
            }
        }

        private IEnumerator RunClient()
        {
            var options = new NanoTransportOptions
            {
                EnableReconnection = true,
                DroppableCapacity = 512,
                ReliableCapacity = 512
            };

            _client = new VerifyClient(OnEchoReply, OnServerCommand, options);
            _client.PackageDropped += (_, _) => Interlocked.Increment(ref VerifyStats.TransportDroppedCount);
            _client.SessionConnected += (_, e) => Debug.Log($"[UnityVerify] client connected: {e.SessionId}");
            _client.SessionDisconnected += (_, e) => Debug.Log($"[UnityVerify] client disconnected: {e.SessionId}");

            var connectTask = _client.ConnectAsync(new IPHost($"{host}:{port}"));
            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            if (connectTask.IsFaulted)
            {
                Debug.LogError($"[UnityVerify] connect failed: {connectTask.Exception}");
                WriteStats();
                yield break;
            }

            Debug.Log($"[UnityVerify] client connected to {host}:{port}");

            var payload = new string('a', telemetryPayloadChars);
            var clock = Stopwatch.StartNew();
            var sendStopwatch = new Stopwatch();
            var nextTelemetry = 0L;
            var nextEcho = 0L;
            var sequence = 0;
            var allocationStart = Profiler.GetTotalAllocatedMemoryLong();
            var allocationSends = 0L;
            var intervalMs = 1000.0 / telemetryPerSecond;

            while (_running && (runSeconds <= 0 || clock.Elapsed.TotalSeconds < runSeconds))
            {
                var nowMs = clock.ElapsedMilliseconds;

                if (nowMs >= nextTelemetry)
                {
                    nextTelemetry = nowMs + (long)intervalMs;

                    sendStopwatch.Restart();
                    var sent = _client.Send(new TelemetryMessage
                    {
                        Sequence = sequence++,
                        SentUtcTicks = DateTime.UtcNow.Ticks,
                        Payload = payload
                    });
                    sendStopwatch.Stop();

                    if (sent)
                    {
                        Interlocked.Increment(ref VerifyStats.ClientTelemetrySent);
                    }
                    else
                    {
                        Interlocked.Increment(ref VerifyStats.ClientTelemetryDropped);
                    }

                    Interlocked.Add(ref VerifyStats.ClientSendTotalTicks, sendStopwatch.ElapsedTicks);
                    Interlocked.Increment(ref VerifyStats.ClientSendSamples);
                    if (sendStopwatch.ElapsedTicks > VerifyStats.ClientSendMaxTicks)
                    {
                        Interlocked.Exchange(ref VerifyStats.ClientSendMaxTicks, sendStopwatch.ElapsedTicks);
                    }

                    allocationSends++;
                }

                if (nowMs >= nextEcho)
                {
                    nextEcho = nowMs + echoEveryMilliseconds;
                    _client.Send(new EchoRequest { SentUtcTicks = DateTime.UtcNow.Ticks });
                }

                yield return null;
            }

            if (allocationSends > 0)
            {
                VerifyStats.ClientSendAllocatedBytes =
                    (Profiler.GetTotalAllocatedMemoryLong() - allocationStart) / allocationSends;
                VerifyStats.ClientSendAllocationSamples = allocationSends;
            }

            WriteStats();
            if (runSeconds > 0)
            {
                Environment.Exit(0);
            }
        }

        private IEnumerator RunServer()
        {
            var options = new NanoTransportOptions
            {
                EnableReconnection = false,
                DroppableCapacity = 1024,
                ReliableCapacity = 1024
            };

            _server = new VerifyServer(OnTelemetry, null, options);
            _server.PackageDropped += (_, _) => Interlocked.Increment(ref VerifyStats.TransportDroppedCount);
            _server.SessionConnected += (_, e) =>
            {
                Interlocked.Increment(ref VerifyStats.ConnectedCount);
                if (VerifyStats.ConnectedCount > VerifyStats.PeakConnections)
                {
                    Interlocked.Exchange(ref VerifyStats.PeakConnections, VerifyStats.ConnectedCount);
                }

                Debug.Log($"[UnityVerify] server session connected: {e.SessionId}");
            };
            _server.SessionDisconnected += (_, e) =>
            {
                Interlocked.Decrement(ref VerifyStats.ConnectedCount);
                Debug.Log($"[UnityVerify] server session disconnected: {e.SessionId}");
            };

            var startTask = _server.StartAsync(new IPHost(port));
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            if (startTask.IsFaulted)
            {
                Debug.LogError($"[UnityVerify] server start failed: {startTask.Exception}");
                WriteStats();
                yield break;
            }

            Debug.Log($"[UnityVerify] server listening on {port}");

            var clock = Stopwatch.StartNew();
            var nextBroadcast = 0L;
            var nextSample = 0L;
            var lastReceived = 0L;

            while (_running && (runSeconds <= 0 || clock.Elapsed.TotalSeconds < runSeconds))
            {
                var nowMs = clock.ElapsedMilliseconds;

                if (nowMs >= nextBroadcast)
                {
                    nextBroadcast = nowMs + 10000;
                    _server.Send(new ServerCommand { Name = "ping", Payload = "broadcast" });
                    Interlocked.Increment(ref VerifyStats.ServerCommandsBroadcast);
                }

                if (nowMs >= nextSample)
                {
                    nextSample = nowMs + 5000;
                    var received = Volatile.Read(ref VerifyStats.ServerTelemetryReceived);
                    var delta = received - lastReceived;
                    Debug.Log(
                        $"[UnityVerify] connected={VerifyStats.ConnectedCount} received={received} " +
                        $"delta5s={delta} dropped={VerifyStats.TransportDroppedCount}");
                    lastReceived = received;
                }

                yield return null;
            }

            WriteStats();
            if (runSeconds > 0)
            {
                Environment.Exit(0);
            }
        }

        private void OnTelemetry(INanoCallContext context, TelemetryMessage request)
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (Volatile.Read(ref VerifyStats.ServerFirstTelemetryTimestamp) == 0)
            {
                Interlocked.CompareExchange(ref VerifyStats.ServerFirstTelemetryTimestamp, timestamp, 0);
            }

            Interlocked.Exchange(ref VerifyStats.ServerLastTelemetryTimestamp, timestamp);
            Interlocked.Increment(ref VerifyStats.ServerTelemetryReceived);
        }

        private void OnEchoReply(EchoReply reply)
        {
            var rttMs = (DateTime.UtcNow.Ticks - reply.SentUtcTicks) / TimeSpan.TicksPerMillisecond;
            lock (VerifyStats.RttSync)
            {
                VerifyStats.RttMilliseconds.Add(rttMs);
            }
        }

        private void OnServerCommand(ServerCommand command)
        {
            Interlocked.Increment(ref VerifyStats.ClientCommandsReceived);
            Debug.Log($"[UnityVerify] command received: {command.Name} {command.Payload}");
        }

        private void OnApplicationQuit()
        {
            WriteStats();
        }

        private void WriteStats()
        {
            if (_statsWritten)
            {
                return;
            }

            _statsWritten = true;

            try
            {
                var root = string.IsNullOrEmpty(outputPath)
                    ? Path.Combine(Application.dataPath, "..", "output")
                    : outputPath;
                Directory.CreateDirectory(root);

                if (_client is not null)
                {
                    var snapshot = new UnityClientStatsJson
                    {
                        telemetrySent = VerifyStats.ClientTelemetrySent,
                        telemetryDropped = VerifyStats.ClientTelemetryDropped,
                        commandsReceived = VerifyStats.ClientCommandsReceived,
                        transportDroppedCount = VerifyStats.TransportDroppedCount,
                        sendSamples = VerifyStats.ClientSendSamples,
                        sendAvgUs = ToMicroseconds(VerifyStats.ClientSendTotalTicks, VerifyStats.ClientSendSamples),
                        sendMaxUs = ToMicroseconds(VerifyStats.ClientSendMaxTicks, 1),
                        gcBytesPerSend = VerifyStats.ClientSendAllocatedBytes,
                        rttSamples = VerifyStats.RttMilliseconds.Count
                    };

                    lock (VerifyStats.RttSync)
                    {
                        var sorted = new List<long>(VerifyStats.RttMilliseconds);
                        sorted.Sort();
                        snapshot.rttP50Ms = Percentile(sorted, 50);
                        snapshot.rttP95Ms = Percentile(sorted, 95);
                        snapshot.rttP99Ms = Percentile(sorted, 99);
                    }

                    File.WriteAllText(
                        Path.Combine(root, "unity-client-stats.json"),
                        ClientStatsJson(snapshot));
                }

                if (_server is not null)
                {
                    var first = Volatile.Read(ref VerifyStats.ServerFirstTelemetryTimestamp);
                    var last = Volatile.Read(ref VerifyStats.ServerLastTelemetryTimestamp);
                    var activeSeconds = (last - first) / (double)Stopwatch.Frequency;
                    var snapshot = new UnityServerStatsJson
                    {
                        telemetryReceived = VerifyStats.ServerTelemetryReceived,
                        commandsBroadcast = VerifyStats.ServerCommandsBroadcast,
                        peakConnections = VerifyStats.PeakConnections,
                        droppedCount = VerifyStats.TransportDroppedCount,
                        throughputPerSecond = activeSeconds <= 0
                            ? 0
                            : VerifyStats.ServerTelemetryReceived / activeSeconds
                    };

                    File.WriteAllText(
                        Path.Combine(root, "unity-server-stats.json"),
                        ServerStatsJson(snapshot));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVerify] failed to write stats: {ex}");
            }
        }

        private static double ToMicroseconds(long ticks, long samples)
        {
            return samples <= 0 ? 0 : ticks * 1_000_000.0 / Stopwatch.Frequency / samples;
        }

        private static double Percentile(List<long> values, double percentile)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            var index = Math.Max(0, (int)Math.Ceiling(percentile / 100.0 * values.Count) - 1);
            return values[index];
        }

        private static string ClientStatsJson(UnityClientStatsJson s)
        {
            var json = new StringBuilder();
            json.Append('{');
            json.Append("\"telemetrySent\":").Append(s.telemetrySent).Append(',');
            json.Append("\"telemetryDropped\":").Append(s.telemetryDropped).Append(',');
            json.Append("\"commandsReceived\":").Append(s.commandsReceived).Append(',');
            json.Append("\"transportDroppedCount\":").Append(s.transportDroppedCount).Append(',');
            json.Append("\"sendSamples\":").Append(s.sendSamples).Append(',');
            json.Append("\"sendAvgUs\":").Append(s.sendAvgUs.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"sendMaxUs\":").Append(s.sendMaxUs.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"gcBytesPerSend\":").Append(s.gcBytesPerSend).Append(',');
            json.Append("\"rttSamples\":").Append(s.rttSamples).Append(',');
            json.Append("\"rttP50Ms\":").Append(s.rttP50Ms.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"rttP95Ms\":").Append(s.rttP95Ms.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"rttP99Ms\":").Append(s.rttP99Ms.ToString("0.###", CultureInfo.InvariantCulture));
            json.Append('}');
            return json.ToString();
        }

        private static string ServerStatsJson(UnityServerStatsJson s)
        {
            var json = new StringBuilder();
            json.Append('{');
            json.Append("\"telemetryReceived\":").Append(s.telemetryReceived).Append(',');
        json.Append("\"commandsBroadcast\":").Append(s.commandsBroadcast).Append(',');
        json.Append("\"peakConnections\":").Append(s.peakConnections).Append(',');
        json.Append("\"droppedCount\":").Append(s.droppedCount).Append(',');
        json.Append("\"throughputPerSecond\":").Append(s.throughputPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
        json.Append('}');
        return json.ToString();
        }
    }

    [Serializable]
    public sealed class UnityClientStatsJson
    {
        public long telemetrySent;
        public long telemetryDropped;
        public long commandsReceived;
        public long transportDroppedCount;
        public long sendSamples;
        public double sendAvgUs;
        public double sendMaxUs;
        public long gcBytesPerSend;
        public int rttSamples;
        public double rttP50Ms;
        public double rttP95Ms;
        public double rttP99Ms;
    }

    [Serializable]
    public sealed class UnityServerStatsJson
    {
        public long telemetryReceived;
        public long commandsBroadcast;
        public int peakConnections;
        public long droppedCount;
        public double throughputPerSecond;
    }
}
