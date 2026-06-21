---
doc_type: issue-fix
issue: 2026-06-21-story-player-presentation-bridge
path: fast-track
fix_date: 2026-06-21
related:
  - ../../audits/2026-06-21-story-system-production-readiness/finding-04.md
tags: [story, runtime, presenter, command]
---

# Story Player presentation bridge 修复记录

## 1. 问题描述

生产可用性审计 F04 指出：Story runtime 只输出 `StoryFrame` 和推进 API，`IStoryCommandHandler` 没有接线，Player 侧缺少正式的帧表现与命令执行桥接契约。

## 2. 根因

- `IStoryCommandHandler` 只定义了 `Execute()`，没有 `CanHandle()`、执行句柄、完成/取消/停止/失败生命周期。
- `StoryModule` 的推进 API 可以被手动调用，但没有一个可复用协调器把当前帧派发给 UI 和命令执行器。
- Editor 预览窗口自行读取 `StoryFrame`，这不能作为 Player 接入层复用。

## 3. 修复方案

- 扩展 runtime 命令契约：新增 `IStoryCommandHandle` 和默认 `StoryCommandHandle`，支持 `Complete`、`Cancel`、`Stop`、`Fail`。
- 扩展 `IStoryCommandHandler`：增加 `CanHandle(StoryCommand)`，`Execute` 改为返回命令句柄。
- 新增 `IStoryFramePresenter` 和 `StoryPresenter`：负责启动/恢复/推进剧情、呈现当前帧、派发命令轨、监听阻塞命令完成后调用 `CompleteCommand`。
- 保持 Story runtime 不依赖 Editor、UI Toolkit、AssetDatabase、AVProVideo 或 Unity `VideoPlayer`。AVPro 具体执行器留给 Player 媒体层实现。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Story/Runtime/IStoryCommandHandler.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过，0 warning / 0 error。

## 6. 遗留事项

- 需要后续新增 Player 侧 AVProVideo command executor，实现 `play_video` 命令到 AVPro 播放器的具体绑定。
- 并行选择后的轨道取消/保留语义仍需单独修复；本次只补 Player 表现桥接，不改变 runner 的并行选择语义。
- F05 已在 `.codestable/refactors/2026-06-21-story-schema-cleanup/story-schema-cleanup-apply-notes.md` 处理。
- F06 已在 `.codestable/refactors/2026-06-21-story-editor-naming-doc-cleanup/story-editor-naming-doc-cleanup-apply-notes.md` 处理。
