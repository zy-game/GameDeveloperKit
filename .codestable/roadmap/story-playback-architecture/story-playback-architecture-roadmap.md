---
doc_type: roadmap
slug: story-playback-architecture
status: completed
created: 2026-06-22
last_reviewed: 2026-06-23
tags: [story, playback, avpro, startup, procedure]
related_requirements: [story-module, story-editor, framework-startup, procedure-module, ui-module, sound-module]
related_architecture: [ARCHITECTURE]
---

# Story Playback Architecture

## 1. 背景

当前 Story 播放相关代码分散在 `Runtime/Story`、`Runtime/StoryPresentation` 和 `Runtime/StoryPresentation.AVPro` 中。`Runtime/Story` 已经承担剧情数据、运行时推进和 `StoryFrame` 输出；`StoryPresentation` / `StoryPresentation.AVPro` 则承担 UGUI 图片、SoundModule 音频、AVProVideo 视频、`StoryPlayerView`、以及一个过重的运行时 demo/bootstrap。

这条边界现在已经不合适：视频播放在本项目中明确使用 AVProVideo，不需要保留“未来也许换播放器”的独立 AVPro 表现包；同时 `StoryPresentation.AVPro` 里出现了资源初始化、加载 UI、章节预加载、Procedure 切换、首帧等待等启动编排职责，导致播放层越来越像一个小型业务启动框架。用户已确认：`GameDeveloperKit.StoryPresentation` 不再保留，`GameDeveloperKit.StoryPresentation.AVPro` 也不作为独立概念保留；播放能力合并为新的 `GameDeveloperKit.StoryPlayback`，AVPro 是这个播放包的默认实现细节。

本 roadmap 的目标是把 Story 运行时核心、默认播放层、框架启动流程和剧情测试入口拆清楚：`Runtime/Story` 保持纯剧情；`Runtime/StoryPlayback` 提供默认播放器；启动初始化由可挂载 Startup 组件承接；剧情测试放到 `Scripts` 下的独立测试 asmdef。

## 2. 范围与明确不做

### 本 roadmap 覆盖

- 新建 `GameDeveloperKit.StoryPlayback` 作为默认剧情播放程序集，合并旧 `StoryPresentation` 和 `StoryPresentation.AVPro` 的有效播放能力。
- 将 AVProVideo 视频播放、视频路径解析、后续队列预热缓冲都收敛在 `StoryPlayback`，不再保留独立 AVPro presentation 包。
- 保留并整理默认播放能力：`StoryPlayerView`、图片命令播放器、音频命令播放器、AVPro 视频命令播放器、视频路径解析器、播放协调器和命令句柄。
- 移除 `StoryRuntimeDemoBootstrap`、`StoryProcedure`、`StoryProcedureRequest` 这类把启动、加载 UI、资源初始化、章节预加载和流程切换塞进 AVPro 播放包的代码。
- 建立可挂载 Startup 组件，让场景入口负责框架初始化、资源/配置/数据 ready，以及初始化完成后的目标 Procedure 切换。
- 在 `Assets/GameDeveloperKit/Scripts` 下建立简单剧情测试 asmdef，只负责注册剧情和播放剧情。
- 规划 AVPro 队列缓冲/预热能力，降低视频切换时的空白期。

### 明确不做

- 不在 `Runtime/Story` 中引用 AVProVideo、UGUI、TextMeshPro、ResourceModule、SoundModule、Loading UI 或 Procedure。
- 不保留“可替换视频后端”的复杂抽象；项目默认视频后端就是 AVProVideo。
- 不把章节资源预加载、加载窗口、LoadingController、UI 模块窗口管理或流程切换逻辑放进 `StoryPlayback`。
- 不在本 roadmap 中重做 Story Editor 节点图、编译器或 `StoryProgram` 数据结构；必要的引用更新只服务于播放包迁移。
- 不把视频重新放进资源包。视频来源仍只有 `streaming_assets`、`persistent_data_path`、`network_stream`。
- 不做完整业务剧情系统、存档、章节选择 UI、加载页、场景切换或发布资源流程。
- 不改写现有 Resource/Sound/UI/Procedure 模块的基本职责；Startup 只编排它们的显式 ready API。

## 3. 模块拆分（概设）

