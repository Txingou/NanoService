# NanoService / NanoTransport 基准测试结果说明（面向非专业读者）

本文把 `BenchmarkDotNet.Artifacts/results/` 下的 7 份基准报告翻译成非专业读者能直接看懂的说明。产品、管理、业务干系人可以直接读执行摘要、简化表和结论；工程师可以在附录 A 核对完整原始数据，并按“如何复现”一节在本地重跑。

数据快照日期：2026-08-10。

## 执行摘要

- 框架的单个环节（序列化、ServiceId 计算、协议头编码、入队、分发）都很快，均值在 16.94 ns 到 152.71 ns，全部处于百纳秒量级。
- 客户端发送侧：`NanoTransport.SendRaw` 和 `NanoService.Send<T>` 单次调用为亚微秒到微秒级（58.5 ns 到 5.06 µs），明显快于裸 TCP 基准的两次同步写入（约 20 µs）。注意吞吐基准只测“客户端完成入队”的成本，不代表真实网络吞吐。
- Echo 往返延迟三条链路都在 47 µs 到 74 µs 量级；NanoTransport 与 NanoService 的均值略低于裸 TCP，但彼此差距很小，且 ShortRun 误差很大，只宜说“同一量级”。
- 内存方面，载荷越大框架层分配越多：`NanoService.Send<T>` 发送 4KB 载荷每次约分配 18 KB，Echo 回环约 58 KB；对内存敏感的 ARM32 设备，这部分需要实机评估。
- 负载场景（256B、2 秒）达到约 699k msg/s 入队/送达、170.73 MB/s；由于客户端以最快速度灌入，约 62% 的发送尝试被 Droppable 队列丢弃，这是饱和状态下的设计行为，不是故障。
- 所有数字来自 Windows 10 1809 x64 桌面机、TCP loopback、ShortRun 3 次迭代，不能直接代表 ARM32 实机，也不做 ARM32 数值推测。

## 怎么看基准数字

BenchmarkDotNet 的表格里有几个固定列，先解释含义，后面的简化表只保留最常用的两列。

| 列 | 含义 | 对非专业读者的建议 |
| --- | --- | --- |
| Mean | 多次测量后的平均耗时或平均分配量 | 看量级即可，例如 100 ns 级、1 µs 级 |
| Error | 95% 置信区间的半宽，反映平均值本身有多不确定 | 误差很大时，不要拿相近数字比胜负 |
| StdDev | 每次迭代之间的波动程度 | 波动越大，说明该测量越不稳定 |
| Gen0/Gen1/Gen2 | 每 1000 次操作发生多少次对应代际的垃圾回收 | 数字越小，说明产生的垃圾越少 |
| Allocated | 每次操作在托管堆上分配的字节数 | 对内存受限设备尤其重要 |

单位换算：

| 单位 | 含义 | 换算 |
| --- | --- | --- |
| ns | 纳秒 | 1000 ns = 1 µs |
| µs | 微秒 | 1000 µs = 1 ms |
| B / KB | 字节 / 千字节 | 1 KB = 1024 B |

本次报告使用 `ShortRun`，只跑了 3 次迭代，`Error` 和 `StdDev` 都比较大。例如 `EnqueuePacket` 的 Mean 是 117.04 ns，Error 却高达 395.95 ns。因此本文只讲量级和趋势，不把相差几十纳秒的结果说成精确胜负。

## 测试环境

| 项目 | 值 |
| --- | --- |
| 操作系统 | Windows 10 1809 x64（10.0.17763.1935） |
| 运行时 | .NET 8.0.23，X64 RyuJIT |
| SDK | .NET SDK 10.0.102 |
| BenchmarkDotNet | 0.15.8 |
| Job | ShortRun |
| 工具链 | InProcessEmitToolchain（进程内执行） |
| 迭代 | 3 次，预热 1 次 |
| CPU | 报告中显示 Unknown processor，未识别型号 |
| 网络 | 本机 TCP loopback（127.0.0.1） |

## 分层微基准

这组测试不经过真实网络，把框架拆成单个环节分别计时，用于定位“时间花在哪一层”。

| 操作 | 含义 | 平均耗时 | 每次分配 |
| --- | --- | --- | ---: |
| EncodePacket | 编码一个 12 字节协议头加 Body | 16.94 ns | 296 B |
| ComputeServiceId | 计算一次业务服务路由 ID | 64.96 ns | 64 B |
| EnqueuePacket | 数据包进入发送队列（Droppable） | 117.04 ns | 284 B |
| DispatchInProcess | 服务端按 ServiceId 分发并调用业务处理器 | 146.06 ns | 636 B |
| SerializeRoundtrip | 业务对象序列化后再反序列化一次 | 152.71 ns | 568 B |

