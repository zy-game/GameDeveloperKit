---
doc_type: feature-acceptance
feature: 2026-06-20-choice-item-branching-contract
requirement: story-editor
roadmap: story-editor-hardening
roadmap_item: choice-item-branching-contract
status: accepted
accepted_at: 2026-06-20
summary: Story Editor 的 Choice item 分支契约已落地，编译器会合成 runtime Choice step，运行时通过 ChoicesReady 和 Select(choiceId) 推进。
tags: [story, editor, compiler, runtime, choice]
---

# Choice Item Branching Contract 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-choice-item-branching-contract/choice-item-branching-contract-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] 作者图示例 `Dialogue.completed -> Choice(textKey).selected -> Branch` 已落在 compiler 测试 fixture：`ProgramCompiler_WhenLineConnectsChoiceItemNodes_BuildsChoiceStepAfterLine`。
- [x] 编译结果示例已落地：line step 后追加 synthetic `StoryStepKind.Choice`，`StoryChoice.ChoiceId` 来自 choice item node id，`TextKey` 来自 `textKey` 参数，`Target` 来自 `selected` 边。
- [x] 运行时输出示例已落地：`StoryOutput.CreateChoice()` 返回 `StoryOutputKind.ChoicesReady`，`StoryModule.Select(choiceId)` 继续推进分支。

**名词层“现状 → 变化”逐项核对**

- [x] Choice item node：Story Editor 作者图中仍使用 `NodeKind.Choice`，但在 line completed 后只表示单个玩家可选项。
- [x] Synthetic choice step：`StoryProgramCompiler.BuildLineChoiceStep()` 为 line node 生成 `{lineNodeId}_choices`，并用稳定后缀处理冲突。
- [x] ChoicesReady output：新增 `StoryOutputKind.ChoicesReady`，旧 `Choice` 保留 obsolete alias 作为过渡。
- [x] Select contract：Runtime 测试覆盖有效 choice、缺失 choice、非选择状态调用和 history 记录。
- [x] Choice condition：line->choice 条件与 choice.selected 条件通过 `CombineConditions()` 按 AND 合并。

**流程图核对**

- [x] `Author -> Compile -> Detect -> Skip -> Synthesize -> Run -> Output -> Select -> Target` 均有代码落点：`StoryEditorPortPolicy`、`StoryProgramCompiler.Choice.cs`、`StoryOutput.CreateChoice()`、`StoryModule.Select()` / runner 测试。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] 一个 Choice 节点只代表一个玩家可选项：compiler 把 line 指向的 Choice 节点识别为 choice item，不让它们作为独立 step。
- [x] 编译器把多个 choice item 合成为一个 stable choice step：`BuildLineChoiceStep()` 使用 line outgoing edge 顺序生成 choices。
- [x] 字段映射正确：`ChoiceId = optionNodeId`，`TextKey = textKey`，`Target = selected target`，tags 从 choice item 参数读取。
- [x] runtime 在文本 Continue 后输出 `ChoicesReady`：runtime 测试断言 `StoryOutputKind.ChoicesReady`。
- [x] 旧式独立 Choice step 仅保留兼容编译路径：`BuildChoiceStep()` 仍存在，但 Story Editor 主 fixture 使用 choice item 语义。

**明确不做逐项核对**

- [x] 未做命令字段 ObjectField、资源 GUID 导出或 command argument schema。
- [x] 未做 graph validation overlay、字段红框或错误 badge。
- [x] 未扩展复杂表达式编辑器，只合并现有边条件和 selected 条件。
- [x] 未引入 Yarn Spinner / Ink runtime，也未引入 Unity 官方 Graph Toolkit。
- [x] 未恢复 `unit`、`payload`、owner action/transition 为作者主界面概念。

**关键决策落地**

- [x] D1 `NodeKind.Choice` 是“选项项节点”：`BuildChoiceItemNodeIds()` 从 line completed 边识别并跳过独立 step 编译。
- [x] D2 line node 触发一次玩家选择：line step 后追加 synthetic choice step，普通直连与 choice item 模式混合时 compiler 报错。
- [x] D3 `ChoicesReady` 是 UI 面向语义：`CreateChoice()` 返回 `ChoicesReady`，`Choice` 成员只作为 obsolete alias。
- [x] D4 `textKey` 必须稳定：缺失 `textKey` 时 compiler 产生 warning，并仅用 title / node id 作为兼容 fallback。

**挂载点反向核对**

- [x] `StoryProgramCompiler` choice helper 已拆到 `StoryProgramCompiler.Choice.cs`，choice item 识别、selected 单目标、条件合并、synthetic id 都集中在这里。
- [x] `StoryOutputKind.ChoicesReady` / `StoryOutput.CreateChoice()` 是 runtime 输出挂载点。
- [x] `StoryModule.Select(choiceId)` / runner 选择推进由 runtime 测试覆盖。
- [x] `StoryEditorPortPolicy` 继续负责 line-to-choice 输入约束，compiler 仍兜底混合连接错误。
- [x] 反向 grep：Runtime Story 目录未引用 `EditorNodeGraph`、`UnityEditor.Experimental.GraphView` 或 editor graph 类型。
- [x] 拔除沙盘推演：移除 compiler choice helper 会丢失 choice item 合成；移除 `ChoicesReady` 会让 UI 输出语义退回混淆命名；移除 `Select(choiceId)` 测试会让玩家选择推进缺少回归保护。