```
Story Playback Architecture
├── Runtime/Story：纯剧情核心，保存 StoryProgram、StoryModule、StoryFrame 和命令数据协议
├── Runtime/StoryPlayback：默认播放包，持有 UGUI/Sound/AVPro 播放实现和播放协调器
├── Framework Startup：可挂载 Startup 组件，负责 App 和模块 ready 编排
└── Scripts/StoryTest：项目级剧情测试入口，注册剧情并调用 StoryPlayback 播放
```

### 3.1 Runtime/Story · 剧情核心

- **职责**：维护剧情数据模型、运行时推进、`StoryFrame` 输出、选项/命令/等待状态和命令数据协议。它只描述“当前剧情需要表现层做什么”，不负责真实播放。
- **承载的子 feature**：`story-playback-package-merge`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Story/Runtime/IStoryCommandHandler.cs` 中与播放协调有关的类型需要迁出或重新归类；`StoryMediaCommandNames` 这类命令数据协议仍保留在 Story 核心。

### 3.2 Runtime/StoryPlayback · 默认播放包

- **职责**：提供项目默认 Story 播放能力，依赖 `GameDeveloperKit.Runtime`、AVProVideo、UGUI、TextMeshPro、ResourceModule、SoundModule。它消费 `StoryFrame`，执行 `play_video` / `show_image` / `play_audio`，并通过 `StoryModule` API 推进剧情。
- **承载的子 feature**：`story-playback-package-merge`、`story-playback-video-prewarm`
- **触碰的现有代码 / 模块**：迁移旧 `Runtime/StoryPresentation` 和 `Runtime/StoryPresentation.AVPro` 中的播放类；删除旧 presentation asmdef；更新 Editor/Test asmdef 引用。

### 3.3 Framework Startup · 可挂载启动组件

- **职责**：提供一个可挂在场景 GameObject 上的 `MonoBehaviour` 启动组件。它调用 `App.Startup()`，按配置显式准备 Resource/Config/Data 等模块，然后通过 Inspector 下拉选择的目标 Procedure 进入业务流程。
- **承载的子 feature**：`startup-component-entry`
- **触碰的现有代码 / 模块**：`Runtime/Procedure`、`Runtime/Core/App`、Resource/Config/Data ready API；必要的 Editor inspector/dropdown 代码放在 `Editor` 下。

### 3.4 Scripts/StoryTest · 剧情测试入口

- **职责**：作为项目级简单测试样例，不放进 framework runtime 包。它只注册示例剧情、找到或持有 `StoryPlayerView`，然后播放指定 Story/Chapter。
- **承载的子 feature**：`story-test-scripts-entry`
- **触碰的现有代码 / 模块**：新增 `Assets/GameDeveloperKit/Scripts/StoryTest` asmdef 和少量脚本；引用 `GameDeveloperKit.Runtime`、`GameDeveloperKit.StoryPlayback`。

## 4. 模块间接口契约 / 共享协议（架构层详设）

这一节是后续 feature-design 的硬约束输入。若实现时需要改动这里的接口边界，应先回到本 roadmap update。

### 4.1 Story 核心与播放层边界

**方向**：`Runtime/StoryPlayback` -> `Runtime/Story`

**形式**：函数调用 + 共享数据类型

**契约**：

```csharp
// Runtime/Story 保留的核心推进 API
public sealed class StoryModule : GameModuleBase
{
    public StoryRunner Start(StoryProgram program, string chapterId = null);
    public StoryRunner StartProgram(string storyId, string chapterId = null);
    public StoryRunner Restore(StorySnapshot snapshot);
    public StoryFrame Continue();
    public StoryFrame Select(string choiceId);
    public StoryFrame CompleteCommand(string commandId, string outcomeId);
    public StoryFrame Evaluate(double time);
    public StoryFrame CurrentFrame { get; }
    public StoryRunner CurrentRunner { get; }
}

