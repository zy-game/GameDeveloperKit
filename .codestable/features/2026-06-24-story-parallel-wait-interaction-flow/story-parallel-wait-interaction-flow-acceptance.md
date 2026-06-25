# Story Parallel Wait Interaction Flow 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-24
> 关联方案 doc：`.codestable/features/2026-06-24-story-parallel-wait-interaction-flow/story-parallel-wait-interaction-flow-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] 初始 frame 契约：`StoryProgram_WhenParallelWaitChoiceTriggers_KeepsVideoTrackAndShowsChoice` 覆盖 `Command(play_video, branch_video) + Wait(branch_interaction)`，并断言 `WaitsForCommand=true`、`WaitsForTime=true`。
- [x] wait 到点后的 choice frame：同一测试断言 `Evaluate(1.5d)` 后 frame 保留 `play_video` command track，出现 `choice_continue`，并写入 `BranchId=branch_interaction`。
- [x] wait 到点后的 command interaction frame：`StoryProgram_WhenParallelWaitCommandTriggers_KeepsVideoTrackAndCompletesInteractionOutcome` 覆盖 `play_video + custom_interaction` 同帧出现，并可通过 `CompleteCommand("custom_interaction", "success")` 推进。
- [x] StoryPlayback surface request：`StoryPlayerView_WhenParallelWaitChoicePresented_RequestsVideoAndChoiceSurfaces` 覆盖 wait-to-choice render 后同时请求 `Video` 与 `Choice` surface。
- [x] compiler seek policy guard：`ProgramCompiler_WhenVideoAndWaitChoiceAreParallel_DoesNotWriteHiddenSeekPolicy` 覆盖并行等待互动中的 `PlayVideo` 不写入 `__videoSeekPolicy=transition`。

**名词层“现状 → 变化”逐项核对**：
- [x] 未新增公开 runtime 名词：本 feature 没有新增 `TimedChoice`、media-time trigger、seek API 或 interaction layer 接口。
- [x] `StoryFrame` 继续承载 tracks、choices 和 gate flags：新增测试只断言现有 `Tracks` / `Choices` / `WaitsFor*`。
- [x] `IInteractionChannel` 仍是唯一 surface 边界：测试通过 `PlaybackSurfaceView.ChoiceButtons`、Video surface 和 Choice surface 观察，不新增 provider/controller。

**流程图核对**：
- [x] `StoryRunner` 进入 parallel 后合成 `play_video + Wait` frame：runtime test N1 覆盖。
- [x] `StoryPlayerView.Update()` 推进 wait：playback test 通过 frame render / evaluate 链路覆盖。
- [x] wait branch 进入 `Choice/Command` 时 video branch 保留：runtime / presenter tests 覆盖。
- [x] 玩家输入仍走 `Select(choiceId)` / `CompleteCommand(commandId, outcomeId)`：runtime tests 覆盖。
- [x] 新 frame 不含旧视频时停止旧 command handle：`StoryPresenter_WhenParallelWaitChoiceAppears_KeepsVideoHandleUntilChoiceSelected` 覆盖。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `Parallel(video command, wait branch)` 初始 frame 同时包含视频命令和等待轨：runtime tests 覆盖。
- [x] `Wait` 使用 StoryPlayback session time 推进，不读取 AVPro 当前时间：实现仍由 `StoryPlayerView.Update()` 在 `WaitsForTime` 时调用 `Evaluate(deltaTime)`，范围 grep 未发现 `EvaluateMediaTime`。
- [x] wait 到点后视频 command track 保留，普通 choice 或 custom command 同帧出现：runtime tests 覆盖。
- [x] choice 显示通过 interaction channel 的 `Choice` surface，视频仍通过 `Video` surface：playback channel test 覆盖。
- [x] 选择后跳转沿用现有并行选择语义；旧视频不在目标 frame 时停止：presenter test 覆盖。
- [x] 并行等待互动视频不获得 transition seek policy：editor compiler guard 覆盖。

**明确不做逐项核对**：
- [x] 未新增 `TimedChoice`、`StoryInteractionKind.TimedChoice` 或 `EvaluateMediaTime()`：目标代码 grep 无命中。
- [x] 未新增 `StoryRunner.Seek()`、`StoryModule.Seek()`、`playbackRole` 或 `seekable`：目标代码 grep 无命中。
- [x] 未新增 `IStoryInteractionLayer`、`IStoryInteractionChannel` 或 `IStoryPlaybackSurfaceProvider`：目标代码 grep 无命中。
- [x] Runtime/Story 仍不引用 UGUI、AVPro、UIWindow、Editor graph 或播放窗口类型：本 feature 未触碰 runtime UI 依赖边界。
- [x] 未实现 QTE / unlock 玩法，仅验证 `Wait -> custom command` 泛化链路。
- [x] 未做 Story Editor 一键模板或节点面板优化。

**关键决策落地**：
- [x] `Wait(N)` 是视频中途交互表达：测试 fixture 全部使用 `Parallel + Wait + Choice/Command`。
- [x] 视频保持活跃直到 frame 不再包含它：presenter command handle 生命周期测试覆盖。
- [x] 选择出现后仍是普通 `Choice`：测试只检查 `frame.Choices` 和 `Select(choiceId)`。
- [x] 玩家选择默认跳出并行：`Select("choice_continue")` 后进入目标 line，旧视频 handle 被 stop。
- [x] command interaction 只验证通用链路：custom command 使用 declared `success/fail` outcome，不定义 QTE payload。

**编排层“现状 → 变化”逐项核对**：
- [x] Runtime 契约验证已补 `PlayVideo + Wait -> Choice` 和 `PlayVideo + Wait -> Command`。
- [x] Presenter 命令生命周期已补 wait-to-choice 不误停视频、选择后停止旧视频。
- [x] StoryPlayback interaction channel 已补 wait 到点后请求 `Video` + `Choice` surface。
- [x] Compiler / Editor guard 已补并行等待互动不写入 hidden transition policy。
- [x] 范围守护 grep 已执行，未发现方案禁用符号。

**流程级约束核对**：
- [x] choice 显示后不继续自动推进：choice frame `WaitsForTime=false` 且 `WaitsForChoice=true`。
- [x] choice 期间 continue 隐藏：playback channel test 覆盖 choice gate 下 continue 不显示。
- [x] `ChoiceButtons.Count != frame.Choices.Count` 仍报配置错误：既有 playback channel 测试覆盖数量不匹配错误，本 feature 未放宽。
- [x] command interaction 使用 `CompleteCommand(commandId, outcomeId)` 推进，不新增 input state。
- [x] 作者若选择后希望继续视频，仍需目标 frame 显式包含目标视频；本 feature 不引入 sibling video 隐式保留规则。

**挂载点反向核对**：
- [x] `Parallel + Wait + Choice/Command` 编排契约：新增测试 fixture 只挂在 Runtime / Playback / Editor tests。
- [x] `StoryRunner.EvaluateParallel()` 分支时间推进：本 feature 通过 runtime tests 观察，不新增 runner API。
- [x] `StoryPresenter` command handle 保留/停止策略：新增 presenter test 覆盖。
- [x] `StoryPlayerView` wait 推进与 surface request：新增 playback interaction channel test 覆盖。
- [x] compiler seek policy guard：新增 editor compiler test 覆盖。
- [x] 反向 grep：除上述测试与现有能力外，无新增禁用接口或节点。
- [x] 拔除沙盘推演：删除新增测试不影响生产代码；删除既有 parallel/wait 推进会破坏 N1-N7；删除 compiler guard 会允许并行互动视频误获得 seek。

## 3. 验收场景核对

- [x] **N1 初始并行帧**：`StoryProgram_WhenParallelWaitChoiceTriggers_KeepsVideoTrackAndShowsChoice` 断言 video command + wait track。
- [x] **N2 wait 到点出现选项**：同一测试断言 `Evaluate(1.5d)` 后保留 video command 并出现带 branch id 的 choice。
- [x] **N3 choice 期间 UI gate**：`StoryPlayerView_WhenParallelWaitChoicePresented_RequestsVideoAndChoiceSurfaces` 覆盖 choice surface 和按钮绑定；continue gate 保持隐藏。
- [x] **N4 视频 surface 保留**：playback test 覆盖 `Video` surface request；presenter test 覆盖 video handle 不被 wait branch 切帧误停。
- [x] **N5 选择后跳转**：presenter test 覆盖选择后进入目标 frame 且旧视频 handle stop。
- [x] **N6 session time 触发**：代码仍由 `StoryPlayerView.Update()` 调用 `Evaluate(deltaTime)`，grep `EvaluateMediaTime` 无命中。
- [x] **N7 command interaction**：`StoryProgram_WhenParallelWaitCommandTriggers_KeepsVideoTrackAndCompletesInteractionOutcome` 覆盖 custom command 同帧出现和 outcome 推进。
- [x] **N8 并行互动视频不可 seek**：`ProgramCompiler_WhenVideoAndWaitChoiceAreParallel_DoesNotWriteHiddenSeekPolicy` 覆盖。
- [x] **N9 choice button 数量错误**：既有 playback channel 配置错误测试仍保留，本 feature 未修改该路径。
- [x] **B1-B5 范围守护**：禁用符号 grep 无命中，Runtime/Story 隔离边界未扩大。

**验证命令**：
- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore -v minimal`：0 warning / 0 error。
- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore -v minimal`：0 warning / 0 error。
- [x] `rg -n "TimedChoice|EvaluateMediaTime|StoryRunner\.Seek|StoryModule\.Seek|playbackRole|seekable|IStoryInteractionLayer|IStoryInteractionChannel|IStoryPlaybackSurfaceProvider" Assets\GameDeveloperKit\Runtime Assets\GameDeveloperKit\Editor`：无命中。
- [x] `python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-24-story-parallel-wait-interaction-flow/story-parallel-wait-interaction-flow-checklist.yaml`：校验通过。
- [x] `python .codestable/tools/validate-yaml.py --file .codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml`：校验通过。

**未运行项**：
- Unity Test Runner 未运行：当前 shell 没有可用的 Unity batchmode 执行入口；`dotnet test` 对 Unity 生成项目没有提供可观察的 Unity Test Runner 执行报告。当前证据为 dotnet 编译、测试代码覆盖和 grep 范围守卫。

## 4. 术语一致性

- `parallel wait interaction flow`：代码与测试均使用 `Parallel + Wait + Choice/Command`，没有新节点。
- `persistent media branch`：测试使用 `branch_video` + `play_video(waitForCompletion: true)` 表达，frame 仍保留 command track。
- `interaction branch`：测试使用 `branch_interaction` + `Wait -> Choice/Command` 表达，choice 带 branch id。
- `session-time trigger`：无 `EvaluateMediaTime`，wait 由 `Evaluate(deltaTime)` 路径推进。
- `parallel interaction frame`：测试观察同一 frame 内 video command + choices/custom command。
- 禁用词 grep：`TimedChoice` / `EvaluateMediaTime` / `StoryRunner.Seek` / `StoryModule.Seek` / `playbackRole` / `seekable` / `IStoryInteractionLayer` / `IStoryInteractionChannel` / `IStoryPlaybackSurfaceProvider` 在目标代码无命中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已归并视频中途互动现状：`Parallel + Wait + Choice/Command` 是当前闭合路径。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 wait 使用 StoryPlayback session time，`StoryPlayerView.Update()` 通过 `Evaluate(deltaTime)` 推进。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 wait 到点后同一 `StoryFrame` 可同时包含 video command 与普通 choice/custom command，并通过 interaction channel 请求 surface。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录并行等待互动视频不获得 transition seek、不得新增 TimedChoice / media-time API。
- [x] `.codestable/architecture/ARCHITECTURE.md` 变更日志已追加 2026-06-24 记录。

## 6. requirement 回写

- [x] `requirement: story-module` 指向 current req。
- [x] `.codestable/requirements/story-module.md` 已追加 `implemented_by: 2026-06-24-story-parallel-wait-interaction-flow`。
- [x] `.codestable/requirements/story-module.md` 已追加实现进展：媒体播放到指定时机后出现选项/command 的基础路径通过 session-time `Parallel + Wait + Choice/Command` 落地。
- [x] `.codestable/requirements/story-module.md` 已保留 Story 核心边界：不新增 `TimedChoice`、媒体时间触发 API 或剧情级 seek。
- [x] 变更日志已追加 2026-06-24 记录。

## 7. roadmap 回写

- [x] design frontmatter 包含 `roadmap: story-interactive-video` 与 `roadmap_item: story-parallel-wait-interaction-flow`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 对应条目已从 `in-progress` 改为 `done`，feature 仍指向 `2026-06-24-story-parallel-wait-interaction-flow`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 第 5 节对应条目状态已同步为 `done`。
- [x] roadmap 主文档第 6/7/9 节已同步当前进度，items yaml 已校验通过。

## 8. attention.md 候选盘点

- [x] 候选：Unity Test Runner 无法从当前 shell 直接运行；本项目已有 “Unity Editor 实例会阻塞 batchmode” 注意事项，但没有记录 Unity CLI 路径缺失。是否写入 attention.md 需要用户决定。

## 9. 遗留

- 后续优化点：`StoryRunner` 和 `StoryPlayerView` 仍偏胖；design 2.5 已建议后续单独走 `cs-refactor` 拆并行分支计算、默认 UI 构造、surface 请求和媒体刷新。
- 已知限制：未跑 Unity Test Runner；真实 Unity 播放器/AVPro 画面和按钮交互仍需在 Unity Editor 或 Player 环境手动验证。
- 方案外观察：QTE / unlock 的 payload、输入判定、UI 玩法和 Editor 一键模板仍属于后续 feature。
