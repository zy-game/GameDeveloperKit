---
doc_type: requirement
slug: story-editor
pitch: 在编辑器里把剧情节点、选项和分支串成可导入导出的时间线，让策划不用手写关系表。
status: draft
last_reviewed: 2026-06-24
implemented_by:
  - 2026-06-20-editor-graph-manual-acceptance
  - 2026-06-20-story-graph-port-policy
  - 2026-06-20-choice-item-branching-contract
  - 2026-06-20-typed-command-fields
  - 2026-06-20-story-graph-validation-feedback
  - 2026-06-20-sample-story-graph-fixture
  - 2026-06-20-story-playback-window
  - 2026-06-21-story-cleanup-avpro-playback
  - 2026-06-21-story-editor-node-simplification
  - 2026-06-24-story-editor-interaction-authoring-patterns
  - 2026-06-25-story-editor-welcome
tags: [story, editor, timeline, excel, export]
---

# 剧情时间线编辑与导入导出

## 用户故事

- 作为策划，我希望在可视化时间线上摆剧情节点和选项，而不是靠几十行 Excel 拼连接关系。
- 作为玩法程序，我希望编辑器产出的剧情文件结构稳定明确，而不是每次都猜表格列和连接规则。
- 作为内容维护者，我希望能把编辑器里的剧情配置导回 Excel，方便和外部表格协作。

## 为什么需要

剧情内容往往既有顺序又有分支。只靠表格行和字符串 key，能录入信息，却很难看出结构、分支和回收关系，也很难在多人协作时避免错连、漏连和重复改名。

## 怎么解决

提供一个编辑器内的剧情时间线工具：用节点、选项和分支来组织剧情；编辑器负责导入 Excel、导出 Excel、校验节点关系，并导出结构稳定的剧情配置文件。运行时怎么读取、播放或保存进度不在这个能力里拍板。

## 边界

- 它不做完整的对话播放器、字幕系统、语音同步或过场动画。
- 它不替代 `ProcedureModule`，也不负责顶层游戏流程切换。
- 它不联动 `ConfigModule`、`DataModule`、`ProcedureModule` 等运行时模块。
- 它不把 Excel 当成唯一真源；Excel 是导入/导出的交换格式。
- 它不处理资源打包、热更发布或远端同步。

## 实现进展

