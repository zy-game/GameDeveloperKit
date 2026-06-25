# Black Rain 架构总入口

> 状态：骨架（待填充）
> 创建日期：2026-05-17
> 最近审阅：2026-06-24

## 1. 项目简介

Black Rain — Unity/C# GameDeveloperKit 框架项目

## 2. 核心概念 / 术语表

### Module Lifecycle（模块生命周期）

`IGameModule` / `GameModuleBase` 是运行时模块的同步生命周期契约：模块实现 `void Startup()` 和 `void Shutdown()`，`IReference.Release()` 对模块同步调用 `Shutdown()`。模块 `Startup()` 只负责同步轻量外壳，例如创建字典、队列、Unity driver/root、默认对象或注册同步 callback；需要文件、网络、资源 manifest、package 初始化、Procedure enter/leave 等 `await` 的 ready 流程必须通过显式 async API 或 Procedure/bootstrap 编排承接。

`App.Startup()`、`App.Shutdown()`、`App.Register<T>()`、`App.Unregister<T>()` 当前仍保留 `UniTask` 外壳以兼容调用侧，但内部驱动模块生命周期时不再等待 `module.Startup()` / `module.Shutdown()`。`App.Startup()` 不再预加载默认模块，只负责框架生命周期状态切换；Runtime `Startup.cs` 场景脚本已删除，框架不再提供自动存在的默认 MonoBehaviour bootstrap。需要场景启动入口时，业务显式挂载 `FrameworkStartup` 组件，由它调用 `App.Startup()`、按配置准备模块同步外壳或显式 ready，再切换到 Inspector 选择的目标 Procedure。

### App Module Resolver（模块按需解析）

`App.GetModule<TModule>()` 是同步按需模块入口，`App.Event`、`App.Resource`、`App.Timer`、`App.Combat` 等同步属性都委托给它。resolver 先检查已注册模块；未注册时读取目标模块上的 `[ModuleDependency]`，递归启动依赖，再创建并 `Startup()` 目标模块。启动成功的模块写入 `_moduleOrder`，`App.Shutdown()` 按反序关闭。

resolver 会去重同一模块上的重复依赖，检测循环依赖并抛包含依赖链的 `GameException`。本次解析中新创建的依赖如果在后续目标启动失败时会回滚；解析请求前已存在的模块不会被误关。`TryGetRegistered<T>()` 只查询已注册模块，`TryGetValue<T>()` 已收口为同样语义，不再创建未启动裸模块。

### Framework Startup（场景启动组件）

`FrameworkStartup` 是位于 Runtime 的可挂载 `MonoBehaviour` 场景入口。它不是 Procedure，也不恢复旧 `Startup.cs` 自动脚本；场景或测试代码可以显式调用 `StartupAsync()`，Unity `Start()` 也会 fire-and-forget 调用同一入口。组件序列化目标 Procedure 的 `AssemblyQualifiedName`、可选 `UnityEngine.Object` userData、`FrameworkStartupModuleOptions` 和销毁时 shutdown 开关。

`StartupAsync()` 的固定顺序是：等待 `App.Startup()` 完成 lifecycle 状态切换，解析并校验目标 Procedure 类型，按 `FrameworkStartupModuleOptions` 准备 Resource / Config / Data / Sound，最后调用 `App.Procedure.ChangeAsync(targetProcedureType, targetUserData)`。Resource 只在 `InitializeResource` 开启时调用 `App.Resource.InitializeAsync(ResourceSettings)`；Config/Data 首版只通过访问 `App.Config` / `App.Data` 创建同步外壳；Sound 在 `ResolveSoundModule` 开启时访问 `App.Sound` 并调用 `ConfigureMixer(SoundMixerSettings)`。`StartupAsync()` 被 await 时向调用方抛异常并记录 `LastError`，Unity 自动启动路径通过 `Debug.LogException` 输出；同一组件成功启动后再次调用直接返回，启动中重复调用等待同一轮 completion，避免重复进入目标 Procedure。

`FrameworkStartupInspector` 位于 Editor 目录，通过 `TypeCache.GetTypesDerivedFrom<ProcedureBase>()` 枚举可创建的目标 Procedure，并过滤 abstract、open generic 和非 Procedure 类型。它只负责 authoring 下拉和写入目标 Procedure 类型名，不创建 Startup Procedure，也不把 Loading UI、StoryPlayback、剧情注册或章节预加载放进启动层。

## 3. 子系统 / 模块索引

### Story Editor / Editor Node Graph

Story Editor 是当前剧情 authoring 的编辑器入口。菜单 `GameDeveloperKit/剧情编辑器` 和 `GameDeveloperKit/剧情编辑/打开示例剧情图` 首先打开 `StoryEditorWelcomeWindow`（VS Code 风格欢迎引导页），用户选择新建/打开/最近资源/示例剧情后进入 `StoryEditorWindow` 主编辑器。主编辑器窗口左侧是中文剧情树，章节打开后中间区域使用项目内 `EditorNodeGraphKit` 承载 ShaderGraph 式节点图；Story 专有语义通过 `StoryEditorGraphAdapter` 映射到通用节点图库。

核心类型：
- `StoryEditorWelcomeWindow` — 独立 EditorWindow，剧情编辑器的启动欢迎页，提供新建/打开/示例剧情按钮、最近打开资源列表和快速开始引导；用户做出选择后打开 `StoryEditorWindow` 并关闭自身。最近资源通过 `StoryEditorRecentAssets` 以 EditorPrefs JSON 持久化，最多保留 10 条，去重并按时间倒序排列。
- `StoryEditorRecentAssets` — Static helper，封装最近打开的 `StoryAuthoringAsset` 路径列表的 EditorPrefs 读写，提供 `GetRecentPaths()` / `RecordOpen(assetPath)` / `IsValidAsset(assetPath)` 三个公开方法。
- `EditorNodeGraphCanvas` — 通用 UI Toolkit graph area，负责右键/Space 创建菜单、Delete/Backspace 删除委托、Esc 取消 pending wire、F 聚焦、pan/zoom、节点库拖入、框选、wire 绘制、小地图和黑板宿主。
- `EditorNodeGraphNodeView` — 通用节点视图，负责节点拖拽、端口 dot 拖线、节点内字段编辑和选中态显示。
- `EditorNodeGraphPaletteView` — 通用节点库，支持从节点库拖模板到 graph area 创建节点，单击不会创建。
- `EditorGraphDiagnostic` / `EditorGraphDiagnosticTargetKind` — 通用节点图库的诊断显示模型，可挂到 graph、node、field、port 或 wire，记录 severity、中文 message、tooltip 和 stale 状态。
- `IEditorNodeGraphAdapter` — 业务模型接入 graph kit 的唯一边界，提供 nodes/wires/templates、创建/移动/选择/连接/删除和字段写回委托；框选通过 `SelectNodes(nodeIds)` 回传多选节点，删除仍统一走 `DeleteSelection()`。
- `StoryEditorGraphAdapter` — Story adapter，把 `StoryAuthoringChapter`、`StoryAuthoringNode`、`StoryAuthoringEdge` 和 `NodeSchemaRegistry` 映射成通用 graph node / wire / template，并在单节点模板之外追加 editor-only 互动编排模板。
- `StoryEditorDiagnostics` / `StoryEditorDiagnosticSet` — Story 诊断 helper，把即时 authoring 校验与 `StoryValidationReport` source 映射为通用 graph diagnostics，并提供中文 summary 文案。
- `StoryEditorPortPolicy` — Story authoring 图的端口策略，集中判定节点是否允许输入/输出、输出端口是否声明、文本节点 / Wait / Merge 到 Choice 的多连语义、单连/多连容量，以及非法连接的中文原因。
- `NodeParameterDefinition` / `NodeSchemaRegistry` — Story 节点字段 schema 的唯一来源，包含字段 key、中文 label、类型、必填性、tooltip 和资源类型字符串；默认作者节点集由 `NodeSchemaRegistry.IsDefaultAuthoringNode()` 统一判定，palette、创建菜单、端口策略、compiler 和 tests 共用这套判断。
- `StoryCommandArgumentDefinition` / `StoryCommandDefinition.ArgumentDefinitions` — runtime command schema 的 typed argument metadata；命令参数只进入 `StoryCommand.Arguments` 基础值，不携带 Unity object。
- `StoryProgramCompiler` — Story authoring graph 到 `StoryProgram` 的编译入口；`Dialogue` / `Narration` / `Merge` / `Wait` 的 `completed` 连到多个 Choice item 时，编译器会生成额外的 synthetic `StoryStepKind.Choice`，并跳过这些 Choice item 的独立 step 编译；同一 completed 端口不能混连 Choice item 和普通流程目标。`PlayVideo` 节点会由 compiler 根据 graph 结构保守推导是否为纯过渡视频；只有可证明的 transition video 才会在 command arguments 写入内部 `__videoSeekPolicy=transition`，作者 schema 不暴露 `playbackRole` 或 `seekable`。
- `StorySampleGraphFixture` — Editor 侧 canonical 示例剧情图构造器，生成 `sample_story_graph` / 四章中文样例，并可创建 `Assets/GameDeveloperKit/Story/SampleStoryGraph.asset` 供 Story Editor 打开。
- `StoryPlaybackSession` — Editor-only 播放会话封装，编译 `StoryAuthoringAsset` 后创建独立 `StoryModule`，通过 `Start()`、`Continue()`、`Select()`、`CompleteCommand()`、`Evaluate()` 推进 runtime `StoryFrame`，并记录播放窗口 history。
- `StoryEditorAvProPlayback` — Editor-only AVPro 播放层，播放窗口遇到 `play_video` command 时创建隐藏 `MediaPlayer`，按 `StoryVideoPathResolver` 的三来源规则打开视频、显示纹理并在完播后调用 runtime command completion；当 command 带内部 transition seek policy、非 loop 且 duration 可用时，暴露 `CanSeek` / `DurationSeconds` / `CurrentTimeSeconds` / `Seek(time)` 给播放窗口预览。
- `StoryEditorPlaybackWindow` — Story Editor 的独立运行时播放窗口，显示当前 story/chapter、编译错误、`StoryFrame` tracks、choices、gate 状态和历史；`play_video` 由 AVProVideo 真实播放，命令资源参数只做 Editor 侧解析/展示，不进入 runtime。窗口会显示只读 `seek policy: transition/disabled` 诊断，并只为带内部 transition policy 的视频显示拖动 slider。
- `StoryFrame` / `StoryFrameTrack` — Story runtime 当前可观察输出契约；同一 frame 可同时包含文本、命令、等待轨和选项，表现层读取 tracks 与 choices 后调用 `Select(choiceId)`、`CompleteCommand(commandId, outcomeId)` 或 `Evaluate(time)` 推进。
- `StoryMediaCommandNames` / `StoryInteractionCommandNames` — 位于 `GameDeveloperKit.Runtime` 的 Story 命令数据协议，定义 `play_video`、`show_image`、`play_audio` 以及 `qte`、`unlock` 等内置命令名。媒体协议包含 `source` / `clip` / `image` 等基础参数；互动协议当前包含 `qte` 的 `inputActionId` / `durationSeconds` / `requiredCount` / `promptTextKey` 参数和 `unlock` 的 `unlockId` / `puzzleType` / `promptTextKey` 参数，以及 `success` / `fail` outcome。Editor schema、compiler、runtime validation 和播放层共用这些常量。内部保留参数 `__videoSeekPolicy` 只由 compiler 写入编译产物，当前取值 `transition` 表示纯过渡视频可由播放层提供媒体时间 seek。
- `StoryPresenter` / `IStoryFramePresenter` / `IStoryCommandHandle` / `IStoryCommandHandler` / `StoryMediaCommandHandler` / `StoryQteCommandHandler` / `StoryUnlockCommandHandler` — 位于 `GameDeveloperKit.StoryPlayback` assembly 的 Player 侧播放协调层，把 `StoryFrame` 派发给 view 和命令播放器，并在 command handle 完成后通过 `StoryModule` API 推进剧情。默认 QTE handler 只提供最小 overlay：挂到 interaction channel 返回的 `CustomRoot`，用按钮点击或 Space 累计输入次数，达到 `requiredCount` 完成 `success`，duration 到期完成 `fail`，停止或取消只清理 overlay 不推进分支；默认 unlock handler 也只提供最小 overlay：挂到同一个 `CustomRoot`，通过 `IUnlockStateProvider` 读写 `unlockId`，已解锁直接 success，Unlock 写入成功后 success，Fail/Cancel 或写入拒绝后 fail，停止或取消只清理 overlay 不推进分支。
- `StorySoundCommandPlayer` — 位于 `GameDeveloperKit.StoryPlayback` assembly，通过 `SoundModule` 执行 `play_audio`，并把 `SoundHandle` 结束、取消、停止映射回 Story command handle。
- `StoryImageCommandPlayer` — 位于 `GameDeveloperKit.StoryPlayback` assembly，通过 `ResourceModule` 加载 `Texture` / `Sprite` 并输出到 UGUI `RawImage`，用于执行 `show_image`。
- `StoryVideoPathResolver` — 位于 `GameDeveloperKit.StoryPlayback` assembly，按 `streaming_assets`、`persistent_data_path`、`network_stream` 三种来源把 `play_video.source + clip` 解析成 AVPro 可打开的绝对路径或 URL；视频不进入资源包。
- `StoryAvProVideoCommandPlayer` / `StoryAvProVideoPlayback` — 位于 `GameDeveloperKit.StoryPlayback` assembly，使用 AVProVideo 执行 `play_video`；`PlayVideo()` 会先尝试从预热队列按 `source + clipPath` 取得已准备的 `MediaPlayer`，未命中时再即时 `OpenMedia`，并通过 `PlaybackStarted` / `ActivePlaybacks` 暴露 `CurrentTexture` 供 Player UI 绑定。`StoryAvProVideoPlayback` 支持 playback-only `CanSeek` / `DurationSeconds` / `CurrentTimeSeconds` / `Seek(time)`；seek 只在 command 带 `__videoSeekPolicy=transition`、非 loop 且 AVPro duration 可用时开放。
- `StoryAvProVideoPreloadQueue` / `StoryAvProVideoPreloadHandle` / `StoryAvProVideoPreloadStatus` — 位于 `GameDeveloperKit.StoryPlayback` assembly，负责 AVPro 视频队列预热、容量控制、`ReadyToPlay` / `FirstFrameReady` / `Failed` / `Canceled` 状态观察和 Acquire 所有权转移；预热只准备视频播放器，不推进剧情、不显示 Loading UI、不扫描章节图片或音频。
- `IInteractionChannel` / `InteractionRequest` / `PlaybackSurfaceView` / `VideoSeekSurface` / `DefaultInteractionChannel` — 位于 `GameDeveloperKit.StoryPlayback` assembly 的交互通道与 surface 协议；StoryPlayback 通过单一 channel 接收 `OnAwake` / `OnStoryStarted` / `OnChapterChanged` / `OnFrameChanged` / `OnStoryStopped`，并按 `InteractionRequest.Kind` 向章节 UI 询问文本、继续、选项、视频、图片和 custom surface。`PlaybackSurfaceView.VideoSeek` 是可选 surface；缺失时视频照常播放，只是不显示时间条。
- `StoryPlayerView` — 位于 `GameDeveloperKit.StoryPlayback` assembly，是 Player 侧 UGUI 播放组件和默认 fallback host；持有 `StoryPresenter`，注册 AVPro 视频、UGUI 图片、SoundModule 音频命令播放器和默认 QTE / Unlock 命令执行器，并通过 interaction channel 在 `OnAwake` 与 `OnStoryStarted` 之间完成启动预热，再在 `Update()` 中推进等待帧、刷新 AVPro 纹理和刷新 transition 视频时间条。`CreateDefault(parent)` 会创建默认 UGUI 播放根、视频 `RawImage`、图片 `RawImage`、对白、按钮和隐藏的 `VideoSeekSurface`，供测试入口或业务在指定 UI 父节点下生成播放器。遇到 `qte` 或 `unlock` command 时，它会向 channel 请求 `InteractionRequestKind.Custom` 并缓存 `CustomRoot` 供默认 QTE / Unlock overlay 使用；缺失 `CustomRoot` 是配置错误。它还会在 active interaction channel 实现 `IUnlockStateProvider` 时优先复用该 provider，否则回退到 session provider。它只播放传入或已注册 Story，不负责 App startup、Resource ready、Loading UI、章节预加载或 Procedure 切换。
- `StoryTestProcedure` / `StoryTestRequest` / `StoryTestRequestAsset` — 位于 `Assets/GameDeveloperKit/Scripts/StoryTest` 的项目级测试入口，不属于 Runtime framework 必选模块。Procedure 只解析请求、必要时注册 `StoryProgram`、解析或创建 `StoryPlayerView` 并调用 `Play()` / `PlayRegistered()`；显式播放器优先，其次复用场景已有播放器，请求提供 prefab 时实例化到 `UILayer.StoryPlayback`，否则调用 `StoryPlayerView.CreateDefault()` 在该层创建默认播放器。

