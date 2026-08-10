# 12 字节自定义固定包头协议

协议头采用 Magic + TotalLength + ServiceId 共 12 字节、大端字节序，Body 为业务序列化数据；TotalLength 表示整包长度（含 12 字节头）。接收端使用 CustomFixedHeaderDataHandlingAdapter 在适配器内完成 Magic 校验与拆包。

未采用 FixedHeaderPackageAdapter，因为其 Int 包头要求首 4 字节是长度字段，与资料中 Magic 在偏移 0、TotalLength 在偏移 4 的布局冲突；自定义固定包头适配器可以同时完成 Magic 同步和业务头解析。
