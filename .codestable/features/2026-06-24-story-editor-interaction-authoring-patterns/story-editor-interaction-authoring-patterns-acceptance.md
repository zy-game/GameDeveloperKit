# Story Editor Interaction Authoring Patterns 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-24
> 关联方案 doc：story-editor-interaction-authoring-patterns-design.md

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] 三个 editor-only 模板 id：`story.pattern.video_wait_choice`、`story.pattern.video_wait_qte`、`story.pattern.video_wait_unlock` 已在 `Assets/GameDeveloperKit/Editor/StoryEditor/StoryEditorGraphAdapter.cs:16` 定义，未进入 runtime schema。
- [x] 节点库展示：`BuildTemplates()` 保持遍历 `NodeSchemaRegistry.Schemas` 的单节点模板路径，并在末尾追加互动模板，见 `StoryEditorGraphAdapter.cs:193` 和 `:223`。
- [x] 模板分派：`CreateNode()` 先识别互动模板 id 并调用 `AddInteractionPatternFromGraph()`，未命中时继续按 `NodeKind` 创建普通节点，见 `StoryEditorGraphAdapter.cs:64`。
- [x] 组合创建入口：新增 `StoryEditorWindow.InteractionPatterns.cs`，`AddInteractionPatternFromGraph()` 根据模板 id 生成现有节点和边，见 `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.InteractionPatterns.cs:12`。
- [x] Wait-owned Choice：`StoryEditorPortPolicy` 与 compiler 的 `CanOwnChoiceItems()` 均包含 `NodeKind.Wait`，见 `StoryEditorGraphAdapter.cs:757` 与 `StoryProgramCompiler.cs:2010`。
- [x] compiler synthetic choice：`BuildOwnedChoiceStep()` 泛化到 owner 节点，`BuildWaitStep()` 在 Wait 拥有 Choice item 时指向 `{waitId}_choices`，见 `StoryProgramCompiler.cs:407` 与 `:588`。
- [x] seek 推导诊断：`StoryEditorDiagnostics.FromCompiledProgram()` 基于编译产物追加 `Info` 诊断，见 `StoryEditorGraphAdapter.cs:1019` 和 `:1091`。

**名词层“现状 -> 变化”逐项核对**：
- [x] 互动编排模板：实现为 editor-only template，不新增 `NodeKind` 或 runtime step。
- [x] Wait-owned choice items：Wait 可作为 Choice item owner，运行时仍是普通 `Wait` step + synthetic `Choice` step。
- [x] Video interaction pattern：模板生成 `Parallel + PlayVideo + Wait + Choice/QTE/Unlock` 现有节点组合。
- [x] seek 推导诊断：只读 `transition/disabled` 信息来自 compiler，不写回作者字段。
- [x] branch interaction video：并行互动模板内 PlayVideo 不获得 `__videoSeekPolicy=transition`。

**流程图核对**：
- [x] 节点库选择模板 -> template id 分派 -> 生成现有节点组合 -> 作者补 clip -> compiler 生成 `StoryProgram` -> seek info 诊断：源码和测试均有落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 节点库出现三类组合模板：`StoryEditorTests.cs:1739` 覆盖三个 template id 和显示名。
- [x] 模板只生成现有节点和连线：`StoryEditorWindow.InteractionPatterns.cs:53`、`:80`、`:107` 分别创建 Choice/QTE/Unlock 组合，节点类型均来自现有 `NodeKind`。
- [x] `Wait.completed` 可连接多个 Choice item：端口策略允许 Wait owner，compiler 编译为 synthetic choice，测试见 `StoryEditorTests.cs:704`。
- [x] 模板中的 PlayVideo 位于 Parallel 分支内，编译后不写 `transition` policy：测试见 `StoryEditorTests.cs:474`、`:1746`、`:1793`、`:1833`。
- [x] 线性纯过渡 PlayVideo 仍由 compiler 写入内部 transition policy：测试见 `StoryEditorTests.cs:434`。
- [x] PlayVideo schema 不暴露 `playbackRole` / `seekable`：测试见 `StoryEditorTests.cs:119`。

