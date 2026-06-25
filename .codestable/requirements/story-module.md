---
doc_type: requirement
slug: story-module
pitch: 在运行时串起剧情、卷、章节、媒体、选项和事件，让复杂分支剧情不用散写在 UI 和流程代码里。
status: current
last_reviewed: 2026-06-24
implemented_by:
  - 2026-06-20-sample-story-graph-fixture
  - 2026-06-21-story-cleanup-avpro-playback
  - 2026-06-22-story-playback-package-merge
  - 2026-06-22-story-test-scripts-entry
  - 2026-06-23-story-playback-video-prewarm
  - 2026-06-23-story-playback-view-input-layers
  - 2026-06-23-story-transition-video-seek-controls
  - 2026-06-24-story-parallel-wait-interaction-flow
  - 2026-06-24-story-video-qte-command
  - 2026-06-24-story-unlock-interaction-flow
tags: [story, runtime, branching, timeline, interaction]
---

# 运行时剧情推进

## 用户故事

- 作为玩法开发者，我希望运行时能按剧情、卷和章节推进故事，而不是在 UI 回调、流程脚本和小游戏回调里散写跳转关系。
- 作为做剧情表现的人，我希望视频、图片、对白、选项、事件和小游戏都能用同一套剧情状态串起来，而不是每种表现各自决定下一步。
- 作为叙事设计的承接方，我希望选项能受条件控制，也能在媒体播放到指定时机后出现，而不是把这些规则硬编码在播放器里。
- 作为需要保存剧情进度的人，我希望能拿到包含当前章节、单元、节点、时间和历史选择的快照，而不是反向拼凑一堆临时变量。
- 作为框架维护者，我希望剧情推进、表现播放、本地化、资源加载、流程编排和存档持久化边界清楚，而不是让剧情系统吞掉所有运行时模块。

## 为什么需要

复杂剧情不只是“从 A 到 B”。一份剧情可能按卷组织，卷下有多个章节，章节图里又有视频、图片、对白、选项、小游戏、事件和跳转。没有统一运行时能力时，当前播到哪里、哪些选项可见、小游戏结果走哪条分支、玩家为什么来到某个结尾都会散落在不同系统里，后续编辑器产物也缺少稳定的消费对象。

## 怎么解决

提供一个运行时剧情推进能力：业务把已经准备好的剧情定义交给系统，系统负责校验剧情、卷、章节、节点、选项、条件和跳转关系；运行时按当前节点产出“需要表现层做什么”的请求，接收选项选择、时间推进或外部结果后切到下一个节点，并能导出和恢复当前进度快照。表现层、文本翻译、资源加载和本地保存仍由业务选择合适模块完成。

## 边界

- 它不提供剧情编辑器、时间线画布、Excel 导入导出或配置表生成工具。
- 它不负责渲染对白、弹出按钮、播放视频、播放语音、镜头、动画或过场表现；它只告诉表现层当前需要展示或等待什么。
- 默认播放能力由独立的 StoryPlayback 播放包承接；剧情核心本身仍不持有 UGUI、AVPro、Sound、Resource 或 Procedure。
- 它不替代全局流程状态机；剧情可以被某个 procedure 使用，但不管理游戏顶层阶段。
- 它不直接读取配置表、媒体资源或本地化文本；调用方负责把剧情定义和表现所需资源准备好。
- 它不直接保存到磁盘；需要持久化时调用方可以把剧情快照交给数据能力保存。
- 它不内置复杂脚本虚拟机、Lua、任务系统或通用变量黑板；条件和外部结果由调用方提供。
- 它不承诺自动 Update 播放；首版按调用方显式选择、时间求值和外部完成结果来改变剧情状态。

## 实现进展

