using System.Collections.Concurrent;
using System.Reflection.PortableExecutable;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace InterfaceExample
{

    /// <summary>
    /// 仅仅用于公开Read和Write方法，同时适配Touchsocket的二进制序列化，
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FastBinaryConverterTwo<T> : FastBinaryConverter<T>
    {
        public FastBinaryConverterTwo()
        {

        }


        /// <summary>
        /// 从字节块中读取对象。必须由具体实现类实现。
        /// </summary>
        /// <param name="reader">包含对象数据的字节块。</param>
        /// <param name="type">要读取的对象的类型。</param>
        /// <typeparam name="TReader">字节块的类型，实现了IByteBlock接口。</typeparam>
        /// <returns>从字节块中读取的对象实例。</returns>
        public T PublicRead<TReader>(ref TReader reader, Type type) where TReader : IBytesReader
        {
            return Read(ref reader, type);
        }

        /// <summary>
        /// 将对象写入字节块。必须由具体实现类实现。
        /// </summary>
        /// <param name="writer">将要包含对象数据的字节块。</param>
        /// <param name="obj">要写入的对象实例。</param>
        /// <typeparam name="TWriter">字节块的类型，实现了IByteBlock接口。</typeparam>
        public void PublicWrite<TWriter>(ref TWriter writer, in T obj) where TWriter : IBytesWriter
        {
            Write(ref writer, in obj);

        }
    }

    internal interface IAbstractRPCService
    {
        void Dispatch(uint serviceId, IBytesReader body);
    }
    public abstract class AbstractRPCService<T, Convert> : IAbstractRPCService
        where Convert : FastBinaryConverterTwo<T>, new()
    {
        Type modleType;
        FastBinaryConverterTwo<T> ConverterTwo;
        public AbstractRPCService()
        {
            this.ConverterTwo = new Convert();
            modleType = typeof(T);
        }
        /// <summary>
        /// 远程访问处理
        /// </summary>
        /// <param name="request"></param>
        protected abstract void RPCVisitHandle(T request);

        void IAbstractRPCService.Dispatch(uint serviceId, IBytesReader body)
        {
            var request = ConverterTwo.PublicRead(ref body, modleType);
            RPCVisitHandle(request);
        }
    }
    public class ServicelayerBase
    {
        public IFastBinaryConverter Converter;
        public Action<object> Handler;
    }
    public class SendServiceDispatcher
    {

        ConcurrentDictionary<Type, IFastBinaryConverter> FastBinaryConverters = new ConcurrentDictionary<Type, IFastBinaryConverter>();

        public void Register<TRequest>(FastBinaryConverterTwo<TRequest> converter) where TRequest : class
        {
            //使用类型作为Key有点丑陋
            FastBinaryConverters.TryAdd(typeof(TRequest), converter);
        }

        public void SendDispatch<TRequest>(TRequest request, int bodyLength = 1024)
        {
            //我觉得Type保存 检索，每次发送都要反射有点过于丑陋，请帮我优化
            if (FastBinaryConverters.TryGetValue(typeof(TRequest), out var converter))
            {
                //找到服务后应该
                var byteBlock = new ByteBlock(bodyLength);
                converter.Write(ref byteBlock, request);

                //发送流程
                //在另一类库中NanoTransport中会得到HashCode(ServiceId),并按照协议进行发送
                //协议头结构（固定 12 字节）
                //偏移 大小  字段 类型  说明
                //0   4   Magic   uint 固定魔数 0x5A6C8F01，用于接收端包同步。
                //4   4   TotalLength uint 整个数据包总长度（含 12 字节头部）。
                //8   4   ServiceId   uint 业务服务标识符，通信库不解析其含义。
                //12  可变 Body    byte[] 业务数据（二进制内容，由业务层自行定义格式）。

                //接收与解析流程
                //Toucksocket 应该使用ITcpReceivingPlugin插件得到数据
                //使用NanoTransport类库进行数据解析,得到ServiceId与Body
                //假设代码
                var ServiceId = (uint)typeof(TRequest).GetHashCode();
                var Body = byteBlock;
                ///重置游标才能正常读取
                byteBlock.SeekToStart();

                //访问ServiceDispatcher.Dispatch函数触发RPC服务分发
                ServiceDispatcher.Static.Dispatch(ServiceId, byteBlock);

            }
        }
    }
    public class ServiceDispatcher
    {
        ConcurrentDictionary<uint, IAbstractRPCService> ServiceDictionary = new ConcurrentDictionary<uint, IAbstractRPCService>();

        //仅仅用于演示，方便SendServiceDispatcher访问，实际不应该如此
        public static ServiceDispatcher Static = new ServiceDispatcher();

        /// <summary>
        /// 分发请求
        /// </summary>
        public void Dispatch(uint serviceId, IBytesReader body)
        {
            if (ServiceDictionary.TryGetValue(serviceId, out var servicelayerBase))
            {
                servicelayerBase.Dispatch(serviceId, body);
            }
            //记录日志
        }
        /// <summary>
        /// 注册服务需要知道转换器与处理对象
        /// </summary>
        public void Register<TRequest, TConverter>(AbstractRPCService<TRequest, TConverter> rpc)
            where TRequest : class
            where TConverter : FastBinaryConverterTwo<TRequest>, new()
        {
            //GetHashCode是有问题的，请帮我修复
            ServiceDictionary.TryAdd((uint)typeof(TRequest).GetHashCode(), rpc);
        }

    }

    /// <summary>
    /// 远程日志
    /// </summary>
    public class RemoteLog
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; }
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

    }
    /// <summary>
    /// 序列化器
    /// </summary>
    public class RemoteLogFastBinaryConverter : FastBinaryConverterTwo<RemoteLog>
    {
        protected override RemoteLog Read<TReader>(ref TReader reader, Type type)
        {
            var result = new RemoteLog();
            result.LogLevel = (LogLevel)ReaderExtension.ReadValue<TReader, int>(ref reader);
            result.Message = ReaderExtension.ReadVarString<TReader>(ref reader);
            return result;
        }
        protected override void Write<TWriter>(ref TWriter writer, in RemoteLog obj)
        {
            WriterExtension.WriteValue(ref writer, obj.LogLevel);
            WriterExtension.WriteString(ref writer, obj.Message);
        }
    }

    public class RemoteLogRPCServer : AbstractRPCService<RemoteLog, RemoteLogFastBinaryConverter>
    {

        protected override void RPCVisitHandle(RemoteLog request)
        {
            var logger = ConsoleLogger.Default;
            logger.Log(request.LogLevel, null, request.Message, null);
            // ... 具体业务
            Console.WriteLine(request.LogLevel);
            Console.WriteLine(request.Message);
        }
    }

}