关键行为：通用节点图库不引用 `GameDeveloperKit.Story`、`StoryEditor`、`NodeKind` 或 `UnityEditor.Experimental.GraphView`；Story 节点/端口合法性和中文错误提示集中在 Story adapter/window/schema 层。默认作者节点只保留 `Start`、`End`、`JumpChapter`、`Parallel`、`Merge`、`Wait`、`Dialogue`、`Narration`、`PlayVideo`、`ShowImage`、`PlayAudio`、`EmitEvent`、`Choice`、`Qte`、`Unlock` 和 `MiniGame`；palette 和创建菜单不显示 Start/End，也不显示多路、随机、条件、标记或辅助节点。Start 无输入且只有 `completed` 输出，End 可作为流程目标但没有输出；`Parallel` 用 `branch`/`branch_*` 输出启动多个轨道，`Merge` 用于等待全部关联轨道完成后继续；`Dialogue` / `Narration.completed` 连普通目标时是单连替换，连多个 `Choice` 时允许多连并表示同一次玩家选择中的多个选项项，两种模式不能混合；`Choice` 只能接在文本或 Merge 完成端口后，并只从 `selected` 端口单连到分支目标或 End；`Qte` 编译为普通 `qte` command，`Unlock` 编译为普通 `unlock` command；`Qte` 首版只允许 `success` / `fail` 两个 outcome，`durationSeconds` 必须大于 0，`requiredCount` 必须至少为 1，两个 outcome 都必须有目标；`Unlock` 首版只允许 `success` / `fail` 两个 outcome，`unlockId`、`puzzleType`、`promptTextKey` 都必须存在，`puzzleType` 只允许 `line_connect` / `node_unlock` / `custom`，两个 outcome 都必须有目标；命令、跳转、等待、合流和小游戏节点只允许 schema 声明的 outcome 端口。`StoryEditorWindow` 写回 edge 时复用同一容量判断，runtime validation 兜底拒绝未知输出端口。Story Editor 图交互已经通过真实 Unity Editor 手测覆盖 N1-N15：打开窗口、选择章节、右键/Space 创建、palette 拖入、节点拖拽、端口连线、空白处创建并连接、鼠标锚点缩放、wire 对齐、pan、Delete/Backspace、Esc、F 聚焦和框选。项目级 StoryTest 入口不初始化 Resource/Config/Data/Sound，不打开 LoadingWindow，不做章节媒体预加载或等待 AVPro 首帧；它只负责把测试用 Story 注册并交给 StoryPlayback 播放。StoryPlayback 的 AVPro 视频预热是播放器内部能力：手动 `PreloadVideoAsync()` 或默认 look-ahead 只提前准备少量后续视频，命中时由 `PlayVideo()` Acquire 复用，失败或取消只影响 preload handle，不自动 fail Story command。视频中途互动的当前闭合路径是 `Parallel + Wait + Choice/Command`：视频分支保持 `play_video(waitForCompletion: true)` command track，交互分支由 `StoryPlayerView.Update()` 按 session time 调用 `Evaluate(deltaTime)` 推进 `Wait(N)`，wait 到点后同一 `StoryFrame` 可同时包含视频 command 与普通 choices 或 custom command；QTE 使用同一路径表达为 `Wait(N) -> qte command`，UI 仍通过 interaction channel 请求 Video / Choice / Custom surface。

Story Editor 节点库当前提供三个 editor-only 互动编排模板：`story.pattern.video_wait_choice`、`story.pattern.video_wait_qte` 和 `story.pattern.video_wait_unlock`。模板创建后图里只留下现有 `Parallel`、`PlayVideo`、`Wait`、`Choice`、`Qte`、`Unlock`、`Narration` 节点和普通边；删除模板入口不会影响旧图资源或 runtime program。视频中途选项模板使用 `Wait.completed -> Choice item A/B -> selected target` 表达“等待 N 秒后出现多个普通选项”，QTE / Unlock 模板使用 `Wait.completed -> qte/unlock command -> success/fail target` 表达互动结果。

选项分支编译契约：`Choice` 在 Story Editor 作者图里表示“一个玩家可选项”，不是独立运行步骤。`Dialogue` / `Narration` / `Merge` / `Wait.completed` 指向多个 Choice item 时，`StoryProgramCompiler` 会按出边顺序合成 `{ownerNodeId}_choices` 运行时 step；每个 `StoryChoice` 的 `ChoiceId` 来自 choice item node id，`TextKey` 来自 `textKey` 参数，`Target` 来自唯一的 `selected` 边，owner->choice 条件与 choice.selected 条件按 AND 合并。`Wait` 拥有 Choice item 时，等待 step 的 target 指向 synthetic choice step；同一 completed 端口混连 Choice item 和普通流程目标会被图上诊断和 compiler 定位为错误。`StoryRunner` 解析到选项时把可用项放入当前 `StoryFrame.Choices`，并通过 `WaitsForChoice` 等待 UI 调用 `Select(choiceId)` 推进。Runtime 只消费 `StoryProgram`，不读取 authoring edge、layout 或 editor graph 类型。

命令字段类型化契约：命令节点字段由 `NodeParameterDefinition` / `NodeSchemaRegistry` 统一声明，`PlayVideo.clip`、`ShowImage.image` 和 `PlayAudio.clip` 使用 `AssetReference` 字段，runtime schema 的资源类型只保存 `video` / `image` / `audio` 等业务别名。`StoryEditorGraphAdapter` 在 Editor 层把这些别名映射为 `ObjectField` 可用的 Unity 类型，`EditorNodeGraphKit` 只消费通用 `EditorGraphFieldValueType.AssetReference` 和 `EditorGraphFieldModel.ResourceType`。`StoryProgramCompiler` 按 schema 导出 command arguments，资源字段编译为 `StoryValue.FromString("Assets/...")`，不保存 `guid:`；`wait` 等控制字段不进入 `StoryCommand.Arguments`。`StoryCommandSchema` 携带 typed argument metadata，`StoryModule.Program.Validation` 在注册 `StoryProgram` 时兜底校验 required argument 和 `String/Number/Boolean/Option/AssetReference` 类型。`Runtime/Story` 不保存 Unity object 实例，不调用 `AssetDatabase` / `ObjectField` / AVProVideo，也不直接引用具体媒体类型；默认播放由 `GameDeveloperKit.StoryPlayback` 承接，业务也可注册自己的 command handler。

