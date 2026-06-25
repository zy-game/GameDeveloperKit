# Story Transition Video Seek Controls 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-24
> 关联方案 doc：`.codestable/features/2026-06-23-story-transition-video-seek-controls/story-transition-video-seek-controls-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `StoryMediaCommandNames.VideoSeekPolicyArgument` / `VideoSeekPolicyTransition`：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryMediaCommandNames.cs:51`、`:56` 已落地，值为 `__videoSeekPolicy` / `transition`。
- [x] `StoryProgramCompiler` hidden policy 写入：`Assets/GameDeveloperKit/Editor/StoryEditor/Compiler/StoryProgramCompiler.cs:653` 调用 `AppendVideoSeekPolicy()`；`:1654` 后的推导 helper 只在 `CanInferTransitionVideo()` 为 true 时写入内部参数。
- [x] `PlaybackSurfaceView.VideoSeek` / `VideoSeekSurface`：`Assets/GameDeveloperKit/Runtime/StoryPlayback/PlaybackSurfaceView.cs:25`、`:75`、`:81` 已落地，构造参数可为 null。
- [x] `StoryAvProVideoPlayback.CanSeek` / `DurationSeconds` / `CurrentTimeSeconds` / `Seek(time)`：`Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryAvProVideoPlayback.cs:113`、`:131`、`:136`、`:227` 已落地。
- [x] Editor preview wrapper 同步控制面：`Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryEditorAvProPlayback.cs:39`、`:51`、`:53`、`:147` 已落地。

**名词层“现状 → 变化”逐项核对**：
- [x] `NodeSchemaRegistry` 不新增 role 字段：grep `playbackRole|seekable` in `Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema` 和 `Assets/GameDeveloperKit/Editor/StoryEditor` 无命中。
- [x] `__videoSeekPolicy` 不进入 command schema argument definitions：测试 `ProgramCompiler_WhenVideoIsLinearTransition_WritesHiddenSeekPolicy` 已断言 schema argument names 不含内部参数。
- [x] `VideoSeekSurface` 是 UI 引用集合，不是新 controller/provider：代码只在 `PlaybackSurfaceView`、`StoryPlayerView` 和测试中使用，没有新增 interaction 接口。

**流程图核对**：
- [x] Compiler 分析 graph → 写入/不写入 hidden policy：`AppendVideoSeekPolicy()`、`CanInferTransitionVideo()`、`IsInsideParallelBranch()`、`IsTransitionVideoTarget()` 均有代码落点。
- [x] StoryPlayback 根据 `playback.CanSeek` 绑定/解绑 seek surface：`StoryPlayerView.UpdateVideoOutput()` 在 `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs:1227` 以 `playback.CanSeek ? m_CurrentVideoSeek : null` 绑定。
- [x] Slider 拖动调用 playback seek：runtime binder 在 `StoryPlayerView.cs:1388` 调用 `m_Playback.Seek(value)`；Editor window 在 `StoryEditorPlaybackWindow.cs:646` 调用 `m_AvProPlayback.Seek(evt.newValue)`。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 作者不填写 `playbackRole`：schema grep 无命中，测试 `NodeSchemaRegistry_WhenMediaNodesQueried_ExposeLoopParameter` 覆盖 `playbackRole` / `seekable` 不存在。
- [x] compiler 只在纯过渡视频写入内部参数：`ProgramCompiler_WhenVideoIsLinearTransition_WritesHiddenSeekPolicy` 通过编译证据；Choice / Parallel / loop 禁用分别由对应测试覆盖。
- [x] runtime 缺少内部参数默认不可 seek：`StoryAvProVideoPlayback.CanSeek` 必须满足 `HasTransitionSeekPolicy(Command)`，缺省 policy 时为 false。
- [x] Editor 播放窗口使用同一内部 policy：`StoryEditorPlaybackWindow.cs:370` 展示 `seek policy`，`:420` 只在 `IsTransitionSeekCommand()` 时渲染 slider。

**明确不做逐项核对**：
- [x] 未新增 `PlayVideo.playbackRole` / `seekable`：目标代码 grep 无命中。
- [x] 未新增剧情级 seek：grep `StoryRunner.Seek|StoryModule.Seek` 无命中。
- [x] 未新增 `TimedChoice` / `EvaluateMediaTime` / `StoryPresentationAnchorPreset`：目标代码 grep 无命中。
- [x] 未新增 `IStoryInteractionLayer` / `IStoryInteractionChannel` / `ITransitionVideoControls`：目标代码 grep 无命中。
- [x] `Runtime/Story` 不引用 UI / AVPro / Editor 类型：grep `UnityEditor|UIElements|RawImage|Slider|AVPro|UIWindow|VideoClip` in `Assets/GameDeveloperKit/Runtime/Story` 无命中。