// Runtime/Story 保留的命令数据协议
public static class StoryMediaCommandNames
{
    public const string PlayVideo = "play_video";
    public const string ShowImage = "show_image";
    public const string PlayAudio = "play_audio";
    public const string ClipArgument = "clip";
    public const string ImageArgument = "image";
    public const string VideoSourceArgument = "source";
    public const string VideoSourceStreamingAssets = "streaming_assets";
    public const string VideoSourcePersistentDataPath = "persistent_data_path";
    public const string VideoSourceNetworkStream = "network_stream";
    public const string CompletedOutcome = "completed";
}
```

**约束**：

- `Runtime/Story` 只能输出 `StoryFrame.Tracks`、`StoryFrame.Choices`、等待/完成状态和基础 `StoryCommand.Arguments`。
- `Runtime/Story` 不持有 `StoryPlayerView`、`StoryPresenter`、命令播放器、AVPro `MediaPlayer`、UGUI 控件、`ResourceModule` 或 `SoundModule`。
- `StoryMediaCommandNames` 作为作者 schema、compiler、runtime validation 和播放层共同使用的数据协议保留在 Story 核心；它不是播放实现。
- 播放层推进剧情只能调用 `Continue()`、`Select(choiceId)`、`CompleteCommand(commandId, outcomeId)`、`Evaluate(time)`，不得直接改 runner 内部状态。

### 4.2 StoryPlayback 对外播放契约

**方向**：业务 / Scripts StoryTest -> `Runtime/StoryPlayback`

**形式**：Unity component + 普通 C# 对象

**契约**：

```csharp
public sealed class StoryPlayerView : MonoBehaviour, IStoryFramePresenter
{
    public StoryPresenter Presenter { get; }
    public StoryFrame CurrentFrame { get; }
    public Exception LastError { get; }

    public event Action<StoryAvProVideoPlayback> FirstVideoFrameReady;

    public static StoryPlayerView CreateDefault(Transform parent = null);

    public void ConfigureModules(
        StoryModule storyModule,
        ResourceModule resourceModule = null,
        SoundModule soundModule = null);

    public void Play(StoryProgram program, string chapterId = null);
    public void PlayRegistered(string storyId, string chapterId = null);
    public void StopPlayback();
    public void Continue();
    public void Select(string choiceId);
}
```

**约束**：

- `StoryPlayerView` 是默认 UGUI 播放视图，不是 LoadingWindow，不负责打开/关闭 UI 模块窗口。
- `StoryPlayerView.CreateDefault()` 只创建播放器视图自身需要的 UGUI 节点、视频 `RawImage`、图片 `RawImage`、对白和按钮；父节点由调用方提供，默认测试入口应挂到 UI 的 `StoryPlayback` 层。
- `ConfigureModules()` 只接收已经创建/初始化好的模块引用；若调用方使用 `App.Resource`，资源 ready 仍由 Startup 或业务 Procedure 显式完成。
- `StoryPlayerView.Play*()` 只启动剧情播放，不负责 `App.Startup()`、`App.Resource.InitializeAsync()`、章节预加载、Procedure 切换或场景加载。
- `StoryPlayback` 允许依赖 AVProVideo、UGUI、TextMeshPro、ResourceModule、SoundModule；不允许反向要求 `Runtime/Story` 依赖这些包。

### 4.3 StoryPlayback 命令执行契约

**方向**：`StoryPresenter` -> 命令播放器

**形式**：接口调用

**契约**：

```csharp
public interface IStoryFramePresenter
{
    void Present(StoryFrame frame, StoryPresenter presenter);
    void Clear(StoryFrame frame);
}

public interface IStoryCommandHandle
{
    StoryCommand Command { get; }
    bool IsCompleted { get; }
    bool IsCanceled { get; }
    bool IsStopped { get; }
    Exception Error { get; }
    string OutcomeId { get; }

    event Action<IStoryCommandHandle> Completed;
    event Action<IStoryCommandHandle> Canceled;
    event Action<IStoryCommandHandle> Stopped;
    event Action<IStoryCommandHandle> Failed;

    void Complete(string outcomeId = null);
    void Cancel();
    void Stop();
    void Fail(Exception exception);
}

public interface IStoryCommandHandler
{
    bool CanHandle(StoryCommand command);
    IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context);
}

