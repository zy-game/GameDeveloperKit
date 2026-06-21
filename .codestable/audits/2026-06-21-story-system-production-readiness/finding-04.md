---
doc_type: audit-finding
id: F04
severity: P1
nature: maintainability
confidence: high
suggested_action: cs-issue
---

# F04 缺少正式 Player 侧播放/命令执行层

## 证据

- `StoryModule` 公开的是 `Continue()`、`Select()`、`CompleteCommand()`、`Evaluate()`，没有 command handler 注册或调度 API：`Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.cs:147`
- `IStoryCommandHandler` 定义了 `Execute()`，但代码库里没有任何 runtime/editor 调用它：`Assets/GameDeveloperKit/Runtime/Story/Runtime/IStoryCommandHandler.cs:6`
- AVPro 真实播放只在 Editor playback window 里实现：`Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryEditorAvProPlayback.cs:4`
- 播放窗口也只是读取 `StoryFrame` 后手动调用 runtime API：`Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryPlaybackSession.cs:85`

## 影响

当前系统能 authoring、编译、预览，并能让外部层消费 `StoryFrame`。但它还不是完整生产播放器：Player 里还需要业务自己实现文本 UI、选项 UI、图片显示、音频播放、AVPro 视频播放、命令完成回调、取消和清理策略。

这不是 runtime 保持纯净的问题，而是缺少一层明确的生产桥接契约。没有这层，项目接入时容易把命令完成、媒体停止、选项取消并行轨道等逻辑散落在多个 UI 脚本里。

## 建议

新增 runtime 或 presentation 层契约，例如：

- `StoryPresenter`：订阅/轮询 `StoryFrame`，渲染 tracks/choices。
- `IStoryCommandExecutor`：按 command name 分发表现命令。
- `IStoryMediaHandle`：支持 complete、cancel、stop、error。
- AVProVideo executor 放在 Player 可用程序集，Editor preview 复用同一抽象。

