---
doc_type: manual-record
feature: 2026-06-21-story-cleanup-avpro-playback
status: pending-user
created: 2026-06-21
---

# Story Cleanup AVPro Playback Manual Record

## 自动证据

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过，0 warning / 0 error，包含 `AVProVideo.Runtime` 引用编译。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- `Runtime/Story` 下 `Definition`、`Execution`、`Events`、`Integration` 旧目录不存在。
- grep 旧 Story API / 旧编辑器路径无命中：`Register(Definition)`、`Timeline`、`ActionRequest`、`InteractionRequest`、`StoryGraphView`、`StoryEditorWindow`、`StoryDefinitionMapper`、`StoryDefinitionExporter`、`StoryCsvExchange`。
- grep `VideoPlayer` 无命中，新代码不引用 Unity 内置 VideoPlayer。
- grep Runtime Story 隔离项无命中：`AVProVideo`、`RenderHeads`、`UnityEditor`、`AssetDatabase`、`ObjectField`、`VideoClip`、`StoryEditorPlaybackWindow`、`EditorNodeGraph`。

## 代码证据

- `StoryEditorAvProPlayback` 创建隐藏 `MediaPlayer`，使用 `OpenMedia(MediaPathType.AbsolutePathOrURL, resolvedPath, true)` 打开 `Assets/...`、绝对路径或 URL。
- `StoryEditorAvProPlayback` 在 `FinishedPlaying` 事件中设置完成状态，在 `Error` 事件中记录中文错误，不直接推进 runtime。
- `StoryEditorPlaybackWindow.OnEditorUpdate()` 只在当前 frame 仍存在对应 `play_video` command 且 `WaitsForCommand` 时，按 declared outcome 调用 `CompleteCommand`。
- `StoryEditorPlaybackWindow` 在 AVPro 错误时显示错误文本；`CanShowCommandCompletion()` 允许用户手动完成命令用于继续排查流程。
- `OnDisable()` / session shutdown 会停止播放、移除 AVPro event listener 并销毁隐藏 GameObject。

## 手测步骤

| 编号 | 操作 | 期望 | 结果 |
|---|---|---|---|
| M1 | 打开 `GameDeveloperKit/剧情编辑器/打开播放窗口`，播放 `sample_story_graph / chapter_arrival` | 窗口显示当前 StoryFrame，含 `play_video` 命令、视频预览区和选项按钮 | pending |
| M2 | 等待 AVPro 输出第一帧 | 视频预览区显示 AVPro 纹理，状态从“正在打开视频”变为“正在播放” | pending |
| M3 | 视频播放期间点击同帧选项 | 选项可立即点击，剧情按 `Select(choiceId)` 推进，不依赖视频结束 | pending |
| M4 | 播放等待完成的视频命令直到结束 | AVPro 完播后窗口调用 `CompleteCommand(commandId, outcomeId)` 并进入下一 frame | pending |
| M5 | 把视频路径改为不存在的 `Assets/...mp4` 后打开播放窗口 | 窗口显示 AVPro 错误，不自动完成命令；用户仍可手动完成命令继续排查 | pending |
| M6 | 关闭或重启播放窗口 | AVPro 播放停止，隐藏播放器对象释放，无持续播放残留 | pending |

## 备注

自动验证已覆盖编译、旧路径清理、资源路径数据边界、runtime 隔离和 AVPro 接线代码。真实视频画面、完播事件和关闭释放仍需要在当前 Unity Editor 内手测确认。
