# Story Video QTE Command 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-24
> 关联方案 doc：`.codestable/features/2026-06-24-story-video-qte-command/story-video-qte-command-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `StoryInteractionCommandNames`：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryMediaCommandNames.cs` 已定义 `qte`、`success`、`fail`、`inputActionId`、`durationSeconds`、`requiredCount`、`promptTextKey`。
- [x] `NodeKind.Qte`：`Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeKind.cs` 已新增 `Qte = 205`。
- [x] QTE authoring schema：`NodeSchemaRegistry` 已注册 `NodeKind.Qte`，分类为 `Interaction`，端口为 `success/fail`，字段类型与必填性符合方案。
- [x] 编译产物：`StoryProgramCompiler` 对 `NodeKind.Qte` 输出普通 `StoryStepKind.Command`，command name 为 `qte`，`waitForCompletion=true`，并注册 typed argument definitions。
- [x] 默认播放 handler：`StoryPlayerView` 注册 `StoryQteCommandHandler`，handler 从 `CustomRoot` 创建默认 overlay 并通过 `Complete("success"|"fail")` 推进。

**名词层“现状 → 变化”逐项核对**：
- [x] QTE 是 command，不是 `StoryStepKind`：代码未新增 `StoryStepKind.Qte`。
- [x] QTE payload 只使用 `StoryValue` 基础值：arguments 由 command schema 声明，未保存 Unity InputAction。
- [x] QTE overlay 挂在 `PlaybackSurfaceView.CustomRoot`：`StoryPlayerView.UpdateCustomSurfaces()` 对 `qte` 请求 `InteractionRequestKind.Custom`，缺少 `CustomRoot` 抛配置错误。
- [x] QTE outcome 首版只允许 `success/fail`：compiler 校验 unsupported outcome，runtime validation 兜底拒绝未声明 outcome。
- [x] session-time QTE：默认倒计时使用 `Time.unscaledDeltaTime`，未读取 AVPro current time。

**流程图核对**：
- [x] `Parallel + PlayVideo + Wait -> qte` 的 runtime frame 已由 `StoryProgram_WhenParallelWaitQteTriggers_KeepsVideoTrackAndCompletesQteOutcome` 覆盖。
- [x] `StoryPresenter` 继续只看 command handle 完成事件，QTE success/fail 仍走 `CompleteCommand(commandId, outcomeId)`。
- [x] `StoryPlayerView` render frame 时先请求 `Video` surface，再为 `qte` 请求 `Custom` surface。
- [x] 默认 overlay 点击达到次数完成 `success`，超时完成 `fail`，停止/取消只清理 overlay。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 支持作者用 `Parallel -> [PlayVideo] + [Wait -> qte]` 表达视频中途 QTE：runtime tests 覆盖 wait 到点后同帧 video command + qte command。
- [x] StoryPlayback 默认 QTE handler 请求 `Custom` surface：playback channel test 覆盖 `Video` 与 `Custom` request。
- [x] `durationSeconds` 内达到 `requiredCount` 完成 `success`：默认 overlay button test 覆盖。
- [x] duration 到期未达次数完成 `fail`：默认 overlay timeout test 覆盖。
- [x] QTE 期间不暂停视频/音频、不显示 continue、不获得 transition seek policy：playback surface test、compiler seek guard 和范围 grep 覆盖。
- [x] QTE 倒计时不读 AVPro 当前媒体时间：代码 grep 未发现 `EvaluateMediaTime`，实现使用 `Time.unscaledDeltaTime`。

**明确不做逐项核对**：
- [x] 未新增 `StoryStepKind.Qte`、`TimedChoice`、`EvaluateMediaTime()`、`StoryRunner.Seek()` 或 `StoryModule.Seek()`：目标代码 grep 无命中。
- [x] 未接入 InputModule、Unity Input System action asset、手柄或平台映射：Runtime/StoryPlayback/StoryEditor 定向 grep 无命中；默认 handler 只用按钮点击和 `KeyCode.Space`。
- [x] 未实现节奏判定、方向键序列、长按、随机键位等复杂玩法。
- [x] 未新增 `timeout` / `canceled` 默认 outcome；`timeout` 仅作为负测非法 outcome 出现。
- [x] 未做 Editor 一键创建 `Parallel + Wait + QTE` 模板。
- [x] 未把 QTE UI 放进 `Runtime/Story`，Runtime/Story 定向 grep 无 UGUI、AVPro、UIWindow、Editor graph 或播放窗口类型。

**关键决策落地**：
- [x] 决策 1：QTE 是 command，不是 runtime step。落点为 `StoryCommand` + command schema + declared outcome。
- [x] 决策 2：首版只保证 `success/fail`。compiler 与 runtime validation 双层拒绝未声明 outcome。
- [x] 决策 3：默认输入只做点击和 Space。overlay button 触发 `RegisterInput()`，`Update()` 监听 `KeyCode.Space`。
- [x] 决策 4：QTE overlay 走 `Custom` surface。缺 `CustomRoot` 时报 `Story custom root surface is missing`。
- [x] 决策 5：默认不暂停媒体。QTE handler 不调用 video pause/seek，视频 command track 同帧保留。

