using BenchmarkDotNet.Attributes;
using NanoService;
using NanoService.Demo.Shared;
using NanoTransport;
using TouchSocket.Core;

namespace NanoService.Benchmarks;

[MemoryDiagnoser]
public class MicroBenchmarks
{
    private TelemetryLog _log = null!;
    private TelemetryLogConverter _converter = null!;
    private ByteBlock _writer = null!;
    private byte[] _body = null!;
    private byte[] _dispatchBody = null!;
    private NanoTransportCore? _core;
    private NanoServiceHost _host = null!;
    private INanoCallContext _context = null!;
    private readonly uint _serviceId = ServiceIdHelper.Compute<TelemetryLog>();

    [GlobalSetup]
    public void Setup()
    {
        _log = new TelemetryLog
        {
            LogLevel = LogLevel.Info,
            Message = new string('x', 256)
        };
        _converter = new TelemetryLogConverter();
        _writer = new ByteBlock(512);
        _body = new byte[256];
        _core = new NanoTransportCore(new NanoTransportOptions(), _ => Task.CompletedTask);
        _host = new NanoServiceHost();
        _host.Register<TelemetryLog, TelemetryLogConverter>(new NoOpService());

        var dispatchWriter = new ByteBlock(512);
        try
        {
            _converter.Serialize(ref dispatchWriter, _log);
            _dispatchBody = dispatchWriter.Memory.Slice(0, dispatchWriter.Length).ToArray();
        }
        finally
        {
            dispatchWriter.Dispose();
        }

        _context = new NanoCallContext("session", null, null!);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _core?.Dispose();
        _writer.Dispose();
        _host.Dispose();
    }

    [Benchmark]
    public void SerializeRoundtrip()
    {
        _writer.Reset();
        _converter.Serialize(ref _writer, _log);
        _writer.SeekToStart();
        _converter.Deserialize(ref _writer, typeof(TelemetryLog));
    }

    [Benchmark]
    public uint ComputeServiceId()
    {
        return ServiceIdHelper.Compute(typeof(TelemetryLog));
    }

    [Benchmark]
    public byte[] EncodePacket()
    {
        return NanoProtocol.EncodePacket(1, _body);
    }

    [Benchmark]
    public bool EnqueuePacket()
    {
        return _core!.TryEnqueue(1, _body, allowDrop: true);
    }

    [Benchmark]
    public void DispatchInProcess()
    {
        _host.Dispatch(_serviceId, _dispatchBody, _context);
    }

    private sealed class NoOpService : NanoServiceBase<TelemetryLog, TelemetryLogConverter>
    {
        protected override void Handle(TelemetryLog request, INanoCallContext context)
        {
        }
    }
}
