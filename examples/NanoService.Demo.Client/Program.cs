using NanoService;
using NanoService.Demo.Shared;
using TouchSocket.Core;
using TouchSocket.Sockets;

var client = new DemoClient();
client.SessionConnected += (_, e) => Console.WriteLine($"已连接：{e.SessionId}");
client.SessionDisconnected += (_, e) => Console.WriteLine($"连接断开：{e.SessionId}");

await client.ConnectAsync(new IPHost("127.0.0.1:7788"));

for (var i = 0; i < 5; i++)
{
    var sent = client.Send(new TelemetryLog { LogLevel = LogLevel.Info, Message = $"设备遥测 {i}" });
    Console.WriteLine($"遥测 {i} 入队：{sent}");
    await Task.Delay(1000);
}

Console.WriteLine("遥测发送完毕，等待服务端命令。按 Ctrl+C 退出。");
await Task.Delay(Timeout.Infinite);

internal sealed class DemoClient : NanoTcpClient
{
    protected override void RegisterServices()
    {
        RegisterService(new DeviceCommandService());
        RegisterService(new TelemetryLogConverter(), SendPolicy.Droppable);
    }
}

internal sealed class DeviceCommandService : NanoServiceBase<DeviceCommand, DeviceCommandConverter>
{
    protected override void Handle(DeviceCommand request, INanoCallContext context)
    {
        Console.WriteLine($"收到命令：{request.Name} {request.Payload}");
    }
}
