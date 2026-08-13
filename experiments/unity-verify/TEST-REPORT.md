# Unity 验证测试报告

状态：本机 Windows 验证完成；Linux Headless 部署验证待后续补充。

## 1. 测试目的

回答四个问题：

1. NanoService / NanoTransport 是否适用于 Unity 场景。
2. 在 Unity 中使用是否方便。
3. 性能消耗处于什么量级。
4. 更适合哪些场景，不适合哪些场景。

验证结果既要能复现，也要能支持"适用 / 不适用"的判断，而不是只证明某一次代码能跑通。

## 2. 测试对象

| 组件 | 版本 | 说明 |
| --- | --- | --- |
| NanoService | 4.5.1 | RPC 层 NuGet 包 |
| NanoTransport | 4.5.1 | TCP 传输层 NuGet 包 |
| TouchSocket | 4.3.1 | NanoTransport 底层 TCP 封装 |
| Unity Editor | 2022.3.62f3c1 | API 兼容级别 .NET Standard 2.1 |
| NuGetForUnity | 4.5.0 | Unity 的 NuGet 管理器 |
| 测试服务端 | Unity Windows Standalone 无头构建 | 与生产 Unity 服务端形态一致 |
| 测试客户端 | Unity Windows Standalone Development 构建 | 用于采集 Profiler 分配数据 |
| 负载生成器 | .NET 8 控制台 + NanoService 4.5.1 | 模拟 1000 个真实协议客户端 |

## 3. 测试设计与原因

### 3.1 编译与运行兼容性

Unity 2022.3 不能直接引用 net8.0 程序集。NanoService 4.5.1 / NanoTransport 4.5.1 提供 `netstandard2.0`、`netstandard2.1` 目标框架，因此本项验证"Unity 能否安装、编译并运行这些包"。

为什么做：这是所有后续测试的前提；如果程序集不兼容，功能与性能数据都没有意义。

### 3.2 双向收发

业务侧同时覆盖两条链路：

- Unity 客户端 -> Unity 服务端：遥测上报。
- Unity 服务端 -> Unity 客户端：命令下发。

为什么做：NanoService 的默认拓扑是"设备主动连接、连接上双向收发"，不能只验证单向发送。

### 3.3 Reliable / Droppable 语义

- `Reliable`：命令等不可丢消息，队列满时等待。
- `Droppable`：遥测等可丢消息，队列满时丢新包并计数。

为什么做：这是 NanoService 的核心可靠性设计。负载下需要证明命令不丢、遥测按策略处理。

### 3.4 1000 连接负载

使用 .NET 8 负载生成器创建 1000 个客户端，每客户端 20 msg/s、平均 220 字节、持续 30 秒。

为什么用 .NET 负载生成器：运行 1000 个 Unity 客户端会把客户端渲染、引擎开销计入服务器结果。负载生成器使用同一个 NanoService 协议栈，只模拟真实客户端网络行为。

为什么是 1000 x 20 msg/s：这是与用户确认的最小游戏场景切片，用于验证大规模长连接下的服务端吞吐。

### 3.5 客户端 Send 开销与 GC

在 Unity 客户端逐次记录 `Send<T>` 耗时，并用 `Profiler.GetTotalAllocatedMemoryLong` 采样单位发送产生的 GC 分配。

为什么做：游戏侧最关心的不是吞吐极限，而是单次调用耗时和每帧 GC 是否可控。GC 数据使用 Development 构建采集，因为 Release 构建下该 Profiler 接口不可用。

### 3.6 echo RTT

业务消息本身是单向的，没有响应和 ack。为了测延迟，验证工程额外加入 `EchoRequest` / `EchoReply` 测试消息：

- 客户端发送 EchoRequest。
- 服务端收到后回送 EchoReply。
- 客户端用发送时间戳计算 RTT。

为什么做：单向消息无法直接获得 RTT。echo 只存在于验证工程，不进业务模型。

## 4. 测试环境

- OS：Windows 10 x64
- CPU / 内存：Unity 日志显示 16 核逻辑处理器、约 16 GB 内存
- 网络：本机 TCP loopback
- .NET SDK：10.0.102，运行时 .NET 8.0.23
- Unity 构建：Windows Standalone x64

本机 loopback 结果不代表远程网络延迟，也不代表 ARM32/64 设备表现。

