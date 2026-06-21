---
doc_type: manual-record
feature: 2026-06-21-story-runtime-multitrack-frame
status: pending-user
created: 2026-06-21
---

# Story Runtime Multitrack Frame Manual Record

## 环境

- Unity Editor：待用户在当前项目 Editor 内确认
- 入口：`GameDeveloperKit/剧情编辑器` -> `打开示例` -> 选择 `雨夜抵达` -> `打开播放窗口`
- 示例：`sample_story_graph` / `chapter_arrival`

## 自动证据

- `StorySampleGraphFixtureTests.SampleFixture_WhenCompiled_BuildsProgramAndRuntimeSmokePath`：覆盖示例图 runtime frame 聚合，入口 frame 同时包含 text、command、text 和 choices。
- `StorySampleGraphFixtureTests.SampleFixture_WhenPlayedThroughPlaybackSession_UsesRuntimeModuleAndRecordsHistory`：覆盖播放会话读取 `CurrentFrame` 并推进 choice、wait、command outcome。
- `StorySampleGraphFixtureTests.SampleFixture_WhenOpenedInPlaybackWindow_ShowsRuntimeOutputAndControls`：覆盖播放窗口同屏显示视频命令、视频参数、文本和选项按钮。
- `StorySampleGraphFixtureTests.StoryRuntime_WhenScanned_DoesNotReferenceEditorPlaybackOrConcreteMediaTypes`：覆盖 Story runtime 不引用 Editor/UI/具体媒体类型。

## 手测步骤

| 编号 | 操作 | 期望 | 结果 |
|---|---|---|---|
| M1 | 打开 `剧情播放窗口` | 当前状态显示 `正在播放：选项` | pending |
| M2 | 检查当前输出区 | 同一页可见 `文本`、`命令`、`play_video`、`clip=Assets/GameDeveloperKit/Simples/videos/0.mp4` 和 `choice_enter_alley` 选项按钮 | pending |
| M3 | 点击 `choice_enter_alley` | 进入图片 + 等待 frame，显示 `show_image` 参数和 `完成等待` 控件 | pending |
| M4 | 点击 `完成等待` | 跳转到 `chapter_alley`，显示暗巷文本和选项 | pending |

## 备注

播放窗口只展示 runtime `CurrentFrame` 数据，不真实播放视频、图片或音频；真实表现层由业务 command handler 处理。
