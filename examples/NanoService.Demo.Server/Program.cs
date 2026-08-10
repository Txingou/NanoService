using NanoService;
using NanoService.Demo.Shared;
using TouchSocket.Sockets;

string? latestSessionId = null;
var server = new DemoServer((sessionId, log) =>
{
    latestSessionId = sessionId;
    Console.WriteLine($"[{sessionId}] {log.LogLevel}: {log.Message}");
});

server.SessionDisconnected += (_, e) =>
{
    if (latestSessionId == e.SessionId)
    {
        latestSessionId = null;
    }
};

await server.StartAsync(new IPHost(7788));
Console.WriteLine("服务端已启动，监听 7788。输入命令内容后回车将下发给最近设备；输入 exit 退出。");

while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrEmpty(line))
    {
        continue;
    }

    if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (latestSessionId is null)
    {
        Console.WriteLine("暂无在线设备。");
        continue;
    }

    var sent = server.Send(new DeviceCommand { Name = "console", Payload = line }, latestSessionId);
    Console.WriteLine(sent ? "命令已入队。" : "命令入队失败。");
}

server.Dispose();

internal sealed class DemoServer : NanoTcpService
{
    private readonly Action<string, TelemetryLog> _onLog;

    public DemoServer(Action<string, TelemetryLog> onLog)
    {
        _onLog = onLog;
    }

    protected override void RegisterServices()
    {
        RegisterService(new TelemetryLogService(_onLog));
        RegisterService(new DeviceCommandConverter(), SendPolicy.Reliable);
    }
}

internal sealed class TelemetryLogService : NanoServiceBase<TelemetryLog, TelemetryLogConverter>
{
    private readonly Action<string, TelemetryLog> _onLog;

    public TelemetryLogService(Action<string, TelemetryLog> onLog)
    {
        _onLog = onLog;
    }

    protected override void Handle(TelemetryLog request, INanoCallContext context)
    {
        _onLog(context.SessionId, request);
    }
}
