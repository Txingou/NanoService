# NanoService

面向 ARM32 嵌入式设备的 .NET 8 远程调用框架（RPC 层），提供类型安全序列化、稳定 ServiceId 路由、可靠 / 可丢弃发送策略，并内置统一 TCP 客户端与服务端。消息传输由 `NanoTransport` 包完成。

## 安装

```bash
dotnet add package NanoService
```

## 特性

- `NanoBinaryConverter<T>`：业务模型与字节之间的类型安全序列化
- `ServiceIdHelper`：按请求类型稳定计算 32 位 ServiceId
- `SendPolicy.Reliable` / `SendPolicy.Droppable`：可靠与可丢弃发送策略
- `NanoTcpClient` / `NanoTcpService`：统一 TCP 客户端与服务端入口
- 广播发送、按会话发送、按会话对象发送

## 快速开始

设备端：

```csharp
using NanoService;
using TouchSocket.Sockets;

internal sealed class DemoClient : NanoTcpClient
{
    protected override void RegisterServices()
    {
        RegisterService(new DeviceCommandService());
        RegisterService(new TelemetryLogConverter(), SendPolicy.Droppable);
    }
}

var client = new DemoClient();
await client.ConnectAsync(new IPHost("127.0.0.1:7788"));
client.Send(new TelemetryLog { Message = "hello" });
```

云端服务端：

```csharp
using NanoService;
using TouchSocket.Sockets;

internal sealed class DemoServer : NanoTcpService
{
    protected override void RegisterServices()
    {
        RegisterService(new TelemetryLogService());
        RegisterService(new DeviceCommandConverter(), SendPolicy.Reliable);
    }
}

var server = new DemoServer();
await server.StartAsync(new IPHost(7788));
server.Send(new DeviceCommand { Name = "reboot" }, sessionId);
```

业务处理器通过 `INanoCallContext.Session` 获取真实会话对象，也可用 `context.GetSession<TClient>()` 按类型获取。

## 核心类型

- `NanoTcpClient` / `NanoTcpService`：统一 TCP 入口
- `NanoServiceHost` / `NanoServiceClient`：路由与发送核心
- `NanoServiceBase<TRequest, TConverter>`：业务处理器基类
- `NanoBinaryConverter<TRequest>`：序列化器基类
- `ServiceIdHelper`：ServiceId 计算
- `SendPolicy`：发送可靠性策略

## 项目链接

- 仓库：https://github.com/Txingou/NanoService
- 传输层：`NanoTransport` 包
