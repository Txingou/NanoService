# NanoService

NanoService 是面向 ARM32 嵌入式设备的 .NET 8 远程调用框架，由 RPC 层（NanoService）与传输层（NanoTransport）组成。设备作为 TCP 客户端主动连接云端，云端作为 TCP 服务端通过同一条长连接双向收发消息。

## 工程结构

```text
src/NanoTransport/                传输层：协议头、双队列、背压丢弃、TCP 封装
src/NanoService/                  RPC 层：序列化器、服务路由、客户端发送
examples/NanoService.Demo.Shared/ 示例模型与序列化器
examples/NanoService.Demo.Server/ 云端示例
examples/NanoService.Demo.Client/ 设备示例
tests/NanoService.Tests/          单元与端到端测试
Data/                             原始需求与开发规范资料
```

## 快速开始

先启动服务端：

```bash
dotnet run --project examples/NanoService.Demo.Server
```

再启动设备端：

```bash
dotnet run --project examples/NanoService.Demo.Client
```

设备端会持续发送 `TelemetryLog` 遥测（Droppable）；在服务端控制台输入内容并回车，会通过会话上下文向该设备下发 `DeviceCommand`（Reliable）。

## 统一使用方式

不需要手动装配 `NanoServiceClient` 和 `NanoServiceHost`，直接继承统一 TCP 对象并在 `RegisterServices()` 中注册即可：

```csharp
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

服务端同样只操作一个对象，并可泛型指定自定义 `TcpSessionClient`：

```csharp
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

业务处理器通过 `INanoCallContext.Session` 获取真实会话对象（`TcpClient` 或 `TcpSessionClient` 子类），也可用 `context.GetSession<TClient>()` 按类型转换。

## 协议

数据包为 12 字节头加可变长度 Body，全部使用大端字节序：

| 偏移 | 大小 | 字段 | 说明 |
| --- | --- | --- | --- |
| 0 | 4 | Magic | 固定 0x5A6C8F01，用于包同步 |
| 4 | 4 | TotalLength | 整包长度，含 12 字节头 |
| 8 | 4 | ServiceId | 业务服务标识 |
| 12 | 可变 | Body | 业务序列化数据 |

粘包拆包由 `CustomFixedHeaderDataHandlingAdapter<NanoRequestInfo>` 完成。

## 核心 API

- `NanoBinaryConverter<T>`：序列化器基类
- `NanoServiceBase<TRequest, TConverter>`：业务处理器基类，实现 `Handle`
- `NanoServiceHost`：服务端路由器，`Register` + `Attach`
- `NanoServiceClient`：客户端发送器，`Register` + `Send`
- `NanoTcpService` / `NanoTcpClient`：统一 TCP 对象，内置注册与发送
- `NanoTcpService<TClient>`：泛型服务端，支持自定义 `TcpSessionClient`
- `ServiceIdHelper`：FNV-1a 32 位 ServiceId 计算

## 可靠性

- `SendPolicy.Reliable`：命令等不可丢消息，队列满时等待
- `SendPolicy.Droppable`：遥测等可丢消息，队列满时丢新包并计数
- `Send<T>` 返回 `bool`，Droppable 被丢弃时返回 `false`
- 队列容量、最大包大小与自动重连均可通过 `NanoTransportOptions` 配置

## 测试

```bash
dotnet test
```

测试覆盖 ServiceId 稳定性、序列化往返、队列丢弃策略、协议头解析与真实 TCP 端到端收发。

## 基准测试

```bash
# BenchmarkDotNet 基准（当前离线环境请使用 --inProcess）
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- --inProcess --job short

# 稳定负载与过载背压
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- load --payload=256 --duration=5
```

基线结果与运行说明见 [docs/benchmarks/README.md](docs/benchmarks/README.md)。

面向非专业读者的结果解读见 [BenchmarkResultsGuide.md](docs/benchmarks/BenchmarkResultsGuide.md)。

## 文档

- [Unity 验证测试报告](experiments/unity-verify/TEST-REPORT.md)：Unity 2022.3 下 NanoService/NanoTransport 的兼容性、便利性、性能与适用场景验证
- [Unity 使用教程](experiments/unity-verify/UNITY-QUICKSTART.md)：从零在 Unity 中安装并接入 NanoService/NanoTransport
- `CONTEXT.md`：领域术语
- `docs/adr/`：架构决策记录
- `docs/benchmarks/`：性能基线
- `Data/`：docx 说明文档与开发规范
