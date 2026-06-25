---
doc_type: roadmap
slug: story-interactive-video
status: active
created: 2026-06-23
last_reviewed: 2026-06-24
tags: [story, playback, interactive-video, qte, ui]
related_requirements: [story-module, story-editor, ui-module, input-module]
related_architecture: [ARCHITECTURE]
---

# Story Interactive Video

## 1. 背景

当前 StoryPlayback 已经承接项目默认的 StoryPlayerView 实现，视频固定使用 AVProVideo，播放 UI 也能在运行时自动创建到 `UILayer.StoryPlayback`。但默认 `StoryPlayerView` 仍把对白、旁白、选项和继续按钮固定在底部 `DialoguePanel` 中，这只能覆盖最基础的视觉小说式流程。

实际剧情表现会更复杂：对白可能在人物附近，旁白可能在画面中央或顶部，选项可能围绕视频热点出现，输入控件可能是局部面板；视频播放过程中也可能在某个剧情时间点出现选项、QTE 或解锁 UI。当前 Story runtime 已经有 `Parallel`、`Wait`、`Choice` 和 command outcome，足够表达“播放视频的同时，等待 35 秒后出现选项”这种编排：一条轨道播放视频，另一条轨道 `Wait(35) -> Choice`。另外，一些视频节点只是纯过渡视频，需要像播放器一样显示时间条并支持 `seek(time)`；另一些视频节点属于分支选项视频，用来承载对话、选项或互动分支背景，这类视频不进入 seek 范围，避免把 Story runtime 变成可任意回放的时间轴内核。因此本 roadmap 不再新增 `TimedChoice` 或 presentation layout hint，而是把重点放在 StoryPlayback 的交互通道：交互通道在切换 frame 时决定提供哪些 `RawImage`、按钮、文本、自定义 UI surface 和过渡视频控制条，并通过现有 StoryPresenter API 推动剧情。

本 roadmap 是 `story-playback-architecture` 之后的后续扩展线，只规划交互视频和动态呈现层。它不回退已经收口的边界：视频仍由 StoryPlayback 内的 AVPro 执行，Startup/Loading/Procedure/章节预加载仍不进入 StoryPlayback。

## 2. 范围与明确不做

### 本 roadmap 覆盖

- 改造默认 `StoryPlayerView`，先拆出剧情 view/input interaction channel，让运行时在切换 frame 时通过 `GetPlaybackSurfaceView(request)` 获取视频 `RawImage`、图片输出、选项按钮、继续按钮和自定义输入 UI。
- 建立 `App.Story.SetInteractions(channel)` 单一交互通道入口：播放某章节时由 interaction channel 自己创建或切换该章节专用 UIWindow / prefab，并只通过 StoryPresenter/StoryModule 的公开 API 推动剧情。
- 支持纯过渡视频的运行时时间条和 `seek(time)`：作者不填写 `playbackRole` 或 `seekable` 字段，只有被 compiler 根据图结构推导为 `transition` 的视频节点开放；分支选项视频、背景循环视频、带选项/QTE/解锁的视频节点不开放，拖动只改变 AVPro 当前媒体时间，不改变 StoryRunner 的分支状态。
- 支持视频过程中的互动编排：作者使用现有 `Parallel + Wait + Choice/Command` 表达“视频播放 N 秒后出现交互”，StoryPlayback 只负责按当前 frame 呈现和收集输入。
- 支持视频播放中的 QTE：QTE 出现时不暂停音视频，通过 success / fail outcome 推进剧情。
- 支持解锁类互动的运行时协议，例如连线解锁、节点解锁、热点解锁；具体输入玩法由 StoryPlayback/UI 承接，Story 核心只接收 outcome。
- 在 Runtime/Playback 协议稳定后，补 Story Editor 的节点模板、图上校验和样例，让作者能用 `Parallel + Wait` 模式配置视频过程中的选择、QTE 和解锁 outcome。
- 提供样例和测试，覆盖非全屏视频 surface、视频过程中等待后出现选项、视频中 QTE、解锁 outcome。

### 明确不做

- 不做完整 UI 设计器、动画时间线编辑器或可视化布局编辑器；首版也不引入 `StoryPresentationAnchorPreset`、normalized rect 或 layout hint 协议。
- 不新增 `TimedChoice` 节点、`EvaluateMediaTime()` runtime API 或专门的 media-time interaction 数据模型；视频过程中出现选项优先由现有 `Parallel + Wait + Choice` 表达。
- 不新增 `StoryRunner.Seek()`、`StoryModule.Seek()` 或全剧情随机访问能力；runtime seek 只面向纯过渡视频的 AVPro 媒体时间。
- 不支持分支选项视频、背景循环视频、带选项/QTE/解锁、分支互动或其它 blocking command 的视频 seek；这些视频仍按剧情状态机正向推进。
- 不把 UGUI、AVProVideo、SoundModule、InputModule、UIWindow 或 Editor graph 类型写入 `Runtime/Story` 核心。
- 不把视频后端抽象成可替换播放器；项目默认视频后端仍是 AVProVideo。
- 不恢复旧 `StoryPresentation` / `StoryPresentation.AVPro` 包，也不把互动逻辑放回 Startup、LoadingWindow、Procedure 或资源预加载。
- 不实现平台级输入映射系统；QTE 只定义输入请求和结果协议，具体按键、手柄、触摸映射可由后续业务或 InputModule feature 承接。
- 不做本地化、存档、章节选择、资源发布或视频加载来源变更。
- 不一次性把所有历史 QTE/Hotspot/InputWait 旧模型复活；只有编译器、runtime、playback 和 tests 都闭合的节点才进入默认作者主路径。

