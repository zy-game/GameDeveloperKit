---
doc_type: audit-finding
audit: 2026-06-08-debug-module
finding_id: "maintainability-05"
nature: maintainability
severity: P2
confidence: high
suggested_action: cs-refactor
status: open
---

# Finding 05：`DebugModule` 聚合过多子域，后续改动风险偏高

## 速答

`DebugModule.cs` 约 879 行，把日志 API、sink、transport、analytics、profile、metrics、IMGUI、命令、Unity log capture、redaction 和 driver 生命周期都放在一个类里，后续修任何子域都容易牵动其它行为。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:76` — 同一个类负责 Startup 初始化 sink、日志 buffer、profile、Console、Unity log capture 和 GUI driver。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:180` — 同一个类实现日志 record 归一化、redaction、buffer、sink 和 transport 输出。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:313` — 同一个类实现 analytics 入口。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:349` — 同一个类实现 Debug Console 命令执行入口。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:428` — 同一个类实现 profile refresh 和 metrics sample。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:457` — 同一个类直接绘制完整 IMGUI Console。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:739` — 同一个类管理 Unity log capture 订阅。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:805` — 同一个类实现 redaction helper。

## 影响

Debug 子系统已经有 `Logs/`、`Profiles/`、`Console/`、`Tools/`、`Transports/` 目录，但核心流程仍集中在单个主类中。P1 问题的共同特征也是边界交叉：transport shutdown、profile exception、redaction before sink。继续堆功能会让测试组合爆炸，修一条边界时也更容易误伤 Console 或日志主路径。

## 修复方向

在不改变公开 `App.Debug` API 的前提下，把 log pipeline、transport dispatcher、profile scheduler、console renderer、redaction policy 拆成内部协作者，并用 DebugModule 只做门面和生命周期编排。

## 建议动作

`cs-refactor`，因为这是结构性债务，适合等 P1 行为修稳后分阶段拆分。