结论：单个环节都在百纳秒量级。最慢的“序列化往返”也只有 152.71 ns，说明框架自身的基础操作不是端到端耗时的主要来源。

## 端到端吞吐（客户端入队/发送侧）

吞吐基准测的是“客户端完成一次发送调用”的成本，包括入队，以及 NanoService 额外做的序列化和路由。它不等于线上每秒消息数，真实送达能力看“负载与过载场景”一节。

| 层 | 64B | 256B | 1KB | 4KB |
| --- | ---: | ---: | ---: | ---: |
| 裸 TCP | 20.90 µs | 19.76 µs | 19.31 µs | 21.92 µs |
| NanoTransport.SendRaw | 58.50 ns | 81.57 ns | 180.91 ns | 976.31 ns |
| NanoService.Send<T> | 199.6 ns | 324.3 ns | 851.8 ns | 5.06 µs |

怎么看：

- 裸 TCP 最慢且几乎不随载荷变化：基准里它做两次同步 socket 写入（4 字节头加 Body），约 20 µs 主要是系统调用成本。
- `NanoTransport.SendRaw` 在本次测量中均值最低，从 58.5 ns 到 976 ns，成本随载荷增大而上升，主要来自复制 Body 和入队。
- `NanoService.Send<T>` 比 `SendRaw` 多出序列化和 ServiceId 路由，64B 时约 0.2 µs，4KB 时约 5 µs。
- 由于误差很大，NanoTransport 与 NanoService 之间不要精确排序；可靠的趋势是“框架层远快于裸 TCP 同步写，载荷越大越慢”。

内存：裸 TCP 发送不分配额外内存；`NanoTransport.SendRaw` 每包分配 181 B 到 5.79 KB；`NanoService.Send<T>` 每包分配 606 B 到 18.1 KB。

## 端到端延迟（Echo 往返）

延迟基准测量完整往返：客户端发出 EchoRequest，服务端原样回 EchoReply，客户端收到回包后停止计时。

| 层 | 64B | 256B | 1KB | 4KB |
| --- | ---: | ---: | ---: | ---: |
| 裸 TCP | 70.97 µs | 70.87 µs | 68.81 µs | 73.64 µs |
| NanoTransport | 47.70 µs | 47.89 µs | 55.40 µs | 64.28 µs |
| NanoService | 51.06 µs | 50.63 µs | 53.87 µs | 64.71 µs |

怎么看：

- 三条链路都在 47 µs 到 74 µs 量级，说明 loopback 上的 TCP/TouchSocket 异步管线占了大头，RPC 层和序列化不是主要瓶颈。
- 64B 时 NanoTransport 均值 47.70 µs、NanoService 均值 51.06 µs，差约 3.4 µs，对应 NanoService 的序列化和路由开销；但两者的误差都超过 30 µs，只能理解为同一量级。
- 裸 TCP 反而最慢（约 69 µs 到 74 µs），因为它使用同步读写。这不代表原始 socket 一定更差，只是这套基准的测量方式不同。
- 4KB 时三种实现都收敛到约 64 µs 到 74 µs，说明大载荷下网络传输成本开始主导。

内存：裸 TCP 每次往返只分配 303 B 到 315 B；NanoTransport 分配 2.38 KB 到 33.88 KB；NanoService 分配 3.25 KB 到 58.38 KB。载荷越大，框架层的临时对象越多。

## 负载与过载场景

这组结果来自 `docs/benchmarks/results/load-overload.json`，不是 BenchmarkDotNet 的 7 份报告之一，但属于同一轮性能基线，用于回答“真实持续发送能力如何”。

### 稳定负载（256B，2 秒）

| 指标 | 数值 | 怎么看 |
| --- | ---: | --- |
| 客户端尝试 | 3,676,267 | 2 秒内客户端试图发送这么多次 |
| 入队成功 | 1,398,643 | 其中约 38% 成功进入发送队列 |
| 服务端送达 | 1,398,643 | 入队成功的消息全部送达 |
| 丢弃 | 2,277,624 | 约 62% 的尝试因队列满被丢弃 |
| 入队速率 | 699,319 msg/s | 客户端侧成功入队速率 |
| 送达速率 | 699,319 msg/s | 服务端侧实际送达速率 |
| 送达带宽 | 170.73 MB/s | 按 256B 载荷计算的送达带宽 |

