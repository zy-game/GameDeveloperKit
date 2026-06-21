---
doc_type: manual-record
feature: 2026-06-20-story-playback-window
status: pending-user
created: 2026-06-20
---

# Story Playback Window Manual Record

## 环境

- Unity Editor：待用户在当前项目 Editor 内确认
- 入口：`GameDeveloperKit/剧情编辑器` -> `打开示例` -> 选择章节 -> `打开播放窗口`
- 示例：`sample_story_graph` / `chapter_arrival`

## 自动证据

- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过。
- `StorySampleGraphFixtureTests.SampleFixture_WhenPlayedThroughPlaybackSession_UsesRuntimeModuleAndRecordsHistory`：覆盖 session 使用 `StoryModule` 启动并手动推进 `StoryFrame`、选项、命令、等待、跨章节和 completed。
- `StorySampleGraphFixtureTests.SampleFixture_WhenOpenedInPlaybackWindow_ShowsRuntimeOutputAndControls`：覆盖播放窗口静态 UI 同屏显示 story/chapter/status、文本轨、视频命令轨、视频参数和选项按钮。
- `StorySampleGraphFixtureTests.SampleFixture_WhenPlaybackWindowAdvances_RefreshesRuntimeOutput`：覆盖播放窗口推进方法刷新 `CurrentFrame` 和 history。

## 手测步骤

| 编号 | 操作 | 期望 | 结果 |
|---|---|---|---|
| P1 | 在 Story Editor 点击 `打开示例`，选择 `雨夜抵达` | 左侧树选中 `chapter_arrival`，图上显示示例章节 | pending |
| P2 | 点击 `打开播放窗口` | 独立 `剧情播放窗口` 打开，显示 `sample_story_graph`、版本、章节和 `正在播放：选项` | pending |
| P3 | 检查当前输出区 | 同一个 `CurrentFrame` 中同时显示旁白文本、`play_video` 命令、守卫对白、`clip=Assets/GameDeveloperKit/Simples/videos/0.mp4` 和选项按钮 | pending |
| P4 | 点击 `choice_enter_alley` 选项 | 进入 `arrival_show_map` frame，显示图片命令和等待轨 | pending |
| P5 | 检查图片命令 | 当前 frame 显示 `image=Assets/GameDeveloperKit/Simples/videos/Club_1.png`，不需要播放窗口真实加载或播放资源 | pending |
| P6 | 完成 `arrival_show_map` 后点击 `完成等待` | 通过 Wait 节点跳转到 `chapter_alley` 的 `alley_line` | pending |
| P7 | 在暗巷选择 `choice_pick_lock`，命令 outcome 选 `success` | 进入暗巷门声/视频命令 frame，完成视频 outcome 后跳转到 `chapter_final` | pending |
| P8 | 检查右侧历史 | 历史包含启动、继续、选择、完成命令、推进等待，记录 chapter/step/kind | pending |
| P9 | 点击 `重启章节` | 会话重新编译并回到当前章节第一个输出 | pending |
| P10 | 关闭窗口 | 窗口关闭后不会继续保留活动播放会话 | pending |

## 备注

本记录当前只代表自动编译和自动测试已通过。真实 Editor 点击路径需要用户确认后把 `status` 和 P1-P10 改为 `passed`。
