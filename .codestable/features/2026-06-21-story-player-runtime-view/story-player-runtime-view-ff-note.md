---
doc_type: feature-ff-note
status: implemented
date: 2026-06-21
scope: story-player-runtime-view
---

# Story Player Runtime View FF Note

## 背景

F04 已补 `StoryPresenter` 与媒体命令 handler，但 Player 侧还缺一个可直接挂到场景中的播放视图。此次补齐 UGUI + AVProVideo 的最小播放闭环：文本、选项、等待、图片、音频、视频都从 `StoryFrame` / `StoryPresenter` 驱动。

## 改动

- 新增 `Assets/GameDeveloperKit/Runtime/StoryPresentation/StoryImageCommandPlayer.cs`
  - 使用 `ResourceModule.LoadAssetAsync(path)` 加载 `Texture` 或 `Sprite`。
  - 输出到 UGUI `RawImage`。
  - 停止/取消时可清空图片并释放资源句柄。
- 新增 `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryPlayerView.cs`
  - `MonoBehaviour` 播放器视图，作为 `IStoryFramePresenter` 渲染当前帧。
  - 注册 `StoryMediaCommandHandler`、`StoryAvProVideoCommandPlayer`、`StoryImageCommandPlayer`、`StorySoundCommandPlayer`。
  - 支持 `Play(program, chapterId)` 和 `PlayRegistered(storyId, chapterId)`。
  - 渲染 `TMP_Text` 文本、选项按钮、继续按钮、错误文本，并在 `Update()` 推进等待帧。
  - 从 `StoryAvProVideoCommandPlayer.ActivePlaybacks` 绑定 AVPro 当前纹理到 `RawImage`。
- 新增 `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryProcedure.cs`
  - `ProcedureBase` 流程，进入流程时播放剧情，离开流程时停止播放。
  - 支持 `StoryProcedureRequest`、storyId 字符串或 `StoryProgram` 作为 `ChangeAsync<StoryProcedure>(userData)` 参数。
  - 可使用场景中已有 `StoryPlayerView`，也可通过请求传入播放器预制体并在离开流程时自动销毁实例。
- 新增 `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryProcedureRequest.cs`
  - 封装 `StoryId`、`Program`、`ChapterId`、`PlayerView`、`PlayerPrefab`、`Parent`、`DestroyInstantiatedPlayerOnLeave`。
- 更新 asmdef：
  - `GameDeveloperKit.StoryPresentation` 增加 `UnityEngine.UI` 引用。
  - `GameDeveloperKit.StoryPresentation.AVPro` 增加 `UniTask`、`UnityEngine.UI`、`Unity.TextMeshPro` 引用。
- 更新 `StoryPresenter`
  - 进入新帧时，停止已经不在新帧中的阻塞命令，避免选择选项后旧视频/音频继续影响流程。
  - 同一并行合成帧中仍存在的阻塞命令不会被误停。
- 更新运行时测试
  - 新增 `StoryPresenter_WhenParallelChoiceSelected_StopsSiblingCommandHandles` 覆盖“视频分支 + 对白选项”场景。
- 新增 `Assets/GameDeveloperKit/Editor/StoryEditor/StoryPlayerViewPrefabBuilder.cs`
  - 提供菜单 `GameDeveloperKit/剧情编辑器/生成运行时播放器预制体`。
  - 自动搭建 UGUI 播放器层级：Canvas、媒体层、图片/视频 `RawImage`、对白面板、说话人/正文/错误 `TMP_Text`、继续按钮、选项根节点与选项按钮模板。
  - 通过 `SerializedObject` 绑定 `StoryPlayerView` 的私有序列化字段。
  - 生成器内部用反射创建 TextMeshPro 组件，避免 Editor 程序集在 Unity `.csproj` 未刷新时直接因 `TMPro` 引用缺失而编译失败。
  - 目标预制体路径为 `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryPlayerView.prefab`。
- 新增 `Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryEditorPlayModeStartup.cs`
  - Editor Play Mode 进入后加载 `Resources/NewStoryAuthoring`。
  - 使用 Editor-only `StoryProgramCompiler` 编译 authoring 资产，等待 `App.Startup()` 后通过 `App.Procedure.ChangeAsync<StoryProcedure>()` 播放。
  - 优先复用场景中的 `StoryPlayerView`；没有则加载 `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryPlayerView.prefab` 实例化。
  - 启动器不直接引用 `Cysharp.Threading.Tasks`，避免 Editor 程序集因 Unity `.csproj` / asmdef 引用刷新不同步报 `UniTaskVoid` 缺失。

## 验证

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过。
- 临时 `StoryPresentationVerification.csproj` 编译新增 `StoryImageCommandPlayer`、`StoryPlayerView`、AVPro 播放器：通过；仅有 Unity 序列化字段未赋值警告。
- 临时 `StoryPlayerViewPrefabBuilderVerification.csproj` 编译 `StoryPlayerViewPrefabBuilder.cs`：通过，0 警告 0 错误。
- 临时 `StoryProcedureVerification.csproj` 编译 `StoryProcedure.cs`、`StoryProcedureRequest.cs`：通过，0 警告 0 错误。
- 临时 `StoryEditorPlayModeStartupVerification.csproj` 编译 `StoryEditorPlayModeStartup.cs`：通过，0 错误；存在临时验证项目手工引用导致的 `System.IO.Compression` 版本警告。
- Unity batchmode 执行 `StoryPlayerViewPrefabBuilder.BuildPrefab`：未能生成，原因是当前 Unity Editor 已打开同一项目，batchmode 被项目锁阻止。需要在已打开的 Unity 中通过菜单生成。

## 限制

- Unity 生成的 `GameDeveloperKit.StoryPresentation*.csproj` 当前未刷新新增 `.cs` 文件；Unity Editor 刷新后会重新生成。
- `StoryPlayerView` 目前直接显示 `TextKey`，没有接入 LocalizationModule 文本解析；后续应补一个可注入的文本解析器。
- `StoryPlayerView.prefab` 尚未落盘；本次只提交生成器。预制体需在 Unity Editor 中执行 `GameDeveloperKit/剧情编辑器/生成运行时播放器预制体` 生成。