客户端以最快速度灌入消息时，Droppable 队列必然被填满，高丢弃率是饱和状态下的设计行为，不是故障。这个场景说明：在 256B、loopback 桌面环境下，端到端可持续处理约 70 万 msg/s；真实设备需要实机复测。

### 过载（Droppable 容量 8，10,000 次入队）

| 指标 | 数值 | 怎么看 |
| --- | ---: | --- |
| 丢弃 | 9,992 | 队列只能放下 8 个，之后新包几乎全部被丢弃 |
| 丢弃率 | 99.92% | Droppable 的“丢新包”语义 |
| 总耗时 | 0 ms | 丢弃走快速失败路径，几乎不耗时 |
| Reliable 二次入队阻塞 | 是 | Reliable 队列满时会等待，而不是丢包 |
| 释放发送循环后完成 | 是 | 队列恢复后，等待的入队操作会完成 |

这个场景验证了双队列的背压行为：遥测类消息（Droppable）在队列满时丢新包并快速返回，命令类消息（Reliable）在队列满时阻塞等待，不会静默丢失。

## 如何复现

以下命令在仓库根目录执行。离线环境请保留 `--inProcess`。

```bash
# BenchmarkDotNet 基准（7 份报告）
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- --inProcess --job short

# 稳定负载与过载背压
dotnet run --project benchmarks/NanoService.Benchmarks -c Release -- load --payload=256 --duration=2
```

注意：本文记录的负载结果对应 `--duration=2` 的运行。`docs/benchmarks/README.md` 里的示例命令是 `--duration=5`，那是另一种可复现配置，跑出来的数字会与本文记录不同。

## 注意事项与限制

- `ShortRun` 只有 3 次迭代，`Error` 很大，几十纳秒到几微秒的差异不能作为精确排名。
- `InProcessEmitToolchain` 在进程内执行基准，与独立进程运行的耗时可能不同。
- 报告显示 `Unknown processor`，CPU 型号未识别，跨机器不能直接比较。
- 全部测试走本机 TCP loopback，没有真实网络延迟，不代表局域网或公网表现。
- 测试机是 Windows x64 桌面环境，本项目目标是 ARM32 嵌入式设备；本文不推测 ARM32 数值，必须在实机复测。
- 吞吐基准只测客户端入队成本，不是线上吞吐；真实送达能力看负载场景。
- Allocated 是托管堆分配，不等同于进程峰值内存。

## 术语表

| 术语 | 说明 |
| --- | --- |
| 基准测试（Benchmark） | 用固定脚本反复测量操作的耗时、内存等指标，用于比较实现或发现回归 |
| 微基准（Micro-benchmark） | 隔离测量单个函数或单个环节的基准，不经过完整业务路径 |
| 延迟（Latency） | 一次操作从开始到结束所花的时间 |
| 往返延迟（Round-trip latency） | 客户端发出请求到收到回包的完整耗时 |
| 吞吐量（Throughput） | 单位时间完成的操作数；本文中吞吐基准指客户端单次发送调用成本 |
| 均值（Mean） | 多次测量的平均值 |
| 误差（Error） | 平均值的 95% 置信区间半宽，误差大表示平均值不可靠 |
| 标准差（StdDev） | 每次迭代结果的波动程度 |
| 分配量（Allocated） | 每次操作在托管堆上分配的字节数 |
| GC 代（Gen0/Gen1/Gen2） | .NET 垃圾回收的代际；数字越小，产生的垃圾越少 |
| 回环（Loopback） | 本机到本机的 TCP 连接，没有真实网络延迟 |
| 入队（Enqueue） | 把待发送数据放入发送队列 |
| 送达（Delivered） | 服务端实际收到并完成分发 |
| 可丢弃（Droppable） | 队列满时丢新包并计数，适用于遥测等可丢消息 |
| 可靠（Reliable） | 队列满时阻塞等待，适用于命令等不可丢消息 |
| 背压（Backpressure） | 发送速度超过消费速度时，通过排队、丢弃或阻塞来保护系统 |
| Echo 回环 | 服务端收到请求后原样返回的测试模型，用于测量往返延迟 |

## 附录 A：原始报告数据

以下表格直接来自 `BenchmarkDotNet.Artifacts/results/` 下的 7 份报告，保留全部原始列，供工程师核对。

### A1 分层微基准（MicroBenchmarks）

