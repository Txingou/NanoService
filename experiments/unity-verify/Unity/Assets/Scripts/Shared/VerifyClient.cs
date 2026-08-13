using System;
using NanoService;
using NanoTransport;

namespace UnityVerify
{
    public sealed class VerifyClient : NanoService.NanoTcpClient
    {
        private readonly Action<EchoReply>? _onEchoReply;
        private readonly Action<ServerCommand>? _onCommand;

        public VerifyClient(
            Action<EchoReply>? onEchoReply = null,
            Action<ServerCommand>? onCommand = null,
            NanoTransportOptions? options = null)
            : base(options)
        {
            _onEchoReply = onEchoReply;
            _onCommand = onCommand;
        }

        protected override void RegisterServices()
        {
            RegisterService(new EchoReplyService(_onEchoReply));
            RegisterService(new ServerCommandService(_onCommand));
            RegisterService(new TelemetryMessageConverter(), SendPolicy.Droppable);
            RegisterService(new EchoRequestConverter(), SendPolicy.Reliable);
        }
    }

    internal sealed class EchoReplyService : NanoServiceBase<EchoReply, EchoReplyConverter>
    {
        private readonly Action<EchoReply>? _onEchoReply;

        public EchoReplyService(Action<EchoReply>? onEchoReply)
        {
            _onEchoReply = onEchoReply;
        }

        protected override void Handle(EchoReply request, INanoCallContext context)
        {
            _onEchoReply?.Invoke(request);
        }
    }

    internal sealed class ServerCommandService : NanoServiceBase<ServerCommand, ServerCommandConverter>
    {
        private readonly Action<ServerCommand>? _onCommand;

        public ServerCommandService(Action<ServerCommand>? onCommand)
        {
            _onCommand = onCommand;
        }

        protected override void Handle(ServerCommand request, INanoCallContext context)
        {
            _onCommand?.Invoke(request);
        }
    }
}