**关键决策落地**：
- [x] 不暴露 `playbackRole`：用 graph 推导替代作者字段。
- [x] runtime 不重新分析 graph：runtime 只消费 command arguments 中的 `__videoSeekPolicy`。
- [x] 不新增 `ITransitionVideoControls`：seek UI 只通过 `PlaybackSurfaceView.VideoSeek` 可选 surface 暴露。
- [x] 推导保守：`loop`、parallel branch、multiple direct edges、Choice / MiniGame / 非白名单 target 均不会写入 transition policy。

**编排层变化核对**：
- [x] compiler 推导入口位于 command step 构建后、argument definition 导出前，确保 hidden argument 进入 command arguments 但不进入 schema definitions。
- [x] runtime seek gating 落在 `StoryAvProVideoPlayback.CanSeek`，同时检查 policy、loop、control/info、`CanPlay()` 和有效 duration。
- [x] default view 只绑定当前输出的 seekable playback；非 seekable 或 surface 缺失时 `VideoSeekBinder.Unbind()` 隐藏控件。
- [x] Editor preview 只显示 transition slider，disabled 视频测试 `StoryEditorPlaybackWindow_WhenVideoSeekPolicyDisabled_HidesSeekSlider` 覆盖。

**流程级约束核对**：
- [x] seek 不调用 `Evaluate()`、`Continue()`、`Select()` 或 `CompleteCommand()`：runtime/editor slider change handler 只调用 AVPro playback `Seek(time)`。
- [x] 拖到末尾不直接完成 command：`Seek()` 只调用 `m_Player.Control.Seek(...)` 并刷新 UI；command completion 仍由现有 AVPro finished path 承接。
- [x] 缺少 `VideoSeekSurface` 不是配置错误：`PlaybackSurfaceView_WhenVideoSeekSurfaceMissing_KeepsOptionalSurfaceNull` 覆盖可选 surface；必需的 `VideoOutput` 仍由原有配置错误路径处理。

**挂载点反向核对**：
- [x] `compiler-inferred __videoSeekPolicy`：代码落点只在 `StoryMediaCommandNames` 和 `StoryProgramCompiler` 推导/消费测试，符合清单。
- [x] `PlaybackSurfaceView.VideoSeek`：代码落点在 surface、default view、binder 和测试，符合清单。
- [x] `StoryAvProVideoPlayback.Seek(time)`：runtime/editor AVPro wrapper 均有 playback-only seek 入口，符合清单。
- [x] 默认 `StoryPlayerView` seek bar：`EnsureDefaultVideoSeekSurface()` 创建隐藏控件，`VideoSeekBinder` 控制显隐和 listener，符合清单。
- [x] `StoryEditorPlaybackWindow` transition slider：窗口渲染和测试均落在 Editor-only harness，符合清单。
- [x] 拔除沙盘推演：删除 hidden policy 会使 runtime/editor `CanSeek=false`；删除 `VideoSeekSurface` 只影响可见时间条；删除 AVPro `Seek()` 会使 slider 无法改变媒体时间；删除 Editor slider 不影响 runtime 播放。

## 3. 验收场景核对

- [x] N1 schema 不暴露 role：`StoryEditorTests.cs:103` 附近测试断言 `playbackRole` / `seekable` 不存在。
- [x] N2 纯过渡推导：`ProgramCompiler_WhenVideoIsLinearTransition_WritesHiddenSeekPolicy`。
- [x] N3 Choice 禁用：`ProgramCompiler_WhenVideoTargetsChoice_DoesNotWriteHiddenSeekPolicy`。
- [x] N4 Parallel 禁用：`ProgramCompiler_WhenVideoIsInsideParallel_DoesNotWriteHiddenSeekPolicy`。
- [x] N5 旧图兼容：schema 不新增作者字段；旧图只在符合推导规则时获得内部 policy。
- [x] N6 transition 可 seek：runtime `CanSeek` / `Seek(time)` 和 default binder 已编译通过；真实 AVPro seek 需 Unity/AVPro 播放环境手动验证。
- [x] N7 loop 不可 seek：compiler 测试 `ProgramCompiler_WhenVideoLoops_DoesNotWriteHiddenSeekPolicy`，runtime `CanSeek` 同时检查 `loop=false`。
- [x] N8 自定义 channel 无 seek surface：`PlaybackSurfaceView_WhenVideoSeekSurfaceMissing_KeepsOptionalSurfaceNull` 覆盖可选 null。
- [x] N9 解绑清理：`StoryPlayerView` 在 stop/dispose/no output/非 seekable 路径调用 `VideoSeekBinder.Unbind()`；编译验证通过。
- [x] N10 Editor 预览：`StoryEditorPlaybackWindow_WhenTransitionVideoCurrent_ShowsSeekSlider` 覆盖 transition slider 出现。
- [x] N11 Editor disabled：`StoryEditorPlaybackWindow_WhenVideoSeekPolicyDisabled_HidesSeekSlider` 覆盖 disabled 不显示 slider。
- [x] B1-B5 范围守护：本次 grep 均无命中。