public sealed class StoryPresenter : IDisposable
{
    public StoryPresenter(StoryModule module, IStoryFramePresenter framePresenter = null);
    public StoryFrame Start(StoryProgram program, string chapterId = null);
    public StoryFrame StartProgram(string storyId, string chapterId = null);
    public StoryFrame Restore(StorySnapshot snapshot);
    public StoryFrame Continue();
    public StoryFrame Select(string choiceId);
    public StoryFrame CompleteCommand(string commandId, string outcomeId);
    public StoryFrame Evaluate(double time);
    public void AddCommandHandler(IStoryCommandHandler handler);
    public void StopActiveCommands();
    public void Stop();
}
```

**约束**：

- 以上播放协调类型归属 `GameDeveloperKit.StoryPlayback` 程序集；C# namespace 可继续沿用现有 `GameDeveloperKit.Story`，避免一次性破坏过多调用点。
- `StoryPresenter` 只负责把 `StoryFrame` 派发给 view/命令播放器，并在命令完成后调用 `StoryModule.CompleteCommand()`；它不初始化框架模块。
- `StoryMediaCommandHandler` 和 `IStoryVideoCommandPlayer` / `IStoryImageCommandPlayer` / `IStoryAudioCommandPlayer` 属于播放包内部默认接线层，不作为 `Runtime/Story` 的长期职责。
- 如果后续确实要缩减接口数量，应在 `StoryPlayback` 内部缩减，不把 AVPro 细节推回 Story 核心。

### 4.4 视频来源与路径解析契约

**方向**：Story command data -> `StoryVideoPathResolver` -> AVPro `MediaPlayer.OpenMedia`

**形式**：命令参数协议 + 函数调用

**契约**：

```csharp
public static class StoryVideoPathResolver
{
    public static string Resolve(string source, string clip);

    public static bool TryResolve(
        string source,
        string clip,
        out string resolvedPath,
        out string errorMessage);
}
```

`play_video` 命令参数：

```yaml
name: play_video
arguments:
  source: streaming_assets | persistent_data_path | network_stream
  clip: string
```

路径规则：

- `streaming_assets`：`clip` 必须是 `Application.streamingAssetsPath` 内的相对路径；允许输入 `Assets/StreamingAssets/...` 或 `StreamingAssets/...` 并归一化；禁止 `Assets/...` 非 StreamingAssets 路径、绝对本地路径、URL、`..`。
- `persistent_data_path`：`clip` 必须是 `Application.persistentDataPath` 内的相对路径；禁止 `Assets/...`、`StreamingAssets/...`、绝对本地路径、URL、`..`。
- `network_stream`：`clip` 必须是绝对 URL；禁止本地相对路径和本地绝对路径。
- 所有来源都禁止 `guid:` 引用。视频不进入资源包。

AVPro 打开规则：

```csharp
mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, resolvedPath, autoPlay);
```

### 4.5 AVPro 视频播放与预热队列契约

**方向**：`StoryAvProVideoCommandPlayer` -> `StoryAvProVideoPreloadQueue` -> AVProVideo

**形式**：普通 C# 对象 + AVPro 事件

**契约**：

```csharp
public sealed class StoryAvProVideoCommandPlayer : IStoryVideoCommandPlayer, IDisposable
{
    public Func<string, string> PathResolver { get; set; }
    public StoryAvProVideoPreloadQueue PreloadQueue { get; set; }
    public IReadOnlyList<StoryAvProVideoPlayback> ActivePlaybacks { get; }

    public event Action<StoryAvProVideoPlayback> PlaybackStarted;

    public IStoryCommandHandle PlayVideo(
        StoryCommand command,
        StoryRuntimeContext context,
        string clipPath);
}

public sealed class StoryAvProVideoPreloadQueue : IDisposable
{
    public int Capacity { get; }
    public int Count { get; }

    public UniTask<StoryAvProVideoPreloadHandle> PreloadAsync(
        StoryCommand command,
        string source,
        string clipPath,
        CancellationToken cancellationToken = default);

    public bool TryAcquire(
        string source,
        string clipPath,
        out StoryAvProVideoPlayback playback);