## 3. 模块拆分（概设）

```
Story Interactive Video
├── Runtime/Story：保持 StoryFrame / Track / Choice / Wait / Command 数据协议，不引用具体播放/UI
├── Runtime/StoryPlayback：interaction channel、InteractionRequest、PlaybackSurfaceView、媒体 command 适配和默认互动控件
├── Runtime/UI：只提供 StoryPlayback 层级根，不解释剧情互动
├── Story Editor：节点 schema、字段编辑、编译和校验
└── Scripts/StoryTest：交互视频样例、测试 Procedure 和验收入口
```

### 3.1 Runtime/Story · 互动数据协议

- **职责**：继续用 `StoryProgram` / `StoryFrame` / `StoryFrameTrack` / `StoryChoice` 描述剧情推进、等待、选择、命令和 outcome。视频中出现选项由 `Parallel + Wait + Choice` 表达，QTE/解锁由 command outcome 表达；核心不描述 UI 位置和具体控件。
- **承载的子 feature**：`story-video-qte-command`、`story-unlock-interaction-flow`
- **触碰的现有代码 / 模块**：`Runtime/Story/Program`、`Runtime/Story/Runtime`、`StoryFrame`、`StoryRunner`、validation；不得引入播放/UI 引用。

### 3.2 Runtime/StoryPlayback · 交互会话与媒体适配

- **职责**：消费 Story frame，并通过 `App.Story.SetInteractions(channel)` 注册的单一 interaction channel 承接章节 UI。interaction channel 在章节切换时创建或切换对应 UIWindow / prefab / 场景 UI；StoryPlayback 在播放视频、显示图片、渲染文本、显示继续或选项时构造 `InteractionRequest`，调用 `GetPlaybackSurfaceView(request)` 获取当前章节的 `RawImage`、文本和按钮 surface。media adapter 只把 `play_video` / `show_image` / `play_audio` command 落到这些 surface 或服务上，不再代表一个固定全屏播放层。
- **承载的子 feature**：`story-playback-view-input-layers`、`story-transition-video-seek-controls`、`story-parallel-wait-interaction-flow`、`story-video-qte-command`、`story-unlock-interaction-flow`
- **触碰的现有代码 / 模块**：`StoryPlayerView`、`StoryPresenter`、`StoryAvProVideoCommandPlayer`、`StoryAvProVideoPlayback`、`StorySoundCommandPlayer`。

### 3.3 Runtime/UI · 层级承载

- **职责**：继续只提供 `UILayer.StoryPlayback` 作为默认运行时播放 UI 父节点；实际视频是否全屏、图片放在哪、按钮怎么布局由注册到 Story 的 interaction channel 控制。
- **承载的子 feature**：`story-playback-view-input-layers`
- **触碰的现有代码 / 模块**：`UIModule.GetLayerRoot(UILayer.StoryPlayback)`；不解释 slot、不渲染剧情、不管理 Story 状态。

### 3.4 Story Editor · 作者入口

- **职责**：在 playback 协议稳定后提供作者模板和校验：`Parallel + Wait + Choice` 表达视频中途选项，`Wait + QTE/Unlock command` 表达视频中途互动结果；不新增 TimedChoice 节点或 layout 字段。
- **承载的子 feature**：`story-editor-interaction-authoring-patterns`
- **触碰的现有代码 / 模块**：`NodeSchemaRegistry`、`StoryEditorPortPolicy`、`StoryProgramCompiler`、graph diagnostics、sample fixture。

### 3.5 Scripts/StoryTest · 交互样例