## 5. 测试结果

### 5.1 编译与运行兼容性

结果：通过。

- NuGetForUnity 成功恢复 `NanoService 4.5.1`、`NanoTransport 4.5.1` 及其依赖。
- Unity 2022.3 编译通过，客户端与服务端均能启动、连接并收发。
- 已核实的包内容：两个 4.5.1 包均包含 `netstandard2.0`、`netstandard2.1` 程序集。

### 5.2 双向收发

结果：通过。

- 客户端遥测：服务端计数与客户端发送数一致。
- 服务端广播命令：客户端成功接收并记录。

### 5.3 Reliable / Droppable 语义

结果：通过。

- 负载生成器发送 600000 条 Droppable 遥测，服务端收到 600000 条，无丢失。
- 服务端 30 秒窗口内广播 3 轮命令，负载生成器收到 3000 条命令，全部到达。

注：本次稳定负载没有把 Droppable 队列打满，因此没有触发丢弃；丢弃路径已有仓库单元测试覆盖。

### 5.4 服务端吞吐

| 指标 | 结果 | 验收线 |
| --- | ---: | ---: |
| 在线连接 | 1000 / 1000 | 1000 |
| 30 秒收到遥测 | 600000 | - |
| 服务端吞吐 | 20020.84 msg/s | >= 20000 msg/s |
| 服务端丢包 | 0 | - |

判定：通过。

原始文件：`output/unity-server-stats.json`

```json
{"telemetryReceived":600000,"commandsBroadcast":5,"peakConnections":1000,"droppedCount":0,"throughputPerSecond":20020.842}
```

负载生成器侧统计：

```json
{
  "clients": 1000,
  "connected": 1000,
  "telemetrySent": 600000,
  "telemetryDropped": 0,
  "commandsReceived": 3000,
  "elapsedSeconds": 30.0169678,
  "throughputPerSecond": 19988.694527633135
}
```

### 5.5 Unity 客户端开销

| 指标 | Development 构建结果 |
| --- | ---: |
| 单次 Send 平均 | 51.755 us |
| 单次 Send 最大 | 8006.3 us |
| GC 分配 | 48 B/send |

原始文件：`output/unity-client-stats.json`

说明：最大耗时 8 ms 来自连接建立后的首次发送/预热，后续稳定在几十微秒量级。

### 5.6 echo RTT

| 指标 | 结果 | 验收线 |
| --- | ---: | ---: |
| p50 | 1 ms | - |
| p95 | 45 ms | <= 50 ms |
| p99 | 45 ms | - |

判定：通过。

## 6. 结论

- 是否适用：是。Unity 2022.3 可以通过 NuGetForUnity 安装 4.5.1，并作为客户端或服务端使用。
- 使用是否方便：中等。4.5.1 多目标后安装路径可用，但首次需要导入 NuGetForUnity 插件并准备 `packages.config` / `NuGet.config`；不建议直接引用仅含 net8.0 的旧包。
- 性能消耗：1000 连接下服务端吞吐约 20020 msg/s；客户端 Send 平均约 51.8 us，GC 约 48 B/send；loopback echo p95 约 45 ms。
- 适用场景：单向遥测/命令、TCP 长连接、游戏大厅或房间级消息、大规模客户端接入。
- 不适合场景：必须 UDP、必须请求-响应/ack、需要严格实时状态同步或强一致确认的业务。

## 7. 局限与后续

- Linux Headless 服务端部署验证尚未执行，需在真实 Linux 主机复测。
- 本机 loopback 延迟不能代表公网或跨地域延迟。
- Unity standalone 在 batchmode 有限时长运行后未自动退出，测试脚本是在统计文件写完后从外部结束进程；这是测试脚本行为，不代表 NanoService 问题。
- Release 构建无法采集 Unity Profiler 的累计分配，因此 GC 数据来自 Development 构建。
- ARM32/64 设备端表现需另行验证。

## 8. 复现步骤

```powershell
cd D:\Project_Net\Codex\NanoService

# 恢复 Unity 包并构建客户端/服务端
powershell -File experiments\unity-verify\setup-unity.ps1

# 构建负载生成器
dotnet build experiments\unity-verify\LoadGen\LoadGen.csproj -c Debug
```

具体运行方式见 [README.md](README.md)。
