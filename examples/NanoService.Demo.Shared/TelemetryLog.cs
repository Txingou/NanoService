using TouchSocket.Core;

namespace NanoService.Demo.Shared;

/// <summary>
/// 设备侧发送给云端的遥测日志。
/// </summary>
public sealed class TelemetryLog
{
    /// <summary>
    /// 日志级别。
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 日志内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