- **职责**：提供可运行样例，验证非全屏视频 surface、并行等待后出现选项、视频中 QTE 和解锁结果能从 `FrameworkStartup -> StoryTestProcedure -> StoryPlayback` 跑通。
- **承载的子 feature**：`story-interactive-video-sample-acceptance`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Scripts/StoryTest` 和 runtime/editor tests。

## 4. 模块间接口契约 / 共享协议（架构层详设）

这一节是后续 feature-design 的硬约束输入。实现时若发现字段命名或推进 API 不合适，应先回到本 roadmap update，而不是在单条 feature 中绕开。

命名约定：现有运行时代码已经位于 `GameDeveloperKit.Story` 命名空间，后续新增公开契约不再默认重复 `Story` 前缀。`StoryModule` 保持模块入口命名；新增交互相关类型使用 `IInteractionChannel`、`InteractionRequest`、`PlaybackSurfaceView`、`DefaultInteractionChannel` 这类短名。已有 `StoryFrame`、`StoryCommand`、`StoryProgram` 等历史类型不在本 roadmap 子 feature 中顺手重命名，若要清理应另起 refactor。

### 4.1 Story 图编排复用契约

**方向**：Story Editor -> compiler -> Runtime/Story -> StoryPlayback

**形式**：现有节点组合规则，不新增 runtime step kind

**契约**：

视频过程中出现交互优先使用现有图编排表达：

```text
Parallel
├── branch_video: PlayVideo(waitForCompletion: true) -> Merge
└── branch_interaction: Wait(35) -> Choice item... -> selected target
```

QTE / 解锁也使用同一模式：

```text
Parallel
├── branch_video: PlayVideo(waitForCompletion: true) -> Merge
└── branch_interaction: Wait(12) -> Qte/Unlock command(success/fail outcome)
```

**约束**：

- 不新增 `TimedChoice` 节点、`StoryInteractionKind.TimedChoice`、`EvaluateMediaTime()` 或 media-time 专用 runtime API。
- `Wait` 的时间由 StoryPlayback session 推进；默认使用 unscaled delta time，interaction channel 可以在媒体暂停或输入阻塞时停止推进 `Evaluate(deltaTime)`。
- 如果后续确实需要“严格绑定 AVPro 当前媒体时间”而不是 session time，应先回本 roadmap update，不能在 feature 内偷偷恢复 `EvaluateMediaTime()`。
- `Choice` 仍是现有普通选择协议，交互通道显示按钮后调用 `Select(choiceId)` 推进。
- QTE / 解锁首版编译为 command track，通过 declared outcome 和 `CompleteCommand(commandId, outcomeId)` 推进，不在 Runtime/Story 中建立另一套 interaction state。

### 4.2 Story 交互通道注册与生命周期协议

**方向**：业务 / StoryPlayback -> `App.Story` -> StoryPlayback interaction channel

**形式**：单一 interaction channel 注册入口；调用侧只理解一个入口

**契约**：

```csharp
public static class ModuleInteractionExtensions
{
    public static void SetInteractions(
        this StoryModule module,
        IInteractionChannel channel);

    public static IInteractionChannel GetInteractions(this StoryModule module);
}

public interface IInteractionChannel : IDisposable
{
    UniTask OnAwake(InteractionContext context, CancellationToken cancellationToken);
    void OnStoryStarted(InteractionContext context);
    void OnChapterChanged(ChapterInteractionContext context);
    void OnFrameChanged(StoryFrame frame);
    PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request);
    void Tick(float deltaTime);
    void OnStoryStopped();
}
```

**约束**：

- 对外调用形态必须是 `App.Story.SetInteractions(channel)`，不再拆出 surface provider、chapter provider、旧 layer 或 controller。
- `SetInteractions` / `GetInteractions` 可以在 StoryPlayback assembly / 包侧作为 extension / registry 实现，避免 `Runtime/Story` 直接引用 UGUI、AVPro 或 UIWindow。
- `OnAwake()` 在剧情预热前调用并返回 `UniTask`，交互通道可用它打开 loading、过渡 UI 或预创建窗口根。
- `OnStoryStarted()` 只在剧情和媒体预热完成后调用，语义是 story ready；不要把它当 loading 出现时机。
- `OnChapterChanged()` 必须发生在当前章节第一次 `GetPlaybackSurfaceView()` 之前，避免拿到空 UI 或上一章 UI。
- interaction channel 可以创建 / 关闭 UIWindow、复用场景 prefab 或返回默认 `StoryPlayerView` surface；Story 核心不关心窗口类型。
- 交互通道不能直接读取 editor graph 边，也不能自行跳 target；所有推进都走 `Continue()`、`Select()`、`CompleteCommand()`、`Evaluate()`。

### 4.3 InteractionRequest 与 PlaybackSurfaceView 协议

**方向**：StoryFrame -> StoryPlayback request -> interaction channel -> media / input adapter

**形式**：一个 request + 一个 surface view 返回值

**契约**：

```csharp
public enum InteractionRequestKind
{
    Text = 0,
    Continue = 1,
    Choice = 2,
    Video = 3,
    Image = 4,
    Custom = 5
}

public readonly struct InteractionRequest
{
    public InteractionRequestKind Kind { get; }
    public StoryFrame Frame { get; }
    public StoryFrameTrack Track { get; }
    public StoryCommand Command { get; }
    public IReadOnlyList<StoryChoice> Choices { get; }
}

