using System.Text;

namespace NanoService;

/// <summary>
/// ServiceId 计算工具，使用 FNV-1a 32 位哈希，保证跨进程一致。
/// </summary>
public static class ServiceIdHelper
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    /// <summary>
    /// 根据请求类型计算 ServiceId。
    /// </summary>
    public static uint Compute<T>()
    {
        return Compute(typeof(T));
    }

    /// <summary>
    /// 根据类型计算 ServiceId。
    /// </summary>
    public static uint Compute(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var fullName = type.FullName ?? type.Name;
        var hash = OffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(fullName))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash;
    }
}
