---
doc_type: issue-fix
issue: 2026-06-22-story-player-view-canvas
status: fixed
severity: P1
summary: StoryPlayerView playback UI could be invisible when controls are not under a renderable Canvas
tags:
  - story
  - ui
  - runtime
---

# Story Player View Canvas Fix Note

## 问题

剧情播放流程可以正常推进，但视频 RawImage、按钮和对白文本在未挂到可渲染 Canvas 下时不会显示。现有 StoryPlayerView prefab 也需要避免根 Canvas 配置导致运行时不可见。

## 根因

`StoryPlayerView` 假定播放 UI 已经处在有效 UGUI Canvas 下。脚本测试或场景手动挂载时，`StoryPlayerView` 可能只是一个普通 GameObject，引用的 RawImage / Button / TMP_Text 没有父级 Canvas，播放逻辑仍执行但 Unity UI 不渲染。

## 修复

- `StoryPlayerView.Awake` 增加 Canvas 自检：没有父级 Canvas 时在视图根补 `Canvas`、`CanvasScaler`、`GraphicRaycaster`。
- 对引用的 RawImage、TMP_Text、Button 做兜底检查：若引用对象不在 Canvas 下，为其合适的 RectTransform 父级补 Canvas。
- 根节点 local scale 为 0 时恢复为 `Vector3.one`，避免整棵 UI 被缩放隐藏。
- StoryPlayerView prefab 和 prefab builder 同步使用可渲染的 overlay Canvas 设置。
- 补充运行时测试覆盖裸挂 `StoryPlayerView` 时自动创建可渲染 Canvas 根。

## 验证

- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`
- `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore`
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`

三项均通过。第一次并行跑 `StoryPlayback` 与 runtime tests 时出现同一输出 dll 文件写入竞争，改为单独运行后通过。
