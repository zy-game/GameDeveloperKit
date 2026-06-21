---
doc_type: feature-acceptance
feature: 2026-06-21-story-runtime-multitrack-frame
status: passed
accepted_at: 2026-06-21
tags: [story, runtime, multitrack, frame, playback]
---

# Story Runtime Multitrack Frame 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-21
> 关联方案 doc：`.codestable/features/2026-06-21-story-runtime-multitrack-frame/story-runtime-multitrack-frame-design.md`

## 1. 接口契约核对

- [x] `StoryFrame` / `StoryFrameTrack` 已落地：`StoryFrame.Tracks`、`Choices`、`WaitsForChoice`、`WaitsForCommand`、`WaitsForTime`、`IsCompleted` 与方案一致。
- [x] `StoryRunner` 推进 API 返回 `StoryFrame`：`Start`、`Continue`、`Select`、`CompleteCommand`、`Evaluate`、`Restore` 均返回完整 frame。
- [x] `StoryModule.CurrentFrame` 和推进桥接已落地，外部表现层无需读取旧单输出。
- [x] 旧 `StoryOutput` / `StoryOutputKind` / `CurrentOutput` 主路径 grep 无命中。

## 2. 行为与决策核对

- [x] 多轨是表现组合，不是执行并行：runtime 仍用单 `StoryRunner`、单当前章节和 step index。
- [x] Choice gate 优先于 command/time gate：`PlayVideo -> Choice` 中 frame 同时含 command track 和 choices，且 `WaitsForChoice == true`、`WaitsForCommand == false`。
- [x] 阻塞命令仍由 `CompleteCommand(commandId, outcomeId)` 推进，非法 command/outcome 抛带 story/chapter/step 的 `GameException`。
- [x] Wait 仍由 `Evaluate(time)` 推进，非法时机调用会抛定位错误。
- [x] Runtime 不播放媒体、不加载资源、不渲染 UI；AVPro 和 `AssetDatabase` 只存在 Editor playback 层。
- [x] 本 feature 没精简节点库；`Parallel`、`Merge`、复杂条件和辅助节点仍在 schema 中，留给 `story-editor-node-simplification`。

## 3. 验收场景核对

- [x] N1 单文本帧：`StoryModuleTests.StoryProgram_WhenStarted_ContinuingAndCompletingProducesExpectedOutputs` 覆盖 text track。
- [x] N2 单选项帧：同测试覆盖 choices 和 `WaitsForChoice`。
- [x] N3 视频 + 选项：`StoryProgram_WhenVideoIsFollowedByChoice_BuildsSingleChoiceFrame` 覆盖同 frame command + choice。
- [x] N4 图片 + 音频 + 旁白 + 选项：`StoryProgram_WhenImageAudioNarrationAreFollowedByChoice_BuildsSingleChoiceFrame` 覆盖 command、command、text、choices。
- [x] N5 非阻塞命令：N4 中 `show_image` / `play_audio` 无 outcome 且非等待，能与后续 text/choice 同帧。
- [x] N6 阻塞命令：`StoryProgram_WhenCommandOutcomeIsValid_JumpsToOutcomeTarget` 和 command wait 场景覆盖。
- [x] N7 命令 outcome：`StoryProgram_WhenCommandOutcomeIsValid_JumpsToOutcomeTarget` 覆盖。
- [x] N8 选择推进：`StoryProgram_WhenStarted_ContinuingAndCompletingProducesExpectedOutputs` 覆盖 `Select(choiceId)`。
- [x] N9 等待推进：`StoryProgram_WhenWaitCompletes_AdvancesToNextFrame` 覆盖。
- [x] N10 完成帧：completed frame tracks/choices 为空的断言已覆盖。
- [x] B1 空选项：`StoryProgram_WhenAllChoiceConditionsAreFalse_ThrowsLocatedError` 覆盖。
- [x] B2 非当前命令完成：`StoryProgram_WhenCommandOutcomeIsInvalid_ThrowsLocatedError` / mismatched command 测试覆盖。
- [x] B3 快照恢复：completed snapshot restore 已覆盖；多轨 frame restore 依赖 `CurrentSnapshotStepId()` 保存 anchor step，后续可补更窄单测。
- [x] E1 Runtime 隔离 grep：无 `EditorNodeGraph`、`UnityEditor`、`AssetDatabase`、`ObjectField`、`UIElements`、`VideoClip`、`StoryEditorPlaybackWindow`、`AVProVideo`、`RenderHeads`。
- [x] E2 `StoryProgram` 资源仍是 `StoryValue` 基础值，未保存 Unity object。
- [x] E3 本 feature 未移除/隐藏节点库节点。

## 4. 术语一致性

- `StoryFrame`、`Frame track`、`Text track`、`Command track`、`Choice gate`、`Command gate` 均在 runtime / editor playback 中按方案语义使用。
- 防冲突 grep：旧 `StoryOutput` 术语在主路径无命中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 `StoryFrame` / `StoryFrameTrack` 输出边界。
- [x] 架构约束已记录：Story runtime 只输出数据，不引用 Editor graph、UI Toolkit editor 类型、UnityEditor、AssetDatabase、AVProVideo 或具体媒体类型。

## 6. requirement 回写

- [x] `.codestable/requirements/story-module.md` 已记录 `StoryFrame` 多轨帧实现进展。
- [x] `.codestable/requirements/story-editor.md` 已记录播放窗口读取 `CurrentFrame` 和同帧显示媒体命令、文本、选项的进展。
- requirement 仍保持 draft，因为后续 `story-editor-node-simplification` 还未完成，整体剧情编辑体验仍在收敛中。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml` 已把 `story-runtime-multitrack-frame` 改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md` 已同步子 feature 表和排期文字。

## 8. attention.md 候选盘点

- 候选：Unity 生成的 `.csproj` 使用显式 `Compile Include`，新增 `.cs` 文件后，命令行 `dotnet build` 可能在 Unity 重新生成工程前看不到新文件。需要么后续用 Unity 触发工程刷新，要么避免把验证建立在未刷新的 generated csproj 上。
- 候选：并行运行多个 `dotnet build` 目标会竞争 `Temp/obj/GameDeveloperKit.Runtime/GameDeveloperKit.Runtime.dll`，必要时顺序跑 build。

## 9. 遗留

- `StoryRunner` 的 frame builder 仍在 `StoryRunner.cs` 内。设计 2.5 建议拆 `StoryRunner.Frame.cs`，但当前命令行工程文件未自动包含新 `.cs`，直接拆会导致 dotnet build 看不到 partial 文件。建议等 Unity 重新生成 `.csproj` 或后续专门重构时处理。
- `story-editor-node-simplification` 是下一项 roadmap，负责真正移除或隐藏 Parallel/Merge、随机、复杂条件、标记和辅助节点。