图上校验反馈契约：`EditorNodeGraphKit` 只消费业务无关 diagnostics，并在节点、字段、端口和连线上按最高严重级别显示 error/warning 样式与 tooltip；它不解析 `story:/chapter:/node:` source，也不引用 Story 专有类型。Story 侧由 `StoryEditorDiagnostics` 统一执行当前章节轻量 authoring 校验和 compiler report 投影，覆盖必填字段、数字/布尔类型、手填资源 warning、Choice selected 缺目标、文本/Wait completed 混连、未知输出端口、目标节点/章节缺失等问题。编译成功后，Story Editor 会把当前章节 PlayVideo 的 compiler seek 推导结果投影成 Info 诊断：`transition` 表示播放窗口会显示视频时间条，`disabled` 表示当前视频不开放 seek。`StoryEditorWindow` 的左侧问题 summary 与 graph badge 同源，点击可选择对应 node 或 wire；当前章节外 issue 只显示章节定位，不错挂到当前画布。图编辑后，上一轮 compiler diagnostics 会标记为 stale，上一轮编译产物上的 seek Info 诊断会被清空，避免用旧推导误导作者。

示例剧情图契约：canonical 样例入口是 `StorySampleGraphFixture`，StoryId 为 `sample_story_graph`，Version 为 `1.0.0`，四章为 `chapter_arrival`、`chapter_station`、`chapter_alley`、`chapter_final`。样例只使用默认作者节点，覆盖 Dialogue、Narration、多个 Choice item、PlayVideo、ShowImage、PlayAudio、Wait、JumpChapter、MiniGame 和 EmitEvent，不再包含条件、标记或辅助节点。该 fixture 用于 Story Editor 手测、compiler/runtime smoke 和播放窗口验证；资源字段保存 `Assets/...` 路径，不把 Unity object 或 Editor 类型写入 runtime `StoryProgram`。

运行时播放窗口契约：`StoryEditorPlaybackWindow` 是 Editor-only harness，不是 Player 运行时 UI，也不是 Inspector。窗口入口来自 Story Editor 工具栏/黑板或菜单；每次启动会重新编译当前 `StoryAuthoringAsset`，有 compiler error 时只显示中文错误，不创建有效播放会话。编译成功后 `StoryPlaybackSession` 创建新的 `StoryModule`，设置 Editor preview function resolver，并调用 `Start(program, chapterId)`；后续按钮只调用 runtime module 的 `Continue()`、`Select(choiceId)`、`CompleteCommand(commandId, outcomeId)`、`Evaluate(time)`。窗口只读取 `CurrentFrame` 渲染 tracks、choices、命令 outcome、等待控件和播放历史；遇到 `play_video` 命令时由 `StoryEditorAvProPlayback` 创建隐藏 AVPro `MediaPlayer`，按 `StoryVideoPathResolver` 的三来源规则打开视频并显示纹理，`FinishedPlaying` 后用已声明 outcome 或 null 调用 `CompleteCommand`。若编译产物带 `__videoSeekPolicy=transition`，播放窗口在视频预览下方显示 slider 并调用 Editor AVPro playback 的 `Seek(time)`；disabled 视频不显示 slider。视频播放中如果 frame 同时含 choices，选项仍在同一窗口中显示。Player 侧 AVProVideo 只允许落在 `GameDeveloperKit.StoryPlayback` assembly，不写入 `StoryModule`、`StoryProgram` 或 `Runtime/Story`。

### UI（运行时窗口与 UIDocument 绑定）

UI 子系统负责运行时窗口管理、uGUI prefab 绑定和窗口代码生成。运行时入口是 `UIModule`，业务按窗口类型打开、切换、回退和关闭 UI；具体页面逻辑写在继承 `UIWindow` 的窗口类型中，prefab 侧通过 `GameDeveloperKit.UI.UIDocument` 保存安全区、全屏根、组件绑定和本地化文本绑定。

核心类型：
- `UIModule` — 运行时 UI 模块，创建 `GameDeveloperKit.UIRoot`、Canvas 和各 `UILayer` 容器，按 `UIOption` 加载 prefab，维护窗口记录、窗口栈、安全区更新、关闭释放和资源句柄卸载；`GetLayerRoot(UILayer layer)` 暴露已创建层级根节点供非 UIWindow 的运行时视图挂载。
- `UIWindow` — 纯 C# 窗口基类，不继承 `MonoBehaviour`；由 `UIModule` 注入 `UIDocument`、实例 `GameObject` 和 layer，并通过 `OnAwakeAsync()`、`OnOpenAsync()`、`OnEnable()`、`OnDisable()`、`Release()` 承接页面生命周期。
- `UIDocument` — 挂在 UI prefab 上的运行时绑定文档，保存 `FullScreenRoot`、`SafeAreaRoot`、`UIBindMapping[]` 和 `UILocalizedTextBinding[]`，并提供 `GetGameObject()` / `GetComponent<T>()` 读取已声明绑定。
- `UIDocumentInspector` / `BindingTreeView` / `UIDocumentLocalizationDrawer` — Editor-only 绑定维护入口，支持扫描 `b_` 子节点、选择目标组件、维护文本 localization key，并触发生成窗口代码。
- `UIDocumentGenerator` — Editor-only 代码生成器，当前输出契约是窗口 logic partial、design partial 和 nested model partial；合法 prefab 名如 `UI_ExampleWindow` 会保留为窗口类型名，model 文件名使用去分隔符后的 Pascal stem，如 `UIExampleWindow.Model.g.cs`。
- `UIOption` / `UILayer` — 窗口资源路径和层级声明；`UIModule` 打开窗口时读取窗口类型上的 `UIOption`。`UILayer.StoryPlayback` 位于 `Message` 之上，用于 StoryPlayback 默认播放器、视频 `RawImage` 和剧情交互按钮等运行时创建的顶层剧情 UI。

生成代码契约：每个 UI 页面只生成或维护一个业务可见窗口类型。`{Window}.cs` 是用户可编辑 logic partial，只在不存在时首次创建；`{Window}.Design.g.cs` 每次覆盖，负责 `UIOption`、`Bindings` 属性、`InitializeDesignAsync()`、`ReleaseDesign()`、组件绑定初始化以及本地化订阅/刷新；`{FileStem}.Model.g.cs` 每次覆盖，给同一个窗口类型注入内嵌 `Model`，字段通过 `Bindings.btn_close` 等方式访问。生成器不再输出 per-window `Module` helper、`Controller` 骨架或顶层 `ExampleModel`。

关键行为：`UIModule` 只管理窗口生命周期、层级、安全区、资源加载和释放，不消费 `LocalizationModule`，也不理解业务页面字段；只有存在 `UIDocument.LocalizedTexts` 的生成窗口代码会在 design partial 中调用 `App.Localization.GetText()` 并订阅 `LocaleChanged`。`UILayer.StoryPlayback` 只提供挂载层，不让 `UIModule` 直接解释 StoryPlayback 的业务控件或播放状态。删除生成的 `.Design.g.cs` / `.Model.g.cs` 后，手写窗口 logic partial 仍可见，但绑定初始化 helper 和强类型 `Model` 会消失，编译期暴露缺失。

### FileSystem（VFS 虚拟文件系统）

入口：`FileModule`（`Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`），实现 `IGameModule`，通过 `Super.FileSystem` 访问。

核心类型：
- `VfsFileEntry` — 文件元数据（路径、CRC32、版本号、时间戳、存储方式）
- `VfsManifest` — JSON 清单索引，持久化到 `_manifest.json`

存储策略：
- 小文件（< 阈值 4096 字节）：合并写入单一 Bundle 文件（`.vfsb` 自定义二进制格式）
- 大文件（≥ 阈值）：独立文件存储在 VFS 根目录下

关键行为：写入自动 CRC32 校验、版本号调用方指定（string 类型，支持 `"1.0.1"` 语义版本）、软删除标记

### Download（下载模块）

入口：`DownloadModule`（`Assets/GameDeveloperKit/Runtime/Download/DownloadModule.cs`），实现 `IGameModule`，通过 `Super.Download` 访问。

核心类型：
- `DownloadHandler` — 单文件下载控制柄，暴露状态、进度、临时路径、失败类型、暂停/恢复/取消、完成等待和进度/终态回调
- `DownloadListHandler` — 批量下载控制柄，聚合多个 `DownloadHandler`，按顺序执行，单项失败不阻断后续项
- `DownloadStatus` / `DownloadFailureKind` — 下载状态和失败分类
- `DownloadChunk` — 大文件分片下载的内部 Range 分片元数据

存储策略：
- 下载期间只写 `Application.temporaryCachePath + "/downloads"` 下的临时文件
- 单流下载使用 `{文件名}.download` 作为 temp 文件，完成后由业务自行决定是否导入 `FileModule`
- 大文件达到模块阈值且服务端支持 Range 时，使用 `.part` 分片文件下载，全部完成后合并为一个 temp 文件

关键行为：支持单文件下载、批量下载、断点续传、暂停、恢复、取消、失败后恢复和大文件分片下载；同 URL 重复下载复用同一个 handler；取消会清理 temp / `.part`，暂停和失败会保留 temp / `.part` 以便恢复。

### Event（事件模块）

入口：`EventModule`（`Assets/GameDeveloperKit/Runtime/Event/EventModule.cs`），实现 `IGameModule`，通过 `App.Event` 访问。`EventModule` 声明 `[ModuleDependency(typeof(TimerModule))]`，因此第一次访问 `App.Event` 时 App resolver 会先启动 `TimerModule`，再启动 Event；Event 在 `Startup()` 中把队列派发注册到 Timer 的 Update tick。

核心类型：
- `IEventArgs` — 事件数据公共契约，提供 `Use()` / `HasUse()` 用于消费事件并停止后续派发。
- `IEventHandle` / `IEventHandle<TEvent>` — 事件处理器契约，统一接收 `sender` 和事件数据。
- `Subscription` — 一次订阅返回的取消句柄，支持 `Cancel()` / `Release()` 取消订阅。
- `BindingAttribute` — source generator 使用的事件绑定标记，用于自动订阅 handle 和生成事件扩展方法。

派发策略：
- `Subscribe<THandle>(handle)` 根据 handle 实现的 `IEventHandle<TEvent>` 识别事件类型并订阅。
- `Subscribe<TEvent>(Action<TEvent>)` 提供轻量委托订阅。
- `Fire<TEvent>(eventData, sender)` 只把事件参数和 sender 保存到 FIFO 队列；队列由 `TimerModule.OnUpdate()` 注册的 `EventModule.Dispatch` 回调在后续 Update 中派发。若当前没有可注册的 `TimerModule`，`Fire()` 会抛 `GameException`，调用方可改用 `FireNow()`。
- `FireNow<TEvent>(eventData, sender)` 是不安全的立即同步派发入口，用于调用方确认不会触发嵌套即时派发的少数场景。
- 任一 handle 调用 `eventData.Use()` 后，后续 handle 不再收到本轮事件。

关键行为：支持强类型订阅、委托订阅、取消订阅、重复订阅去重、Update 队列派发、事件消费中断和 source generator 自动绑定；单次 Update 只处理进入本轮前已经排队的事件，listener 中再次 `Fire()` 的事件留到下一次 Update，避免嵌套派发改写当前快照；不提供异步事件完成等待、优先级、限流、跨进程/网络事件或编辑器可视化面板。

### Procedure（流程状态机模块）

入口：`ProcedureModule`（`Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs`），实现 `IGameModule`，通过 `App.Procedure` 按需访问，用于维护全局顶层流程状态。`ProcedureModule` 声明 `[ModuleDependency(typeof(TimerModule))]`，因此第一次访问 `App.Procedure` 时 App resolver 会先启动 Timer，再启动 Procedure。Procedure 不再依赖默认启动计划排序；需要资源、配置、数据等异步 ready 的启动流程由业务 Procedure 显式编排。Procedure 持有内部 `ProcedureProfileHandle`，Debug 已注册时会把 `Procedure` profile 软注册进 Profiles tab；Debug 后启动时也会回扫已注册 Procedure。

