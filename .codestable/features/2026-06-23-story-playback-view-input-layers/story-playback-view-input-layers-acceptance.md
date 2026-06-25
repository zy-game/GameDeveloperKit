# Story Playback View Input Layers 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-23
> 关联方案 doc：`.codestable/features/2026-06-23-story-playback-view-input-layers/story-playback-view-input-layers-design.md`

## 1. 接口契约核对

对照方案第 2.1 节，接口契约已落地：

- [x] `IInteractionChannel` 包含 `OnAwake`、`OnStoryStarted`、`OnChapterChanged`、`OnFrameChanged`、`GetPlaybackSurfaceView`、`Tick` 和 `OnStoryStopped`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/IInteractionChannel.cs:10`
- [x] `OnAwake` 返回 `UniTask` 并接收 `CancellationToken`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/IInteractionChannel.cs:18`
- [x] `InteractionRequestKind` 覆盖 `Text`、`Continue`、`Choice`、`Video`、`Image`、`Custom`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/InteractionRequest.cs:9`
- [x] `InteractionRequest` 携带 `Kind`、`Frame`、`Track`、`Command`、`Choices`，未写入 `StoryFrame`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/InteractionRequest.cs:22`
- [x] `PlaybackSurfaceView` 携带视频、图片、文本、继续按钮、选项按钮和 custom root。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/PlaybackSurfaceView.cs:12`
- [x] `ModuleInteractionExtensions.SetInteractions/GetInteractions` 是单一注册入口，并位于 StoryPlayback assembly。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/ModuleInteractionExtensions.cs:19`
- [x] `DefaultInteractionChannel` 作为默认 fallback。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/DefaultInteractionChannel.cs:13`

流程图核对：`StoryPlayerView.ExecutePlaybackAsync()` 按 `OnAwake -> PrewarmPlaybackAsync -> OnStoryStarted -> presenter.Start` 执行，随后 `RenderFrame()` 先通知章节，再发 frame 和 surface 请求。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:759`

## 2. 行为与决策核对

需求摘要逐项验证：

- [x] 对外只需要 `App.Story.SetInteractions(channel)`。代码入口为 `ModuleInteractionExtensions`，没有新增 provider/controller/layer 并行接口。
- [x] 交互通道负责章节 UI：`NotifyChapterChanged()` 在 surface 查询前调用 `OnChapterChanged()`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:858`
- [x] surface 查询统一通过 `GetPlaybackSurfaceView(request)`：文本、继续、选项、视频、图片均从该入口获取。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:883`
- [x] `OnStoryStarted` 表示 ready：启动流程会先完成首帧视频预热，失败时不触发 `OnStoryStarted`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:780`
- [x] 默认兼容路径保留：未注册 channel 时 `ResolveInteractionChannel()` 走 `EnsureDefaultInteractionChannel()`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:1186`

明确不做核对：

- [x] 未新增 `IStoryPlaybackSurfaceProvider`、`IStoryChapterInteractionProvider`、旧 `IStoryInteractionLayer` / `IStoryInteractionChannel` / `IStoryInteractionController`。grep 无命中。
- [x] 未新增 `TimedChoice`、`StoryPresentationAnchorPreset`、`normalizedRect` 或 layout slot。grep 无命中。
- [x] 未新增 `StoryRunner.Seek` / `StoryModule.Seek` 或视频 seek UI。grep 无命中。
- [x] `Runtime/Story` 未新增 `RawImage`、`Button`、`TMP_Text`、`UIWindow`、prefab 创建或 `Instantiate`。grep 无命中。
- [x] 未修改 `StoryFrame`、`StoryRunner`、`StoryStepKind`、快照或历史选择模型。

挂载点核对：

- [x] `App.Story.SetInteractions(channel)`：`ModuleInteractionExtensions`。
- [x] `GetPlaybackSurfaceView(request)`：`StoryPlayerView.RenderTextSurface/BindContinueSurface/BindChoiceSurface/UpdateMediaSurfaces`。
- [x] 生命周期通知：`ExecutePlaybackAsync()`、`RenderFrame()`、`StopPlayback()`。
- [x] `DefaultInteractionChannel`：默认兼容路径。

拔除沙盘推演：移除 `ModuleInteractionExtensions` 会让业务无法注册自定义 channel；移除 `GetPlaybackSurfaceView` 会让视频/图片/按钮 surface 无法从章节 UI 获取；移除 `DefaultInteractionChannel` 会破坏默认 `StoryPlayerView.CreateDefault()` 兼容路径，均符合挂载点清单。

## 3. 验收场景核对

