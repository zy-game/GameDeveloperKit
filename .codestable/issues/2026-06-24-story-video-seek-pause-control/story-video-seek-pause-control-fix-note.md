---
doc_type: issue-fix
issue: 2026-06-24-story-video-seek-pause-control
path: fast-track
fix_date: 2026-06-24
tags: [story, playback, seek, pause]
---

# 可 seek 视频缺少暂停控制修复记录

## 1. 问题描述

当前过渡视频已经可以通过 `__videoSeekPolicy=transition` 显示 seek 时间条，但默认播放 surface 只提供 slider 和时间文本，没有暂停 / 继续入口。

## 2. 根因

`VideoSeekSurface` 只抽象了 `Root`、`Slider` 和 `TimeText`，`StoryAvProVideoPlayback` 也没有面向 seek 控件的暂停 / 恢复 API。Prefab 生成器和当前 `StoryPlayerView.prefab` 因此无法生成暂停按钮。

补充发现：控制条显示也直接依赖 `playback.CanSeek`。这个属性要求 AVPro duration 已经有效；当视频已经是 transition seek 视频但 duration 暂时还没稳定时，整个时间条会一直处于隐藏路径，用户看起来就是 prefab 有控件但运行时不显示。

## 3. 修复方案

把暂停按钮作为 `VideoSeekSurface` 的可选组成部分，只在 transition seek 视频上由 `VideoSeekBinder` 绑定。底层 `StoryAvProVideoPlayback` 提供 `CanShowSeekControls`、`CanSeek`、`CanPause`、`Pause()`、`Resume()` 和 `IsPaused`。

显示控制条和允许拖动分开处理：只要视频带 transition seek policy 就显示控制条；duration 未就绪时 slider 禁用并显示 `--:--`，duration 有效后 slider 自动变为可拖动。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/StoryPlayback/PlaybackSurfaceView.cs`
- `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryAvProVideoPlayback.cs`
- `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.cs`
- `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.prefab`
- `Assets/GameDeveloperKit/Editor/StoryEditor/StoryPlayerViewPrefabBuilder.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/StoryPlaybackInteractionChannelTests.cs`
- `Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过，0 warning / 0 error。
- 文本检查确认 `StoryPlayerView.prefab` 已包含 `m_VideoSeekPauseButton`、`PauseButton` GameObject 和按钮文字。
- 未运行 Unity Test Runner；当前环境未确认可用 Unity batchmode。

## 6. 遗留事项

- 手工修改了 prefab YAML；建议下次在 Unity Editor 内执行一次“GameDeveloperKit/剧情编辑/生成运行时播放器预制体”并检查 prefab diff 是否稳定。
