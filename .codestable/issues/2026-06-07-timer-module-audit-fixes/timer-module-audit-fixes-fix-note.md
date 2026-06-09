---
doc_type: issue-fix
issue: 2026-06-07-timer-module-audit-fixes
path: fast-track
fix_date: 2026-06-07
status: fixed
tags:
  - timer
  - audit
  - runtime
  - architecture
---

# Timer Module Audit Fixes 修复记录

## 1. 问题描述

来源：`.codestable/audits/2026-06-07-timer-module/` 的 5 条 finding。用户确认“修复全部”后，按审计清单一次性处理 Timer 时钟漂移、unscaled 语义失效、重复启动 driver 泄漏、Interval 大步长漂移和架构档案漂移。

## 2. 根因

- Timer driver 使用 Unity `FixedUpdate()` 触发，但 `TimerSettings.FPS` 只影响内部 `_fixedDeltaTime`，导致配置 FPS 和 Unity fixed timestep 不一致时 Timer 时间漂移。
- `DeltaTime` 与 `UnscaledDeltaTime` 都被赋成同一个 `_fixedDeltaTime`，`useUnscaledTime` 只保存标记但没有真实分流。
- `Startup()` 每次都会创建新的 `"Timer"` GameObject 并覆盖 `_timer`，重复启动后旧 driver 无法由 `Shutdown()` 清理。
- `TimerHandle.Advance()` 对 repeat timer 到期后直接清零 elapsed，单帧跨多个 interval 时少触发且丢失余量。
- Timer/Debug redesign 已落地，但 `.codestable/architecture/ARCHITECTURE.md` 未记录 Timer 子系统、Debug Timers tab 和默认启动顺序。

## 3. 修复方案

- Timer driver 改为内部 `MonoBehaviour.Update()` 按 `TimerSettings.FPS` 或默认 50 FPS 累积 Timer 自有 fixed tick，不再依赖 Unity `FixedUpdate()` 频率，也不改写 Unity 全局 `Time.fixedDeltaTime`。
- 每个 Timer tick 根据当前 `Time.timeScale` 生成 scaled delta，同时保留 unscaled delta；TimerHandle 调度按 `UseUnscaledTime` 选择对应 clock。
- `Startup()` 增加 `_timer != null` 幂等 guard；`Shutdown()` 仍清理 driver 和所有 timer。
- Repeat timer 在 elapsed 覆盖多个 interval 时循环补触发，保留剩余 elapsed，并把每次 callback delta 收口为单个 interval。
- 在架构总入口补 Timer 与 Debug 当前事实、硬边界和变更日志。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs`
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.Timer.cs`
- `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/TimerModuleTests.cs`
- `.codestable/architecture/ARCHITECTURE.md`
- `.codestable/audits/2026-06-07-timer-module/`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning，0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning，0 error。
- `python .codestable/tools/validate-yaml.py --dir .codestable/audits/2026-06-07-timer-module`：通过，6 个文件全部有效。

## 6. 遗留事项

- 未运行 Unity Test Runner；本次完成 runtime 与 runtime tests C# 构建验证。
- 顺手发现：`Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs` 所在目录已是 `Runtime/Debug/`，但 namespace 仍是 `GameDeveloperKit.Logger`。本次只按架构现状记录，没有纳入 Timer audit 修复范围做命名迁移。