核心类型：
- `ProcedureModule` — 保存已注册流程字典、当前流程、切换状态、pending change request 和 Timer update handle，提供注册、查询、切换、后续切换请求和 shutdown 释放。
- `ProcedureBase` — 流程基类，提供 `OnInitializeAsync()`、`OnEnterAsync(previous, userData)`、`OnLeaveAsync(next, userData)`、`OnUpdate(deltaTime, unscaledDeltaTime)` 和 `Release()` 生命周期。
- `ProcedureUpdateHandle` — `ProcedureModule` 内部 `UpdateTimerHandle`，注册到 Timer Update，用 `TimerUpdateContext.DeltaTime` / `UnscaledDeltaTime` 推进当前流程。
- `ProcedureProfileHandle` — `ProcedureModule` 内部 `ProfileHandle`，Name 为 `Procedure`，自绘 current type、changing、pending type 和 update handle 状态。

关键行为：`Startup()` 不再创建 `GameDeveloperKit.ProcedureRoot` 或独立 `MonoBehaviour` driver，而是注册 tag 为 `ProcedureModule.Update`、owner 为自身的 Timer `UpdateTimerHandle`；若 DebugModule 已注册，同时注册 `Procedure` profile。`RegisterProcedure()` 初始化并登记实例，缺失流程可由 `ChangeAsync(type, userData)` 通过私有/公开无参构造懒创建；切换期间 `IsChanging == true`，重入切换会抛 `GameException`，当前流程的 `OnUpdate()` 会被跳过。切换顺序为 previous `OnLeaveAsync(next, userData)`、清空 `Current`、next `OnEnterAsync(previous, userData)`、设置 `Current`；切换完成不再通过 `EventModule` 发送 `ProcedureChangedEventArgs`。Timer Update 推进时，Procedure 只在 `Current != null && IsChanging == false` 时调用当前流程 `OnUpdate(deltaTime, unscaledDeltaTime)`；当前 procedure 的 update 异常由 Timer update handle 记录到 `LastException`，不阻断其他 Timer handles。启动 Procedure 需要在 `OnEnterAsync` 中完成 Resource / Config / Data 等异步 ready 后进入业务流程时，不应直接重入 `ChangeAsync()`，而应调用 `RequestChange<TProcedure>(userData)`；该请求只允许在切换期间调用，当前 enter / leave 成功结束后由 `ProcedureModule` 串行消费，且同一轮只保留最后一次请求。`HasPendingChange` / `PendingChangeType` 只暴露当前 pending request 的诊断状态。`Shutdown()` 会先注销 Procedure profile、取消 Procedure update handle，Timer snapshot 不保留已取消的 Procedure handle，再清理当前流程和注册表。

### Resource（资源模块）

当前资源模块已落地为 `ManifestInfo` 清单、资源句柄、`ResourceModule` 门面、`ModeBase` 运行模式、`ProviderBase` bundle provider、play mode package operation 和 provider bundle/loading operation 的组合。业务侧通过 `App.Resource` 按需访问 `ResourceModule`；App resolver 会根据 `ResourceModule` 的 `[ModuleDependency]` 先启动 `OperationModule`、`DownloadModule` 和 `FileModule`。`ResourceModule.Startup()` 只建立同步外壳并清空 setting / manifest / modes；资源 ready 由显式 `InitializeAsync(ResourceSettings)` 完成，释放 ready 状态由 `UninitializeAsync()` 完成。

核心类型：
- `ManifestInfo` — 资源总清单，包含 `Version`、`BuildTime` 和 `Packages`，并实现 `GetBundle()` / `GetDependencies()` 按 `Packages[*].Bundles` 查询 bundle 与依赖。
- `PackageInfo` — 资源组信息，包含 package 的 `Name`、`Version`、`Hash` 和 `Bundles`。
- `BundleInfo` — bundle 元数据，包含 `Name`、`Hash`、`Size`、`Crc`、`Version`、`Assets` 和 `Dependencies`。
- `AssetInfo` — 资源条目，包含 `Location`、`TypeName` 和 `Labels`。
- `ResourceHandle<T>` / `ResourceHandle` — 资源句柄基类，保存 `Info`、`Error`，通过 `Error == null` 判断 `IsValid`。
- `AssetHandle` — Unity `Object` 资源句柄，保存 `Asset` 并提供 `GetAsset<T>()`。
- `RawAssetHandle` — 原始资源句柄，保存 `byte[] Data` 并提供 UTF-8 `GetString()`。
- `SceneAssetHandle` — 场景资源句柄，保存 `Scene Asset`，`SceneName` 来自 `Info.Location`，提供 `Active()`。
- `BundleHandle` — bundle 句柄，保存 `AssetBundle Asset`，释放时会空值保护并卸载 AssetBundle。
- `OperationModule` — operation 的执行 / 等待 / 按 key 终态回写入口，当前可创建 `OperationHandle`、登记运行中 operation、调用 `Execute(args)`、等待完成、通过 `SetResult(key, value)` / `SetException(key, ex)` / `SetCanceled(key)` 完成运行中 operation，并在 `Shutdown()` 时取消清理未完成 operation。
- `ResourceSettings` — `[Serializable]` 普通配置对象，包含 `Mode`、`DefaultPackages`、`ServerUrl`、`ManifestName` 和 `CachePath`，由调用方或 `FrameworkStartupModuleOptions` 显式传给资源模块；不再作为 `ScriptableObject` 或 Resources 默认 asset 自举。
- `ResourceInitializeState` — 显式资源 ready 状态，区分 `NotInitialized`、`Initializing`、`Initialized` 和 `Failed`。
- `ResourceModule` — 资源模块门面，持有 `_manifest`、`_setting`、初始化状态和 `List<ModeBase>`，通过 `App.Resource` 暴露 API；同步 `Startup()` 不读取资源设置、manifest 或初始化默认 package。
- `ModeBase` — 运行模式抽象，持有 `ManifestInfo`，声明 package 生命周期、asset/raw/scene 加载、卸载和释放 API。
- `BuiltinMode` / `StreamingAssetMode` / `BundleMode` / `WebGLMode` / `EditorSimulatorMode` — 当前五种 mode 实现；除 `BuiltinMode` 为单 provider 外，其余 mode 都持有 provider 列表，并各自承载自己的 package lifecycle operation。
- `ProviderBase` — bundle provider 抽象，持有 `BundleInfo Info`，统一执行 asset 查询、缓存命中、批量加载遍历、句柄登记和 pending unload 管理；具体 provider 只实现 asset/raw/scene 的实际加载 operation。
- `BuiltinAssetProvider` / `BundleAssetProvider` / `EditorAssetProvider` / `WebAssetProvider` — 当前 provider 实现，均承载自己的 bundle lifecycle 与 loading nested operation；`EditorAssetProvider` 位于 Runtime Resource 目录，通过 `#if UNITY_EDITOR` 包裹 `UnityEditor.AssetDatabase` 调用；`WebAssetProvider` 由 `WebGLMode` 创建并通过 `UnityWebRequestAssetBundle` 加载远端 bundle。
- `ResourceModule.ManifestOperationHandle`、`*Mode.InitializePackageOperationHandle`、`*Mode.UninitializePackageOperationHandle`、`*Provider.InitializeBundleOperationHandle`、`*Provider.UninitializeBundleOperationHandle`、`*Provider.LoadingAssetOperationHandle` / `LoadingRawAssetOperationHandle` / `LoadingSceneAssetOperationHandle` — 资源模块异步编排入口；BundleMode + BundleAssetProvider、WebGLMode + WebAssetProvider、Builtin / Editor provider loading operation 已存在。

清单关系：
- `ManifestInfo.Packages` 包含多个 `PackageInfo`。
- `PackageInfo.Bundles` 包含多个 `BundleInfo`。
- `BundleInfo.Assets` 包含多个 `AssetInfo`。
- `AssetInfo.Location` 是业务加载地址，也是 provider 查询资源的主键。
- `AssetInfo.TypeName` 和 `AssetInfo.Labels` 也会被 provider 的 `HasAsset(key)` 用于 type / label 查询。

句柄关系：
- `ResourceHandle : ResourceHandle<AssetInfo>`。
- `AssetHandle : ResourceHandle`。
- `RawAssetHandle : ResourceHandle`。
- `SceneAssetHandle : ResourceHandle`。
- `BundleHandle : ResourceHandle<BundleInfo>`。

契约关系：
- `App.Resource` 通过 App resolver 按需创建 `ResourceModule` 外壳，并递归创建其声明依赖；不会隐式调用或等待 `ResourceModule.InitializeAsync()`。
- `ResourceModule.Startup()` 只清空 `_setting` / `_manifest` / `modes` 并形成同步外壳，不读取 `ResourceSettings`，不加载 manifest，不初始化 `BUILTIN` 或默认 package。
- `ResourceModule.InitializeAsync(ResourceSettings settings)` 是资源模块唯一显式 async ready 入口：调用方必须传入 `ResourceSettings`；随后通过 `InitializeOperationHandle` 加载 manifest，创建 `BuiltinMode` 和当前 `ResourceSettings.Mode` 对应 mode，manifest 包含 `BUILTIN` bundle 时初始化 builtin mode，再初始化 `DefaultPackages`。
- `ResourceModule.InitializeAsync(settings)` 已初始化时直接返回，初始化中调用会等待同一次初始化，失败后状态转 `Failed` 并允许再次调用重试。
- `ResourceModule.UninitializeAsync()` 释放所有 mode/provider，清空 `Settings` / `Manifest`，状态回到 `NotInitialized`；`Shutdown()` 同步释放并清空状态。
- `ResourceModule.InitializePackageAsync(package)` 要求资源模块已经完成显式初始化；未初始化状态下会抛 `GameException("ResourceModule is not initialized. Call InitializeAsync first.")`。
- `BundleMode.InitializePackageAsync(package)` 通过 `BundleMode.InitializePackageOperationHandle(package, providers, Manifest)` 解析目标 package 的 bundle 和递归依赖，创建并初始化 `BundleAssetProvider`，成功后注册到 provider 列表，失败时回滚本次已注册 provider。
- `BundleMode.UninitializePackageAsync(package)` 通过 `BundleMode.UninitializePackageOperationHandle(package, providers, Manifest)` 释放并移除目标 package 及其依赖对应 provider。
- `ResourceMode.EditorSimulator` 通过 `EditorSimulatorMode` 创建 `EditorAssetProvider`；Editor 专用 API 留在 Runtime 目录是当前允许的边界，但必须由 `#if UNITY_EDITOR` 保护，非编辑器路径需要明确失败。
- `ResourceMode.Web` 通过 `WebGLMode` 进入 Web 模式；`WebGLMode.InitializePackageOperationHandle` 创建 `WebAssetProvider`。
- 单资源加载通过 `modes.FirstOrDefault(x => x.HasAsset(location))` 找 mode，再由 mode 找 provider。
- `ModeBase` 持有 `ManifestInfo`；除 `BuiltinMode` 外，mode 通过 `List<ProviderBase>` 管理 provider。
- `ProviderBase` 只持有自己的 `BundleInfo`，不持有 `ManifestInfo`。

