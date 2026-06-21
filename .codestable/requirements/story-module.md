---
doc_type: requirement
slug: story-module
pitch: 在运行时串起剧情、卷、章节、媒体、选项和事件，让复杂分支剧情不用散写在 UI 和流程代码里。
status: draft
last_reviewed: 2026-06-21
implemented_by:
  - 2026-06-20-sample-story-graph-fixture
  - 2026-06-21-story-cleanup-avpro-playback
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
- 它不替代全局流程状态机；剧情可以被某个 procedure 使用，但不管理游戏顶层阶段。
- 它不直接读取配置表、媒体资源或本地化文本；调用方负责把剧情定义和表现所需资源准备好。
- 它不直接保存到磁盘；需要持久化时调用方可以把剧情快照交给数据能力保存。
- 它不内置复杂脚本虚拟机、Lua、任务系统或通用变量黑板；条件和外部结果由调用方提供。
- 它不承诺自动 Update 播放；首版按调用方显式选择、时间求值和外部完成结果来改变剧情状态。

## 实现进展

- 2026-06-20：示例剧情图 fixture 已通过 runtime smoke。`sample_story_graph` 可编译为 `StoryProgram`，并通过 `StoryModule.Register` / `StartProgram` 观察到 line、choice、command、wait 和章节跳转或 completed；媒体和小游戏仍只由 `CompleteCommand` 模拟表现层完成，不表示 runtime 已负责真实播放。
- 2026-06-21：runtime 可观察输出已重构为 `StoryFrame`。`StoryRunner` / `StoryModule` 的推进 API 返回完整 frame，同一 frame 可以同时包含文本轨、命令轨、等待轨和选项；播放窗口读取 `CurrentFrame` 后可同屏展示视频命令、文本和选项。Runtime 仍不播放媒体、不加载资源、不引用 Editor/UI 类型。
- 2026-06-21：旧 `Definition` / `Timeline` / `ActionRequest` / `Payload` / `NodeType` 等 Story runtime 兼容路径已清理，`StoryModule` 收敛为 `StoryProgram` / `StoryRunner` / `StoryFrame` 主路径。资源参数仍是数据字符串，编译产物保存 `Assets/...` 路径，不保存 `guid:`，Runtime Story 不引用 AVProVideo、UnityEditor、AssetDatabase 或 UI Toolkit editor 类型。
