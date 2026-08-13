# Unity 验证报告

状态：本机 Windows 验证完成；Linux Headless 部署待做。

## 结论摘要

- 是否适用：是。NanoService 4.5.1 可在 Unity 2022.3（.NET Standard 2.1）中通过 NuGetForUnity 安装并运行。
- 使用是否方便：中等。4.5.1 多目标后可用 NuGetForUnity CLI 恢复；仍需先导入 NuGetForUnity 插件并准备 `packages.config` / `NuGet.config`。
- 性能消耗：见下表，1000 连接下服务端吞吐 20020 msg/s，客户端 Send 平均 51.8 us，GC 48 B/send。
- 适用场景：单向遥测/命令、TCP 长连接、游戏大厅/房间级消息；不适合 UDP、请求-响应/ack 或严格实时状态同步。

## 测试环境

- Unity Editor：2022.3.62f3c1
- Unity 平台：Editor / Windows Standalone（Mono）
- .NET：LoadGen 使用 net8.0，引用 NanoService 4.5.1
- 服务端形态：Unity Windows 无头构建（Linux Headless 为后续追加项）

## 验证矩阵

- a. 编译与运行兼容性
- b. 双向收发 + Reliable/Droppable 语义
- c. 性能：客户端 Send 开销与 GC、1000 连接吞吐、echo RTT

## 数据

| 指标 | 结果 | 验收线 |
| --- | --- | --- |
| 服务端吞吐 | 20020.84 msg/s | >= 20k msg/s（1000 连接 x 20 msg/s） |
| echo RTT p95 | 45ms | <= 50ms |
| Unity 客户端单次 Send 平均/最大 | 51.755 us / 8006.3 us | 只报告 |
| Unity 客户端单次 Send GC 分配 | 48 B/send | 只报告 |

原始数据：

- `output/unity-server-stats.json`：`telemetryReceived=600000`，`peakConnections=1000`，`droppedCount=0`。
- `output/loadgen-full.json`：1000/1000 连接，发送 600000 条，丢弃 0，接收广播命令 3000。
- `output/unity-client-stats.json`：发送 300 条，RTT p50/p95/p99 = 1/45/45 ms，命令接收 2。

## 验收判定

- [x] 吞吐达标（20020.84 msg/s）
- [x] RTT 达标（p95 45ms）
- [x] 双向收发与语义验证通过（Reliable 命令 3000 条全部接收，Droppable 无丢失）

## 后续

- [ ] Linux Headless 服务端部署与同指标复测
