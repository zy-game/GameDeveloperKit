# debug-profilehandle-hardening 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-08
> 关联方案 doc：`.codestable/features/2026-06-08-debug-profilehandle-hardening/debug-profilehandle-hardening-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `ProfileTable` 构造已改为接收安全读取后的 name/category/columns/rows，不再在构造时调用 `ProfileHandle.Name` / `Category` getter。
- [x] `DebugProfileRegistry` 已安全读取 `Name`、`Category`、`RefreshInterval`、`Enabled`、`Columns`、`Snapshot()`。
- [x] `DebugStatusProfileHandle` 已作为内建 `ProfileHandle` 注册到 Debug startup。

**名词层逐项核对**：
- [x] `ProfileHandle` 仍是唯一 profile 扩展点，未新增第二套接口。
- [x] `ProfileTable.HasError` 仍以 `Exception != null` 表达错误状态。
- [x] redaction safe stringify 仅在 redaction 开启时作用于 exception/context。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] profile metadata/snapshot 任意 getter 抛异常只影响对应 table。
- [x] exception/context `ToString()` 抛异常不再击穿日志 API。
- [x] Debug startup 后 `Profiles.Snapshot()` 包含内建 `Runtime/Debug` table。

**明确不做逐项核对**：
- [x] 未新增第二套 profile 接口。
- [x] 未实现 transport pending/cancel/shutdown dispatcher。
- [x] 未把 Debug refresh 接入 Timer。
- [x] 未新增 Network 模块或网络发送实现。
- [x] 未重命名 `GameDeveloperKit.Logger` namespace。

## 3. 验收场景核对

- [x] N1/N2：正常 profile 与 `Snapshot()` 抛异常 profile 均能生成 table，坏 profile 不影响好 profile；证据：`ProfileRefresh_WhenHandleThrows_IsolatesError`。
- [x] N3/N4/N5：`Name`、`Category`、`RefreshInterval`、`Columns` 抛异常时 register/refresh 不抛，并记录 error table；证据：`ProfileRefresh_WhenMetadataThrows_IsolatesError`。
- [x] N6/N7：redaction 开启时 exception/context `ToString()` 抛异常不击穿日志 API；证据：`Log_WhenRedactionToStringThrows_UsesFallback`。
- [x] N8：Debug startup 后包含内建 `Runtime/Debug` table；证据：`Startup_RegistersDebugStatusProfile`。
- [x] B1：redaction 关闭时 exception/context 保持原对象；证据：`Log_WhenRedactionDisabled_DoesNotStringifyExceptionOrContext`。
- [x] E1：`RegisterProfile(null)` / `UnregisterProfile(null)` 仍抛 `ArgumentNullException`；证据：`UnregisterProfile_WhenHandleRegistered_RemovesProfileTable`。

## 4. 术语一致性

- `ProfileHandle`：仍是唯一 profile 扩展点。
- `ProfileTable`：保存安全 metadata 后的 table，不再调用不可信 getter。
- `DebugStatusProfileHandle`：只作为 Debug 内建状态 profile，不替代后续 transport dispatcher。
- 防冲突：未新增 `DiagnosticsModule`、Network 发送实现或 Timer refresh handle。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已更新 Debug 小节，记录 profile metadata/snapshot 异常隔离。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 redaction safe stringify 和 redaction disabled 保留原对象语义。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录内建 `Runtime/Debug` 状态 profile。
- [x] 架构文档已明确 transport dispatcher 和 Timer refresh 仍是后续 roadmap item。

## 6. requirement 回写

- [x] design frontmatter 指向 `runtime-diagnostics`。本次修的是 Debug 诊断中枢内部鲁棒性和可观测性，未改变 requirement 用户故事边界；无需更新 requirement 正文。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `debug-profilehandle-hardening` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done，并在变更日志记录本次完成项。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime 快速编译命令仍适用。

## 9. 遗留

- 后续 roadmap item：`debug-transport-dispatcher` 仍需处理 transport pending/failed 计数和 shutdown 收束。
- 后续 roadmap item：`debug-timer-refresh` 仍需把 metrics/profile refresh 接入 Timer update handle。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore /p:UseSharedCompilation=false` 通过；`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore /p:UseSharedCompilation=false` 通过。
