# 统一 TCP 门面对象

NanoService 提供 `NanoTcpClient` 与 `NanoTcpService`（含泛型 `NanoTcpService<TClient>`）作为统一入口，内部组合 NanoServiceClient 与 NanoServiceHost。业务方通过继承并在 `RegisterServices()` 中注册入站处理器与出站序列化器，之后直接调用 `Send<T>`，无需手动装配底层对象。

底层 NanoServiceClient / NanoServiceHost 仍保持公开，供需要精细控制的高级场景使用；泛型服务端通过 `TcpService<TClient>` 支持自定义 TcpSessionClient，且 INanoCallContext 携带真实会话对象。