**验证命令**：
- [x] `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`
- [x] `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore`
- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`
- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`
- [x] `python .codestable/tools/validate-yaml.py` 校验 checklist / roadmap items。

**未运行项**：
- Unity Test Runner 未运行：命令行未找到 `Unity` / `Unity.exe` 可执行入口。当前证据为 dotnet 编译、测试代码覆盖和 grep 范围守卫。

## 4. 术语一致性

- `compiler-inferred seek policy`：代码命名落为 `AppendVideoSeekPolicy` / `CanInferTransitionVideo`，含义一致。
- `transition video`：代码只使用 `VideoSeekPolicyTransition` 常量表达内部 policy，不暴露作者字段。
- `branch interaction video`：无单独类型；通过禁用条件表达，符合 design。
- `__videoSeekPolicy`：只作为内部 command argument 常量出现。
- `VideoSeekSurface`：仅作为 `PlaybackSurfaceView` 可选 UI surface 出现。
- `StoryAvProVideoPlayback.Seek(time)`：runtime wrapper 方法名一致。
- 禁用词 grep：`playbackRole` / `seekable` / `TimedChoice` / `EvaluateMediaTime` / `ITransitionVideoControls` / `IStoryInteractionLayer` / `IStoryInteractionChannel` 在目标代码无命中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已更新 Story Editor / StoryPlayback 类型索引：记录 compiler hidden policy、`StoryMediaCommandNames` 内部参数、`VideoSeekSurface`、runtime/editor AVPro playback seek 和默认 `StoryPlayerView` 时间条。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已更新运行时播放窗口契约：Editor window 复用内部 policy 显示 transition slider，disabled 视频不显示。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已新增 StoryPlayback 过渡视频 seek 边界：seek 只改变 AVPro 当前媒体时间，不新增剧情级 seek，不改变 frame、choice、outcome 或 chapter。
- [x] `.codestable/architecture/ARCHITECTURE.md` 变更日志已追加 2026-06-24 记录。

## 6. requirement 回写

- [x] `requirement: story-module` 指向 current req。
- [x] `.codestable/requirements/story-module.md` 已追加 `implemented_by: 2026-06-23-story-transition-video-seek-controls`。
- [x] `.codestable/requirements/story-module.md` 已追加实现进展：compiler-inferred transition video、可选 `VideoSeekSurface`、playback-only `Seek(time)`、分支互动视频不可 seek 和 Story 核心无剧情级 seek。
- [x] `last_reviewed` 已更新为 2026-06-24，变更日志已追加。

## 7. roadmap 回写

- [x] design frontmatter 包含 `roadmap: story-interactive-video` 与 `roadmap_item: story-transition-video-seek-controls`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 对应条目已从 `in-progress` 改为 `done`，feature 仍指向 `2026-06-23-story-transition-video-seek-controls`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 第 5 节对应条目状态已同步为 `done`。
- [x] roadmap 主文档和 items 的 `last_reviewed` 已更新为 2026-06-24，roadmap 变更日志已追加。

## 8. attention.md 候选盘点

- [x] 候选：命令行未找到 Unity 可执行入口，Unity Test Runner 无法由当前 shell 直接运行。该信息和现有 attention 中“已有 Unity Editor 实例会阻塞 batchmode”不同，但是否属于所有 feature 都会踩的环境问题，需要用户决定是否用 `cs-note` 追加。

## 9. 遗留

- 后续优化点：`StoryProgramCompiler.cs` 和 `StoryPlayerView.cs` 仍偏胖；design 2.5 已建议后续单独走 `cs-refactor` 拆 graph 分析 helper、默认 UI 构造、输入绑定、媒体 surface 更新。
- 已知限制：未跑 Unity Test Runner；真实 AVPro 播放和拖动体验需要在 Unity Editor / Player 环境手动验证。
- 方案外观察：本 feature 不支持分支互动视频 seek，不支持剧情级随机访问，不支持播放/暂停、倍速、章节选择或存档回放。