**编排层“现状 → 变化”逐项核对**：
- [x] compiler 已把 `NodeKind.Qte` 编译为 `qte` command，并强制 wait。
- [x] compiler 已校验 duration、requiredCount、success/fail 目标和 unsupported outcome。
- [x] StoryPlayback 已为 `qte` 请求 `Custom` surface。
- [x] StoryPlayback 已注册默认 QTE handler / overlay。
- [x] StoryPresenter 未新增 QTE 专用推进 API。
- [x] 方案 2.5 已回填结构落点：默认 QTE 玩法保留在 StoryPlayback command handler 边界内；当前因 Unity 生成 csproj 使用 explicit compile include，暂挂现有 handler 文件，后续目录重组再拆独立文件。

**流程级约束核对**：
- [x] QTE 与视频同帧时不停止视频：playback surface test 观察到同帧 `Video` 和 `Custom` request。
- [x] QTE active 时 frame `WaitsForCommand=true`，continue button 不显示：channel request count 覆盖 continue 为 0。
- [x] 同一 command session stop/cancel/fail/completed 后清理 overlay：handler stop cleanup test 覆盖。
- [x] QTE 完成后只通过 command handle 完成事件推进剧情。
- [x] QTE declared outcome 由 schema 和 runtime validation 共同守护。
- [x] 默认倒计时使用 session/unscaled time，不依赖 AVPro current time。

**挂载点反向核对**：
- [x] `NodeKind.Qte` + `NodeSchemaRegistry` 默认 schema：grep 命中 authoring schema 和 editor tests。
- [x] `StoryInteractionCommandNames.Qte` command 协议：grep 命中 runtime protocol、compiler、tests 和 playback handler。
- [x] StoryPlayback QTE command handler 注册：`StoryPlayerView` 只新增 handler 注册和 current custom root provider。
- [x] `Custom` surface request for `qte`：`UpdateCustomSurfaces()` 是唯一播放层 surface 挂点。
- [x] compiler transition seek guard：`ProgramCompiler_WhenVideoTargetsQte_DoesNotWriteHiddenSeekPolicy` 覆盖。
- [x] 反向 grep：QTE 生产引用均落在协议、authoring schema、compiler、runtime validation、StoryPlayback handler/surface 和 tests 内。
- [x] 拔除沙盘推演：删除 QTE schema 会失去作者节点；删除 command 协议会打断 compiler/playback 统一名称；删除 custom request 会让默认 handler 缺 root；删除 handler 注册会让 blocking qte 无默认完成；删除 seek guard 会让承载 QTE 的视频误获得 transition seek。

## 3. 验收场景核对

- [x] **N1 QTE schema**：`NodeSchemaRegistry_WhenQteNodeQueried_ExposesQteSchema` 覆盖默认节点、字段和 success/fail 端口。
- [x] **N2 QTE compile**：`ProgramCompiler_WhenQteNodeIsValid_BuildsQteCommand` 覆盖 command name、waitForCompletion、arguments、outcome targets 和 typed definitions。
- [x] **N3 参数校验**：`ProgramCompiler_WhenQteDurationIsInvalid_ReturnsLocatedError` 与 `ProgramCompiler_WhenQteRequiredCountIsInvalid_ReturnsLocatedError` 覆盖定位错误。
- [x] **N4 outcome 校验**：`ProgramCompiler_WhenQteOutcomeTargetMissing_ReturnsLocatedError`、`ProgramCompiler_WhenQteHasUnsupportedOutcome_ReturnsLocatedError` 和 runtime undeclared outcome 测试覆盖。
- [x] **N5 并行 QTE**：`StoryProgram_WhenParallelWaitQteTriggers_KeepsVideoTrackAndCompletesQteOutcome` 覆盖 `Evaluate(1.5d)` 后 video command + qte command 同帧。
- [x] **N6 success 推进**：`StoryPlayerView_WhenDefaultQteButtonClickedRequiredTimes_AdvancesSuccess` 覆盖默认 overlay 点击两次推进 success target。
- [x] **N7 fail 推进**：`StoryPlayerView_WhenDefaultQteTimesOut_AdvancesFail` 覆盖超时推进 fail target。
- [x] **N8 surface request**：`StoryPlayerView_WhenQteFramePresented_RequestsCustomSurfaceAndKeepsVideoSurface` 覆盖 `Custom` + `Video` request，continue 为 0。
- [x] **N9 缺 CustomRoot**：`StoryPlayerView_WhenQteCustomRootMissing_ThrowsConfigurationError` 覆盖配置错误。
- [x] **N10 stop/cancel 清理**：`StoryQteCommandHandler_WhenHandleStopped_CleansOverlayWithoutCompletingOutcome` 覆盖 stop 清理且不触发 completed。
- [x] **N11 不暂停媒体**：QTE handler 不调用 pause/seek；同帧视频 surface 保留。当前没有专用 video pause spy，结论来自代码审查 + surface test + seek/pause API grep。
- [x] **N12 QTE 视频不可 seek**：`ProgramCompiler_WhenVideoTargetsQte_DoesNotWriteHiddenSeekPolicy` 覆盖 `play_video` 不包含 `__videoSeekPolicy=transition`。
- [x] **B1-B4 范围守护**：禁用符号、输入系统接入和 Runtime/Story UI 依赖 grep 已通过。

