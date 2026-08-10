namespace NanoService;

/// <summary>
/// 消息发送可靠性策略。
/// </summary>
public enum SendPolicy
{
    /// <summary>
    /// 可靠消息，队列满时等待，不丢弃。
    /// </summary>
    Reliable = 0,

    /// <summary>
    /// 可丢弃消息，队列满时丢弃新包并计数。
    /// </summary>
    Droppable = 1
}