**明确不做逐项核对**：
- [x] 未新增 `TimedChoice`、`EvaluateMediaTime`、`StoryRunner.Seek`、`StoryModule.Seek`、layout slot、anchor 或 `StoryPresentationAnchorPreset`：范围 grep 无源码命中。
- [x] 未新增 `playbackRole` / `seekable` 作者字段：源码 grep 只命中测试里的不得包含断言。
- [x] 未新增 runtime interaction channel、surface 接口或 runtime UI 依赖：本 feature 改动落在 Editor compiler/window/diagnostics 与 tests。
- [x] 未实现 `submit_choice`、单选提交或多选提交 command：范围 grep 无命中。

**关键决策落地**：
- [x] 组合模板不进入 runtime：模板 id 位于 Editor adapter，创建后资源只保留普通节点和边。
- [x] `Wait` 成为 Choice item owner：端口策略、图上混连诊断和 compiler 均已接入。
- [x] 默认值只填结构必需字段：模板给 PlayVideo/Wait/QTE/Unlock 设置可编辑安全默认值，clip 仍由作者补齐。
- [x] seek 诊断只读：诊断由 `m_LastCompiledProgram` 生成，图修改时清空旧编译产物，见 `StoryEditorWindow.cs:354` 与 `:481`。
- [x] 不混入提交式选择：未新增 submit choice 实现。

**挂载点反向核对**：
- [x] 模板 id / 展示：`StoryEditorGraphAdapter.cs`。
- [x] 模板创建入口：`StoryEditorWindow.InteractionPatterns.cs`。
- [x] Wait-owned Choice 端口/编译规则：`StoryEditorGraphAdapter.cs` + `StoryProgramCompiler.cs`。
- [x] seek policy Info 诊断：`StoryEditorGraphAdapter.cs` + `StoryEditorWindow.cs` + `.uss` info 样式。
- [x] tests：`Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs`。
- [x] 拔除沙盘推演：按以上挂载点逆向删除可回到手动搭图能力；旧图资源和 runtime program 不依赖 template id。

## 3. 验收场景核对

- [x] **N1 模板可见**：`StoryEditorTests.cs:1739` 断言三个模板存在。
- [x] **N2 普通模板兼容**：`StoryEditorGraphAdapter.CreateNode()` 未命中 pattern 时仍走 `NodeKind` 单节点路径；测试同时确认 PlayVideo 单模板仍存在。
- [x] **N3 视频中途选项模板**：`StoryEditorTests.cs:1746` 断言生成 Parallel、PlayVideo、Wait、两个 Choice item、两个 target 和 branch/completed 边。
- [x] **N4 Wait 多选项**：`StoryEditorTests.cs:704` 覆盖 Wait-owned Choice synthetic step 与运行时 frame choice。
- [x] **N5 Wait 混连错误**：`StoryEditorTests.cs:741` 和 `:1570` 覆盖 compiler 与图上定位错误。
- [x] **N6 QTE 模板**：`StoryEditorTests.cs:1793` 覆盖 `qte` command 和 success/fail target。
- [x] **N7 Unlock 模板**：`StoryEditorTests.cs:1833` 覆盖 `unlock` command 和 success/fail target。
- [x] **N8 并行互动不可 seek**：`StoryEditorTests.cs:474` 以及三类模板编译测试确认不写 `__videoSeekPolicy=transition`。
- [x] **N9 纯过渡仍可 seek**：`StoryEditorTests.cs:434` 覆盖线性 PlayVideo 写入 transition policy。
- [x] **N10 seek 诊断**：`StoryEditorTests.cs:519` 覆盖编译成功后的 transition / disabled Info 诊断。
- [x] **N11 播放窗口兼容**：`StoryEditorTests.cs:584` 覆盖 disabled 视频隐藏 slider；既有 transition slider 测试覆盖显示路径。
- [x] **N12 stale 行为**：`StoryEditorTests.cs:543` 覆盖图修改后清空旧 seek policy 诊断。
- [x] **B1-B6 范围守护**：grep 无范围外源码命中；`playbackRole` / `seekable` 只出现在测试断言。

验证命令：

