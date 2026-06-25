# story-test-scripts-entry 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-23
> 关联方案 doc：.codestable/features/2026-06-22-story-test-scripts-entry/story-test-scripts-entry-design.md

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `GameDeveloperKit.Scripts.StoryTest` asmdef：引用 `GameDeveloperKit.Runtime`、`GameDeveloperKit.StoryPlayback`、`UniTask`，未引用 Editor 程序集。
- [x] `StoryTestRequest`：普通 C# 请求对象，已包含 `Program`、`StoryId`、`ChapterId`、`PlayerView`、`PlayerViewPrefab`；构造时缺少 Program/storyId 会抛 `ArgumentException`。
- [x] `StoryTestRequestAsset`：`ScriptableObject` Inspector 桥接，序列化 `StoryProgramAsset`、story/chapter、PlayerView 和 PlayerViewPrefab，并通过 `ToRequest()` 转为运行时 request。
- [x] `StoryTestProcedure`：继承 `ProcedureBase`，实现 `OnEnterAsync`、`OnLeaveAsync`、`OnUpdate`，可由 `FrameworkStartup` 或代码侧 `App.Procedure.ChangeAsync` 进入。

**名词层变化核对**

- [x] Scripts StoryTest 独立程序集已落地在 `Assets/GameDeveloperKit/Scripts/StoryTest`，测试入口未放回 `Runtime/StoryPlayback`。
- [x] `StoryPlayerView.CreateDefault(parent)` 已落地，默认创建视频 `RawImage`、图片 `RawImage`、对白文本、继续按钮和选项模板。
- [x] `UILayer.StoryPlayback` 与 `UIModule.GetLayerRoot()` 已落地，用作运行时默认播放器挂载点。

**流程图核对**

- [x] `FrameworkStartup -> StoryTestProcedure -> ResolveRequest -> ResolvePlayerView -> Play/PlayRegistered` 均有代码落点。
- [x] 缺省播放器路径已按新需求修正为自动创建，不再把“缺播放器”作为错误路径。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] 独立剧情测试程序集存在，可通过 Procedure 进入并播放 Story。
- [x] 代码侧 request 和 Inspector asset 两种入口都存在。
- [x] Procedure 只注册/播放剧情，不初始化 Resource/Config/Data/Sound，不打开 LoadingWindow，不做章节预加载或等待首帧。
- [x] 未传播放器时自动创建默认 `StoryPlayerView`，并挂到 `UILayer.StoryPlayback`。

**明确不做逐项核对**

- [x] `Assets/GameDeveloperKit/Scripts/StoryTest` 未引用 `UnityEditor`、`AssetDatabase`、`StoryProgramCompiler` 或 `StorySampleGraphFixture`。
- [x] `Assets/GameDeveloperKit/Scripts/StoryTest` 未调用 `App.Startup()`、`InitializeAsync()`、`ConfigureMixer()`、`LoadingWindow` 或 `FirstFrameReady`。
- [x] 旧 `StoryRuntimeDemoBootstrap`、`StoryProcedure`、`StoryProcedureRequest` 未恢复。
- [x] 缺省播放器由运行时代码直接创建，不从资源包自动加载 `StoryPlayerView.prefab`。

**关键决策落地**

- [x] 请求两层结构落地：代码对象 `StoryTestRequest` + Inspector asset `StoryTestRequestAsset`。
- [x] 播放器解析顺序落地：显式 PlayerView -> 场景已有 PlayerView -> 显式 prefab -> `CreateDefault()`。
- [x] Procedure 薄编排落地：只解析请求、注册程序、调用 `Play()` / `PlayRegistered()` 和离开时停止/清理。
- [x] 模块 ready 仍归 Startup/业务；StoryTest 不做 Resource/Sound ready。

**挂载点反向核对**

- [x] 挂载点清单覆盖 asmdef、Procedure、Request、RequestAsset、runtime tests。
- [x] 反向 grep 确认 StoryTest 类型只在 Scripts/tests/spec 中出现，未进入 Runtime/StoryPlayback。
- [x] 拔除沙盘：删除 `Assets/GameDeveloperKit/Scripts/StoryTest` 与相关 tests 后，Runtime/StoryPlayback 仍是独立播放层；失去的是项目级剧情测试入口。

## 3. 验收场景核对

