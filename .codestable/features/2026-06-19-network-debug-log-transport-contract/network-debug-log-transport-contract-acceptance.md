# network-debug-log-transport-contract 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-19
> 关联方案 doc：`.codestable/features/2026-06-19-network-debug-log-transport-contract/network-debug-log-transport-contract-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `DebugLogPayload` 已落在 `GameDeveloperKit.Logger`，公开 `Sequence` / `Timestamp` / `FrameCount` / `TimerTick` / `Level` / `Category` / `Message` / `Exception` / `Context` / `Tags`。
- [x] `IDebugLogNetworkSender.SendDebugLogAsync(DebugLogPayload payload)` 已落地，具体发送实现由外部实现。
- [x] `DebugLogNetworkBridge(DebugLogBuffer logs, IDebugLogNetworkSender sender)`、`FlushAsync()`、`LastSentSequence` 和 `ToPayload(DebugLogRecord)` 已落地。

**名词层逐项核对**：
- [x] payload 字段来自 `DebugLogRecord`；`Level` 使用字符串，`Exception` / `Context` 使用安全字符串化结果。
- [x] `DebugLogNetworkBridge` 只接收 `DebugLogBuffer` 和 sender，不接收 `DebugModule` 或 `NetworkModule`。
- [x] `DebugLogPayload` 未继承 `Message`，不绑定 socket/HTTP 具体协议。

**流程图核对**：
- [x] Debug 写入日志 -> `DebugLogBuffer` 保存已脱敏 `DebugLogRecord` 的既有路径未改。
- [x] 外部显式创建 bridge -> `FlushAsync()` -> `Snapshot()` -> 过滤 `Sequence > LastSentSequence` -> sender 已落地。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `GameDeveloperKit.Logger` 命名空间提供 payload、sender 和 bridge。
- [x] bridge 从 `DebugLogBuffer.Snapshot()` 读取记录并调用 sender。
- [x] bridge 按 `Sequence` 维护游标，只发送尚未发送的新日志。
- [x] payload 中 exception/context 是字符串字段，转换过程不因 `ToString()` 抛异常而失败。
- [x] DebugModule 未新增 sender、transport、sink、analytics 或旧输出扩展点。

**明确不做逐项核对**：
- [x] 未实现具体服务端协议、HTTP endpoint、socket message、鉴权、连接状态、重试、批量上传、断线缓存或限流。
- [x] 未让 DebugModule 持有 Network sender/transport，未让 Debug 声明 NetworkModule 依赖。
- [x] bridge 未调用 `DebugRedactionUtility` 做二次 redaction。
- [x] 未新增本地日志持久化、rolling file、上传包或手动导出。
- [x] 未改变 `DebugLogBuffer` ring buffer 语义或 `DebugLogRecord` 字段。

**关键决策落地**：
- [x] bridge 属于 Debug/Logger 导出契约：类型都在 `GameDeveloperKit.Logger`，`DebugModule` 只暴露 logs/records，不持有发送生命周期。
- [x] sender 不绑定 `NetworkModule`：接口只固定 payload 发送方法。
- [x] sequence 游标已落地：`LastSentSequence` 只在 sender 成功后推进。
- [x] Network 转换不做二次 redaction：只进行字段映射和安全字符串化。

**挂载点反向核对**：
- [x] `Assets/GameDeveloperKit/Runtime/Debug/DebugLogPayload.cs` / `IDebugLogNetworkSender.cs` / `DebugLogNetworkBridge.cs`：新增 payload、sender、bridge 契约。
- [x] 未新增自动启动挂载点；删除这些契约后 Debug/Network 其他能力不受影响。

## 3. 验收场景核对

- [x] N1：构造 `DebugLogPayload` 且 tags 为 null -> tags 为空列表；证据：`DebugLogPayload_WhenTagsAreNull_UsesEmptyTags`。
- [x] N2/N3：`ToPayload(record)` 映射 sequence/timestamp/frame/tick/level/category/message/tags，并把 exception/context 转字符串；证据：`DebugLogNetworkBridge_ToPayload_MapsDebugLogRecordFields`。
- [x] N4：exception/context `ToString()` 抛异常 -> payload 使用 fallback；证据：`DebugLogNetworkBridge_ToPayload_WhenToStringThrows_UsesFallbackText`。
- [x] N5/N6/N7：首次 flush 发送全部新记录，重复 flush 不重发，追加后只发新增；证据：`DebugLogNetworkBridge_WhenFlushed_SendsOnlyNewRecords`。
- [x] E1：sender 第二条失败 -> flush 抛异常，游标只推进到第一条，后续 flush 重试失败记录；证据：`DebugLogNetworkBridge_WhenSenderFails_StopsAndKeepsCursorAtLastSuccess`。
- [x] E2：构造 bridge 参数 null -> 抛 `ArgumentNullException`；证据：`DebugLogNetworkBridge_WhenArgumentsAreInvalid_Throws`。

## 4. 术语一致性

- `DebugLogPayload` / `IDebugLogNetworkSender` / `DebugLogNetworkBridge` 均在 `GameDeveloperKit.Logger`。
- `DebugLogRecord` / `DebugLogBuffer` 仍在 `GameDeveloperKit.Logger`，Network 只读取公开表面。
- 防冲突：未新增 `GameDeveloperKit.Network.IDebugLogNetworkSender`、`GameDeveloperKit.Network.DebugLogNetworkBridge`、`GameDeveloperKit.Network.DebugLogPayload` 或旧 `IDebugLogTransport`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` Debug 小节已更新：实时网络日志由 Debug/Logger 侧 bridge 读取已脱敏记录，DebugModule 不持有 sender/transport。
- [x] `.codestable/architecture/ARCHITECTURE.md` Network 小节已更新：Network 只记录 channel/HTTP 现状，不承载 Debug log bridge 类型。
- [x] 已知约束补充 Debug log network export 边界：只做字段转换、安全字符串化和 sequence 游标，不做 retry/batch/cache/auth/throttle/endpoint。

## 6. requirement 回写

- [x] design frontmatter 指向 `runtime-diagnostics`。已把 `2026-06-19-network-debug-log-transport-contract` 补入 `implemented_by`。
- [x] `runtime-diagnostics` 仍保持 `draft`，因为本次只实现 bridge 契约，命令工具和完整实时服务端发送能力还不是统一闭环。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `network-debug-log-transport-contract` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done。
- [x] roadmap 主文档状态已改为 `completed`，因为全部 items 均为 done。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime / Tests 快速编译命令仍适用。

## 9. 遗留

- 后续如果要做完整日志上报，需要另起 feature 覆盖 sender 具体实现、endpoint、鉴权、连接状态、重试、批量、断线缓存和限流。
- `DebugLogPayload` / sender / bridge 已从 `NetworkModule` 拆出为 Debug 目录下独立文件，当前 Runtime csproj 已包含这些文件。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 通过。
- 验证：`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-19-network-debug-log-transport-contract/network-debug-log-transport-contract-checklist.yaml --yaml-only` 通过。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml --yaml-only` 通过。
