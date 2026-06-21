---
doc_type: feature-apply-notes
status: implemented
scope: story-player-media-command-handlers
date: 2026-06-21
---

# Story Player Media Command Handlers Apply Notes

## 背景

F04 已补 `StoryPresenter` 和 `IStoryCommandHandler` 接线，但 Player 侧还缺少可直接注册的媒体命令执行层。此次补齐 `play_video`、`play_audio`、`show_image` 的分发契约，并提供 AVProVideo 与 SoundModule 的可用适配。

## 改动

- `Assets/GameDeveloperKit/Runtime/Story/Runtime/IStoryCommandHandler.cs`
  - 新增 `StoryMediaCommandNames`、`StoryMediaCommandUtility`。
  - 新增 `IStoryVideoCommandPlayer`、`IStoryImageCommandPlayer`、`IStoryAudioCommandPlayer`。
  - 新增 `StoryMediaCommandHandler`，把 Story 命令转发给宿主播放器，并校验必填资源路径与返回 handle。
- `Assets/GameDeveloperKit/Runtime/StoryPresentation/`
  - 新增 `GameDeveloperKit.StoryPresentation.asmdef`。
  - 新增 `StorySoundCommandPlayer`，用 `SoundModule` 执行 `play_audio`。
- `Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/`
  - 新增 `GameDeveloperKit.StoryPresentation.AVPro.asmdef`。
  - 新增 `StoryAvProVideoCommandPlayer`，用 AVProVideo 执行 `play_video`，并暴露 `PlaybackStarted` / `ActivePlaybacks` 供 UI 绑定 `MediaPlayer` 或 `CurrentTexture`。
- `Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs`
  - 新增媒体命令 handler 的分发、参数转发和缺参错误测试。

## 边界

- `Runtime/Story` 仍不直接引用 AVProVideo、Unity `VideoPlayer`、`SoundModule` 或 UI 组件。
- `show_image` 只定义 Player 接口，具体图片加载和 UI 绑定由宿主实现 `IStoryImageCommandPlayer`；不在 Story runtime 内假设 `Assets/...` 可直接同步加载。
- 视频播放只提供 AVProVideo 适配，不兼容 Unity `VideoPlayer`。

## 验证

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过。
- Unity batchmode：项目已被当前 Editor 实例占用，无法打开同一项目。
- 临时 csproj 编译 `StorySoundCommandPlayer.cs` 与 `StoryAvProVideoCommandPlayer.cs`：通过。