    public void Release(string source, string clipPath);
    public void Clear();
}
```

预热状态：

```csharp
public enum StoryAvProVideoPreloadStatus
{
    Pending,
    ReadyToPlay,
    FirstFrameReady,
    Failed,
    Canceled
}
```

**约束**：

- 预热队列只负责打开/准备 AVPro `MediaPlayer`，不显示 UI、不推进剧情、不切 Procedure。
- `ReadyToPlay` 对应 AVPro `MediaPlayerEvent.EventType.ReadyToPlay`；`FirstFrameReady` 对应 `MediaPlayerEvent.EventType.FirstFrameReady`。
- `PlayVideo()` 优先复用与 `source + clipPath` 匹配的预热播放器；未命中时按当前逻辑立即打开播放。
- 被 `Acquire` 的预热播放器所有权转移给 `StoryAvProVideoPlayback`，由播放实例负责完成、失败、取消和释放。
- 队列容量满时按 feature-design 选定策略淘汰未 acquire 的旧项；不得停止正在播放的 `ActivePlaybacks`。
- 预热失败只记录在 preload handle 中，不自动让剧情失败；真实 `play_video` 命令仍以 `PlayVideo()` 的结果为准。

### 4.6 Startup 组件入口契约

**方向**：Unity scene startup component -> `App` / `ProcedureModule`

**形式**：MonoBehaviour + Inspector 配置 + Procedure 直接切换

**契约**：

```csharp
[Serializable]
public sealed class FrameworkStartupModuleOptions
{
    public bool InitializeResource { get; }
    public ResourceSettings ResourceSettings { get; }
    public bool ResolveConfigModule { get; }
    public bool ResolveDataModule { get; }
    public bool ResolveSoundModule { get; }
    public SoundMixerSettings SoundMixerSettings { get; }
}

public sealed class FrameworkStartup : MonoBehaviour
{
    // Inspector 下拉选择，序列化时保存 AssemblyQualifiedName。
    [SerializeField] private string m_TargetProcedureTypeName;
    [SerializeField] private UnityEngine.Object m_TargetUserData;
    [SerializeField] private FrameworkStartupModuleOptions m_Modules;
    [SerializeField] private bool m_ShutdownAppOnDestroy;

    public bool IsRunning { get; }
    public bool HasStarted { get; }
    public Exception LastError { get; }
    public Type TargetProcedureType { get; }

    public UniTask StartupAsync();
}
```

启动顺序：

1. `FrameworkStartup.StartupAsync()` 调用 `await App.Startup()`，只进入 App lifecycle 状态，不依赖 App 自动创建默认模块。
2. `FrameworkStartup` 解析 Inspector 选择的 target procedure type；该下拉只选择初始化完成后要进入的业务 Procedure。
3. `FrameworkStartup` 按 `FrameworkStartupModuleOptions` 访问 `App.Resource` / `App.Config` / `App.Data` 等模块，创建同步模块外壳。
4. 若启用资源初始化，调用 `await App.Resource.InitializeAsync(resourceSettings)`；若未配置资源初始化，不隐式初始化资源。
5. Config/Data 首版只通过 `App.Config` / `App.Data` 创建同步模块外壳；不得在 roadmap/feature 中凭空新增宽泛 `ConfigModule.InitializeAsync()` 或 `DataModule.InitializeAsync()`。
6. 初始化完成后调用 `await App.Procedure.ChangeAsync(targetProcedureType, targetUserData)` 进入目标 Procedure。
7. 若 `m_ShutdownAppOnDestroy` 启用，组件销毁时调用 `await App.Shutdown()` 或等价的 fire-and-forget shutdown 包装；具体错误处理在 feature-design 中定义。

**约束**：

- `FrameworkStartup` 是可挂载场景组件，不继承 `ProcedureBase`，也不引入 `StartupProcedureBase` / `DefaultStartupProcedure`。
- Inspector 下拉只允许选择继承 `ProcedureBase` 且非 abstract、非 open generic 的目标 Procedure 类型；不提供 startup procedure type 下拉。
- `FrameworkStartup` 可以负责注册或确保目标 Procedure 可创建，但最终进入业务流程时直接调用 `App.Procedure.ChangeAsync(targetProcedureType, targetUserData)`。
- `FrameworkStartup` 不使用 `App.Procedure.RequestChange()`；该 API 只适合 Procedure 切换期间的 pending change，不适合作为外部场景组件入口。
- Startup 组件只做框架/模块初始化和目标 Procedure 切换，不注册剧情、不播放剧情、不打开 LoadingWindow、不做章节预加载、不等待 AVPro 首帧。
- Editor 下拉/PropertyDrawer 只能放在 `Assets/GameDeveloperKit/Editor` 或 Editor-only asmdef 中。

### 4.7 Scripts StoryTest 契约

**方向**：`Scripts/StoryTest` -> `StoryPlayback`

**形式**：项目脚本 asmdef + Procedure/MonoBehaviour

**契约**：

```csharp
public sealed class StoryTestProcedure : ProcedureBase
{
    public override UniTask OnEnterAsync(ProcedureBase previous, object userData);
    public override void OnUpdate(float deltaTime, float unscaledDeltaTime);
}

