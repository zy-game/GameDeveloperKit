---
doc_type: issue-fix
issue: 2026-06-22-story-player-view-prefab-instantiation
status: fixed
severity: P1
summary: Story test startup did not instantiate StoryPlayerView prefab when the scene had no player view
tags:
  - story
  - startup
  - runtime
---

# Story Player View Prefab Instantiation Fix Note

## 问题

剧情测试启动后没有创建 `StoryPlayerView.prefab`，导致视频 RawImage、按钮和对白 UI 根本不存在，播放流程即使启动也没有可见播放界面。

## 根因

`StoryTestProcedure` 只接受 request 中显式传入的 `StoryPlayerView`，或在场景里查找已有 `StoryPlayerView`。当场景没有预放播放器时，它不会从 prefab 创建实例。

`SampleScene` 的 startup user data 也指向了剧情程序资产本身，而不是带播放器 prefab 配置的 `StoryTestRequest.asset`。

## 修复

- `StoryTestRequest` / `StoryTestRequestAsset` 增加 `PlayerViewPrefab` 配置。
- `StoryTestProcedure` 找不到 request / scene 中的播放器实例时，从 `PlayerViewPrefab` 实例化 `StoryPlayerView`。
- procedure 只销毁自己实例化的播放器；外部传入或场景已有的播放器只停止播放。
- `Assets/Scenes/StoryTestRequest.asset` 配置 sample 剧情和 `StoryPlayerView.prefab`。
- `Assets/Scenes/SampleScene.unity` 的 startup user data 改为 `StoryTestRequest.asset`。
- 新增运行时测试覆盖通过 prefab 自动创建播放器的路径。

## 验证

- `dotnet build GameDeveloperKit.Scripts.StoryTest.csproj --no-restore`
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`

两项均通过。并行编译时曾出现同一中间 dll 写入竞争，单独运行后通过。
