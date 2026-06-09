---
doc_type: audit-finding
audit: 2026-06-08-debug-module
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 02：ProfileHandle 的 `Name`/`Category` 抛异常会击穿隔离

## 速答

`DebugProfileRegistry` 只隔离了 `Columns` 和 `Snapshot()` 的一部分异常，但 catch 后创建错误 table 时仍会读取 `handle.Name` / `handle.Category`；这些 getter 一旦抛异常，整个 profile refresh 会继续向外抛。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/ProfileHandle.cs:7` — `Name` 是扩展点必须实现的 abstract getter，外部模块可以提供任意逻辑。
- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/ProfileHandle.cs:9` — `Category` 是 virtual getter，也可能被外部 handle 覆盖。
- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/DebugProfileRegistry.cs:86` — `Refresh(ProfileState)` 用 try/catch 包住 profile 刷新。
- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/ProfileTable.cs:15` — `ProfileTable` 构造函数读取 `handle.Name`。
- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/ProfileTable.cs:16` — 同一构造函数读取 `handle.Category`。
- `Assets/GameDeveloperKit/Runtime/Debug/Profiles/DebugProfileRegistry.cs:107` — catch 分支再次 `new ProfileTable(state.Handle, ...)`，如果 `Name` 或 `Category` 是异常源，catch 分支会重复触发并向外抛。

## 影响

架构文档记录单个 profile 抛异常时应记录到对应 table，不影响其他 profile。当前实现对 `Snapshot()` 抛异常已有覆盖，但对 `Name` / `Category` getter 没有兜底；一个坏的 profile handle 可以让注册或刷新路径整体失败，进而影响 Debug Console 的 Profiles tab 和 metrics 更新。

## 修复方向

在 registry 层先安全读取 profile 元数据，或让错误 table 接受已经兜底后的 name/category 字符串，避免 catch 分支再次调用不可信 getter。

## 建议动作

`cs-issue`，因为这是扩展点异常隔离契约没有完整落地，且触发路径明确。
