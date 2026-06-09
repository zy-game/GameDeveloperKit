---
doc_type: audit-finding
audit: 2026-06-07-timer-module
finding_id: "bug-03"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 03：重复 `Startup()` 会遗留旧 Timer driver，只清理最后一个对象

## 速答

`TimerModule.Startup()` 每次都会创建新的 `"Timer"` GameObject 并覆盖 `_timer`，没有像其他持有 Unity root 的模块一样先判断已启动；如果同一个 module 实例被重复启动，旧 driver 会继续留在场景中，`Shutdown()` 只能销毁最后一次赋给 `_timer` 的对象。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:44` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:49` — `Startup()` 无已启动 guard，每次都会 `new GameObject("Timer").AddComponent<Timer>()` 并 `DontDestroyOnLoad`。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:67` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:71` — `Shutdown()` 只根据当前 `_timer` 引用清理一个 GameObject。
- `Assets/GameDeveloperKit/Runtime/Sound/SoundModule.cs:28` 到 `Assets/GameDeveloperKit/Runtime/Sound/SoundModule.cs:33` — 同类 root 模块在 `Startup()` 开头用 `m_Root != null` 保证重复启动幂等。
- `Assets/GameDeveloperKit/Runtime/UI/UIModule.cs:38` 到 `Assets/GameDeveloperKit/Runtime/UI/UIModule.cs:43` — UI 模块也采用重复 `Startup()` no-op 模式。
- `Assets/GameDeveloperKit/Tests/Runtime/TimerModuleTests.cs:93` 到 `Assets/GameDeveloperKit/Tests/Runtime/TimerModuleTests.cs:96` — 现有测试覆盖重复 `Shutdown()`，但没有覆盖重复 `Startup()`。

## 影响

通过 `App.Register<TimerModule>()` 时 App 会阻止重复注册，但测试、手动创建模块、未来热重载或外部生命周期编排仍可能直接调用同一实例 `Startup()`。一旦发生，旧 `"Timer"` driver 的 `onUpdate` 仍指向同一个 module，后续每个 `FixedUpdate` 可能被多个 driver 推进；`Shutdown()` 只销毁最后一个 driver，旧对象会跨场景残留。

## 修复方向

在 `Startup()` 开头加入 `_timer != null` guard，并补重复 `Startup()` 只保留一个 `"Timer"` driver、tick 不被双倍推进的测试。

## 建议动作

`cs-issue`，因为这是 Unity 生命周期对象泄漏和重复 tick 的组合问题。
