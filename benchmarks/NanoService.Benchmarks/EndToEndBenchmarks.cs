using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using NanoService;
using NanoTransport;
using TouchSocket.Sockets;

namespace NanoService.Benchmarks;

[MemoryDiagnoser]
public class RawTcpThroughputBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private System.Net.Sockets.TcpListener _listener = null!;
    private Task _serverTask = null!;
    private System.Net.Sockets.TcpClient _client = null!;
    private NetworkStream _stream = null!;
    private byte[] _body = null!;
    private byte[] _header = null!;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _body = new byte[PayloadSize];
        _header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(_header, PayloadSize);
        _listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _serverTask = Task.Run(() => EndToEndHelpers.RawServerReadLoop(_listener, PayloadSize, echo: false));
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };
        _client.Connect(IPAddress.Loopback, port);
        _stream = _client.GetStream();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _listener.Stop();
        try
        {
            _serverTask.Wait(100);
        }
        catch
        {
        }
    }

    [Benchmark]
    public void SendOne()
    {
        _stream.Write(_header);
        _stream.Write(_body);
    }
}

[MemoryDiagnoser]
public class RawTcpLatencyBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private System.Net.Sockets.TcpListener _listener = null!;
    private Task _serverTask = null!;
    private System.Net.Sockets.TcpClient _client = null!;
    private NetworkStream _stream = null!;
    private byte[] _body = null!;
    private byte[] _header = null!;
    private byte[] _replyHeader = null!;
    private byte[] _replyBody = null!;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _body = new byte[PayloadSize];
        _header = new byte[4];
        _replyHeader = new byte[4];
        _replyBody = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32BigEndian(_header, PayloadSize);
        _listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _serverTask = Task.Run(() => EndToEndHelpers.RawServerReadLoop(_listener, PayloadSize, echo: true));
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };
        _client.Connect(IPAddress.Loopback, port);
        _stream = _client.GetStream();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _listener.Stop();
        try
        {
            _serverTask.Wait(100);
        }
        catch
        {
        }
    }

    [Benchmark]
    public async Task EchoOne()
    {
        _stream.Write(_header);
        _stream.Write(_body);
        await _stream.ReadExactlyAsync(_replyHeader);
        await _stream.ReadExactlyAsync(_replyBody);
    }
}

[MemoryDiagnoser]
public class NanoTransportThroughputBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private NanoTcpService _server = null!;
    private NanoTcpClient _client = null!;
    private byte[] _body = null!;
    private long _received;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _body = new byte[PayloadSize];
        _server = new NanoTcpService();
        _server.PackageReceived += (_, _) => Interlocked.Increment(ref _received);
        _server.StartAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
        _client = new NanoTcpClient();
        _client.ConnectAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark]
    public bool SendOne()
    {
        return _client.SendRaw(1, _body, allowDrop: true);
    }
}

[MemoryDiagnoser]
public class NanoTransportLatencyBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private NanoTcpService _server = null!;
    private NanoTcpClient _client = null!;
    private byte[] _body = null!;
    private Channel<byte[]> _channel = null!;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _body = new byte[PayloadSize];
        _channel = Channel.CreateUnbounded<byte[]>();
        _server = new NanoTcpService();
        _server.PackageReceived += (_, e) => _server.SendRaw(e.SessionId, 1, e.Body, allowDrop: false);
        _server.StartAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
        _client = new NanoTcpClient();
        _client.PackageReceived += (_, e) => _channel.Writer.TryWrite(e.Body);
        _client.ConnectAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark]
    public async Task EchoOne()
    {
        _client.SendRaw(1, _body, allowDrop: false);
        var reply = await _channel.Reader.ReadAsync();
        if (reply.Length != _body.Length)
        {
            throw new InvalidOperationException("回环数据长度不一致。");
        }
    }
}

[MemoryDiagnoser]
public class NanoServiceThroughputBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private NanoTcpService _server = null!;
    private NanoTcpClient _client = null!;
    private string _message = null!;
    private long _received;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _message = new string('x', PayloadSize);
        _server = new CountingServer(() => Interlocked.Increment(ref _received));
        _server.StartAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
        _client = new ThroughputClient();
        _client.ConnectAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark]
    public bool SendOne()
    {
        return _client.Send(new EchoRequest { Message = _message });
    }
}