关键行为：`ResourceModule` 对 `location` / `label` / `name` / `package` 做 null/空白校验；未初始化时资源加载、package 生命周期和 unload API 抛包含 `Call InitializeAsync first` 的 `GameException`，避免同步外壳状态下空引用崩溃；`UnloadAsset(null)` 抛 `ArgumentNullException`，失败或已释放句柄因 `Info == null` 幂等返回。Provider 通过 `AssetInfo.Location`、`TypeName`、`Labels` 查询资源，已加载资源保存在 `_assets`，卸载时转移到 pending unload 列表；`ProviderBase.UnloadUnusedAssetAsync()` 只释放 pending handle，`ResourceModule.UnloadUnusedAssetAsync()` 在所有 mode/provider 释放后统一调用一次 `UnityEngine.Resources.UnloadUnusedAssets()`。`OperationModule` 以 `(operation key, operation type)` 登记运行中 operation；同一 key 的不同 operation type 可并存，同一 key + type 不允许同时运行；只有 key 的外部 `Set*` 回写遇到同 key 多个运行项时抛明确异常，避免随机完成某个 operation。`ResourceMode.Online` 对应的 `BundleMode` + `BundleAssetProvider` 已具备 package -> provider -> bundle -> asset/raw/scene 的 operation 链路：`BundleAssetProvider.InitializeBundleOperationHandle` 以 `BundleInfo.Name` 作为本地路径加载 AssetBundle；`ResourceMode.Web` 对应的 `WebGLMode` + `WebAssetProvider` 以 `ResourceSettings.ServerUrl + BundleInfo.Name` 通过 `UnityWebRequestAssetBundle` 加载远端 AssetBundle。资源模块仍不是全模式端到端闭环：当前仓库 `Assets/StreamingAssets/manifest.json` 仍是旧 JSON 形态，需由资源构建链路同步生成 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo` 当前清单格式；`EditorAssetProvider` 的 Editor API 保护式 Runtime 放置是当前接受的实现边界。

### Combat（战斗 ECS 模块）

入口：`CombatModule`（`Assets/GameDeveloperKit/Runtime/Combat/CombatModule.cs`），实现 `IGameModule`，通过 `App.Combat` 按需访问；`CombatModule` 声明 `[ModuleDependency(typeof(TimerModule))]`，因此第一次访问 `App.Combat` 时 App resolver 会先启动 Timer，再启动 Combat。启动时创建默认 `GameDeveloperKit.Combat.World`，并注册 tag 为 `CombatModule.Update`、owner 为自身的 Timer `FixedUpdateTimerHandle`，由 Timer FixedUpdate phase 推进默认 world。Combat 持有内部 `CombatProfileHandle`，Debug 已注册时会把 `Combat` profile 软注册进 Profiles tab；Debug 后启动时也会回扫已注册 Combat。

核心类型：
- `World` — 战斗世界门面，内部持有 `Massive.MassiveWorld`、`EntityManager` 和 `SystemManager`，暴露 `Create` / `Destroy`、组件增删查、系统加载卸载、`ForEach(Queryable)`、固定步 `Update` / `Step`、`SaveFrame` / `Rollback`、`Clear` / `Dispose`。
- `EntityManager` — 管理 combat `Entity` wrapper 与底层 massive entity 的映射，所有组件 mutation 都经过它触发系统匹配生命周期。
- `SystemManager` — 管理 `SystemBase` 注册、include/exclude 过滤条件、实体进入/离开匹配集合的 `OnCreate` / `OnDestroy`，以及固定步 `OnUpdate`。
- `Entity` — 受管引用句柄，包含 `Id`、`Version`、`IsAlive` 和组件快捷方法；同一个 alive id/version 复用同一个 wrapper。
- `ComponentBase` — Combat 组件基类；公开组件 API 与 `Queryable` 条件只接受其派生类型。
- `Queryable` — include/exclude 查询描述，保存 `Type[]`，空 include 表示无必需组件，空 exclude 表示无排除组件；重复类型去重，include/exclude 冲突抛 `GameException`。
- `SystemBase` — Combat 系统基类，提供 `Query`、`Initialize(World)`、`OnCreate(Entity)`、`OnDestroy(Entity)`、`OnUpdate(Entity)`。
- `CombatProfileHandle` — `CombatModule` 内部 `ProfileHandle`，Name 为 `Combat`，自绘 default world tick/time/frame rate/fixed delta 和 fixed update handle 状态。

匹配与生命周期：
- `World.Create()` 创建实体后会通知当前匹配 `Queryable.All` 或相应条件的系统进入集合。
- `AddComponent` / `RemoveComponent` 在变更前捕获受影响系统的匹配状态，变更后只对 include/exclude 涉及该组件类型的系统做差异通知，避免组件 churn 时全系统扫描。
- `OnDestroy` 表示实体离开系统匹配集合，不只表示实体销毁；组件移除、加入 exclude 组件、rollback 后不再匹配、实体销毁和系统卸载都会触发对应回调。
- `World.Step()` 按系统注册顺序，通过 Massive `Query` 现查当前匹配实体并调用 `OnUpdate`；`World.Update(deltaTime)` 按 `FrameRate` / `FixedDeltaTime` 累积真实时间并推进 0 到 N 个固定步。
- 系统回调期间允许加载/卸载系统；`SystemManager` 使用注册表快照和 active 标记避免回调中修改系统列表破坏当前枚举，新系统从后续 step/通知开始参与。
- `World.Rollback(frames)` 在回滚前捕获当前匹配集合，回滚后重建 entity wrapper 缓存并比较回滚前后差异触发生命周期回调。

关键行为：`World.Dispose()` 后旧 world 引用不再允许继续访问公开 API，旧 entity 的 `IsAlive` 返回 false；继续调用 world/entity mutation 会抛 `GameException`。组件读取缺失组件时由 Combat 层抛 `GameException`，组件类型不继承 `ComponentBase` 时抛 `ArgumentException`。Combat startup 不再创建 `GameDeveloperKit.CombatRoot` 或独立 `CombatRuntimeDriver`；Timer FixedUpdate 推进时调用默认 `World.Update(context.DeltaTime)`，Update / LateUpdate 不驱动 Combat world；直接 `new CombatModule().Startup()` 且 Timer 未注册时会抛 `GameException`。Combat startup 在 DebugModule 已注册时注册 `Combat` profile；shutdown 会先注销 Combat profile、取消 fixed update handle，再 dispose 默认 world。Combat 不实现具体战斗规则、伤害、技能、Buff、AI、寻路、动画、特效、网络同步、GameObject / Transform 自动绑定、程序集扫描式系统自动注册、多 world 全局调度、多线程或 Burst / Job System 安全承诺；底层存储、查询和 rollback 仍由 massive 承担，combat 层只做项目级 API 与生命周期封装。

### Timer（运行时时钟与调度）

入口：`TimerModule`（`Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs`），实现 `IGameModule`，通过 `App.Timer` 按需访问，并作为 Event、Debug、Procedure、Combat 等模块的声明依赖由 App resolver 自动先行启动。Timer 给 Event 队列派发、Debug 日志 tick、Timer tab、Procedure update、Combat world update、业务延时/倒计时/循环调度，以及 runtime update/late-update/fixed-update 回调提供统一时间口径。Timer 持有内部 `TimerProfileHandle`，Debug 已注册时会把 `Timer` profile 软注册进 Profiles tab；Debug 后启动时也会回扫已注册 Timer。

核心类型：
- `TimerModule` — 持有唯一全局 `Tick` / `Time` / `UnscaledTime` / `DeltaTime` / `UnscaledDeltaTime`，统一登记、推进、取消和快照所有 timer handles。
- `TimerModule.Timer` — 内部 `MonoBehaviour` driver，分别通过 Unity `Update()`、`LateUpdate()`、`FixedUpdate()` 调用 `TimerModule.Update(...)`；不改写 Unity 全局 `Time.fixedDeltaTime`。
- `TimerHandle` — timer 句柄基类，只保存 module、owner/tag、取消/完成/暂停状态和 `Advance()` 模板；delay/interval/countdown 业务字段不在基类定义。
- `TimerDelayHandle` / `TimerCountdownHandle` / `TimerIntervalHandle` — 分别承载延时执行一次、倒计时状态回调和按 interval 循环调度；三者当前默认走 Update tick。
- `TimerUpdateHandle` — update 型 timer handle 基类，保存 `Enabled`、`LastTick`、`LastException` 和 `HasError`，并隔离单个回调异常；Late/Fixed 派生 handle 可配置 handle 级 `fps` 门控。
- `UpdateTimerHandle` / `LateUpdateTimerHandle` / `FixedUpdateTimerHandle` — 显式选择 Unity `Update`、`LateUpdate` 或 `FixedUpdate` phase 的运行时回调句柄，可通过 `Register(new XxxTimerHandle(...))` 或 `OnUpdate` / `OnLateUpdate` / `OnFixedUpdate` 注册。
- `TimerUpdateContext` — update handle 回调上下文，公开 tick、time、unscaled time、delta 和 unscaled delta；内部携带 tick kind 用于调度和测试。
- `TimerSnapshot` — Debug 和外部诊断读取的只读快照，包含 tick、time、unscaled time、delta、active delays/countdowns/intervals/updates。
- `TimerProfileHandle` — `TimerModule` 内部 `ProfileHandle`，Name 为 `Timer`，通过 `TimerSnapshot` 自绘 clock、delta、handle 数量和 update handle 摘要。

关键行为：Timer driver 的 Unity `Update()` 推进唯一全局 clock；`LateUpdate()` / `FixedUpdate()` 只作为 phase trigger 检查匹配的 update handles，不额外推进 `TimerModule.Tick` / `Time` / `UnscaledTime`。`Delay()` 到点执行一次并自动完成；`Countdown()` 每个 Update tick 更新 remaining/progress，到 0 触发 complete；`Interval()` 按 interval 循环执行，单帧跨多个 interval 时补触发并保留余量。`OnUpdate()` / `OnLateUpdate()` / `OnFixedUpdate()` 注册的 update handle 只在匹配 phase 被调用，context 使用当前全局 clock；Late/Fixed handle 传入 `fps` 时按 phase 的 unscaled delta 做 handle 级门控，未传时每个匹配 phase 都调用。单个 update handle 抛异常会记录到该 handle，不阻断后续 handle。`useUnscaledTime` 的 timer 使用 unscaled clock，不受 `Time.timeScale` 暂停影响；默认 timer 使用 scaled clock。callback 中允许取消自己或其他 timer，遍历期间取消不会修改当前 dispatch buffer，清理在本轮推进后统一发生。`Startup()` 重复调用为 no-op；DebugModule 已存在时 startup 会注册 `Timer` profile，shutdown 会先注销该 profile，再取消全部 timer 并销毁 driver；重复 shutdown 不抛异常。

### Debug（运行时调试中枢）

入口：`DebugModule`（`Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs`），实现 `IGameModule`，通过 `App.Debug` 按需访问；当前源码命名空间仍为 `GameDeveloperKit.Logger`，但目录和默认入口已经归入 Runtime Debug 子系统。Debug 声明 `[ModuleDependency(typeof(TimerModule))]`，因此第一次访问 `App.Debug` 时 App resolver 会先启动 Timer，再启动 Debug。Debug 对 Command 的读取仍是可选集成，继续通过 `App.TryGetRegistered<T>()` 判断是否存在。Debug startup 注册内建 Memory / Device Info 后，会回扫已注册 Timer、Procedure、Combat 并注册对应 module profile；这些 profile 不让对应模块反向依赖 Debug。

核心类型：
- `DebugModule` — 唯一调试模块入口和 profile lifecycle 门面，负责注册内建/外部 `ProfileHandle`、回扫已注册 runtime module profiles、维护 Unity log capture、Console/Command 薄入口、注册 Timer refresh handle，并把日志/metrics 兼容 API 委托给内建 handles。
- `DebugProfileHandle` — Debug 日志入口，负责接收日志、脱敏、过滤 category/minimum level，并持有 `DebugLogBuffer`；不再作为 Profiles tab 默认内建 profile 注册。
- `MemoryProfileHandle` — Debug 内建内存/metrics profile，startup 时注册到 Profiles tab；负责采样 `DebugMetricSnapshot`，持有固定容量 memory sample buffer，并自行绘制 memory 柱状图和摘要。
- `DeviceInfoProfileHandle` — Debug 内建设备信息 profile，startup 时注册到 Profiles tab；自行绘制平台、Unity 版本、设备型号/类型、CPU、内存、显卡和图形 API 等非敏感设备信息。
- `DebugSettings` — 控制日志容量、metric 采样间隔、Console、Command、Unity log capture、redaction 和 metrics 开关；Console/Command/Unity capture 默认跟随 `UnityEngine.Debug.isDebugBuild`。
- `DebugLogBuffer` / `DebugLogRecord` — O(1) ring buffer 与结构化日志记录，记录 timestamp、sequence、Unity frame、Timer tick、level、category、message、exception、context 和 tags。
- `DebugLogPayload` / `IDebugLogNetworkSender` / `DebugLogNetworkBridge` — Debug log network export 契约，bridge 显式读取已脱敏 `DebugLogRecord` 并把 payload 交给外部 sender；具体网络协议由 sender 实现。
- `ProfileHandle` / `DebugProfileRegistry` — Profiles tab 扩展点；`ProfileHandle` 公开契约只保留 `Name`，绘制由派生类通过内部 draw hook 自行完成。registry 只维护 handle 注册顺序，安全读取 `Name`，并在 GUI 绘制时隔离单个 profile 的 draw 异常。
- `DebugConsole` / `DebugGuiDriver` — 运行时 IMGUI Console 状态和 Unity `OnGUI` 桥接；GUI 绘制细节在 driver 内，Console tabs 包含 Logs、Profiles、Timers、Tools、Settings，并提供 Close 按钮；Console 初始关闭，关闭态只在右上角绘制带 FPS 的重开按钮，IMGUI 按 1920x1080 参考分辨率缩放以适配高分屏。

关键行为：日志写入路径由 `DebugProfileHandle` 校验 level、过滤 enabled/category/minimum level，然后对 category/message/exception/context/tags 做 redaction，再进入内存 buffer。exception/context 在 redaction 前使用安全字符串化，`ToString()` 抛异常时写入 fallback 文本而不让日志 API 反向抛异常；redaction 关闭时保留原 exception/context 对象。Unity 原生日志可接入同一 pipeline，但 Debug 不再提供独立 sink、analytics sink 或 transport 扩展点；实时网络日志导出契约由 Debug/Logger 侧 `DebugLogNetworkBridge` 提供，bridge 显式读取已脱敏 `DebugLogRecord`，转换为 `DebugLogPayload` 后交给外部 `IDebugLogNetworkSender`，DebugModule 不持有 sender/transport，Network 或业务模块只实现 sender。`MemoryProfileHandle` 按 `DebugSettings.MetricSampleInterval` 采样并更新 `DebugMetricSnapshot`，memory 柱状图只保留固定容量 ring buffer；采样由 Debug 内部 `DebugRefreshHandle` 注册到 Timer `UpdateTimerHandle`，使用 `TimerUpdateContext.UnscaledDeltaTime` 推进，Debug disabled 时不继续采样。Profiles tab 遍历 `DebugProfileRegistry` handles，显示 fallback-safe `Name` 并调用派生类自绘；单个 profile 绘制失败只显示该 profile 错误，不影响其他 profile。Debug 默认内建 profile 仍只有 `MemoryProfileHandle` 和 `DeviceInfoProfileHandle`；`Timer`、`Procedure`、`Combat` profile 是已启动 runtime module 的软接入，Debug startup 通过 `RegisterRuntimeModuleProfiles()` 回扫，模块 startup/shutdown 也会在 Debug 已注册时自行注册/注销。Timers tab 通过 `App.TryGetRegistered<TimerModule>()` 读取 `TimerModule.Snapshot()`，展示 tick、time、delta、delay/countdown/interval/update handle 数量和句柄状态，暂不因 `Timer` profile 存在而删除。Tools tab 的命令执行仍走已注册的 `CommandModule`，Debug 不自建独立 GM registry。Debug shutdown / unregister 会取消 refresh handle，Timer snapshot 不保留已取消的 Debug refresh handle。

### Network（网络连接与 HTTP 封装）

入口：`NetworkModule`（`Assets/GameDeveloperKit/Runtime/Network/NetworkModule.cs`），实现 `IGameModule`，通过 `App.Network` 按需访问，用于统一管理 socket channel、消息发送/等待响应、消息分发和 HTTP 请求封装。

核心类型：
- `NetworkModule` — 维护命名 `NetworkChannel` registry，提供 `CreateChannel()` / `TryGetChannel()` / `GetChannel()` / `CloseChannelAsync()` 和 `SendHttpAsync()`。
- `IChannel` / `NetworkChannel` — 长连接 channel 契约与内部实现，负责 connect/close、`SendAsync(request)`、`WaitAsync<TResponse>(request)`、pending response、message listener 和 transport 收包分发。
- `Message` / `MessageHandle<T>` / `MessageSubscription` — 网络消息基类、对象式消息处理器和可取消订阅句柄。
- `INetworkCodec` / `INetworkTransport` — 序列化和底层传输扩展点；默认测试/空实现为 `JsonNetworkCodec` 与 `NullNetworkTransport`。
- `HttpRequest` / `HttpResponse` / `NetworkException` / `NetworkFailureKind` — HTTP 请求封装、响应值对象和网络错误分类。

关键行为：`NetworkModule` channel 通过 name 去重，重复创建抛 `GameException`；channel 连接失败会设置 `Status == Failed` 并记录错误；`SendAsync()` 为 request 分配 sequence 和 pending slot，未连接发送不登记 pending；响应按 `SequenceId` 完成对应 pending，主动推送按具体消息类型和全局 listener 分发；单个 handler 抛异常只记录到 channel，不阻断其他 handler；`CloseAsync()` / `Shutdown()` 会取消 pending、清理订阅并释放 transport。HTTP 只接受绝对 HTTP/HTTPS URL，2xx 返回 `HttpResponse`，非 2xx 或 UnityWebRequest 错误转为 `NetworkException`。需要发送 Debug log payload 时，具体 `IDebugLogNetworkSender` 可按需调用 Network API，但 `NetworkModule` 不持有 bridge/payload 契约。

## 4. 关键架构决定

- **模块生命周期同步化**：`IGameModule` / `GameModuleBase` 生命周期使用同步 `void Startup()` / `void Shutdown()`；App 级启动/注册 API 暂保留 `UniTask` 外壳，但模块自身生命周期只允许同步轻量外壳，异步 ready 必须由显式 API 或 Procedure/bootstrap 编排。

## 5. 已知约束 / 硬边界

- **CRC32 数据完整性**：FileModule.ReadFileAsync 读取后强制 CRC32 校验，不匹配抛 `GameException`
- **幂等写入**：同路径多次 WriteFileAsync 为覆盖更新，Manifest 仅保留最新条目
- **根目录固定**：`Application.persistentDataPath + "/vfs"`，不可配置
- **首版不做并发控制**：所有公开 API 假定在主线程调用
- **模块 Startup/Shutdown 同步轻量**：运行时模块生命周期不得阻塞等待 async；Resource、File、UI、Sound、Network、Procedure 等需要异步 ready/teardown 的能力必须通过业务 API 或后续显式流程承接
- **App 模块按需解析**：`App.GetModule<T>()` / `App.X` 同步属性会按 `[ModuleDependency]` 递归创建模块外壳；`App.Startup()` 不再预加载默认模块，`TryGetValue<T>()` 不再创建未启动裸模块
- **无默认 Runtime Startup 脚本**：框架不再提供自动存在的 `Startup : MonoBehaviour` 场景入口；业务入口可显式挂载 `FrameworkStartup`，或自行调用 `App.Startup()` / `App.Shutdown()` 以及 `App.Procedure.ChangeAsync(...)` 等启动流程。
- **FrameworkStartup 边界**：`FrameworkStartup` 是场景组件，不是 Procedure；目标 Procedure 通过 Inspector 下拉保存 `AssemblyQualifiedName` 并由组件直接 `ChangeAsync` 进入。它只做 App lifecycle、Resource/Sound 显式配置、Config/Data 同步外壳解析和目标 Procedure 切换，不注册剧情、不播放剧情、不打开 LoadingWindow、不做章节预加载。
- **下载临时落盘**：DownloadModule 只写 `Application.temporaryCachePath + "/downloads"`，不主动写入 FileModule
- **下载恢复语义**：暂停和失败保留 temp / `.part`；取消和 CancelAll 删除 temp / `.part`
- **批量下载失败继续**：DownloadListHandler 中单项 Failed 不阻断后续下载
- **事件模块 Update 队列派发**：EventModule 公开 API 假定主线程调用；`Fire<TEvent>` 依赖已注册的 TimerModule，只入队并由 Timer Update 回调派发，直到事件被 `Use()` 消费或 listener 列表结束；listener 中再次 `Fire()` 的事件进入下一次 Update
- **事件模块即时派发边界**：`FireNow<TEvent>` 是不安全的同步派发入口，调用方需自行保证不会产生破坏当前派发快照的嵌套即时派发；EventModule 不提供异步完成等待、优先级、限流或跨线程并发安全承诺
- **Procedure 切换无全局事件**：ProcedureModule 切换完成只更新 `Current`，不再通过 EventModule 发送 `ProcedureChangedEventArgs`；需要切换通知时由调用侧显式扩展
- **Procedure bootstrap 切换请求**：启动 Procedure 可以在 enter / leave 切换期间调用 `RequestChange<TProcedure>()` 请求后续流程；直接在 `OnEnterAsync` / `OnLeaveAsync` 中重入 `ChangeAsync()` 仍会抛 `GameException`。Resource / Config / Data 等异步 ready 必须由启动 Procedure 显式 await 或调用对应 API，不由 App 自动完成。
- **Procedure Timer Update 驱动**：ProcedureModule 通过 `[ModuleDependency(typeof(TimerModule))]` 依赖 Timer，并由 Timer `UpdateTimerHandle` 推进当前流程 `OnUpdate`；Procedure 不再创建独立 Unity update driver 或 `GameDeveloperKit.ProcedureRoot`，也不使用 LateUpdate / FixedUpdate 驱动。
- **OperationModule 不提供调度语义**：OperationModule 只管理运行中 operation 的登记、等待、终态回写和关闭清理，不提供队列、优先级、重试、调度线程或线程安全承诺；公开 API 假定 Unity 主线程调用
- **资源清单当前根类型**：资源模块当前代码使用 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo`，不要在新方案里继续引用未落地的 `ResourceManifest` / `ResourceBundleInfo` / `ResourceAssetInfo`
- **资源 Mode / Provider 当前抽象**：资源模块当前代码使用 `ModeBase` / `ProviderBase`，不要在实现计划里继续把未落地的 `IResourcePlayMode` / `IResourceProvider` 当作现状
- **资源 Provider 不持有 Manifest**：Provider 只负责自己 `BundleInfo` 内的资源操作；跨 package / bundle 查询属于 Mode 或 Manifest 层
- **资源句柄释放语义**：`ResourceHandle<T>.Release()` 清空 `Info` / `Error`；`AssetHandle.Release()` 额外清空 `Asset`；`BundleHandle.Release()` 会 `AssetBundle.Unload(true)`
- **BundleMode / WebGLMode provider operation 已最小闭环**：`OperationModule`、`ManifestInfo.GetBundle()` / `GetDependencies()`、BundleMode package operation 与 BundleAssetProvider bundle/loading operation、WebGLMode 与 WebAssetProvider 接线已补齐；未覆盖 Builtin / StreamingAssets / EditorSimulator 的全部真实加载差异
- **BundleInfo.Name 当前兼任加载定位**：BundleMode 以 `BundleInfo.Name` 作为 AssetBundle 本地路径或 URI；后续如需下载缓存、CRC、远端 URL 策略，应新增显式字段或解析服务
- **EditorSimulator 允许在 Runtime 目录**：`EditorSimulatorMode` / `EditorAssetProvider` 当前位于 Runtime Resource 目录；`UnityEditor` API 调用必须由 `#if UNITY_EDITOR` 包裹，Player 路径不得直接引用 `UnityEditor`，非编辑器路径需要明确抛错；不要求仅因使用受保护的 Editor API 移到 Editor-only asmdef
- **在线资源模式当前命名**：当前没有独立 `HostingPlayMode`；`ResourceMode.Online` 对应 `BundleMode`
- **Resource 显式 ready**：`App.Resource` 只创建同步外壳，不隐式初始化资源；业务 Procedure/bootstrap/FrameworkStartup 必须显式 `await App.Resource.InitializeAsync(resourceSettings)`，未初始化资源 API 抛 `Call InitializeAsync first`
- **UI 生成代码 partial 契约**：`UIDocumentGenerator` 当前只生成 `{Window}.cs`、`{Window}.Design.g.cs` 和 `{FileStem}.Model.g.cs`；用户逻辑文件只首次创建，`.g.cs` 可覆盖。per-window `Module`、`Controller` 和顶层 `Model` 不再是生成结构。
- **UIDocument 数据边界**：`UIDocument` 的 `Mappings` / `LocalizedTexts` 是 Inspector 与生成器共享的 prefab authoring 数据；生成器可以改变源码输出契约，但不得随意改 serialized field 名称或让 `UIModule` 直接解释业务绑定字段。
- **UIModule 运行时边界**：`UIModule` 只负责窗口打开关闭、层级、安全区、资源生命周期和窗口栈；页面业务逻辑、绑定字段访问和本地化刷新落在具体 `UIWindow` partial / generated design partial 中。
- **StoryPlayback UI 挂载边界**：运行时自动创建的 Story 播放 UI 必须挂到 `UILayer.StoryPlayback`；`UIModule` 只提供层级根节点，不接管 StoryPlayerView 的播放、按钮、视频纹理或剧情状态。
- **StoryPlayback AVPro 预热边界**：AVPro 视频预热只落在 `GameDeveloperKit.StoryPlayback`，按 `source + clipPath` 准备和复用 `MediaPlayer`；不做章节资源预加载、Loading UI、Procedure 切换、ResourceModule 视频加载、图片/音频预热或 Story 核心依赖。
- **StoryPlayback 过渡视频 seek 边界**：视频 seek 只落在 `GameDeveloperKit.StoryPlayback` 和 Editor playback 的当前 AVPro 媒体时间；启用条件来自 compiler 写入的内部 `__videoSeekPolicy=transition`，并要求非 loop、duration 可用。不得新增 `StoryRunner.Seek()` / `StoryModule.Seek()`，不得用 slider 改变 `StoryFrame`、choice 历史、command outcome 或章节位置；分支互动视频、并行等待互动视频和缺省 policy 视频默认不可 seek。
- **StoryPlayback 并行等待互动边界**：视频过程中出现选项或命令互动使用现有 `Parallel + Wait + Choice/Command`，`Wait` 使用 StoryPlayback session time 而不是 AVPro current time；wait 到点时视频 command handle 在 frame 仍包含该 command 的情况下继续保留，玩家选择或 command outcome 跳到不含旧视频的新 frame 后再停止旧媒体。不得新增 `TimedChoice`、`EvaluateMediaTime()` 或专用 interaction runtime step。
- **StoryPlayback QTE 边界**：视频中的 QTE 使用 `Wait(N) -> qte command` 表达，`qte` 是普通 blocking command，不是 `StoryStepKind`；默认播放层只提供 `CustomRoot` overlay、按钮/Space 输入、session-time 倒计时和 `success` / `fail` outcome，不暂停媒体、不读取 AVPro current time、不接入 InputModule / Unity Input System action asset，也不让承载 QTE 的视频获得 transition seek policy。
- **StoryPlayback 解锁边界**：视频中的 unlock 使用 `Wait(N) -> unlock command` 表达，`unlock` 是普通 blocking command，不是 `StoryStepKind`；默认播放层只提供 `CustomRoot` overlay 和 `IUnlockStateProvider`，已解锁直接 success，Fail/Cancel 或写入拒绝走 fail；不新增 `IConditionResolver`、不接入 InputModule / Unity Input System，也不让承载 unlock 的视频获得 transition seek policy。
- **Story Editor Excel 导入导出**：位于 `Assets/GameDeveloperKit/Editor/StoryEditor/Excel/` 的 Editor-only 工具，提供 `StoryExcelExporter.Export()` 和 `StoryExcelImporter.Import()` 两个静态入口。导出将 `StoryAuthoringAsset` 的所有 Chapter 序列化为两 sheet 的 .xlsx 文件——ChapterDefine（ChapterId/Title/Description/EntryNodeId/PreviewImage）和 ChapterData（ChapterId/NodeId/Title/NodeKind/Args/Targets）。Targets 列为纯节点 ID 数组 `[node_a, node_b]`，连接语义由目标节点 NodeKind 隐式决定。Args 列为 `key=value;key=value` 格式。导入为全量覆盖模式——全部校验通过后原子替换所有 Chapters。新建节点和章节的 ID 使用 GUID。导入端依赖 Luban 自带的 `ExcelDataReader.dll`，导出端依赖 `EPPlus.dll`（v4.x LGPL），两个 DLL 均放入 `Editor/Plugins/`。菜单入口在 StoryEditorWindow 工具栏（"导出 Excel"/"导入 Excel"）和 Welcome 引导窗口（"从 Excel 导入"），以及 `GameDeveloperKit/剧情编辑/` 下的独立 MenuItem。预览图字段 `PreviewImage` 在 `StoryAuthoringChapter` 上为 Texture2D 引用，编译器提取 `AssetDatabase.GetAssetPath()` 后写入 `StoryChapter.PreviewImagePath`（string）。`StoryChapter.Description` 为纯文本简介，同样经过编译器 → StoryProgramAsset 全链路。