| Method | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| SerializeRoundtrip | 152.71 ns | 90.795 ns | 4.977 ns | 0.0339 | - | 568 B |
| ComputeServiceId | 64.96 ns | 20.757 ns | 1.138 ns | 0.0038 | - | 64 B |
| EncodePacket | 16.94 ns | 5.674 ns | 0.311 ns | 0.0177 | - | 296 B |
| EnqueuePacket | 117.04 ns | 395.950 ns | 21.703 ns | 0.0169 | 0.0005 | 284 B |
| DispatchInProcess | 146.06 ns | 46.414 ns | 2.544 ns | 0.0381 | 0.0098 | 636 B |

### A2 吞吐：裸 TCP（RawTcpThroughputBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| SendOne | 64 | 20.90 µs | 11.701 µs | 0.641 µs | - |
| SendOne | 256 | 19.76 µs | 8.659 µs | 0.475 µs | - |
| SendOne | 1024 | 19.31 µs | 9.679 µs | 0.531 µs | - |
| SendOne | 4096 | 21.92 µs | 8.774 µs | 0.481 µs | - |

### A3 吞吐：NanoTransport.SendRaw（NanoTransportThroughputBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SendOne | 64 | 58.50 ns | 28.12 ns | 1.542 ns | 0.0108 | 0.0027 | - | 181 B |
| SendOne | 256 | 81.57 ns | 45.57 ns | 2.498 ns | 0.0299 | - | - | 495 B |
| SendOne | 1024 | 180.91 ns | 527.50 ns | 28.914 ns | 0.0916 | 0.0894 | - | 1481 B |
| SendOne | 4096 | 976.31 ns | 1,068.44 ns | 58.565 ns | 0.5274 | 0.1421 | 0.0191 | 5794 B |

### A4 吞吐：NanoService.Send<T>（NanoServiceThroughputBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SendOne | 64 | 199.6 ns | 191.0 ns | 10.47 ns | 0.0365 | - | - | 606 B |
| SendOne | 256 | 324.3 ns | 169.4 ns | 9.29 ns | 0.0782 | 0.0005 | - | 1293 B |
| SendOne | 1024 | 851.8 ns | 660.6 ns | 36.21 ns | 0.3204 | 0.3090 | - | 4976 B |
| SendOne | 4096 | 5,060.6 ns | 5,261.8 ns | 288.42 ns | 1.8005 | 0.4883 | 0.0763 | 18114 B |

### A5 延迟：裸 TCP（RawTcpLatencyBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| EchoOne | 64 | 70.97 µs | 42.26 µs | 2.317 µs | 303 B |
| EchoOne | 256 | 70.87 µs | 28.83 µs | 1.580 µs | 306 B |
| EchoOne | 1024 | 68.81 µs | 21.15 µs | 1.159 µs | 311 B |
| EchoOne | 4096 | 73.64 µs | 28.59 µs | 1.567 µs | 315 B |

### A6 延迟：NanoTransport（NanoTransportLatencyBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| EchoOne | 64 | 47.70 µs | 34.017 µs | 1.865 µs | 0.1221 | - | 2.38 KB |
| EchoOne | 256 | 47.89 µs | 31.338 µs | 1.718 µs | 0.1831 | 0.0610 | 3.88 KB |
| EchoOne | 1024 | 55.40 µs | 6.481 µs | 0.355 µs | 0.6104 | 0.1221 | 9.88 KB |
| EchoOne | 4096 | 64.28 µs | 38.028 µs | 2.084 µs | 2.9297 | 0.2441 | 33.88 KB |

### A7 延迟：NanoService（NanoServiceLatencyBenchmarks）

| Method | PayloadSize | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| EchoOne | 64 | 51.06 µs | 50.37 µs | 2.761 µs | 0.1221 | - | 3.25 KB |
| EchoOne | 256 | 50.63 µs | 22.97 µs | 1.259 µs | 0.3662 | 0.1221 | 5.88 KB |
| EchoOne | 1024 | 53.87 µs | 18.40 µs | 1.008 µs | 1.0376 | 0.1221 | 16.38 KB |
| EchoOne | 4096 | 64.71 µs | 28.07 µs | 1.539 µs | 4.3945 | 0.2441 | 58.38 KB |

## 数据来源

- `BenchmarkDotNet.Artifacts/results/*-report-github.md`：7 份 BenchmarkDotNet 报告。
- `BenchmarkDotNet.Artifacts/results/*-report.csv` 与 `*-report.html`：同一轮结果的 CSV 和 HTML 版本。
- `docs/benchmarks/results/load-overload.json`：负载与过载场景结果。
- `docs/benchmarks/results/microbenchmarks.json`、`end-to-end.json`：仓库内已有的汇总 JSON。