- 2026-06-20：完成 Story Editor v4 图交互最小闭环验收。当前编辑器可在真实 Unity Editor 中完成打开窗口、选择章节、右键/Space 创建、节点库拖入、节点拖拽、端口连线、空白处创建并连接、鼠标锚点缩放、wire 对齐、pan、Delete/Backspace、Esc、F 聚焦和框选。后续仍需继续推进端口语义、选项分支契约、命令字段类型化和图上校验反馈，所以本 requirement 保持 draft。
- 2026-06-20：完成 Story v4 节点端口语义收紧。当前 Story Editor v4 已把 Start/End、Dialogue/Narration、Choice、命令/动作、条件、跳转/等待和辅助节点的输入输出、单连/多连、合法目标与中文错误提示收敛到 Story port policy；runtime validation 会拒绝未知输出端口和 editor-only 节点。选项分支编译契约、命令字段类型化和图上校验反馈仍未完成，所以本 requirement 继续保持 draft。
- 2026-06-20：完成选项项分支契约。当前 Story Editor v4 的 `Dialogue` / `Narration.completed` 可以连接多个 Choice item，编译器会合成 runtime `StoryStepKind.Choice`，运行时通过 `ChoicesReady` 输出选项并由 UI 调用 `Select(choiceId)` 推进。命令字段类型化、图上校验反馈和示例剧情 fixture 仍未完成，所以本 requirement 继续保持 draft。
- 2026-06-20：完成命令节点字段类型化。当前 `PlayVideo` / `ShowImage` / `PlayAudio` 等命令节点由 schema 声明资源字段，V4 图节点可展示资源选择和稳定资源 ID，编译后的 `StoryCommand.Arguments` 只保存基础 `StoryValue` 字符串/数字/布尔，runtime 通过 typed command schema 兜底校验缺参和类型错误。图上校验反馈和示例剧情 fixture 仍未完成，所以本 requirement 继续保持 draft。
- 2026-06-20：完成 Story Graph 校验反馈。当前 V4 图会把缺必填字段、数字/布尔类型错误、手填资源 warning、Choice selected 缺目标、文本 completed 混连、未知输出端口、辅助节点进入运行时流程、缺目标节点/章节等问题显示到节点、字段、端口或连线上；左侧中文问题列表与 graph badge 同源，支持点击定位，图编辑后会提示上一轮编译诊断已过期。示例剧情 fixture 仍未完成，所以本 requirement 继续保持 draft。
- 2026-06-20：完成示例剧情图 fixture。当前 Story Editor v4 可通过菜单或工具栏打开 `sample_story_graph`，示例包含 `雨夜抵达`、`旧车站`、`暗巷`、`余波` 四章，覆盖旁白、对白、Choice item、媒体命令、等待、小游戏和章节跳转；compiler/runtime smoke 和真实 Editor 手测均已收口。后续仍需运行时播放窗口来观察完整交互流程，所以本 requirement 继续保持 draft。
- 2026-06-20：实现 Story Editor v4 运行时播放窗口。当前编辑器可从当前剧情/章节打开独立 `剧情播放窗口`，窗口会编译 authoring graph，并用运行时 `StoryModule` 启动 `StoryProgram`；Line、ChoicesReady、Command、Wait、Completed 均可在窗口内观察和手动推进。手测记录已建立，等待真实 Editor 点击确认，所以本 requirement 继续保持 draft。
- 2026-06-21：Story Editor v4 清理旧 GraphView/mapper/export/CSV 兼容路径，authoring model 不再暴露 volumes/units/payload/nodeType/owner actions/interactions/transitions。资源字段编译后保存 `Assets/...` 路径；`剧情播放窗口` 对 `play_video` 使用 AVProVideo 打开并显示视频纹理，完播后通过 runtime `CompleteCommand` 推进，视频和选项可同帧显示。Unity 内置 VideoPlayer 不作为后端。
- 2026-06-21：Story Editor v4 节点库已收敛到默认作者主路径。palette、创建菜单、端口策略和编译器共享 `IsDefaultAuthoringNode`，只允许内容、媒体、音频、等待、选项、小游戏、事件、章节跳转和 Start/End 边界节点；并行、合流、多路、随机、条件、标记和辅助节点不再可创建，旧资源中保留这些节点时会在图上诊断和编译报告里定位报错。canonical sample 已改为只使用主路径节点，并用线性多轨帧覆盖视频/图片/音频/文本/选项组合。
- 2026-06-24：完成互动影游作者模板。当前 Story Editor 节点库提供“视频中途选项 / QTE / Unlock”三个 editor-only 组合模板，一键生成现有 `Parallel + PlayVideo + Wait + Choice/Qte/Unlock` 图结构；`Wait.completed` 可拥有多个 Choice item 并编译成普通 synthetic `Choice` step；编译成功后 PlayVideo 节点会显示只读 `seek policy: transition/disabled` 信息。模板不新增 runtime step、TimedChoice、layout slot、`playbackRole`、`seekable` 或提交式单选/多选命令，所以本 requirement 仍因导入导出等更大编辑器愿景保持 draft。
- 2026-06-25：完成剧情编辑器欢迎引导页。菜单入口现改为 StoryEditorWelcomeWindow（VS Code 风格欢迎页），提供新建/打开/示例剧情按钮、最近打开资源列表（EditorPrefs 持久化，最多 10 条）和快速开始引导；用户选择后进入 StoryEditorWindow 主编辑器。欢迎页为独立 EditorWindow，不改变编辑器核心行为。本 requirement 仍因导入导出等更大编辑器愿景保持 draft。
