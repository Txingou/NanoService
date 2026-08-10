using TouchSocket.Core;

namespace NanoService;

/// <summary>
/// 业务模型与字节之间的序列化契约，发送端与接收端必须共用同一实现。
/// </summary>
/// <typeparam name="T">业务模型类型。</typeparam>
public abstract class NanoBinaryConverter<T> : FastBinaryConverter<T>
{
    /// <summary>
    /// 从字节读取器中反序列化对象。
    /// </summary>
    public T Deserialize<TReader>(ref TReader reader, Type type)
        where TReader : IBytesReader
    {
        return Read(ref reader, type);
    }

    /// <summary>
    /// 将对象序列化到字节写入器。
    /// </summary>
    public void Serialize<TWriter>(ref TWriter writer, in T obj)
        where TWriter : IBytesWriter
    {
        Write(ref writer, in obj);
    }
}
