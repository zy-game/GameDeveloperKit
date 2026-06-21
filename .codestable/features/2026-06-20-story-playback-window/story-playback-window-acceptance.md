---
doc_type: feature-acceptance
feature: 2026-06-20-story-playback-window
status: accepted
accepted_at: 2026-06-21
design: .codestable/features/2026-06-20-story-playback-window/story-playback-window-design.md
roadmap: story-editor-hardening
roadmap_item: story-playback-window
tags: [story, editor, runtime, playback]
---

# Story Playback Window 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-21
> 关联方案 doc：`.codestable/features/2026-06-20-story-playback-window/story-playback-window-design.md`

## 1. 接口契约核对

- [x] `StoryEditorPlaybackWindow` 是独立 EditorWindow：实现位于 `Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryEditorPlaybackWindow.cs`，入口为 Story Editor 按钮和 `GameDeveloperKit/剧情编辑器/打开播放窗口` 菜单。
- [x] `StoryPlaybackSession` 真实使用 runtime：`Start()` 编译 authoring asset 后创建 `StoryModule`，调用 `Startup()`、`SetFunctionResolver()`、`Start(program, chapterId)`，后续 `Continue()` / `Select()` / `CompleteCommand()` / `Evaluate()` 均委托 runtime module。
- [x] Output renderer 只在 Editor 展示：窗口初版按单输出模型渲染 UI；`2026-06-21-story-runtime-multitrack-frame` 后已迁移为读取 `CurrentFrame`，不写入 runtime。
- [x] 命令展示不替代业务 handler：命令卡只显示 command id/name/arguments/outcome，并提供手动完成按钮。
- [x] 媒体预览只做 Editor 解析：`guid:` / `Assets/` 资源解析使用 `AssetDatabase`，限定在 Editor playback window。

## 2. 行为与决策核对

- [x] 打开窗口会重新编译当前 authoring graph；编译失败时显示中文错误并不启动有效 runtime session。
- [x] 无错误时使用运行时 `StoryModule` 启动，不复制 editor-only runner。
- [x] Text track、choices、command track、wait track、completed 都有对应 UI 和推进动作。
- [x] 关闭窗口调用 `OnDisable()`，释放 `StoryPlaybackSession`。
- [x] 自动快速预览仍保留在 `StoryEditorPlaybackPreview`，完整交互观察改用播放窗口。

范围守护：
- [x] `Assets/GameDeveloperKit/Runtime/Story` grep 未命中 `EditorNodeGraph`、`UnityEditor.Experimental.GraphView`、`UnityEngine.UIElements`、`StoryEditorPlaybackWindow`、`StoryPlaybackSession`、`AssetDatabase`、`ObjectField`。
- [x] 播放窗口没有恢复 `unit`、`payload`、owner action/transition 作为播放概念；这些只在旧迁移代码或旧编辑器测试中存在。
- [x] 未引入 Yarn Spinner / Ink，未把剧情改成脚本语言。

挂载点核对：
- [x] Story Editor 入口：`OpenPlaybackWindow()`。
- [x] 独立窗口：`StoryEditorPlaybackWindow`。
- [x] 会话封装：`StoryPlaybackSession`。
- [x] 输出渲染：`RenderLine` / `RenderChoices` / `RenderCommand` / `RenderWait` / `RenderCompleted`。
- [x] 验收证据：`story-playback-window-manual-record.md` 与自动测试。

## 3. 验收场景核对

- [x] N1 打开入口：`SampleFixture_WhenOpenedInPlaybackWindow_ShowsRuntimeOutputAndControls` 覆盖窗口显示 storyId/chapter/status/output/control。
- [x] N2 编译失败：`PlaybackWindow_WhenCompileFails_ShowsErrorAndDoesNotStartRuntimeSession` 覆盖缺必填字段时显示编译失败且 session 未启动。
- [x] N3 启动 runtime：`SampleFixture_WhenPlayedThroughPlaybackSession_UsesRuntimeModuleAndRecordsHistory` 覆盖 session 使用 `StoryModule` 启动 sample。
- [x] N4 Line 推进：session 和 window advance 测试覆盖 `Continue()`。
- [x] N5 Choice 推进：session 和 window advance 测试覆盖 `Select(choice_enter_alley)`。
- [x] N6 Command 展示：窗口 UI 测试覆盖输出控件；session 测试覆盖 command arguments。
- [x] N7 Command 完成：session 测试覆盖 `CompleteCommand(..., completed)` 与 `CompleteCommand(..., success)`。
- [x] N8 Wait 推进：session 和 window advance 测试覆盖 `Evaluate(2d)`。
- [x] N9 Completed：session 测试覆盖最终 completed frame。
- [x] N10 历史记录：session/window 测试覆盖 history 中选择、命令、等待和 Completed。
- [x] N11 重启章节：窗口 `RestartSession()` 每次先 `ShutdownSession()` 再新建 session；入口和 UI 存在。
- [x] N12 关闭窗口：`PlaybackWindow_WhenDisabled_ShutsDownSession` 覆盖 `OnDisable()` 清空 session。
- [x] E1 runtime 隔离 grep 通过。
- [x] E2 媒体命令只显示资源 key/asset 解析并手动完成，不要求真实播放。
- [x] E3 播放窗口不暴露旧 unit/payload/owner 概念。

人工记录：
- `story-playback-window-manual-record.md` 保留 P1-P10 真实 Unity Editor 点击清单。当前验收以自动测试和构建为准；后续如用户在 Editor 内补点，可把该 manual record 从 `pending-user` 改为 `passed`。

## 4. 术语一致性

- Playback window：代码为 `StoryEditorPlaybackWindow`，文案为“剧情播放窗口”。
- Playback session：代码为 `StoryPlaybackSession`。
- Output renderer：落在播放窗口的各 `Render*` 方法。
- Command presentation / Media preview：只存在 Editor playback window。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 Story Editor 播放窗口边界：Editor-only harness 使用 runtime `StoryModule`，runtime 不反向引用 Editor。
- [x] 架构已包含 `StoryPlaybackSession`、`StoryEditorPlaybackWindow`、`StoryFrame` 和 Story Editor 播放窗口硬边界。

## 6. requirement 回写

- [x] `.codestable/requirements/story-editor.md` 已在实现进展中记录 Story Editor 运行时播放窗口。
- 保持 `draft`：剧情编辑器整体仍有后续多轨帧模型和节点库精简工作。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml` 已将 `story-playback-window` 改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md` 子 feature 清单已同步 `story-playback-window = done`，并把下一步指向 `story-runtime-multitrack-frame`。

## 8. attention.md 候选盘点

- 无新增候选。已有 attention 已记录 Unity Test Runner batchmode 与打开的 Unity Editor 实例冲突。

## 9. 遗留

- 多轨帧模型已由 `2026-06-21-story-runtime-multitrack-frame` 实现：当前 runtime 输出 `StoryFrame`，可以表达“视频 + 音频 + 文字 + 选项”同帧呈现。
- 节点库仍需精简：Parallel/Merge/条件/随机/辅助等高理解成本节点应在多轨帧模型后重新设计或隐藏。
- 真实 Unity Editor 点击记录 P1-P10 仍可由用户后补确认。