[MemoryDiagnoser]
public class NanoServiceLatencyBenchmarks
{
    [Params(64, 256, 1024, 4096)]
    public int PayloadSize { get; set; }

    private EchoServer _server = null!;
    private NanoTcpClient _client = null!;
    private string _message = null!;
    private Channel<string> _channel = null!;

    [GlobalSetup]
    public void Setup()
    {
        var port = EndToEndHelpers.GetFreePort();
        _message = new string('x', PayloadSize);
        _channel = Channel.CreateUnbounded<string>();
        _server = new EchoServer();
        _server.Owner = _server;
        _server.StartAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
        _client = new LatencyClient(reply => _channel.Writer.TryWrite(reply));
        _client.ConnectAsync(new IPHost($"127.0.0.1:{port}")).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark]
    public async Task EchoOne()
    {
        _client.Send(new EchoRequest { Message = _message });
        var reply = await _channel.Reader.ReadAsync();
        if (reply.Length != _message.Length)
        {
            throw new InvalidOperationException("回环消息长度不一致。");
        }
    }
}

internal sealed class CountingServer : NanoTcpService
{
    private readonly Action _onRequest;

    public CountingServer(Action onRequest)
    {
        _onRequest = onRequest;
    }

    protected override void RegisterServices()
    {
        RegisterService(new CountingRequestService(_onRequest));
    }
}

internal sealed class CountingRequestService : NanoServiceBase<EchoRequest, EchoRequestConverter>
{
    private readonly Action _onRequest;

    public CountingRequestService(Action onRequest)
    {
        _onRequest = onRequest;
    }

    protected override void Handle(EchoRequest request, INanoCallContext context)
    {
        _onRequest();
    }
}

internal sealed class ThroughputClient : NanoTcpClient
{
    protected override void RegisterServices()
    {
        RegisterService(new EchoRequestConverter(), SendPolicy.Droppable);
    }
}

internal sealed class EchoServer : NanoTcpService
{
    public NanoTcpService? Owner { get; set; }

    protected override void RegisterServices()
    {
        RegisterService(new EchoRequestService(Owner!));
        RegisterService(new EchoReplyConverter(), SendPolicy.Reliable);
    }
}

internal sealed class EchoRequestService : NanoServiceBase<EchoRequest, EchoRequestConverter>
{
    private readonly NanoTcpService _server;

    public EchoRequestService(NanoTcpService server)
    {
        _server = server;
    }

    protected override void Handle(EchoRequest request, INanoCallContext context)
    {
        _server.Send(new EchoReply { Message = request.Message }, context.SessionId);
    }
}

internal sealed class LatencyClient : NanoTcpClient
{
    private readonly Action<string> _onReply;

    public LatencyClient(Action<string> onReply)
    {
        _onReply = onReply;
    }

    protected override void RegisterServices()
    {
        RegisterService(new EchoReplyService(_onReply));
        RegisterService(new EchoRequestConverter(), SendPolicy.Reliable);
    }
}

internal sealed class EchoReplyService : NanoServiceBase<EchoReply, EchoReplyConverter>
{
    private readonly Action<string> _onReply;

    public EchoReplyService(Action<string> onReply)
    {
        _onReply = onReply;
    }

    protected override void Handle(EchoReply request, INanoCallContext context)
    {
        _onReply(request.Message);
    }
}

internal static class EndToEndHelpers
{
    public static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static void RawServerReadLoop(System.Net.Sockets.TcpListener listener, int bodyLength, bool echo)
    {
        using var client = listener.AcceptTcpClient();
        using var stream = client.GetStream();
        var header = new byte[4];
        var body = new byte[bodyLength];

        while (true)
        {
            try
            {
                stream.ReadExactly(header);
                var length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length != bodyLength)
                {
                    return;
                }

                stream.ReadExactly(body);
                if (echo)
                {
                    stream.Write(header);
                    stream.Write(body);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }
}
