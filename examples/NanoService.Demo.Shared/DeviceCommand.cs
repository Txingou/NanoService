namespace NanoService.Demo.Shared;

/// <summary>
/// 云端下发给设备的命令。
/// </summary>
public sealed class DeviceCommand
{
    /// <summary>
    /// 命令名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 命令参数。
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