- **Story Editor 互动模板边界**：视频中途 Choice / QTE / Unlock 作者入口只允许作为 editor-only 模板生成现有节点组合，不新增 template-only runtime node、`TimedChoice`、layout slot、anchor、`playbackRole` 或 `seekable` 作者字段。
- **Story Wait-owned Choice 边界**：`Wait.completed` 可以拥有多个 Choice item 并在 compiler 中合成 synthetic choice step；同一端口不得同时连接 Choice item 和普通流程目标，运行时仍只看到普通 `Wait` + `Choice` step。
- **Combat 首版边界**：CombatModule 按需启动；不做具体战斗规则、表现同步、自动系统扫描、多 world 全局调度、多线程安全或网络同步；公开 API 假定 Unity 主线程调用
- **Combat Timer FixedUpdate 驱动**：CombatModule 通过 `[ModuleDependency(typeof(TimerModule))]` 依赖 Timer，并由 Timer `FixedUpdateTimerHandle` 推进默认 `World.Update(context.DeltaTime)`；Combat 不再创建独立 Unity update driver 或 `GameDeveloperKit.CombatRoot`，也不使用 Timer Update / LateUpdate 驱动。
- **Combat lifecycle 封口**：`World.Dispose()` 后旧 world 公开 API 抛 `GameException`，旧 entity `IsAlive == false`
- **Combat 系统重入语义**：系统回调内可加载/卸载系统；当前 step/通知使用注册表快照和 active 标记，新系统不插入当前枚举，已卸载系统不继续更新
- **Combat 组件变更匹配索引**：组件增删只通知 include/exclude 涉及该组件类型的系统；全量匹配只用于 create/destroy/rollback/system add/remove 等需要全局判断的路径
- **Timer Unity tick 口径**：TimerModule 只有一份全局 `Tick` / `Time` / `UnscaledTime` / `DeltaTime` / `UnscaledDeltaTime`，仅 Unity `Update` 推进全局 clock；`LateUpdate` / `FixedUpdate` 只触发对应 update handles，可用 handle 级 `fps` 门控；不改写 Unity 全局 `Time.fixedDeltaTime`
- **Timer 主线程语义**：Timer 公开 API 假定 Unity 主线程调用，不提供后台线程、任务队列、优先级、重试、job system、服务器时间、网络锁步或回滚确定性承诺
- **Debug 运行时边界**：Debug Console、command、Unity log capture 默认随 debug build 开启；DebugModule 不直接持有 sink/transport/analytics 列表，日志由 `DebugProfileHandle` 接收，内存/metrics 由 `MemoryProfileHandle` 采样并自绘柱状图，设备信息由 `DeviceInfoProfileHandle` 自绘；metrics 采样由 Timer Update handle 驱动，`DebugGuiDriver` 只保留 IMGUI `OnGUI` 绘制桥接；日志只进入内存 buffer，不提供本地 rolling file、DebugBundle、离线上传包或 Debug 自有网络 transport；需要实时网络日志时由 Debug/Logger 侧 `DebugLogNetworkBridge` 读取已脱敏记录并显式 flush
- **Runtime module profile 软接入**：Timer、Procedure、Combat 持有各自内部 `ProfileHandle` 并只在 DebugModule 已注册时软注册；DebugModule startup 会回扫已注册 runtime modules。运行模块不声明 Debug 依赖，无 Debug 时照常运行；模块 shutdown / unregister 负责注销自身 profile。
- **Debug log network export 边界**：`DebugLogNetworkBridge` 只做 `DebugLogRecord` 到 `DebugLogPayload` 的字段转换、exception/context 安全字符串化和 sequence 游标；不做二次 redaction、鉴权、连接管理、重试、批量、断线缓存、限流或具体 endpoint。
- **Editor Node Graph 运行时隔离**：`EditorNodeGraphKit` 只位于 Editor 目录并通过 `IEditorNodeGraphAdapter` 接入业务编辑器；Runtime 代码不得引用 `EditorNodeGraph`、`UnityEditor.Experimental.GraphView` 或 UI Toolkit editor graph 类型。
- **Story 端口策略边界**：Story 专有连接语义只允许落在 `StoryEditorGraphAdapter` / `StoryEditorPortPolicy` / `StoryEditorWindow` / `NodeSchemaRegistry` / runtime validation 这一侧；`EditorNodeGraphKit` 只询问 `IEditorNodeGraphAdapter.CanConnect()`，不得引用 `NodeKind` 或中文 Story 业务规则。
- **Story 图诊断边界**：通用 graph diagnostics 只能表达 graph/node/field/port/wire 的 severity、message、tooltip 和 stale；Story source 解析、中文化、当前章节过滤、compiler stale 状态和本地 authoring 校验只允许在 Story adapter/window/helper 层完成。
- **StoryFrame 输出边界**：Story runtime 的可观察输出统一表达为 `StoryFrame`；表现层只能消费 `Tracks` / `Choices` / gate flags，并通过 `Select(choiceId)`、`CompleteCommand(commandId, outcomeId)`、`Evaluate(time)` 或 `Continue()` 推进，不得直接读取 editor graph 边或自行跳转 target。
- **Story 命令资源字段边界**：Story runtime command arguments 只保存 `StoryValue` 基础值；媒体/资源字段可在 Editor 中选择 Unity asset，但编译后只写路径或业务资源 key，不保存 `guid:`。`Runtime/Story` 不得依赖 `AssetDatabase`、UI Toolkit `ObjectField`、Unity object 实例或具体媒体类型；默认播放/加载由 `GameDeveloperKit.StoryPlayback` 承接，业务 command handler 可自定义扩展。AVProVideo 只允许存在于 `GameDeveloperKit.StoryPlayback` 和 Editor playback。
- **StoryTest 入口边界**：`Assets/GameDeveloperKit/Scripts/StoryTest` 是项目级测试 asmdef，只注册/播放剧情，不初始化 Resource/Config/Data/Sound，不打开 LoadingWindow，不做章节媒体预加载，不等待 AVPro 首帧，也不把测试 Procedure 放回 `Runtime/StoryPlayback`。
- **Story Editor 播放窗口边界**：Editor 播放窗口可以使用 `AssetDatabase`、UI Toolkit、EditorWindow 和 AVProVideo 展示 `StoryFrame`，但只能作为 Editor-only harness 调用运行时 `StoryModule` API；Runtime Story 目录不得引用播放窗口、Editor graph、UI Toolkit editor 类型、UnityEditor API 或 AVProVideo。

