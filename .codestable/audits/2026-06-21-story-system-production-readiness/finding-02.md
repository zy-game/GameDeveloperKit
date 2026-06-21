---
doc_type: audit-finding
id: F02
severity: P1
nature: bug
confidence: high
suggested_action: cs-issue
---

# F02 Wait 使用绝对时间导致后续等待可能立即完成

## 证据

- 普通 wait 在 `Evaluate(time)` 中直接把 `m_CurrentTime = time`，然后比较 `m_CurrentTime >= GetWaitSeconds(m_CurrentFrame)`：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:312`
- 并行 wait 也用同样语义比较 `m_CurrentTime >= waitSeconds`：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:783`
- `StoryFrameTrack.CreateWait()` 只保存 `WaitSeconds`，没有 wait enter time 或 elapsed：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryFrame.cs:159`
- 当前测试只覆盖 `module.Evaluate(2d)` 这种一次性完成，不覆盖“全局时间已经大于 waitSeconds 后进入等待”的场景：`Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs:334`

## 影响

如果游戏层用真实全局时间调用 `Evaluate(Time.time)` 或 story elapsed time，剧情播放到 30 秒后进入一个 2 秒 wait 时，`30 >= 2` 会立刻完成。真实播放中多个 wait 会不稳定。

## 建议

明确 API 语义并实现其中一种：

- `Evaluate(deltaTime)`：runner 内部累计当前 wait elapsed。
- `Evaluate(now)`：进入 wait 时记录 `waitStartedAt`，比较 `now - waitStartedAt >= waitSeconds`。

并补充连续 wait、并行 wait、恢复后 wait 的测试。

