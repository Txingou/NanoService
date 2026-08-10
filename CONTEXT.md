# NanoService

NanoService 是面向 ARM32 嵌入式设备的 .NET 8 远程调用框架，由 RPC 层（NanoService）和传输层（NanoTransport）组成。本框架解决设备与云端之间的类型安全、单向消息传递与自动路由问题。

## Language

**NanoService**:
RPC 层，将请求类型、序列化器、业务处理器绑定为独立单元，并按 ServiceId 路由到对应处理器。
_Avoid_: 网络传输、连接管理

**NanoServiceHost**:
服务端路由器，按 ServiceId 找到业务处理器并调用。
_Avoid_: ServiceDispatcher

**NanoServiceClient**:
客户端发送器，把强类型请求序列化后交给 NanoTransport 发送。
_Avoid_: SendServiceDispatcher

**统一 TCP 客户端**:
组合 NanoTransport 客户端、NanoServiceClient 与 NanoServiceHost 的单一入口，业务只调用 Send<T> 即可发送。
_Avoid_: 分别手动装配三个对象

**统一 TCP 服务端**:
组合 NanoTransport 服务端、NanoServiceClient 与 NanoServiceHost 的单一入口，可泛型指定 TcpSessionClient 子类。
_Avoid_: 分别手动装配三个对象

**服务注册钩子（RegisterServices）**:
派生类集中注册入站处理器与出站序列化器的入口，框架在首次连接或发送前自动调用。
_Avoid_: 构造函数内注册、手动 Initialize

**NanoBinaryConverter**:
业务模型与字节之间的序列化契约，发送端与接收端必须共用同一实现。
_Avoid_: FastBinaryConverterTwo

**NanoServiceBase**:
业务处理器基类，绑定请求类型与序列化器，子类实现请求处理入口。
_Avoid_: AbstractRPCService、RPCVisitHandle

**NanoTransport**:
传输层，负责 12 字节协议头、发送队列、背压丢弃和 TCP 连接；不解析业务模型。
_Avoid_: RPC 层、业务序列化

**ServiceId**:
业务服务的唯一路由标识，客户端与服务端必须使用同一算法计算。
_Avoid_: 方法名、字符串类型名

**RPC（本项目语境）**:
单向远程调用：发送方发出消息后即返回，不等待响应、不要求 ack。
_Avoid_: 请求-响应调用

**消息可靠性**:
消息的丢弃策略：遥测消息可丢弃（Droppable），命令消息不可丢弃（Reliable）。
_Avoid_: 优先级（暗示数值排序）

**发送策略（SendPolicy）**:
注册发送类型时显式声明的消息可靠性级别，默认 Reliable，遥测注册为 Droppable。
_Avoid_: 调用时临时指定

**设备**:
ARM32 嵌入式设备，作为 TCP 客户端主动连接云端。
_Avoid_: 服务器、被连接方

**云端**:
集中式服务端，作为 TCP 服务端接收设备连接，并通过同一长连接向设备发送命令。
_Avoid_: 客户端、被连接方

**会话**:
设备与云端之间的一条 TCP 长连接，连接上可双向收发消息。
_Avoid_: 端口、socket 句柄

**远程调用上下文（INanoCallContext）**:
处理器收到的会话身份信息，用于识别消息来自哪个设备。
_Avoid_: IRemoteCallContext、请求头

**入队速率**:
客户端 Send<T> 成功入队的消息速率，衡量业务侧发送调用本身的成本。
_Avoid_: 发送成功率

**送达速率**:
服务端 NanoServiceHost 实际收到并完成分发的消息速率，衡量端到端真实处理能力。
_Avoid_: 客户端调用次数

**端到端延迟**:
基准项目通过 EchoRequest/EchoReply 回环模型测得的往返耗时，单向处理约为其一半。
_Avoid_: RPC 响应时间

**分层微基准**:
隔离测量某一层单个操作（序列化、ServiceId、协议头编码、入队、进程内分发）耗时与分配的基准，不经过真实网络。
_Avoid_: 端到端测试、性能压测

**客户端入队吞吐**:
吞吐基准中的客户端发送侧指标，指单次 Send<T> 或 SendRaw 调用完成序列化与入队所需的耗时。
_Avoid_: 网络吞吐、每秒消息数

**负载场景**:
基准项目中的持续发送场景，客户端以最快速度连续入队，统计入队、送达与丢弃速率。
_Avoid_: 压测报告、压力测试

**过载场景**:
基准项目中的背压验证场景，通过调小 Droppable 容量并连续入队，验证丢新包与 Reliable 阻塞行为。
_Avoid_: 崩溃测试、断连测试