## 6. 变更日志

- 2026-06-25：同步 Story Editor Excel 导入导出功能，记录两 sheet .xlsx 结构（ChapterDefine + ChapterData）、Args key=value 格式、Targets 纯 ID 数组、EPPlus + ExcelDataReader 依赖、GUID ID 生成、StoryChapter.PreviewImagePath/Description 字段及全链路（Editor → Compiler → StoryProgramAsset）。

- 2026-06-24：同步 Story Editor 互动编排模板现状，记录 `story.pattern.video_wait_choice/qte/unlock` 三个 editor-only 模板、`Wait.completed` 拥有 Choice item 的 synthetic choice 编译契约，以及编译成功后的 PlayVideo seek policy Info 诊断。
- 2026-06-24：同步并行等待互动现状，记录 `Parallel + Wait + Choice/Command` 作为视频中途互动闭合路径、session-time wait 推进、同帧视频/交互输出，以及并行互动视频不获得 transition seek。
- 2026-06-24：同步视频 QTE command 现状，记录 `qte` command 协议、默认 `CustomRoot` overlay、点击/Space 输入、`success` / `fail` outcome、session-time 倒计时，以及不新增 QTE runtime step / media-time trigger / 输入系统接入 / QTE 视频 seek。
- 2026-06-24：同步视频解锁互动现状，记录 `unlock` command 协议、`IUnlockStateProvider`、默认 `CustomRoot` overlay、`success` / `fail` outcome、已解锁幂等 success，以及不新增 `IConditionResolver` / runtime step / media-time trigger / 输入系统接入 / 解锁视频 seek。
- 2026-06-23：同步 StoryPlayback 交互通道与启动预热现状，记录 `IInteractionChannel` / `InteractionRequest` / `PlaybackSurfaceView` / `DefaultInteractionChannel`、`StoryPlayerView` 默认 fallback host 和 `OnAwake` -> prewarm -> `OnStoryStarted` 的启动顺序。
- 2026-06-24：同步纯过渡视频 seek 控制现状，记录 compiler-inferred `__videoSeekPolicy=transition`、`VideoSeekSurface`、StoryPlayback / Editor AVPro playback-only `Seek(time)`，以及不新增剧情级 seek 的边界。
- 2026-06-23：同步 StoryPlayback AVPro 视频预热现状，记录 `StoryAvProVideoPreloadQueue` / `Handle` / `Status`、`PlayVideo()` acquire-first 编排、默认 look-ahead 以及预热不承接 Loading/Procedure/章节资源预加载的边界。
- 2026-06-23：同步 StoryTest 脚本入口与 StoryPlayback 默认 UI 现状，记录 `StoryTestProcedure` / `StoryTestRequestAsset` 位于 Scripts asmdef、缺省播放器挂到 `UILayer.StoryPlayback`，以及 `UIModule.GetLayerRoot()` 只提供层级根节点不接管播放业务。
- 2026-06-22：同步 FrameworkStartup 现状，记录可显式挂载的启动组件、目标 Procedure 下拉、Resource/Sound 内嵌配置、Config/Data 同步外壳解析，以及 Startup 不承接 StoryPlayback/Loading/章节预加载的边界。
- 2026-06-22：同步 Resource/Sound 配置来源变化，记录 `ResourceSettings` / `SoundMixerSettings` 已改为 `[Serializable]` 普通配置对象，Resource 显式 ready 直接接收 `ResourceSettings`，不再通过 Resources asset 自举。
- 2026-06-22：同步 UI codegen partial 现状，记录 `UIDocumentGenerator` 已从四件套收敛为窗口 logic partial、design partial 和 nested model，per-window Module / Controller / 顶层 Model 不再生成，`UIModule` 运行时语义保持窗口生命周期、层级、安全区和资源释放边界。
- 2026-06-22：同步 StoryPlayback 播放包合并现状，记录 `GameDeveloperKit.StoryPlayback` 取代旧 `StoryPresentation` / `StoryPresentation.AVPro`，`Runtime/Story` 只保留命令数据协议，AVProVideo 只进入 StoryPlayback 和 Editor playback。
- 2026-06-21：同步 Story Player 媒体命令接线当时状态，记录 `StoryMediaCommandHandler`、`StorySoundCommandPlayer`、`StoryAvProVideoCommandPlayer`，以及 AVProVideo 当时只进入独立 `StoryPresentation.AVPro` assembly 的边界；该边界已由 2026-06-22 StoryPlayback 合并记录取代。
- 2026-06-21：同步 Story Editor 节点精简现状，记录默认作者节点集、palette/menu 过滤、旧复杂节点清理，以及 canonical sample 不再包含条件/标记/辅助节点。
- 2026-06-21：同步 Story cleanup / AVPro 播放现状，记录旧 Definition/Timeline/editor 兼容路径删除、资源字段编译为 `Assets/...` 路径、播放窗口 `play_video` 只用 AVProVideo，以及 runtime 不引用具体媒体类型。
- 2026-06-20：同步 Story Graph 校验反馈现状，记录通用 `EditorGraphDiagnostic`、Story diagnostics helper、node/field/port/wire 图上 error/warning、summary 点击定位、compiler stale 提示，以及 runtime/editor graph 隔离边界。
- 2026-06-20：同步 Story Editor 运行时播放窗口现状，记录 `StoryPlaybackSession`、`StoryEditorPlaybackWindow`、使用 runtime `StoryModule` 真实推进 Line/Choice/Command/Wait/Completed，以及 Editor-only 资源参数解析边界。
- 2026-06-20：同步示例剧情图 fixture 现状，记录 `StorySampleGraphFixture`、`sample_story_graph` 四章样例、Story Editor 手测入口，以及 compiler/runtime smoke 边界。
- 2026-06-20：同步 Story 命令字段类型化现状，记录 `AssetReference` 字段、资源类型字符串 metadata、Editor graph `ObjectField` 渲染、compiler 稳定 command argument 导出，以及 runtime typed command schema 校验和资源播放边界。
- 2026-06-20：同步 Story 选项项分支契约，记录 Choice item 合成 synthetic `StoryStepKind.Choice`、当前通过 `StoryFrame.Choices -> Select(choiceId)` 运行时推进，以及 runtime 只消费 `StoryProgram` 的边界。
- 2026-06-20：补充 Story Editor / Editor Node Graph 现状，记录项目内 UI Toolkit 节点图库、`StoryEditorGraphAdapter` 边界、N1-N15 真实 Editor 手测通过，以及 runtime 不引用 editor graph 的隔离约束。
- 2026-06-20：同步 Story 端口策略现状，记录 `StoryEditorPortPolicy`、Start/End/Text/Choice/Command/Parallel/Merge 连接规则、单连/多连写回语义，以及 runtime validation 拒绝未知输出端口的兜底。
- 2026-06-19：同步 Debug log network export 契约，记录 `DebugLogPayload`、`IDebugLogNetworkSender`、`DebugLogNetworkBridge` 位于 Debug/Logger 侧，以及 DebugModule 不持有网络 sender/transport、Network 不承载 Debug bridge 类型的边界。
- 2026-06-18：同步 Runtime module profile handles 现状，记录 Timer/Procedure/Combat 内部自绘 ProfileHandle、Debug startup 回扫已注册 runtime modules、模块 startup/shutdown 软注册/注销 profile，以及 Debug 默认 profile 仍保持 Memory 和 Device Info。
- 2026-06-18：同步 Combat Timer consumer 现状，记录 CombatModule 声明 TimerModule 依赖，Timer `FixedUpdateTimerHandle` 通过 FixedUpdate phase 驱动默认 world，并移除独立 `CombatRuntimeDriver` / `CombatRoot`。
- 2026-06-18：同步 Procedure Timer consumer 现状，记录 ProcedureModule 声明 TimerModule 依赖，内部 `ProcedureUpdateHandle` 通过 Timer Update 驱动当前流程 `OnUpdate`，并移除独立 `ProcedureRuntimeDriver` / `ProcedureRoot`。
- 2026-06-18：同步 Debug Timer refresh 现状，记录 DebugModule 声明 TimerModule 依赖，内部 `DebugRefreshHandle` 通过 Timer Update 驱动 `MemoryProfileHandle` 采样，`DebugGuiDriver` 不再实现 Update 采样。
- 2026-06-16：同步模块生命周期契约现状，记录 `IGameModule` / `GameModuleBase` 已改为同步 `Startup()` / `Shutdown()`，App 暂保留 `UniTask` 外壳，Resource `Startup()` 只保留同步外壳并以未初始化 guard 收口资源 API。
- 2026-06-17：同步 App 模块按需 resolver 现状，记录 `App.GetModule<T>()` / `App.X` 按 `[ModuleDependency]` 递归启动依赖，移除 App 默认预加载列表，`TryGetValue<T>()` 不再创建未启动裸模块。
- 2026-06-17：同步 Resource 显式 ready 现状，记录 `InitializeAsync(options)` / `UninitializeAsync()` 状态机、未初始化错误语义和 `App.Resource` 不隐式初始化资源的边界。
- 2026-06-17：同步 Procedure bootstrap flow 现状，记录 `RequestChange<TProcedure>()` 作为切换期间请求后续流程的安全入口，启动 Procedure 显式完成 Resource ready 后再进入业务流程。
- 2026-06-17：同步 Startup removal 现状，记录 Runtime `Startup.cs` MonoBehaviour 已删除，框架不再提供场景默认 bootstrap。
- 2026-06-09：同步 Timer 全局 clock 修正现状：TimerModule 不再维护 Update/LateUpdate/FixedUpdate 三套 clock，Late/Fixed 只做 phase trigger，并支持 handle 级 fps 门控。
- 2026-06-09：同步 Event 与 Procedure audit 修复现状：`Fire<TEvent>` 改为 Timer Update 队列派发，新增不安全即时 `FireNow<TEvent>`，Procedure 切换不再发送全局 `ProcedureChangedEventArgs`，默认启动顺序改为 Timer 先于 Event。
- 2026-06-09：同步 MemoryProfileHandle 绘制现状：memory 采样仍为固定容量 ring buffer，Profiles tab 中改为柱状图，不再使用旋转矩阵绘制曲线。
- 2026-06-09：同步 Debug Console 关闭态交互和高分屏 IMGUI 适配现状：Console startup 后默认关闭，关闭态显示右上角 FPS 重开按钮，driver 使用参考分辨率矩阵缩放并缓存基础 GUIStyle。
- 2026-06-09：同步 Debug ProfileHandle table-first 设计修正后的现状，记录 `ProfileHandle` 只公开 `Name`、profile 派生类自绘、默认 Profiles tab 收敛为 Memory 曲线和 Device Info。
- 2026-06-09：同步 Debug 旧输出扩展点移除后的现状，记录 DebugProfileHandle 只负责日志接收、脱敏、过滤和内存 buffer，实时网络日志由后续显式 bridge/sender 契约承接。
- 2026-06-08：同步 Debug profile-centric 现状，记录 DebugModule 只管理 profile/lifecycle/薄门面，DebugProfileHandle 接收日志，MemoryProfileHandle 承载 metrics snapshot，GUI 绘制迁入 DebugGuiDriver。
- 2026-06-08：同步 Debug ProfileHandle 硬化现状，记录 profile metadata/snapshot 异常隔离、redaction 安全字符串化和内建 Runtime/Debug 状态 profile。
- 2026-06-08：同步 Timer 显式 update handle 现状，记录 Update/LateUpdate/FixedUpdate 三类 tick、TimerHandle 基类瘦身、UpdateTimerHandle/LateUpdateTimerHandle/FixedUpdateTimerHandle 和 TimerSnapshot.Updates。
- 2026-06-07：补充 Timer 与 Debug 子系统现状，记录 Timer 自有 fixed tick、Delay/Countdown/Interval、scaled/unscaled 语义、Debug log/profile/console/timers tab 和默认启动顺序。
- 2026-06-07：补充 Combat 子系统现状，记录默认 world、Entity/Component/System/Queryable 生命周期、固定步、rollback、dispose 封口、系统回调重入和组件变更匹配索引。
- 2026-05-27：同步 Resource 模块现状，更新 provider 命名、Web 模式 provider 接线状态，以及 Editor API 在 Runtime 中由 `#if UNITY_EDITOR` 保护且可接受的边界。
- 2026-05-27：同步 Resource 审计修复后的现状，记录 settings/manifest 自举、WebAssetProvider 接线、ProviderBase 加载编排上移、卸载调度集中到 ResourceModule，以及当前 StreamingAssets 清单仍需生成侧同步为 `ManifestInfo` 格式。
