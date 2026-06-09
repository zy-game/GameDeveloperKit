---
doc_type: audit-finding
audit: 2026-06-07-timer-module
finding_id: "bug-04"
nature: bug
severity: P2
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 04：Interval 大步长只触发一次并丢弃超出时间，循环调度会漂移

## 速答

重复 timer 到期后直接 `Elapsed = 0f`，没有保留本帧超出的 elapsed，也没有在一次大 delta 中补触发多个 interval；当帧卡顿、timeScale 变化或 FPS 配置与 FixedUpdate 不一致时，循环回调会少触发并持续漂移。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:115` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:122` — `Advance()` 先把本帧 `deltaTime` 全量累加，达到 delay 后只执行一次回调。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:128` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:134` — `Repeat` 到期后把 `Elapsed` 清零、`Remaining` 重置为 `Interval`，丢弃超出 interval 的时间。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerIntervalHandle.cs:16` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerIntervalHandle.cs:19` — interval callback 每次执行只收到当前帧 delta，没有 missed-count 或 catch-up 信息。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:421` 到 `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:423` — 验收要求 Interval 在推进多次后按 interval 重复执行，Cancel 后停止。

## 影响

当前测试在正常 fixed tick 下通过，但如果单帧 delta 大于 interval 的整数倍，例如 interval=0.1 秒而一次推进 0.35 秒，理论上可能需要触发 3 次或至少保留 0.05 秒余量；当前只触发 1 次，并把余量全部清零。对调试采样、轮询、冷却 tick 等低频 interval 来说，这会让触发频率在卡顿后永久偏慢。

## 修复方向

先拍板 interval 语义：如果要 catch-up，就按 `while (Elapsed >= Interval)` 触发并保留余量；如果只允许每帧最多一次，也应保留 `Elapsed -= Interval`，避免长期漂移，并把语义写入测试。

## 建议动作

`cs-issue`，因为需要明确 Interval 的运行时语义并补大 delta 回归用例。