public sealed class StoryTestRequest
{
    public StoryProgram Program { get; }
    public string StoryId { get; }
    public string ChapterId { get; }
    public StoryPlayerView PlayerView { get; }
    public StoryPlayerView PlayerViewPrefab { get; }
}
```

**约束**：

- `StoryTestProcedure` 可以注册 `StoryProgram`，然后调用 `StoryPlayerView.Play()` 或 `PlayRegistered()`。
- `StoryTestProcedure` 优先使用请求中的 `PlayerView`，其次复用场景已有 `StoryPlayerView`；若请求提供 `PlayerViewPrefab` 则实例化到 `UILayer.StoryPlayback`；若仍无播放器则调用 `StoryPlayerView.CreateDefault()` 在 `UILayer.StoryPlayback` 下创建默认播放 UI。
- `StoryTestProcedure` 不做 Resource 初始化、Config 初始化、LoadingWindow、章节预加载或 AVPro 首帧等待；这些由 Startup 或 StoryPlayback 自身能力处理。
- `Scripts/StoryTest` 是项目示例/测试入口，不作为 Runtime framework 的必选模块。

## 5. 子 feature 清单

1. **story-playback-package-merge** — 新建 `GameDeveloperKit.StoryPlayback`，迁移默认播放类和播放协调类型，移除旧 `StoryPresentation` / `StoryPresentation.AVPro` 作为对外程序集。
   - 所属模块：Runtime/Story + Runtime/StoryPlayback
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-06-22-story-playback-package-merge
   - 备注：最小闭环；完成后场景中已有 `StoryPlayerView` 能播放传入或已注册 `StoryProgram`，视频走 AVPro，图片走 ResourceModule，音频走 SoundModule。

2. **startup-component-entry** — 建立可挂载 Startup 组件和目标 Procedure 类型下拉选择，把框架初始化与目标流程切换从 Story/AVPro 层移走。
   - 所属模块：Framework Startup
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-06-22-startup-component-entry
   - 备注：Startup 是场景组件，不是 Procedure；提供后续业务启动链路，不依赖 StoryPlayback，不播放剧情、不打开 LoadingWindow、不做章节预加载。

3. **story-test-scripts-entry** — 在 `Assets/GameDeveloperKit/Scripts` 下创建简单剧情测试 asmdef，注册剧情并调用 StoryPlayback 播放。
   - 所属模块：Scripts/StoryTest
   - 依赖：`story-playback-package-merge`、`startup-component-entry`
   - 状态：done
   - 对应 feature：2026-06-22-story-test-scripts-entry
   - 备注：替代旧 `StoryRuntimeDemoBootstrap` 的测试用途；缺省播放器挂到 `UILayer.StoryPlayback`，但不恢复资源预加载和 loading UI 职责。

4. **story-playback-video-prewarm** — 在 `StoryPlayback` 中实现 AVPro 视频队列缓冲/预热，减少视频切换时黑屏或空白期。
   - 所属模块：Runtime/StoryPlayback
   - 依赖：`story-playback-package-merge`
   - 状态：done
   - 对应 feature：2026-06-23-story-playback-video-prewarm
   - 备注：已落地 `StoryAvProVideoPreloadQueue` / `Handle` / `Status`、默认小容量队列和 look-ahead；只做视频播放器准备和复用，不做章节资源预加载和 UI loading。

5. **story-playback-architecture-acceptance** — 全部子 feature 落地后回查架构现状，确认 architecture/requirements 已记录 StoryPlayback、AVPro 边界和启动流程的当前状态。
   - 所属模块：跨模块验收
   - 依赖：`story-test-scripts-entry`、`story-playback-video-prewarm`
   - 状态：done
   - 对应 feature：未启动
   - 备注：该条已由前序 feature 的验收回写和现状文档同步完成；roadmap 仅作收口归档，不再单独启动代码 feature。

**最小闭环**：第 1 条 `story-playback-package-merge` 做完后，项目拥有新的 `GameDeveloperKit.StoryPlayback` 播放包；播放层不再分裂成 `StoryPresentation` / `StoryPresentation.AVPro`，并且可在场景中通过 `StoryPlayerView.Play()` 播放一个 `StoryProgram`。

## 6. 排期思路

技术依赖上，`story-playback-package-merge` 必须先做，因为后续剧情测试入口和视频预热都要依赖新的 `StoryPlayback` 包。`startup-component-entry` 与播放包迁移没有直接代码依赖，可以并行或紧接着做；但 `story-test-scripts-entry` 应在二者之后，因为它既需要可挂载 Startup 组件，也需要新的播放包。视频预热最好放在播放包迁移之后单独做，避免在移动程序集、删旧 bootstrap 的同时引入 AVPro 播放生命周期变化。

第一条选为最小闭环，是因为它直接消除当前最混乱的包边界：旧 StoryPresentation 不再作为对外结构，AVPro 也不再孤立成一个“表现层插件”。完成这条后，后续 Startup、Scripts 测试和 Prewarm 都能围绕稳定的包边界推进。

## 7. 观察项

- `.codestable/architecture/ARCHITECTURE.md` 已在 `story-playback-package-merge` 验收中回写为 AVProVideo 只允许位于 `StoryPlayback` 和 Editor playback。
- `StoryRuntimeDemoBootstrap` 当前包含资源初始化、loading UI、章节预加载、首帧等待和 Procedure 切换；删除它时要同步清理 Editor tests 中对 `StoryProcedure` / `StoryProcedureRequest` 的反射测试。
- `StoryMediaCommandNames` 被 Story Editor schema、compiler、runtime validation 和测试广泛引用，应作为命令数据协议保留在 Story 核心，不随播放器迁走。
- `StoryPresenter`、命令 handler 和 command handle 当前位于 `Runtime/Story/Runtime/IStoryCommandHandler.cs`，但职责更接近播放协调层；迁移时要补上 Runtime tests 到 StoryPlayback tests/Editor tests 的引用调整。
- 旧 `StoryPlayerView.prefab` 位于 `Runtime/StoryPresentation.AVPro`，迁移到 `Runtime/StoryPlayback` 时不要手写 `.meta`，由 Unity 生成/维护。

## 8. 自查

- 模块拆分：已按 Story 核心、StoryPlayback 默认播放包、Framework Startup、Scripts StoryTest 分层，每层职责一句话可说明。
- 接口契约：已写到 `StoryPlayerView`、`StoryPresenter`、命令句柄、视频路径解析、预热队列、Startup 组件和 StoryTest request 级别。
- 子 feature slug：已检查 `.codestable/features/`，未发现冲突。
- 依赖关系：DAG，无循环；`story-test-scripts-entry` 依赖播放包和启动入口；预热只依赖播放包。
- 最小闭环：`story-playback-package-merge` 做完即可端到端播放一个 `StoryProgram`。
- 明确不做：已列出不在 Story 核心引用播放/UI/Procedure、不做可替换视频后端、不做 loading/chapter preload 等边界。
- 与现有 req/arch：本 roadmap 与 `story-module` “Runtime 不播放媒体”不冲突，因为播放迁入独立 `StoryPlayback`；与现有 architecture 中 `StoryPresentation.AVPro` 边界冲突已列为观察项，待落地后回写现状。

## 9. 变更日志

- 2026-06-23：`story-playback-architecture-acceptance` 收口完成，StoryPlayback 合并、Startup、StoryTest 和 AVPro 预热四项已回写现状文档；本 roadmap 标记为 completed 并归档。
- 2026-06-23：启动 `story-playback-video-prewarm`，进入 AVPro 视频队列预热 feature；范围保持在 StoryPlayback 内部播放器准备/复用，不恢复旧章节资源预加载或 Loading UI。
- 2026-06-23：`story-playback-video-prewarm` 验收完成，StoryPlayback 支持 AVPro 视频队列预热、play_video 优先 Acquire 预热播放器和未命中即时播放 fallback。
- 2026-06-22：按用户确认将 Startup 从 Procedure 方案改为可挂载场景组件；组件负责 `App.Startup()`、显式模块 ready 和目标 Procedure 切换，Startup 本身不继承 `ProcedureBase`，也不引入 startup procedure type 下拉。
- 2026-06-22：`startup-component-entry` 验收完成，Startup 契约同步为 `FrameworkStartup` + `FrameworkStartupModuleOptions`，Resource 直接接收 `ResourceSettings`，Config/Data 只解析同步外壳，Sound 通过内嵌 `SoundMixerSettings` 显式配置。
- 2026-06-23：`story-test-scripts-entry` 验收完成，Scripts StoryTest 入口标为 done；契约同步为缺省播放器自动创建到 `UILayer.StoryPlayback`。
