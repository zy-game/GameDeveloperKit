---
doc_type: audit-finding
id: F01
severity: P1
nature: bug
confidence: high
suggested_action: cs-issue
---

# F01 快照恢复无法还原并行运行状态

## 证据

- `StoryRunner.CreateSnapshot()` 只写入 `CurrentSnapshotStepId()`、当前时间、变量、历史和 completed 状态：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:340`
- `StoryRunner.Restore()` 恢复时只 `EnterStep(snapshot.StepId)`，然后清空 `m_CurrentParallelFrame` 并重新 `ResolveFrameUntilStop()`：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:386`
- `CurrentSnapshotStepId()` 对当前帧返回 `m_CurrentFrame.AnchorStep.StepId`；并行帧的 anchor 是 parallel step 本身：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:1190`
- `BuildParallelFrame()` 每次都会从 parallel step 的 branch entry 重新构建全部分支：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:500`
- `StorySnapshot` 类型没有 branch cursor、pending command、pending choice、wait start/progress 等字段：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StorySnapshot.cs:14`

## 影响

在并行播放中保存，恢复后会从 parallel 入口重新构建所有轨道。已经完成的视频/音频/文本分支可能重复播放；等待中的 command、choice、wait 也无法精确恢复。对生产存档来说这是 P1 阻塞。

## 建议

扩展 snapshot 状态模型，至少保存：

- 当前 runner state。
- 当前 frame anchor。
- 并行分支列表：branchId、chapterId、stepId、completed、当前 frame 等待类型。
- 正在等待的 commandId/outcome 状态。
- wait 的 startTime 或 elapsed。

同时增加 parallel 中途保存/恢复的 runtime 测试。