**验证命令**：
- [x] `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：0 warning / 0 error。
- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：0 warning / 0 error。
- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：0 warning / 0 error。
- [x] `rg -n "TimedChoice|EvaluateMediaTime|StoryRunner\.Seek|StoryModule\.Seek|StoryStepKind\.Qte" Assets\GameDeveloperKit\Runtime\Story Assets\GameDeveloperKit\Runtime\StoryPlayback Assets\GameDeveloperKit\Editor\StoryEditor`：无命中。
- [x] `rg -n "InputModule|UnityEngine\.InputSystem|InputAction<|InputActionAsset|InputActionReference" Assets\GameDeveloperKit\Runtime\Story Assets\GameDeveloperKit\Runtime\StoryPlayback Assets\GameDeveloperKit\Editor\StoryEditor`：无命中。
- [x] `rg -n "UGUI|AVPro|UIWindow|UnityEditor|EditorNodeGraph|GraphView|StoryPlayback|RawImage|Button|TMP_Text|RectTransform" Assets\GameDeveloperKit\Runtime\Story`：无命中。

**未运行项**：
- Unity PlayMode batchmode 未跑成：尝试使用 `D:\unity2022.3.62f2c1\Editor\Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter GameDeveloperKit.Tests.StoryPlaybackInteractionChannelTests -logFile Logs\qte-playmode-tests.log -quit`，日志显示同一项目已有 Unity Editor 实例打开，batchmode 被阻塞。该环境限制已记录在 `.codestable/attention.md`。

## 4. 术语一致性

- `qte` command：生产代码统一使用 `StoryInteractionCommandNames.Qte`，字符串 `"qte"` 主要出现在测试 fixture / step id。
- `QTE payload`：代码字段为 `inputActionId`、`durationSeconds`、`requiredCount`、`promptTextKey`，与方案一致。
- `QTE overlay`：默认实现位于 StoryPlayback，挂载到 `CustomRoot`，未进入 Runtime/Story。
- `QTE success/fail`：默认 outcome 只保留 `success/fail`；`timeout` 只在负测中出现。
- `session-time QTE`：未新增 media-time runtime API；默认 overlay 用 `Time.unscaledDeltaTime`。
- 禁用词 grep：`TimedChoice` / `EvaluateMediaTime` / `StoryRunner.Seek` / `StoryModule.Seek` / `StoryStepKind.Qte` 在目标代码无命中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已归并 `StoryInteractionCommandNames` / `qte` command 协议。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 QTE 默认作者节点进入 Story Editor 默认节点集，端口为 `success/fail`。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 StoryPlayback 默认 QTE handler 使用 `Custom` surface overlay、点击/Space 输入、success/fail outcome。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 QTE 不暂停媒体、不新增 media-time trigger、不获得 transition seek 的边界。
- [x] `.codestable/architecture/ARCHITECTURE.md` 变更日志已追加 2026-06-24 记录。

## 6. requirement 回写

- [x] `requirement: story-module` 指向 current req。
- [x] `.codestable/requirements/story-module.md` 已追加 `implemented_by: 2026-06-24-story-video-qte-command`。
- [x] `.codestable/requirements/story-module.md` 已追加实现进展：视频中 QTE 通过普通 command + success/fail outcome 推进，默认播放层用 Custom surface overlay 承接。
- [x] `.codestable/requirements/story-module.md` 已保留 Story 核心边界：不新增 runtime step、media-time trigger、剧情级 seek 或 UI 依赖。
- [x] 变更日志已追加 2026-06-24 记录。

## 7. roadmap 回写

- [x] design frontmatter 包含 `roadmap: story-interactive-video` 与 `roadmap_item: story-video-qte-command`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 对应条目已从 `in-progress` 改为 `done`，feature 仍指向 `2026-06-24-story-video-qte-command`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 第 5 节对应条目状态已同步为 `done`，对应 feature 已填入。
- [x] roadmap 主文档第 6/7/9 节已同步 QTE 已完成、unlock 仍待推进。

## 8. attention.md 候选盘点

- [x] 无新增候选：Unity Editor 实例阻塞 batchmode 的注意事项已存在于 `.codestable/attention.md`，本 feature 未暴露新的每次都需要知道的环境规则。

## 9. 遗留

- 后续优化点：`StoryPlayerView.cs` 和 `StoryProgramCompiler.cs` 仍偏胖；design 2.5 已建议后续单独走 `cs-refactor` 拆默认 surface/overlay、media refresh、input binding 和 compiler 节点构建 helper。
- 已知限制：未跑 Unity Test Runner；真实 Unity Editor / Player 中的 overlay 视觉、AVPro 画面与 Space 输入仍建议在现有 Editor 内跑 PlayMode tests 或手测确认。
- 实现阶段观察：默认 QTE input 是最小点击/Space；复杂输入映射、手柄、暂停策略、QTE 模板和 unlock 玩法仍属于后续 feature。