- [x] N1 注册交互通道：`StoryPlaybackInteractionChannelTests.StoryPlayerView_WithRegisteredChannel_UsesLifecycleAndInputSurfaces` 覆盖注册后的生命周期与 surface 使用。测试代码：`Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs:69`
- [x] N2 默认兼容路径：`DefaultInteractionChannel` 包装默认 UI surface，`StoryPlayerView.CreateDefaultSurfaceView()` 保留默认字段。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/DefaultInteractionChannel.cs:68`
- [x] N3 预热生命周期：启动流程 `OnAwake -> PrewarmPlaybackAsync -> OnStoryStarted`，预热失败用例确认不触发 `started` 或 surface 查询。测试代码：`Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs:156`
- [x] N4 章节切换通知：测试确认 chapter2 的 `OnChapterChanged` 先于 surface 查询。测试代码：`Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs:113`
- [x] N5 章节 UIWindow / surface：custom channel 返回的 surface 被 Video request 使用。测试代码：`Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs:133`
- [x] N6 文本 + 继续：Text request 写 speaker/body；Continue request 绑定 continue button 并调用 `Continue()`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:934`
- [x] N7 选项 frame：Choice request 要求等量 `ChoiceButtons`，按顺序绑定并调用 `Select(choiceId)`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:1021`
- [x] N8 视频 command：`play_video` 通过 Video request 取得 `RawImage`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:883`
- [x] N9 图片 command：`show_image` 通过 Image request 取得 `RawImage`。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:883`
- [x] N10 缺失 surface：缺少 video surface 会抛配置错误，不复用旧控件。测试代码：`Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs:180`
- [x] N11 StopPlayback：`StopPlayback()` 调用 `OnStoryStopped()` 并清理绑定输入。代码：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:436`
- [x] B1-B4 范围守护：grep 无命中，Runtime/Story 隔离保持。

执行证据：

- `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- 范围守护 grep：旧 interaction 接口、TimedChoice、layout/slot/seek 和 Runtime/Story UI 依赖均无命中。
- Unity Test Runner 本轮未执行：当前项目已有 Unity Editor 实例打开，batchmode PlayMode 测试会被 Unity 多实例锁阻断；需要在 Editor 内手动运行或关闭 Editor 后跑 batchmode。

## 4. 术语一致性

- `IInteractionChannel`：代码和文档一致。
- `InteractionRequest` / `InteractionRequestKind`：代码和文档一致。
- `PlaybackSurfaceView`：代码和文档一致。
- `DefaultInteractionChannel`：代码和文档一致。
- 禁用词 grep：旧 provider/controller/layer 名称、`TimedChoice`、layout slot / anchor / normalized rect 无命中。
- 已有历史类型 `StoryFrame`、`StoryCommand`、`StoryPresenter`、`StoryPlayerView`、`StoryProgram` 未在本 feature 中重命名。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已归并 `IInteractionChannel`、`InteractionRequest`、`PlaybackSurfaceView`、`DefaultInteractionChannel` 和 `OnAwake -> prewarm -> OnStoryStarted` 启动顺序。
- [x] 架构文档继续记录 StoryPlayback 位于独立 assembly，`Runtime/Story` 不引用 UGUI、AVPro、UIWindow 或 prefab。
- [x] 架构变更日志已追加 2026-06-23 记录。

## 6. requirement 回写

- [x] `requirement: story-module` 指向 `.codestable/requirements/story-module.md`。
- [x] requirement 当前已是 `current`，本 feature 不改变能力状态。
- [x] 已追加 `implemented_by: 2026-06-23-story-playback-view-input-layers`。
- [x] 已追加实现进展：StoryPlayback 单一交互通道、章节 UI surface 接管和预热生命周期边界。

## 7. roadmap 回写

- [x] design frontmatter 有 `roadmap: story-interactive-video` 与 `roadmap_item: story-playback-view-input-layers`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 对应 item 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 第 5 节对应条目已同步为 `done`。
- [x] roadmap 变更日志已追加本 feature 验收回写记录。

## 8. attention.md 候选盘点

- [x] 无新增候选。`attention.md` 已记录 Runtime 快速编译命令和 Unity Test Runner 多实例限制，本 feature 没暴露新的通用环境约束。

## 9. 遗留

已知限制：

- Unity Test Runner 未在本轮执行；需要关闭当前 Unity Editor 或在现有 Editor 内运行 `StoryPlaybackInteractionChannelTests` / `StoryVideoPreloadTests` 做执行确认。
- 本 feature 只实现 interaction channel 和首帧视频启动预热，不实现过渡视频 seek、QTE、unlock 或视频中途 `Parallel + Wait` 样例。

后续优化点：

- `story-transition-video-seek-controls`：在该交互通道基础上实现纯过渡视频时间条与 `seek(time)`。
- `story-parallel-wait-interaction-flow`：验证视频播放过程中 `Parallel + Wait + Choice/Command` 到点显示交互。
