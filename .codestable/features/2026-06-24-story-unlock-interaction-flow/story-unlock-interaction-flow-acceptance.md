---
doc_type: feature-acceptance
feature: 2026-06-24-story-unlock-interaction-flow
status: passed
accepted_at: 2026-06-24
design: .codestable/features/2026-06-24-story-unlock-interaction-flow/story-unlock-interaction-flow-design.md
roadmap: story-interactive-video
roadmap_item: story-unlock-interaction-flow
tags: [story, playback, unlock, command, interaction]
---

# Story Unlock Interaction Flow 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-24
> 关联方案 doc：`.codestable/features/2026-06-24-story-unlock-interaction-flow/story-unlock-interaction-flow-design.md`

## 1. 接口契约核对

- [x] `NodeKind.Unlock` / `NodeSchemaRegistry`：默认作者节点已暴露 `Unlock`，字段为 `unlockId`、`puzzleType`、`promptTextKey`，端口为 `success/fail`。
- [x] `StoryProgramCompiler`：`ProgramCompiler_WhenUnlockNodeIsValid_BuildsUnlockCommand` 证明 Unlock 编译为普通 `unlock` command，typed arguments 与 declared outcomes 正常落盘。
- [x] 参数校验：`ProgramCompiler_WhenUnlockPuzzleTypeIsInvalid_ReturnsLocatedError`、`ProgramCompiler_WhenUnlockIdMissing_ReturnsLocatedError`、`ProgramCompiler_WhenUnlockPromptTextKeyMissing_ReturnsLocatedError` 证明非法 `puzzleType` 和缺失必填字段都会返回定位错误。
- [x] outcome 校验：`ProgramCompiler_WhenUnlockOutcomeTargetMissing_ReturnsLocatedError` 与 `ProgramCompiler_WhenUnlockHasUnsupportedOutcome_ReturnsLocatedError` 证明 Unlock 只接受 `success/fail` 且两端都必须有目标。

## 2. 行为与决策核对

- [x] `Parallel + Wait -> unlock`：`StoryProgram_WhenParallelWaitUnlockTriggers_KeepsVideoTrackAndCompletesSuccessOutcome` 证明视频轨与 unlock 轨能同帧共存，wait 到点后仍保留视频 command。
- [x] 成功分支：默认 overlay 点击 Unlock 后会写入 `IUnlockStateProvider`，并通过 `success` outcome 推进剧情。
- [x] 失败分支：点击 Fail/Cancel 或写入被拒绝时，不写入 unlocked 状态，直接走 `fail` outcome。
- [x] 已解锁幂等：`StoryPlayerView_WhenDefaultUnlockAlreadyUnlocked_AdvancesSuccessWithoutOverlay` 证明已解锁时不会再显示 overlay。
- [x] 范围决策：实现中没有新增 `TimedChoice`、`EvaluateMediaTime()`、`StoryRunner.Seek()`、`StoryModule.Seek()` 或 `IConditionResolver`。
- [x] 默认播放边界：Unlock 走 `CustomRoot` 默认 overlay，不接入 InputModule / Unity Input System，也不让承载 unlock 的视频获得 transition seek policy。

## 3. 验收场景核对