```powershell
dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore
python .codestable/tools/validate-yaml.py --file .codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml
python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-24-story-editor-interaction-authoring-patterns/story-editor-interaction-authoring-patterns-checklist.yaml
rg -n "TimedChoice|EvaluateMediaTime|StoryRunner\.Seek|StoryModule\.Seek|StoryPresentationAnchorPreset|PresentationAnchor|LayoutSlot|layout slot|submit_choice" Assets/GameDeveloperKit/Editor/StoryEditor Assets/GameDeveloperKit/Runtime/Story Assets/GameDeveloperKit/Runtime/StoryPlayback Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs -g "*.cs"
rg -n "playbackRole|seekable" Assets/GameDeveloperKit/Editor/StoryEditor Assets/GameDeveloperKit/Runtime/Story Assets/GameDeveloperKit/Runtime/StoryPlayback Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs -g "*.cs"
```

结果：`dotnet build` 通过，0 warning / 0 error；两个 YAML 文件校验通过；范围 grep 符合预期。未运行 Unity Test Runner，原因是当前环境只做了 .NET 项目构建验证，Unity Editor 内的 Test Runner 仍需人工或 batchmode 单独跑。

## 4. 术语一致性

- `story.pattern.video_wait_choice/qte/unlock`：只在 Editor adapter、Editor window partial 和 Editor tests 中使用，一致。
- Wait-owned Choice：代码用现有 Choice item owner 模型，没有引入 `TimedChoice`。
- seek 推导诊断：代码使用 `__videoSeekPolicy` 内部编译产物参数与 `Info` 诊断，不暴露 `playbackRole` / `seekable`。
- branch interaction video：通过 Parallel / Choice/QTE/Unlock 图结构让 compiler 禁用 transition policy，不新增作者字段。
- 防冲突 grep：范围外术语无源码命中；`playbackRole` / `seekable` 仅作为测试守护出现。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已写入 Story Editor 组合互动模板现状：三个 editor-only template id 只生成现有节点组合，不进入 runtime。
- [x] 已写入 Wait-owned Choice 编译契约：`Wait.completed` 可拥有多个 Choice item，合成 `{ownerNodeId}_choices`，混连普通目标是错误。
- [x] 已写入 seek Info 诊断现状：编译成功后 PlayVideo 显示 `transition/disabled` 只读信息，图修改后清空旧推导。
- [x] 已补边界与变更日志：明确不新增 template runtime step、TimedChoice、layout slot、anchor、`playbackRole`、`seekable`。

## 6. requirement 回写

- [x] frontmatter 指向 `requirement: story-editor`，已更新 `.codestable/requirements/story-editor.md`。
- [x] `implemented_by` 已追加 `2026-06-24-story-editor-interaction-authoring-patterns`。
- [x] 实现进展已追加 2026-06-24 条目，记录互动模板、Wait-owned Choice 和只读 seek policy 诊断。
- [x] requirement 仍保持 `draft`：本 feature 完成的是互动影游作者模板，不代表 story-editor 的导入导出等完整愿景已全部 current。
- [x] `.codestable/requirements/VISION.md` 无需移动分组，`story-editor` 仍在 Draft。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-items.yaml` 中 `story-editor-interaction-authoring-patterns` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-interactive-video/story-interactive-video-roadmap.md` 第 5 节对应条目已改为 `done`，并写入 feature 目录。
- [x] roadmap 排期和观察项已更新：作者模板已完成，后续只剩样例验收。
- [x] YAML 校验通过：`validate-yaml.py --file ...story-interactive-video-items.yaml`。

## 8. attention.md 候选盘点

- [x] 无候选：本 feature 未暴露每个后续 feature 都必须提前知道的新环境 / 工具 / 工作流信息。

## 9. 遗留

- 后续优化点：`StoryEditorGraphAdapter.cs` 同时承载 adapter、port policy、diagnostics，后续继续增加编辑器能力时建议另走 `cs-refactor` 拆分。
- 已知限制：提交式单选/多选不属于本 feature；需要继续按 `story-submit-choice-command` 或后续 roadmap update 推进。
- 验证缺口：Unity Test Runner 未在本次命令行执行；当前已完成 .NET Editor Tests 项目构建与范围 grep。
- 工作流状态：等待用户终审确认。
