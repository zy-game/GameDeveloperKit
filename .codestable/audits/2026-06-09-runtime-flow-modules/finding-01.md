---
doc_type: audit-finding
audit: 2026-06-09-runtime-flow-modules
finding_id: "bug-01"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 01：EventModule 嵌套派发复用同一个 dispatch cache

## 速答

原问题中 `EventModule.Fire()` 用模块级 `m_DispatchCache` 承载当前派发快照；如果某个 listener 内再次调用 `Fire()`，内层派发会清空并改写同一个 List，外层 `foreach` 下一次推进时会抛集合修改异常。当前已修复：`Fire()` 改为入队，Timer Update 再派发；即时同步入口改为显式的 `FireNow()`，调用方需自行承担重入风险。

## 修复状态

- 状态：已修复。
- 修复记录：`.codestable/issues/2026-06-09-runtime-flow-modules-audit-fixes/runtime-flow-modules-audit-fixes-fix-note.md`
- 验证：新增覆盖 `Fire()` 延迟到 Timer Update、listener 中再次 `Fire()` 推迟到下一次 Update 的测试。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:13`：`private readonly List<Listener> m_DispatchCache = new List<Listener>();`，全模块只有一个派发缓存。
- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:193` 到 `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:195`：`m_DispatchCache.Clear(); m_DispatchCache.AddRange(listeners); foreach (var listener in m_DispatchCache)`，外层派发正在枚举这个共享 List。
- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:207` 到 `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:213`：listener 回调是任意业务代码，可以再次调用同一个 `EventModule.Fire()`。
- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:217`：派发结束再次 `m_DispatchCache.Clear()`，内层派发会修改外层正在枚举的集合版本。

## 影响

触发条件很普通：事件 A 的 listener 内同步触发事件 B 或再次触发事件 A，且外层 listener 后面还有待派发项。结果不是单纯顺序变化，而是 `List<T>` 枚举器检测到集合版本变化后抛 `InvalidOperationException`，导致外层事件链中断。

## 修复方向

让每次 `Fire()` 使用局部快照，或引入 dispatch stack / pooled snapshot，保证嵌套派发不会修改外层正在枚举的集合。

## 建议动作

`cs-issue`，因为这是确定性运行时 bug，修复点集中在 EventModule 派发快照策略。