- [x] N1 Unlock schema：`NodeSchemaRegistry_WhenUnlockNodeQueried_ExposesUnlockSchema` 通过。
- [x] N2 Unlock compile：`ProgramCompiler_WhenUnlockNodeIsValid_BuildsUnlockCommand` 通过。
- [x] N3 参数校验：`ProgramCompiler_WhenUnlockPuzzleTypeIsInvalid_ReturnsLocatedError`、`ProgramCompiler_WhenUnlockIdMissing_ReturnsLocatedError`、`ProgramCompiler_WhenUnlockPromptTextKeyMissing_ReturnsLocatedError` 通过。
- [x] N4 outcome 校验：`ProgramCompiler_WhenUnlockOutcomeTargetMissing_ReturnsLocatedError` / `ProgramCompiler_WhenUnlockHasUnsupportedOutcome_ReturnsLocatedError` 通过。
- [x] N5 并行 Unlock：`StoryProgram_WhenParallelWaitUnlockTriggers_KeepsVideoTrackAndCompletesSuccessOutcome` 通过。
- [x] N6 state 已解锁：`StoryPlayerView_WhenDefaultUnlockAlreadyUnlocked_AdvancesSuccessWithoutOverlay` 通过。
- [x] N7 unlock 成功：`StoryPlayerView_WhenDefaultUnlockButtonClicked_AdvancesSuccessAndWritesState` 通过。
- [x] N8 unlock 失败：`StoryPlayerView_WhenDefaultUnlockFailClicked_AdvancesFailWithoutWritingState`、`StoryPlayerView_WhenDefaultUnlockCancelClicked_AdvancesFail` 与 `StoryPlayerView_WhenDefaultUnlockWriteRejected_AdvancesFailWithoutWritingState` 通过。
- [x] N9 surface request：`StoryPlayerView_WhenUnlockFramePresented_RequestsCustomSurfaceAndKeepsVideoSurface` 通过。
- [x] N10 缺 CustomRoot：`StoryPlayerView_WhenUnlockCustomRootMissing_ThrowsConfigurationError` 通过。
- [x] N11 stop/cancel 清理：`StoryPlayerView_WhenDefaultUnlockStopped_CleansOverlayWithoutCompletingOutcome` 通过。
- [x] N12 Unlock 视频不可 seek：`ProgramCompiler_WhenVideoTargetsUnlock_DoesNotWriteHiddenSeekPolicy` 通过。
- [x] B1-B5 范围守护：runtime/playback/editor grep 未命中 `StoryStepKind.Unlock`、`TimedChoice`、`EvaluateMediaTime`、`StoryRunner.Seek`、`StoryModule.Seek`、`IConditionResolver`、InputModule 或 Runtime/Story 的 UGUI/AVPro/UIWindow/Editor graph 引用。

## 4. 术语一致性

- [x] 代码命名一致：`Unlock`、`IUnlockStateProvider`、`StoryUnlockCommandHandler`、`StoryInteractionCommandNames.Unlock` 已统一出现，未出现第二套 unlock 术语。
- [x] 禁用词守护：runtime / playback / editor 代码路径未命中 `TimedChoice`、`EvaluateMediaTime`、`IConditionResolver`、`StoryRunner.Seek`、`StoryModule.Seek`。
- [x] 作者侧没有暴露 `playbackRole` / `seekable` 字段；seek 仍由 compiler 内部 policy 推导，不进入作者契约。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已写入 unlock command 数据协议、`StoryUnlockCommandHandler`、`IUnlockStateProvider`、`StoryPlayerView` 的 unlock surface/provider 处理，以及 unlock 边界与变更日志。

## 6. requirement 回写

- [x] `.codestable/requirements/story-module.md` 已将本 feature 加入 `implemented_by`，并追加 unlock command / state provider / default overlay 的实现进展与变更日志。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 已把 `story-unlock-interaction-flow` 状态改为 `done`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 已同步子 feature 清单、排期思路与观察项。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 `.codestable/attention.md` 的新环境约束或编译口径。

## 9. 遗留

- 后续仍待推进：`story-editor-interaction-authoring-patterns` 与 `story-interactive-video-sample-acceptance`。
- 已知限制：默认 unlock 只提供最小 overlay，复杂谜题仍交给自定义 interaction channel。
- 现有仓库级验证阻塞：`GameDeveloperKit.Runtime.csproj` 仍受缺失的 network 源文件影响，`GameDeveloperKit.Runtime.Tests.csproj` 仍受 `NetworkModuleTests` 里 `RegisterMessage` 接口变更影响；两者都与本 feature 无关。