public sealed class PlaybackSurfaceView
{
    public RawImage VideoOutput { get; }
    public RawImage ImageOutput { get; }
    public TMP_Text SpeakerText { get; }
    public TMP_Text BodyText { get; }
    public Button ContinueButton { get; }
    public IReadOnlyList<Button> ChoiceButtons { get; }
    public RectTransform CustomRoot { get; }
}
```

投影规则：

- `frame.Choices.Count > 0` -> `Choice` request，交互通道返回与 `request.Choices` 等量的 `ChoiceButtons`；播放层按顺序绑定按钮并调用 `Select(choiceId)`，不实例化或销毁按钮 prefab。
- `frame.WaitsForChoice == false && frame.WaitsForCommand == false && frame.WaitsForTime == false && frame.IsCompleted == false` -> `Continue` request，交互通道返回 continue button，播放层绑定 `Continue()`。
- `StoryFrameTrackKind.Text` -> `Text` request，交互通道返回 speaker/body 文本控件。
- `StoryFrameTrackKind.Command` 且 command name 为 `play_video` -> `Video` request，交互通道返回视频 `RawImage`。
- `StoryFrameTrackKind.Command` 且 command name 为 `show_image` -> `Image` request，交互通道返回图片 `RawImage`。
- QTE / unlock / seek 等后续互动使用 `Custom` request 或后续新增 request kind；不新增另一套 channel/controller。

**约束**：

- `StoryFrame` 继续只暴露 `Tracks`、`Choices` 和 gate flags；不新增 `Interactions` 集合。
- 媒体 command adapter 不自行创建全局全屏 `RawImage`；它必须通过 interaction channel 的 surface 返回值获取输出目标。
- 自定义 channel 对必需 surface 返回 null、缺字段或 `ChoiceButtons.Count != request.Choices.Count` 时是配置错误，不得静默复用上一章节 UI。
- `StoryPlayerView.CreateDefault()` 是默认 interaction channel 的 fallback UI，不是固定播放层。
- `StoryPlayerView.Update()` 可以继续负责视频纹理刷新和 wait 推进，但不依据 AVPro 当前时间触发 runtime 分支。

### 4.4 纯过渡视频 seek 与时间条协议

**方向**：StoryFrame media command -> StoryPlayback media adapter -> AVProVideo

**形式**：compiler hidden metadata + seekable AVPro playback handle + optional `VideoSeekSurface`

**契约**：

```csharp
public static class StoryMediaCommandNames
{
    public const string VideoSeekPolicyArgument = "__videoSeekPolicy";
    public const string VideoSeekPolicyTransition = "transition";
}

public sealed class VideoSeekSurface
{
    public RectTransform Root { get; }
    public Slider Slider { get; }
    public TMP_Text TimeText { get; }
}

public sealed class PlaybackSurfaceView
{
    public VideoSeekSurface VideoSeek { get; }
}

public sealed class StoryAvProVideoPlayback
{
    StoryCommand Command { get; }
    bool CanSeek { get; }
    double DurationSeconds { get; }
    double CurrentTimeSeconds { get; }
    bool IsPlaying { get; }

    void Seek(double timeSeconds);
}
```

`PlayVideo` 节点 schema 不增加 `playbackRole` / `seekable`。compiler 只在能证明当前 `play_video` 是纯过渡视频时，向编译产物追加内部保留参数：

```yaml
name: play_video
arguments:
  source: streaming_assets | persistent_data_path | network_stream
  clip: string
  loop: boolean
  __videoSeekPolicy: transition
```

该字段不进入节点 Inspector、command schema argument definitions 或作者可编辑字段；runtime 缺少该字段时默认不可 seek。

**可 seek 判定**：

- command arguments 包含 `__videoSeekPolicy == transition`。
- 当前 `play_video` 是 compiler 能证明的纯过渡视频节点：它本身不承担分支选项、QTE、解锁或其它 blocking interaction。
- 当前播放不是 loop，AVPro duration/current time 可用，且 duration > 0。
- 当前帧只能把 seek 用作媒体控制；不得用 seek 改变 `StoryRunner.CurrentTime`、choice 历史、command outcome 或章节位置。

**约束**：

- 不新增 `StoryRunner.Seek()` / `StoryModule.Seek()`；seek 只作用于 `StoryPlayback` 当前 AVPro 播放句柄。
- 分支互动视频不显示 seek bar；即使 UI 提供了 `VideoSeekSurface`，playback gating 也必须保持不可 seek。
- 拖动 seek bar 时调用 `StoryAvProVideoPlayback.Seek(timeSeconds)`；拖到末尾不直接改 runner，仍以 AVPro 完播事件或明确的 command completion 触发 `CompleteCommand()`。
- seek bar 的 UI surface 由 interaction channel 通过 `GetPlaybackSurfaceView(Video)` 返回的同一个 `PlaybackSurfaceView.VideoSeek` 提供；没有提供时仍可正常播放视频，只是不显示时间条。
- `Runtime/Story` 不保存 slider、button、时间文本或 AVPro 句柄。
- 如果未来需要“带互动视频也能 seek”，必须先回本 roadmap update，定义 choice/outcome 历史重放和 command 副作用策略，不能在本 feature 中顺手扩大范围。

### 4.5 QTE 命令互动协议

**方向**：StoryFrame command track -> StoryPlayback interaction channel -> Runtime/Story command outcome

**形式**：command argument + declared outcome

**契约**：

```csharp
public static class StoryInteractionCommandNames
{
    public const string Qte = "qte";
    public const string SuccessOutcome = "success";
    public const string FailOutcome = "fail";
    public const string InputActionIdArgument = "inputActionId";
    public const string DurationSecondsArgument = "durationSeconds";
    public const string RequiredCountArgument = "requiredCount";
    public const string PromptTextKeyArgument = "promptTextKey";
}
```

`qte` command arguments：

```yaml
name: qte
arguments:
  inputActionId: string
  durationSeconds: number
  requiredCount: number
  promptTextKey: string