- 2026-06-20：示例剧情图 fixture 已通过 runtime smoke。`sample_story_graph` 可编译为 `StoryProgram`，并通过 `StoryModule.Register` / `StartProgram` 观察到 line、choice、command、wait 和章节跳转或 completed；媒体和小游戏仍只由 `CompleteCommand` 模拟表现层完成，不表示 runtime 已负责真实播放。
- 2026-06-21：runtime 可观察输出已重构为 `StoryFrame`。`StoryRunner` / `StoryModule` 的推进 API 返回完整 frame，同一 frame 可以同时包含文本轨、命令轨、等待轨和选项；播放窗口读取 `CurrentFrame` 后可同屏展示视频命令、文本和选项。Runtime 仍不播放媒体、不加载资源、不引用 Editor/UI 类型。
- 2026-06-21：旧 `Definition` / `Timeline` / `ActionRequest` / `Payload` / `NodeType` 等 Story runtime 兼容路径已清理，`StoryModule` 收敛为 `StoryProgram` / `StoryRunner` / `StoryFrame` 主路径。资源参数仍是数据字符串，编译产物保存 `Assets/...` 路径，不保存 `guid:`，Runtime Story 不引用 AVProVideo、UnityEditor、AssetDatabase 或 UI Toolkit editor 类型。
- 2026-06-22：默认播放层已合并为 `GameDeveloperKit.StoryPlayback`。`StoryPlayerView`、播放协调器、图片/音频/AVPro 视频播放器和视频路径解析器迁入独立播放包；`Runtime/Story` 只保留剧情推进和命令数据协议。旧 StoryPresentation / StoryPresentation.AVPro 引导层删除，Startup、Loading、Procedure 切换和视频预热仍留待后续能力推进，所以本 requirement 继续保持 draft。
- 2026-06-23：项目级剧情测试入口已落在 `Assets/GameDeveloperKit/Scripts/StoryTest`。`StoryTestProcedure` 可由 `FrameworkStartup` 进入，接收 `StoryTestRequestAsset` 或代码侧 `StoryTestRequest`，注册 StoryProgram 并调用 StoryPlayback 播放；未传入播放器时会在 `UILayer.StoryPlayback` 下创建默认 `StoryPlayerView`。
- 2026-06-23：StoryPlayback 已支持 AVPro 视频队列预热。`StoryAvProVideoPreloadQueue` 可按 `source + clipPath` 准备 streaming assets、persistent data path 或 network stream 视频；`StoryAvProVideoCommandPlayer.PlayVideo()` 优先 Acquire 预热播放器，未命中时保持即时播放，默认 `StoryPlayerView` 会配置小容量队列和前瞻预热。预热仍是播放层能力，不进入 Story 核心，也不接管 Loading、Procedure、Resource 视频加载或章节媒体预加载；本 requirement 达到 current。
- 2026-06-23：StoryPlayback 已完成单一交互通道和 view/input surface 接线。业务可通过 `App.Story.SetInteractions(channel)` 注册 `IInteractionChannel`，由 channel 按章节创建或切换 UI，并通过 `GetPlaybackSurfaceView(InteractionRequest)` 提供视频、图片、文本、继续按钮和等量选项按钮；`OnAwake` 早于启动预热，`OnStoryStarted` 只在预热完成后触发。Story 核心仍不创建 UIWindow、prefab 或 UGUI 控件。
- 2026-06-24：StoryPlayback 已支持纯过渡视频时间条和 playback-only seek。Story Editor compiler 根据图结构保守推导 transition video，并只为可证明纯过渡的 `play_video` 写入内部 `__videoSeekPolicy=transition`；默认 `StoryPlayerView` 可通过可选 `VideoSeekSurface` 显示时间条，`StoryAvProVideoPlayback.Seek(time)` 只改变 AVPro 当前媒体时间。分支互动视频、并行等待互动视频、loop 视频、缺省 policy 和 duration 不可用视频默认不可 seek；Story 核心仍不提供剧情级 seek。
- 2026-06-24：视频播放到指定时机后出现交互的基础路径已通过现有 `Parallel + Wait + Choice/Command` 落地。视频轨保持 `play_video(waitForCompletion: true)` command 活跃，交互轨由 StoryPlayback session time 推进 `Wait(N)`，到点后同一 `StoryFrame` 可同时保留视频 command 并出现普通 `Choices` 或 custom command；选项仍通过 `Select(choiceId)` 推进，command 互动仍通过 `CompleteCommand(commandId, outcomeId)` 推进，不新增 `TimedChoice`、媒体时间触发 API 或剧情级 seek。
- 2026-06-24：视频播放中的 QTE 已作为普通 `qte` command 落地。作者用 `Parallel + Wait(N) -> qte` 表达出现时机，compiler 输出 typed arguments 和 `success/fail` declared outcome，runtime validation 兜底拒绝未声明 outcome；默认 StoryPlayback 在 `CustomRoot` 上创建最小 overlay，用按钮点击或 Space 在 session time 内累计输入，达到 `requiredCount` 完成 `success`，超时完成 `fail`。Story 核心仍不新增 QTE step、media-time trigger、剧情级 seek 或 UI/Input 系统依赖。
- 2026-06-24：视频播放中的解锁互动已作为普通 `unlock` command 落地。作者用 `Parallel + Wait(N) -> unlock` 或普通 command 链路表达出现时机，compiler 输出 `unlockId`、`puzzleType`、`promptTextKey` typed arguments 和 `success/fail` declared outcome；默认 StoryPlayback 在 `CustomRoot` 上创建最小 overlay，并通过 `IUnlockStateProvider` 读写 `unlockId` 状态，已解锁直接 success，Unlock 写入成功后 success，Fail/Cancel 或写入拒绝时 fail。Story 核心仍不新增 unlock step、media-time trigger、剧情级 seek、`IConditionResolver` 或 UI/Input 系统依赖。

## 变更日志

- 2026-06-23：StoryPlayback 播放包、Scripts StoryTest 入口和 AVPro 视频队列预热均已落地，`story-module` 从 draft 升为 current。
- 2026-06-23：追加 StoryPlayback view/input interaction channel 实现进展，记录章节 UI surface 接管和 `OnAwake` -> prewarm -> `OnStoryStarted` 生命周期边界。
- 2026-06-24：追加纯过渡视频 seek 控制实现进展，记录 compiler-inferred hidden policy、可选 `VideoSeekSurface` 和播放层 seek 边界。
- 2026-06-24：追加并行等待互动实现进展，记录 session-time `Parallel + Wait + Choice/Command` 路径、同帧视频/交互输出和无剧情级 seek 边界。
- 2026-06-24：追加视频 QTE command 实现进展，记录 `Wait(N)->qte` 编排、`success/fail` outcome、默认 Custom overlay 和无媒体时间触发 / 无输入系统接入边界。
- 2026-06-24：追加视频解锁互动实现进展，记录 `Wait(N)->unlock` 编排、`IUnlockStateProvider`、`success/fail` outcome、默认 Custom overlay 和不新增 `IConditionResolver` / 无媒体时间触发 / 无输入系统接入边界。
