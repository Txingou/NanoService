# Unity 使用 NanoService / NanoTransport 教程

本教程演示在 Unity 2022.3 中接入 `NanoService 4.5.1`，实现 Unity 客户端与 Unity 服务端之间的双向单向消息通信。

## 1. 准备

- Unity 2022.3.62f3c1
- `Data/NuGetForUnity.4.5.0.unitypackage`
- `NanoService 4.5.1` NuGet 包
- 可选：.NET 8 SDK，用于运行负载生成器

## 2. 导入 NuGetForUnity

方式 A：在 Unity Editor 中执行 `Assets -> Import Package -> Custom Package...`，选择 `NuGetForUnity.4.5.0.unitypackage`。

方式 B：直接运行：

```powershell
tar -xzf Data\NuGetForUnity.4.5.0.unitypackage -C $env:TEMP\nufu
# 将路径为 Assets/NuGet/... 的 asset 文件复制到 Unity 工程的 Assets/NuGet/...
```

验证工程已经提供了脚本化的导入与安装流程，见 [setup-unity.ps1](setup-unity.ps1)。

## 3. 安装 NanoService

### 3.1 Editor 方式

导入后打开 `NuGet -> Manage NuGet Packages`，搜索 `NanoService`，安装 `4.5.1`。NuGetForUnity 会自动带上 `NanoTransport`、`TouchSocket` 等依赖。

### 3.2 CLI / CI 方式

在 Unity 工程中准备两个文件。

`Assets/NuGet.config`：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <packageSources>
        <clear />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
    <activePackageSource>
        <add key="All" value="(Aggregate source)" />
    </activePackageSource>
    <config>
        <add key="repositoryPath" value="./Packages" />
    </config>
</configuration>
```

`Assets/packages.config`：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<packages>
    <package id="NanoService" version="4.5.1" />
    <package id="NanoTransport" version="4.5.1" />
    <package id="TouchSocket" version="4.3.1" />
    <package id="TouchSocket.Core" version="4.3.1" />
    <package id="System.IO.Pipelines" version="6.0.3" />
    <package id="System.Text.Json" version="8.0.5" />
    <package id="System.Memory" version="4.5.5" />
    <package id="System.Threading.Channels" version="8.0.0" />
    <package id="System.Buffers" version="4.5.1" />
    <package id="System.Threading.Tasks.Extensions" version="4.5.4" />
    <package id="System.Runtime.CompilerServices.Unsafe" version="6.0.0" />
    <package id="System.Text.Encodings.Web" version="8.0.0" />
    <package id="Microsoft.Bcl.AsyncInterfaces" version="8.0.0" />
    <package id="System.Numerics.Vectors" version="4.4.0" />
</packages>
```

然后执行：

```powershell
dotnet tool install --global NuGetForUnity.Cli --version 4.5.0
nugetforunity restore <UnityProjectPath>
```

## 4. 定义消息

```csharp
public sealed class TelemetryMessage
{
    public int Sequence { get; set; }
    public long SentUtcTicks { get; set; }
    public string Payload { get; set; } = string.Empty;
}

public sealed class ServerCommand
{
    public string Name { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
```

## 5. 定义序列化器

```csharp
using System;
using NanoService;
using TouchSocket.Core;

public sealed class TelemetryMessageConverter : NanoBinaryConverter<TelemetryMessage>
{
    protected override TelemetryMessage Read<TReader>(ref TReader reader, Type type)
    {
        return new TelemetryMessage
        {
            Sequence = ReaderExtension.ReadValue<TReader, int>(ref reader),
            SentUtcTicks = ReaderExtension.ReadValue<TReader, long>(ref reader),
            Payload = ReaderExtension.ReadVarString<TReader>(ref reader)
        };
    }

    protected override void Write<TWriter>(ref TWriter writer, in TelemetryMessage obj)
    {
        WriterExtension.WriteValue(ref writer, obj.Sequence);
        WriterExtension.WriteValue(ref writer, obj.SentUtcTicks);
        WriterExtension.WriteVarString(ref writer, obj.Payload);
    }
}
```

`ServerCommandConverter` 同理，按 `Name`、`Payload` 顺序读写即可。

## 6. 实现 Unity 客户端

```csharp
using System;
using NanoService;
using TouchSocket.Sockets;

public sealed class GameClient : NanoService.NanoTcpClient
{
    private readonly Action<ServerCommand> _onCommand;

    public GameClient(Action<ServerCommand> onCommand)
    {
        _onCommand = onCommand;
    }

    protected override void RegisterServices()
    {
        RegisterService(new ServerCommandService(_onCommand));
        RegisterService(new TelemetryMessageConverter(), SendPolicy.Droppable);
    }
}

public sealed class ServerCommandService : NanoServiceBase<ServerCommand, ServerCommandConverter>
{
    private readonly Action<ServerCommand> _onCommand;

    public ServerCommandService(Action<ServerCommand> onCommand)
    {
        _onCommand = onCommand;
    }

    protected override void Handle(ServerCommand request, INanoCallContext context)
    {
        _onCommand(request);
    }
}
```

连接并发送：

```csharp
var client = new GameClient(command => UnityEngine.Debug.Log(command.Name));
await client.ConnectAsync(new IPHost("127.0.0.1:7788"));

client.Send(new TelemetryMessage
{
    Sequence = 1,
    SentUtcTicks = DateTime.UtcNow.Ticks,
    Payload = "hello"
});
```

## 7. 实现 Unity 服务端

```csharp
using System;
using NanoService;
using TouchSocket.Sockets;

public sealed class GameServer : NanoService.NanoTcpService
{
    private readonly Action<string, TelemetryMessage> _onTelemetry;

    public GameServer(Action<string, TelemetryMessage> onTelemetry)
    {
        _onTelemetry = onTelemetry;
    }

    protected override void RegisterServices()
    {
        RegisterService(new TelemetryMessageService(_onTelemetry));
        RegisterService(new ServerCommandConverter(), SendPolicy.Reliable);
    }
}

public sealed class TelemetryMessageService : NanoServiceBase<TelemetryMessage, TelemetryMessageConverter>
{
    private readonly Action<string, TelemetryMessage> _onTelemetry;

    public TelemetryMessageService(Action<string, TelemetryMessage> onTelemetry)
    {
        _onTelemetry = onTelemetry;
    }

    protected override void Handle(TelemetryMessage request, INanoCallContext context)
    {
        _onTelemetry(context.SessionId, request);
    }
}
```

启动服务端并下发命令：

```csharp
var server = new GameServer((sessionId, log) => UnityEngine.Debug.Log(log.Payload));
await server.StartAsync(new IPHost(7788));

server.Send(new ServerCommand { Name = "ping", Payload = "now" }, sessionId);
```

不传 `sessionId` 时，`Send` 会广播到所有在线会话。

## 8. 注意事项

- 两端必须使用相同的 `ServiceIdHelper` 算法和相同的序列化器实现。
- `SendPolicy` 在注册出站类型时声明，不要在每次调用时临时指定。
- 本项目 RPC 是单向消息，不等待响应、不提供 ack；需要 RTT 时可在测试代码中加 echo 消息。
- `Reliable` 表示本地队列满时等待，不表示业务层送达确认。
- NanoTransport 当前是 TCP-only；需要 UDP 的场景不适用。
- Unity 2022.3 应使用 `netstandard2.1` 程序集；旧版仅 net8.0 的包无法直接引用。

完整可运行代码见 [Unity/Assets/Scripts](Unity/Assets/Scripts)。
