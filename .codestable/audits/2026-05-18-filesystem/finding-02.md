---
doc_type: audit-finding
audit: 2026-05-18-filesystem
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 02：Startup 前调用公开 API 会空引用崩溃

## 速答

`FileModule` 构造后立即通过 `Super.FileSystem` 暴露，但 `m_Manifest` 只在 `Startup()` 中初始化，公开 API 没有生命周期守卫。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:27` — `Super.Register(this);` —— 构造期间即注册，外部可通过 `Super.FileSystem` 取得实例。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:76` — `if (!m_Manifest.TryGetEntry(path, out var entry))` —— `ReadFileAsync` 未检查 `m_Manifest` 是否为 null。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:115` — `if (!m_Manifest.TryGetEntry(path, out var entry))` —— `Exists` 同样直接解引用。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:172` — `m_Manifest = await VfsManifest.LoadAsync(m_RootPath);` —— 初始化发生在 `Startup()`，调用顺序依赖没有在 API 层表达。

## 影响

任何在 `Super.StartupAllAsync()` 前或 `Release()` 后误用 FileSystem 的调用都会抛 `NullReferenceException`，错误信息不可诊断，且 Unity 初始化顺序变动时容易触发。

## 修复方向

给公开 API 增加统一的已启动状态检查，或调整注册/初始化顺序，抛出明确的框架异常。

## 建议动作

`cs-issue`，因为这是生命周期边界缺陷，应该补守卫和对应测试。
