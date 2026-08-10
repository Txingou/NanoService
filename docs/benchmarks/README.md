# NanoService / NanoTransport 性能基线

本目录记录基准测试方法与基线结果。测试在当前 Windows x64 开发机上通过 TCP loopback 运行，结果不代表 ARM32 实机表现，ARM32 需按下方步骤在设备上复测。

非专业读者可先阅读 [BenchmarkResultsGuide.md](BenchmarkResultsGuide.md) 的结果解读。

## 运行方法

```bash
# BenchmarkDotNet 基准（当前离线环境请使用 --inProcess）
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- --inProcess --job short

# 稳定负载 + 过载背压场景
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- load --payload=256 --duration=5
```

基准包含：

- 分层微基准：序列化往返、ServiceId 计算、协议头编码、队列入队、进程内分发
- 端到端吞吐：裸 TCP、NanoTransport.SendRaw、NanoService.Send<T>
- 端到端延迟：EchoRequest/EchoReply 回环
- 负载与过载：客户端入队速率、服务端送达速率、Droppable 丢弃率、Reliable 阻塞行为

## 环境

- OS：Windows 10 1809 x64
- Runtime：.NET 8.0.23
- SDK：10.0.102
- BenchmarkDotNet：0.15.8（ShortRun，InProcessEmitToolchain，3 次迭代）

## 分层微基准

| Method | Mean | Allocated |
| --- | ---: | ---: |
| SerializeRoundtrip | 152.71 ns | 568 B |
| ComputeServiceId | 64.96 ns | 64 B |
| EncodePacket | 16.94 ns | 296 B |
| EnqueuePacket | 117.04 ns | 284 B |
| DispatchInProcess | 146.06 ns | 636 B |

## 端到端吞吐（客户端入队/发送侧）

| 层 | 64B | 256B | 1KB | 4KB |
| --- | ---: | ---: | ---: | ---: |
| 裸 TCP | 20.90 us | 19.76 us | 19.31 us | 21.92 us |
| NanoTransport.SendRaw | 58.50 ns | 81.57 ns | 180.91 ns | 976.31 ns |
| NanoService.Send<T> | 199.60 ns | 324.30 ns | 851.80 ns | 5.06 us |

吞吐基准测量的是客户端调用成本；真实送达速率见下方负载场景。

## 端到端延迟（Echo 往返）

| 层 | 64B | 256B | 1KB | 4KB |
| --- | ---: | ---: | ---: | ---: |
| 裸 TCP | 70.97 us | 70.87 us | 68.81 us | 73.64 us |
| NanoTransport | 47.70 us | 47.89 us | 55.40 us | 64.28 us |
| NanoService | 51.06 us | 50.63 us | 53.87 us | 64.71 us |

## 负载与过载

稳定负载（256B，2 秒）：

| 指标 | 数值 |
| --- | ---: |
| 客户端尝试 | 3,676,267 |
| 入队成功 | 1,398,643 |
| 服务端送达 | 1,398,643 |
| 丢弃 | 2,277,624 |
| 入队速率 | 699,319 msg/s |
| 送达速率 | 699,319 msg/s |
| 送达带宽 | 170.73 MB/s |

过载（Droppable 容量 8，10,000 次入队）：

| 指标 | 数值 |
| --- | ---: |
| 丢弃 | 9,992 |
| 丢弃率 | 99.92% |
| 总耗时 | 0 ms |
| Reliable 二次入队阻塞 | 是 |
| 释放发送循环后完成 | 是 |

## 说明

- NanoService.Send<T> 比 NanoTransport.SendRaw 多出的开销来自序列化、ServiceId 计算与入队；端到端延迟差距约 3-4 us。
- NanoTransport 延迟在本机低于裸 TCP 基线，原因是裸 TCP 基线使用同步读写，而 NanoTransport 使用 TouchSocket 异步管线，不代表框架比原始 socket 更快。
- 原始结果 JSON 见 `results/` 目录。