outcomes: [success, fail]
```

**约束**：

- 首版 QTE 只保证 `success` / `fail`；`timeout` / `canceled` 仍是后续 roadmap 词汇，不在默认 schema 和默认播放层里启用。
- QTE 输入完成由 StoryPlayback interaction channel 或业务注入的输入适配层判断，Story 核心只接收 `CompleteCommand(commandId, outcomeId)`。
- QTE 激活期间是否暂停视频和音频由 interaction channel 决定；默认不暂停。
- 作者需要在视频中第 N 秒出现 QTE 时，使用 `Parallel + Wait(N) -> qte command`，不新增 QTE trigger time 字段。

### 4.6 解锁类命令和条件协议

**方向**：StoryFrame command track -> StoryPlayback / 业务状态 -> Runtime/Story command outcome

**形式**：command argument + resolver 接口 + declared outcome

**契约**：

```csharp
public interface IUnlockStateProvider
{
    bool TryGetUnlockState(string unlockId, out bool unlocked);
    bool TrySetUnlockState(string unlockId, bool unlocked, out string errorMessage);
}
```

`unlock` command arguments：

```yaml
name: unlock
arguments:
  unlockId: string
  puzzleType: line_connect | node_unlock | custom
  promptTextKey: string
outcomes: [success, fail]
```

**约束**：

- 条件判断继续复用现有 `StoryExpression` / `IStoryFunctionResolver`，不新增第二套 condition resolver；解锁状态不直接读取 DataModule，业务可把 DataModule 包装成 `IUnlockStateProvider`。
- `unlock` command 的 UI 玩法由 StoryPlayback 默认 interaction channel 或业务自定义 interaction channel 承接；Story 核心只关心 command outcome 和可选的 unlock state。
- 连接解锁、节点解锁、热点解锁都先落为 `unlock` / custom command，不急着在 Runtime/Story 中建立多套小游戏模型。
- 作者需要在视频中第 N 秒出现解锁 UI 时，使用 `Parallel + Wait(N) -> unlock command`。

### 4.7 StoryPlayerView 默认 interaction channel 契约

**方向**：StoryPlayback 默认 view 内部

**形式**：Unity component API

**契约**：

```csharp
public sealed class StoryPlayerView : MonoBehaviour
{
    public StoryPresenter Presenter { get; }
    public StoryFrame CurrentFrame { get; }
    public void SetInteractionChannel(IInteractionChannel channel);
    public PlaybackSurfaceView CreateDefaultSurfaceView();
}

public sealed class DefaultInteractionChannel : IInteractionChannel
{
    public PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request);
}
```

**约束**：

- `StoryPlayerView.CreateDefault()` 可以继续创建一个默认全屏根，但这只是缺省 interaction channel；业务可以通过 `App.Story.SetInteractions(customChannel)` 接管章节 UI。
- 自定义 interaction channel 可以按章节返回不同 surface，例如局部视频 `RawImage`、等量选项按钮列表、角色旁对白文本或小游戏面板。
- `StoryPlayerView` 不保存 `StoryPresentationAnchorPreset`、normalized rect 或 slot profile；布局由 prefab / interaction channel 自己控制。
- 纯过渡视频 seek 控件通过 `PlaybackSurfaceView.VideoSeek` 这个可选 surface 提供；没有提供时仍可正常播放视频，只是不显示时间条。
- `UIModule` 只提供默认父层级，不接管 RawImage、button、TMP 文本、QTE 或视频纹理。

### 4.8 Editor authoring 模板与校验契约

**方向**：Story Editor -> compiler -> StoryProgram

**形式**：现有节点模板 + validation

**契约**：

默认作者模式：

```yaml
VideoChoiceTemplate:
  graph: Parallel -> [PlayVideo] + [Wait(seconds) -> Choice]

TransitionVideo:
  graph: PlayVideo -> Dialogue/Jump/End/普通后续流程
  compiler: infer __videoSeekPolicy=transition
  seek controls: allowed by compiled internal policy

BranchInteractionVideo:
  graph: PlayVideo -> Choice/QTE/Unlock/Parallel wait interaction
  compiler: no __videoSeekPolicy
  seek controls: disabled by default

QteCommand:
  inputActionId: string required
  durationSeconds: number required
  requiredCount: number optional
  success -> target required
  fail -> target required

Unlock:
  unlockId: string required
  puzzleType: option required
  success -> target required
  fail -> target required