- [x] **N1**：编译解析 Scripts StoryTest。
  - 证据来源：`dotnet build GameDeveloperKit.Scripts.StoryTest.csproj --no-restore` 通过。
- [x] **N2**：`FrameworkStartup + StoryTestRequestAsset` 可进入 `StoryTestProcedure` 并播放。
  - 证据来源：`StoryTestProcedureTests.StartupAsync_WithStoryTestRequestAsset_EntersProcedureAndPlaysStory` 覆盖。
- [x] **N3**：`StoryTestRequestAsset.ProgramAsset` 转换并注册进 `App.Story`。
  - 证据来源：同上测试断言 `App.Story.HasProgram(...)`。
- [x] **N4**：代码 request 播放指定 Program/Chapter。
  - 证据来源：`ChangeAsync_WithStoryTestRequest_RegistersProgramAndPlaysChapter`。
- [x] **N5**：只传已注册 StoryId 时调用 registered playback。
  - 证据来源：`ChangeAsync_WithRegisteredStoryId_PlaysRegisteredStory`。
- [x] **N6**：场景已有 PlayerView 时复用，不实例化 prefab。
  - 证据来源：代码路径优先返回 `request.PlayerView` 或 scene find，现有 request 测试覆盖显式 view。
- [x] **N7**：缺省播放器自动创建到 `UILayer.StoryPlayback`。
  - 证据来源：`ChangeAsync_WhenPlayerViewMissing_CreatesDefaultPlayerViewInStoryPlaybackLayer` 断言父节点和 `MediaLayer/VideoOutput`。
- [x] **N8**：无效 userData 抛明确异常。
  - 证据来源：`ChangeAsync_WhenUserDataIsInvalid_Throws`。
- [x] **N9**：离开 Procedure 时显式播放器只停止，Procedure 创建的播放器会销毁。
  - 证据来源：`ChangeAsync_WhenLeavingStoryTestProcedure_StopsPlaybackWithoutDestroyingPlayer` 与 prefab/default 创建路径测试。
- [x] **B1-B5**：范围守护与编译验证通过。
  - 证据来源：grep 核对与 dotnet build 见第 9 节。

## 4. 术语一致性

- `StoryTestProcedure`、`StoryTestRequest`、`StoryTestRequestAsset` 术语与 design / roadmap / 代码一致。
- `StoryPlayback`、`StoryPlayerView`、`UILayer.StoryPlayback` 术语已同步到 roadmap 和 architecture。
- 防冲突 grep 未发现旧 `StoryProcedure` / `StoryProcedureRequest` 被恢复到新入口中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已补充 `StoryTestProcedure` / `StoryTestRequest` / `StoryTestRequestAsset` 的项目级测试入口现状。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已补充 `StoryPlayerView.CreateDefault(parent)`、`UILayer.StoryPlayback` 和 `UIModule.GetLayerRoot()` 的层级挂载边界。
- [x] 已知约束补充 StoryTest 不做 Resource/Config/Data/Sound ready、Loading、章节预加载或首帧等待。

## 6. requirement 回写

- [x] `.codestable/requirements/story-module.md` 已追加 `2026-06-22-story-test-scripts-entry` 到 `implemented_by`。
- [x] 实现进展已追加 2026-06-23 记录：项目级 StoryTest 入口已落地，缺省播放器挂到 `UILayer.StoryPlayback`。
- [x] requirement 仍保持 `draft`，因为视频预热仍是 roadmap 后续能力。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-items.yaml` 中 `story-test-scripts-entry` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-roadmap.md` 子 feature 清单已同步为 `done`，并记录对应 feature。
- [x] roadmap 变更日志已追加 2026-06-23 条目。

## 8. attention.md 候选盘点

- [x] 无候选。本 feature 未暴露需要每次启动 CodeStable 都知道的新环境或命令陷阱。

## 9. 遗留

- 后续优化点：`story-playback-video-prewarm` 仍待实现，用于减少视频切换空白期。
- 已知限制：`StoryPlayerView` 仍直接显示 `TextKey`，未接入 LocalizationModule；这属于正式播放器 UI 后续设计范围。
- 验证命令：
  - `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`
  - `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore`
  - `dotnet build GameDeveloperKit.Scripts.StoryTest.csproj --no-restore`
  - `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`
