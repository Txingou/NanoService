# NanoTransport

面向 ARM32 嵌入式设备的 .NET 8 TCP 传输层，提供 12 字节协议头、双队列发送（可靠 / 可丢弃）与背压丢弃策略。NanoService 的 RPC 层依赖本包完成消息传输。

## 安装

```bash
dotnet add package NanoTransport
```

## 特性

- 12 字节大端协议头：Magic、TotalLength、ServiceId
- 可靠队列：队列满时等待，不丢包
- 可丢弃队列：队列满时丢弃新包并计数
- 基于 TouchSocket 的 TCP 长连接与自动重连
- 队列容量、最大包大小与重连开关均可配置

## 快速开始

设备端连接服务端并发送原始业务包：

```csharp
using NanoTransport;
using TouchSocket.Sockets;

var client = new NanoTcpClient(new NanoTransportOptions
{
    DroppableCapacity = 1024,
    ReliableCapacity = 64,
    MaxPackageSize = 1024 * 1024
});

await client.ConnectAsync(new IPHost("127.0.0.1:7788"));
client.SendRaw(serviceId, body, allowDrop: true);
```

云端服务端监听：

```csharp
using NanoTransport;
using TouchSocket.Sockets;

var server = new NanoTcpService();
await server.StartAsync(new IPHost(7788));
```

## 协议

| 偏移 | 大小 | 字段 | 说明 |
| --- | --- | --- | --- |
| 0 | 4 | Magic | 固定 0x5A6C8F01，用于包同步 |
| 4 | 4 | TotalLength | 整包长度，含 12 字节头 |
| 8 | 4 | ServiceId | 业务服务标识 |
| 12 | 可变 | Body | 业务序列化数据 |

## 配置

`NanoTransportOptions` 支持以下选项：

- `DroppableCapacity`：可丢弃队列容量，默认 1024
- `ReliableCapacity`：可靠队列容量，默认 64
- `MaxPackageSize`：最大数据包大小，默认 1MB
- `EnableReconnection`：客户端自动重连，默认开启

## 项目链接

- 仓库：https://github.com/Txingou/NanoService
- RPC 层：`NanoService` 包
