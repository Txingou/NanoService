using TouchSocket.Core;

namespace InterfaceExample.Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //处理
            ServiceDispatcher service = ServiceDispatcher.Static = new ServiceDispatcher();

            //发送
            SendServiceDispatcher sendService = new SendServiceDispatcher();
            //服务的转换器
            RemoteLogFastBinaryConverter converter = new RemoteLogFastBinaryConverter();
            //服务的处理
            RemoteLogRPCServer logRPCServer = new RemoteLogRPCServer();
            //服务数据包
            RemoteLog log = new RemoteLog()
            {
                LogLevel = TouchSocket.Core.LogLevel.Info,
                Message = "123"
            };
            //在实际使用中，调用方不应该知道服务的处理的具体逻辑，所以将转换器与服务处理进行拆分。
            //允许(TCP/UPD)客户端访问服务器，也允许反向访问

            //提供服务方 注册服务
            service.Register(logRPCServer);

            //访问端 注册转换器
            sendService.Register(converter);

            sendService.SendDispatch(log);
        }

    }
}