## 3. 验收场景核对

- [x] N1 Line.completed 连接两个 Choice item 后生成 line step 和 `{lineNodeId}_choices`：editor compiler 测试覆盖。
- [x] N2 choice item node id 不作为独立 step 出现：editor compiler 测试覆盖。
- [x] N3 choices 字段映射 node id、textKey、selected target：editor compiler 测试覆盖。
- [x] N4 line->choice 条件与 choice.selected 条件合并为 AND：runtime 条件过滤测试覆盖。
- [x] N5 choice item 没有 selected 输出时报错并定位 node：editor compiler 测试覆盖。
- [x] N6 choice item 有多个 selected 输出时报错并定位 node：editor compiler 测试覆盖。
- [x] N7 line completed 同时连 Choice item 和普通目标时报错并定位 completed 端口：compiler 兜底逻辑保留。
- [x] N8 choice item 缺 textKey 时 warning 包含 node id，并使用兼容 fallback：editor compiler 测试覆盖。
- [x] N9 synthetic step id 冲突时生成稳定后缀：editor compiler 测试覆盖。
- [x] N10 runner Continue 通过 line 后输出 `StoryOutputKind.ChoicesReady`：runtime 测试覆盖。
- [x] N11 `Select(validChoiceId)` 跳到 target 并记录 history：runtime 测试覆盖。
- [x] N12 `Select` 不存在 choice id 抛 `GameException` 且包含 story/chapter/step/choice：runtime 测试覆盖。
- [x] N13 非选择等待状态调用 `Select(choiceId)` 抛当前没有激活选择错误：runtime 测试覆盖。
- [x] N14 choice condition 为 false 时过滤；全部不可用时抛无可用选项错误：runtime 测试覆盖。
- [x] N15 Story Editor fixture 使用 line-to-choice-item-to-selected-target，并为新 Choice 节点填入非空 `textKey`：editor 测试覆盖。

自动验证：

- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。
- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过；仅有既有 obsolete / unused warnings。
- [x] scoped `git diff --check` 通过。
- [x] feature checklist 与 roadmap items YAML 校验通过。

验证限制：

- [x] 未通过命令行 Unity Test Runner 跑 EditMode XML；当前验收使用 Editor.Tests / Runtime.Tests 构建和测试源码证据。早前 Unity batchmode 刷新会被已打开的 Unity Editor 实例拦截。
- [x] 全仓 `git diff --check` 会被既有无关文件尾随空格阻塞：`Assets/GameDeveloperKit/Simples/UI/Test.prefab`、`UserSettings/Layouts/default-2022.dwlt`。

## 4. 术语一致性

- Choice item node：文档称“选项项节点”，代码仍以 `NodeKind.Choice` 表示作者图节点类型，语义由 compiler/policy 决定。
- Synthetic choice step：代码使用 `BuildLineChoiceStep()` 与 `MakeSyntheticChoiceStepId()`，和设计命名一致。
- ChoicesReady output：runtime enum、factory、测试均使用 `ChoicesReady`，旧 `Choice` 只作 obsolete alias。
- Select contract：runtime API 仍是 `Select(choiceId)`，未让 UI 直接执行 target。
- 禁用词 / 边界 grep：本 feature 未新增 `ObjectField` / `VideoClip` 字段类型化；Runtime 未引用 editor graph/UI Toolkit/GraphView。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已在 Story Editor / Editor Node Graph 章节补充 `StoryProgramCompiler`、Choice item 合成 synthetic `StoryStepKind.Choice`、`ChoicesReady -> Select(choiceId)` 运行时推进。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已在硬边界补充 Story 选项推进边界，明确表现层不得直接读取 editor graph 边或自行跳转 target。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已在变更日志记录 2026-06-20 Story 选项项分支契约现状。

## 6. requirement 回写

- [x] `.codestable/requirements/story-editor.md`：已加入 `implemented_by: 2026-06-20-choice-item-branching-contract`。
- [x] `.codestable/requirements/story-editor.md`：已追加实现进展，说明选项项分支契约已完成。
- [x] requirement 保持 `draft`：命令字段类型化、图上校验反馈和示例剧情 fixture 仍未完成。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml`：`choice-item-branching-contract` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md`：第 5 节子 feature 清单中 `choice-item-branching-contract` 已改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md`：第 6 节排期建议已切到下一项 `typed-command-fields`。

## 8. attention.md 候选盘点

- 候选 1：Unity 生成的 `.csproj` 可能不会立刻包含新建 Editor partial 文件；项目已打开时 batchmode 刷新会失败。若后续 feature 也频繁新建 Editor 文件，建议通过现有 Editor 刷新项目文件或关闭 Editor 后再跑 batchmode。

## 9. 遗留

- `typed-command-fields`：下一步优先，命令节点字段类型化、资源/对象字段和稳定 runtime 参数导出仍未做。
- `story-graph-validation-feedback`：图上缺字段、断边、非法连接、未知命令等中文定位反馈仍未做。
- `sample-story-graph-fixture`：覆盖多章节、对白、旁白、选项、命令和分支的示例剧情图仍未做。
- `StoryProgramCompiler` 仍可继续按 command / condition / target 等职责拆 partial；本 feature 只拆 choice 相关逻辑。
