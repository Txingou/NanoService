# Unity Verify

验证 NanoService 4.5.1 在 Unity 2022.3 中的适用性、使用便利性、性能量级与适用场景。

## 已验证前提

- Unity 2022.3.62f3c1，API 兼容级别为 .NET Standard 2.1。
- `NanoService 4.5.1` 与 `NanoTransport 4.5.1` 已发布到 nuget.org，包含 `net462`、`net8.0`、`netstandard2.0`、`netstandard2.1` 四个目标框架。
- 使用 `NuGetForUnity 4.5.0` 安装 `NanoService 4.5.1`。

## 目录

```text
Unity/       Unity 验证工程（客户端/服务端双模式）
LoadGen/     .NET 8 负载生成器（1000 连接压测）
output/      运行产物（client/server/loadgen 统计 JSON）
```

## 准备

1. 打开 Unity 工程 `Unity/`（2022.3.62f3c1）。
2. 导入 `Data/NuGetForUnity.4.5.0.unitypackage`。
3. 通过 NuGet 安装 `NanoService 4.5.1`（会自动带上 `NanoTransport 4.5.1`、`TouchSocket 4.3.1` 等依赖）。

也可以运行 `setup-unity.ps1` 自动完成 2、3 两步。

## 运行

本机先跑服务端：

```text
Unity.exe -batchmode -nographics -projectPath Unity -executeMethod UnityVerify.VerifyBuild.BuildWindowsServer
Builds/UnityVerifyServer/UnityVerifyServer.exe -batchmode -nographics -mode server -port 7788 -outputPath <abs path>/output
```

再跑客户端（编辑器 Play 或 Standalone）：

```text
UnityVerifyClient.exe -batchmode -nographics -mode client -host 127.0.0.1 -port 7788 -outputPath <abs path>/output
```

最后用 .NET 8 负载生成器压服务端：

```powershell
dotnet run --project LoadGen -- --host 127.0.0.1 --port 7788 --clients 1000 --rate 20 --duration 30 --payload 220 --output output/loadgen-client-stats.json
```

## 报告

结果写入 `output/`：

- `unity-client-stats.json`：Send 开销、GC、RTT p50/p95/p99、命令接收数。
- `unity-server-stats.json`：连接数、遥测送达、广播命令数、吞吐。
- `loadgen-client-stats.json`：1000 连接下的入队/丢弃/命令接收统计。

结论模板见 [VERIFICATION-REPORT.md](VERIFICATION-REPORT.md)。