```

**约束**：

- Editor 不新增 `TimedChoice` 节点，不给 Dialogue / Choice / QTE / Unlock 增加 slot 或 anchor 字段。
- 如果提供“一键创建视频中途选项”模板，模板必须生成现有 `Parallel + Wait + Choice` 节点组合，而不是生成新 runtime step。
- Editor 不在 `PlayVideo` 上暴露 `playbackRole` / `seekable` 字段；过渡视频与分支互动视频由模板结构和图结构表达。
- 编译器必须保守推导 `__videoSeekPolicy=transition`：连接到 Choice/QTE/Unlock 模板、处于 Parallel 等待互动结构、或其它无法证明纯过渡的 `PlayVideo` 都不得写入内部 policy。
- 编译器必须校验 Wait seconds 非负、QTE/Unlock outcome 端口目标合法、command 参数完整。
- 旧 `Choice` 语义保持：普通对白/旁白 completed 连接多个 `Choice` 仍合成当前 `StoryFrame.Choices`；视频中途选项仍是普通 choice，只是被 `Wait` 节点编排到对应时间出现。

### 4.9 StoryPlayback 交互通道 UI 生命周期

**方向**：`StoryFrame` -> StoryPlayback -> `IInteractionChannel`

**形式**：单一交互通道对象 + story/chapter/frame 生命周期通知

**生命周期**：

| 触发时机 | 调用 | 要求 |
|---|---|---|
| 播放 session 建立后、预热前 | `OnAwake(context, cancellationToken)` | 交互通道打开 loading、过渡 UI 或预创建窗口根，StoryPlayback 等待该 `UniTask` 完成后再进入预热 |
| 剧情和媒体预热完成 | `OnStoryStarted(context)` | 交互通道拿到 `StoryPresenter` / story id，关闭 loading 并进入 story ready 状态 |
| 章节变化 | `OnChapterChanged(context)` | 先关闭或清理旧章节 UI，再创建/切换新章节 UIWindow |
| Frame enter | `OnFrameChanged(frame)` | 交互通道可以刷新当前章节 UI 状态 |
| 需要 surface | `GetPlaybackSurfaceView(request)` | 返回当前章节的 `RawImage`、文本、继续按钮、等量选项按钮或 custom root |
| Update | `Tick(deltaTime)` | 只用于 UI 动画、倒计时、等待推进协作 |
| 播放停止 | `OnStoryStopped()` | 清理本次剧情 UI 引用和临时按钮 listener |

**默认 UI 规则**：

| 请求 | 条件 | 默认 UI | 推动剧情的输入 |
|---|---|---|---|
| `Text` | 有文本轨 | `SpeakerText` / `BodyText` | 不推进 |
| `Continue` | 无 choice / command / wait gate | `ContinueButton` | 点击继续 -> `Continue()` |
| `Choice` | 有普通 `Choices` | `ChoiceButtons`，数量等于 `frame.Choices.Count` | 点击选项 -> `Select(choiceId)` |
| `Video` | `play_video` command track | `VideoOutput` | 命令完成 -> `CompleteCommand(commandId, outcomeId)` |
| `Image` | `show_image` command track | `ImageOutput` | 命令完成 -> `CompleteCommand(commandId, outcomeId)` |
| `Custom` | QTE / unlock / seek 后续扩展 | `CustomRoot` 或章节自定义面板 | 由扩展通过 presenter 完成 |

**约束**：

- 不新增旧名 `IStoryInteractionLayer` / `IStoryInteractionChannel` / `IStoryInteractionController` / `IStoryChapterInteractionProvider`；章节差异由 `IInteractionChannel` 自己处理。
- `OnAwake()` 早于预热，`OnStoryStarted()` 晚于预热；loading 和过渡 UI 的出现/关闭不能混成同一个回调。
- 章节切换时必须清理旧 UI 引用；自定义 channel 缺少必需 surface 或选项按钮数量不匹配时应报配置错误，不得静默复用上一章控件。
- `ContinueButton` 只在没有 blocking choice / command / time gate 时可见；一旦出现玩家决定输入，继续按钮隐藏。
- 交互通道只能通过 `StoryPresenter` 回传结果，不得直接改 runner 内部状态，也不得自己跳转 chapter。
- Runtime/Story 不保存任何 `RawImage`、`Button`、`TMP_Text`、UIWindow 或 prefab 引用。

## 5. 子 feature 清单

1. **story-playback-view-input-layers** — 建立 `App.Story.SetInteractions(channel)` 单一交互通道入口，让 interaction channel 按章节创建 UIWindow，并通过 `GetPlaybackSurfaceView(request)` 提供视频、图片、文本、继续和选项 surface。
   - 所属模块：Runtime/StoryPlayback
   - 依赖：无
   - 状态：done
   - 对应 feature：`2026-06-23-story-playback-view-input-layers`
   - 备注：最小闭环；完成后播放一个章节时，UI 不再依赖底部单面板或全屏播放层，而是由注册的 interaction channel 根据当前章节提供 RawImage、按钮、文本和自定义 UI surface。

2. **story-transition-video-seek-controls** — 由 compiler 推导纯过渡视频并写入内部 `__videoSeekPolicy=transition`，为这类视频提供运行时时间条和 `seek(time)`，分支互动视频不显示时间条。
   - 所属模块：Runtime/StoryPlayback + Story Editor
   - 依赖：`story-playback-view-input-layers`
   - 状态：done
   - 对应 feature：`2026-06-23-story-transition-video-seek-controls`
   - 备注：不暴露作者 role 字段；只 seek AVPro 当前视频时间，不改变 StoryRunner 状态。例如开场纯过渡视频可拖动，后续承载对白/选项分支的视频节点不开放 seek。

3. **story-parallel-wait-interaction-flow** — 用现有 `Parallel + Wait + Choice/Command` 跑通视频播放过程中出现选项或命令互动，不新增 TimedChoice 或 media-time runtime API。
   - 所属模块：Runtime/StoryPlayback + Tests
   - 依赖：`story-playback-view-input-layers`
   - 状态：done
   - 对应 feature：`2026-06-24-story-parallel-wait-interaction-flow`
   - 备注：已验证 `PlayVideo` 分支和 `Wait(35)->Choice/Command` 分支可同章并行，wait 到点后由 interaction channel 显示普通选项或 custom command；该类并行互动视频不获得 transition seek policy。

4. **story-video-qte-command** — 支持视频播放中的 QTE command overlay，默认不暂停音视频，并通过 success/fail outcome 推进剧情。
   - 所属模块：Runtime/Story + Runtime/StoryPlayback
   - 依赖：`story-playback-view-input-layers`、`story-parallel-wait-interaction-flow`
   - 状态：done
   - 对应 feature：`2026-06-24-story-video-qte-command`
   - 备注：QTE 出现时机由 `Wait(N) -> qte command` 表达；已实现默认 CustomRoot overlay、最小点击/Space 输入和 success/fail outcome。

5. **story-unlock-interaction-flow** — 建立 unlock command 和 unlock state provider，复用现有 function resolver 做条件判断，用 outcome 表达连线解锁、节点解锁等互动结果。
   - 所属模块：Runtime/Story + Runtime/StoryPlayback
   - 依赖：`story-playback-view-input-layers`、`story-parallel-wait-interaction-flow`
   - 状态：done
   - 对应 feature：`2026-06-24-story-unlock-interaction-flow`
   - 备注：解锁出现时机由 `Wait(N) -> unlock command` 或普通 command 链路表达；已实现协议、unlock state provider 和最小默认 Custom overlay，不做完整谜题编辑器。

6. **story-editor-interaction-authoring-patterns** — 在 Story Editor 中提供 `Parallel + Wait + Choice/QTE/Unlock` 作者模板、图上校验和编译器推导诊断，不新增 TimedChoice、layout slot、anchor 或视频 seek 作者字段。
   - 所属模块：Story Editor
   - 依赖：`story-transition-video-seek-controls`、`story-parallel-wait-interaction-flow`、`story-video-qte-command`、`story-unlock-interaction-flow`
   - 状态：done
   - 对应 feature：`2026-06-24-story-editor-interaction-authoring-patterns`
   - 备注：已提供视频中途 Choice/QTE/Unlock 三个 editor-only 组合模板，模板只生成现有节点和连线；`Wait.completed` 可拥有 Choice item，compiler 继续通过图结构决定是否写入内部 seek policy。

7. **story-interactive-video-sample-acceptance** — 提供交互视频样例和验收测试，覆盖非全屏视频 surface、过渡视频 seek、并行等待后出现选项、QTE success/fail 和解锁 outcome。
   - 所属模块：Scripts/StoryTest + Tests
   - 依赖：`story-editor-interaction-authoring-patterns`
   - 状态：planned
   - 对应 feature：未启动
   - 备注：验收完成后再回写 architecture/requirements 当前能力。

已废弃的规划种子：

- **story-presentation-layout-slots** — dropped。原因：交互通道提供 surface 后，不再需要 Runtime/Story 或 StoryPlayback 共享 `StoryPresentationAnchorPreset`、slot、normalized rect 等呈现协议。
- **story-video-timed-choice** — dropped。原因：现有 `Parallel + Wait + Choice` 已能表达视频过程中出现选项，不新增 TimedChoice 节点或 media-time runtime API。
- **story-editor-interactive-nodes** — dropped。原因：不开放 TimedChoice/layout 字段，改由 `story-editor-interaction-authoring-patterns` 提供现有节点组合模板。

**最小闭环**：第 1 条 `story-playback-view-input-layers` 做完后，StoryPlayback 就拥有明确的 `App.Story.SetInteractions(channel)`、章节生命周期通知和 `GetPlaybackSurfaceView(request)` surface 获取协议；当前“所有交互都挤在底部 DialoguePanel / 全屏播放层”才能从结构上解掉。

## 6. 排期思路

技术依赖上，`story-playback-view-input-layers`、`story-transition-video-seek-controls`、`story-parallel-wait-interaction-flow`、`story-video-qte-command` 和 `story-unlock-interaction-flow` 已完成：单一 interaction channel、纯过渡视频 playback-only seek、现有 `Parallel + Wait + Choice/Command` 表达视频播放中途交互，以及默认 QTE / Unlock command overlay 的底座已经闭合，不需要恢复 TimedChoice、slot preset 或媒体时间 runtime API。

QTE 和解锁类互动都复用同一条多轨编排链路：视频轨负责播放，交互轨用 `Wait(N)` 把 UI 出现时机编排到剧情里，再由 command outcome 推动分支。当前 QTE 已落地为默认 `qte` command overlay，unlock 类互动已落地为默认 `unlock` command + `IUnlockStateProvider` + Custom overlay；Story Editor 已落地视频中途 Choice/QTE/Unlock 组合模板和只读 seek 推导诊断。Editor 模板生成现有节点组合，而不是新增 runtime step；过渡视频和分支选项视频通过图结构区分，compiler 决定是否写入内部 seek policy。后续样例验收覆盖非全屏 video surface、过渡视频 seek、等待后出现选项、QTE 和 unlock outcome。

## 7. 观察项

- `story-playback-architecture` 当前只剩 `story-playback-architecture-acceptance`；这份互动视频 roadmap 不应阻塞基础播放架构的最终验收。
- `.codestable/requirements/story-module.md` 已经把“媒体播放到指定时机后出现选项”写入用户故事；当前已通过 session-time `Parallel + Wait + Choice/Command` 路径覆盖基础选项和 command 互动，QTE 与 Unlock 命令互动及 Story Editor 作者模板已完成，后续仍待样例验收。
- 早期 `story-module-design` 和旧 Story Editor 方案中出现过 QTE/Hotspot/InputWait 概念，但当前默认作者主路径已刻意收紧；本 roadmap 只重新引入明确闭合的互动节点。
- 当前 `StoryPlayerView` 自动创建 UI 仍有底部 `DialoguePanel` 假设；第一条 feature 应优先改这里，而不是只给 Editor 增字段。

## 8. 自查

- 模块拆分：已按 Runtime/Story 数据协议、StoryPlayback 交互会话与媒体适配、UI 层级承载、Story Editor 作者入口和 Scripts/StoryTest 样例拆分。
- 接口契约：已写到 `App.Story.SetInteractions(channel)`、`IInteractionChannel`、`InteractionRequest`、`PlaybackSurfaceView`、章节生命周期通知、纯过渡视频 seek、QTE payload、unlock state provider、UI 生命周期和 Editor 模板字段级别。
- 子 feature slug：已检查 `.codestable/features/`，未发现冲突。
- 依赖关系：DAG，无循环；Editor 节点依赖 runtime/playback 闭合，最终样例依赖 Editor 节点。
- 最小闭环：`story-playback-view-input-layers` 先把单一 interaction channel、章节生命周期和 surface request 定住，再让后续过渡视频 seek、`Parallel + Wait`、QTE 和 unlock 复用同一套承载层。
- 明确不做：已列出 UI 设计器、播放器后端抽象、Startup/Loading/Procedure、平台输入系统、本地化/资源发布等边界。
- 与现有 req/arch：不修改现状文档；只在观察项记录 story-module 用户故事已有但实现未覆盖的部分，以及 Story Editor 对 Parallel/Merge 作者入口的现状需在后续 feature 中复核。

## 9. 变更日志

- 2026-06-24：`story-editor-interaction-authoring-patterns` 完成验收回写，Story Editor 已提供视频中途 Choice/QTE/Unlock 三个 editor-only 组合模板、Wait-owned Choice item 编译和只读 seek policy 诊断。
- 2026-06-24：`story-unlock-interaction-flow` 完成验收回写，视频中解锁互动已落地为默认 `unlock` command、`IUnlockStateProvider`、Custom overlay、`success` / `fail` outcome 和不新增 `IConditionResolver` / 无媒体时间触发边界。
- 2026-06-24：`story-video-qte-command` 完成验收回写，视频中 QTE 已落地为默认 `qte` command overlay、click/Space 输入、`success` / `fail` outcome 和无媒体时间触发边界。
- 2026-06-24：`story-parallel-wait-interaction-flow` 完成验收回写，视频中途选项/command 互动已落地为 session-time `Parallel + Wait + Choice/Command` 编排，并保持并行互动视频不可 seek。
- 2026-06-24：`story-transition-video-seek-controls` 完成验收回写，纯过渡视频 seek 控制已落地为 compiler hidden policy、可选 `VideoSeekSurface` 和 StoryPlayback / Editor playback-only AVPro seek。
- 2026-06-23：`story-playback-view-input-layers` 完成验收回写，StoryPlayback 已落地单一 interaction channel、章节生命周期、surface request 和启动预热边界。
- 2026-06-23：根据反馈移除固定呈现协议和 TimedChoice 路线；StoryPlayback 改为单一 interaction channel、surface request 和媒体适配器，视频中途交互统一由 `Parallel + Wait + Choice/Command` 编排。
- 2026-06-23：新增纯过渡视频 seek 规划，并收敛为 compiler 根据图结构推导内部 `__videoSeekPolicy=transition`；不暴露 `playbackRole` 作者字段，分支选项视频不支持 seek。
- 2026-06-23：将交互通道从“layout slots 先行”调整为“StoryPlayback view/input layer 先行”，并进一步收敛为 `App.Story.SetInteractions(channel)`、章节生命周期通知和 `GetPlaybackSurfaceView(request)`；QTE、unlock 改为建立在该层之上。
