using System.Net;
using System.Net.Sockets;
using NanoService;
using NanoService.Demo.Shared;
using TouchSocket.Core;
using TouchSocket.Sockets;
using Xunit;

namespace NanoService.Tests;

public sealed class EndToEndTests
{
    [Fact]
    public async Task UnifiedClientAndServer_RoundTrip()
    {
        var port = GetFreePort();
        var sessionTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionObjectTcs = new TaskCompletionSource<TcpSessionClient?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionTouched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandTcs = new TaskCompletionSource<DeviceCommand>(TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new TestServer((context, log) =>
        {
            sessionTcs.TrySetResult(context.SessionId);
            sessionObjectTcs.TrySetResult(context.GetSession<TcpSessionClient>());
            sessionTouched.TrySetResult(context.Session is not null);
        });
        var client = new TestClient(command => commandTcs.TrySetResult(command));

        try
        {
            await service.StartAsync(new IPHost($"127.0.0.1:{port}"));
            await client.ConnectAsync(new IPHost($"127.0.0.1:{port}"));

            var sent = client.Send(new TelemetryLog
            {
                LogLevel = LogLevel.Info,
                Message = "端到端遥测"
            });
            Assert.True(sent);

            var sessionId = await sessionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(string.IsNullOrEmpty(sessionId));

            var sessionObject = await sessionObjectTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(sessionObject);
            Assert.True(await sessionTouched.Task.WaitAsync(TimeSpan.FromSeconds(5)));

            var commandSent = service.Send(new DeviceCommand
            {
                Name = "reboot",
                Payload = "now"
            }, sessionId);
            Assert.True(commandSent);

            var command = await commandTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("reboot", command.Name);
            Assert.Equal("now", command.Payload);
        }
        finally
        {
            client.Dispose();
            service.Dispose();
        }
    }

    [Fact]
    public async Task GenericServer_UsesCustomSessionType()
    {
        var port = GetFreePort();
        var sessionTcs = new TaskCompletionSource<DemoSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandTcs = new TaskCompletionSource<DeviceCommand>(TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new TestGenericServer((context, log) =>
        {
            sessionTcs.TrySetResult(context.GetSession<DemoSession>());
        });
        var client = new TestClient(command => commandTcs.TrySetResult(command));

        try
        {
            await service.StartAsync(new IPHost($"127.0.0.1:{port}"));
            await client.ConnectAsync(new IPHost($"127.0.0.1:{port}"));

            client.Send(new TelemetryLog
            {
                LogLevel = LogLevel.Info,
                Message = "泛型会话遥测"
            });

            var session = await sessionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(session);
            Assert.IsType<DemoSession>(session);
        }
        finally
        {
            client.Dispose();
            service.Dispose();
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class TestServer : NanoTcpService
    {
        private readonly Action<INanoCallContext, TelemetryLog> _onLog;

        public TestServer(Action<INanoCallContext, TelemetryLog> onLog)
        {
            _onLog = onLog;
        }

        protected override void RegisterServices()
        {
            RegisterService(new CapturingTelemetryLogService(_onLog));
            RegisterService(new DeviceCommandConverter(), SendPolicy.Reliable);
        }
    }

    private sealed class TestGenericServer : NanoTcpService<DemoSession>
    {
        private readonly Action<INanoCallContext, TelemetryLog> _onLog;

        public TestGenericServer(Action<INanoCallContext, TelemetryLog> onLog)
        {
            _onLog = onLog;
        }

        protected override void RegisterServices()
        {
            RegisterService(new CapturingTelemetryLogService(_onLog));
        }
    }

    private sealed class TestClient : NanoTcpClient
    {
        private readonly Action<DeviceCommand> _onCommand;

        public TestClient(Action<DeviceCommand> onCommand)
        {
            _onCommand = onCommand;
        }

        protected override void RegisterServices()
        {
            RegisterService(new CapturingDeviceCommandService(_onCommand));
            RegisterService(new TelemetryLogConverter(), SendPolicy.Droppable);
        }
    }

    private sealed class CapturingTelemetryLogService : NanoServiceBase<TelemetryLog, TelemetryLogConverter>
    {
        private readonly Action<INanoCallContext, TelemetryLog> _onLog;

        public CapturingTelemetryLogService(Action<INanoCallContext, TelemetryLog> onLog)
        {
            _onLog = onLog;
        }

        protected override void Handle(TelemetryLog request, INanoCallContext context)
        {
            _onLog(context, request);
        }
    }

    private sealed class CapturingDeviceCommandService : NanoServiceBase<DeviceCommand, DeviceCommandConverter>
    {
        private readonly Action<DeviceCommand> _onCommand;

        public CapturingDeviceCommandService(Action<DeviceCommand> onCommand)
        {
            _onCommand = onCommand;
        }

        protected override void Handle(DeviceCommand request, INanoCallContext context)
        {
            _onCommand(request);
        }
    }

    private sealed class DemoSession : TcpSessionClient
    {
        public DemoSession()
        {
        }
    }
}
