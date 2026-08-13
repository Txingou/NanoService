using System;
using NanoService;
using NanoTransport;

namespace UnityVerify
{
    public sealed class VerifyServer : NanoService.NanoTcpService
    {
        private readonly Action<INanoCallContext, TelemetryMessage>? _onTelemetry;
        private readonly Action<INanoCallContext, EchoRequest>? _onEcho;

        public VerifyServer(
            Action<INanoCallContext, TelemetryMessage>? onTelemetry = null,
            Action<INanoCallContext, EchoRequest>? onEcho = null,
            NanoTransportOptions? options = null)
            : base(options)
        {
            _onTelemetry = onTelemetry;
            _onEcho = onEcho;
        }

        protected override void RegisterServices()
        {
            RegisterService(new TelemetryMessageService(_onTelemetry));
            RegisterService(new EchoRequestService(this, _onEcho));
            RegisterService(new ServerCommandConverter(), SendPolicy.Reliable);
            RegisterService(new EchoReplyConverter(), SendPolicy.Reliable);
        }
    }

    internal sealed class TelemetryMessageService : NanoServiceBase<TelemetryMessage, TelemetryMessageConverter>
    {
        private readonly Action<INanoCallContext, TelemetryMessage>? _onTelemetry;

        public TelemetryMessageService(Action<INanoCallContext, TelemetryMessage>? onTelemetry)
        {
            _onTelemetry = onTelemetry;
        }

        protected override void Handle(TelemetryMessage request, INanoCallContext context)
        {
            _onTelemetry?.Invoke(context, request);
        }
    }

    internal sealed class EchoRequestService : NanoServiceBase<EchoRequest, EchoRequestConverter>
    {
        private readonly VerifyServer _server;
        private readonly Action<INanoCallContext, EchoRequest>? _onEcho;

        public EchoRequestService(VerifyServer server, Action<INanoCallContext, EchoRequest>? onEcho)
        {
            _server = server;
            _onEcho = onEcho;
        }

        protected override void Handle(EchoRequest request, INanoCallContext context)
        {
            _onEcho?.Invoke(context, request);
            _server.Send(new EchoReply { SentUtcTicks = request.SentUtcTicks }, context.SessionId);
        }
    }
}
