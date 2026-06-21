---
doc_type: feature-acceptance
feature: 2026-06-20-story-graph-port-policy
requirement: story-editor
roadmap: story-editor-hardening
roadmap_item: story-graph-port-policy
status: accepted
accepted_at: 2026-06-20
summary: Story Editor 节点端口语义已收紧，Story 专有连接策略、写回容量和 runtime validation 兜底已落地并通过构建验证。
tags: [story, editor, node-graph, ports, validation]
---

# Story Graph Port Policy 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-story-graph-port-policy/story-graph-port-policy-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `StoryConnectionPolicy.Evaluate` 对外表现：`StoryEditorGraphAdapter.CanConnect()` 调用 `StoryEditorPortPolicy.CanConnect()`，返回 `EditorGraphConnectionResult`，失败信息为中文。
- [x] `IsMultipleOutput(node, portId, targetNode)` 对外表现：`StoryEditorWindow.IsMultipleOutputPort(node, portId, targetNode)` 与 `StoryAuthoringAsset.EnsureSemanticEdgeCardinality()` 均支持文本 completed 到 Choice 多连，其余端口按 schema。
- [x] `HasDeclaredOutput(nodeKind, portId)` 对外表现：`StoryEditorPortPolicy.HasDeclaredOutputPort()` 与 runtime `StoryModule.HasOutputPort()` 都只接受 schema 声明的输出端口。

**名词层“现状 → 变化”逐项核对**

- [x] Story port policy：已落在 `StoryEditorPortPolicy`，未进入通用 `EditorNodeGraphKit`。
- [x] Port role / Port capacity：方向仍由 `EditorGraphPortDirection` 表达，Story 语义由 policy 判断，容量由 schema + line-to-choice 特判共同决定。
- [x] Semantic connection result：拖线失败通过 `EditorGraphConnectionResult.Fail(message)` 返回中文原因。
- [x] Line node / Choice item node：V4 fixture 改为 `line_intro.completed -> choice.selected`，测试覆盖文本到 Choice 多连与模式互斥。
- [x] Command outcome port：本 feature 仅约束 schema outcome 合法性，未做字段类型化。
- [x] Auxiliary node：policy 禁止辅助节点作为 runtime flow 来源或目标。

**流程图核对**

- [x] `Drag -> Adapter -> Policy -> Capacity -> Write -> Refresh -> Validate` 均有落点：`EditorNodeGraphCanvas.ConnectPorts()`、`StoryEditorGraphAdapter.CanConnect()`、`StoryEditorPortPolicy`、`StoryEditorWindow.AddEdgeToChapter()`、`RefreshAll()`、`StoryModule.ValidateEdges()`。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] 开始/结束节点端口规则稳定：adapter/build ports 和窗口删除保护已覆盖。
- [x] 文本、选项、命令、条件、跳转/等待、辅助节点的连接规则可表格化：schema 输出 + `StoryEditorPortPolicy` 固定判断。
- [x] 单连端口不会留下多条同源同端口边：`AddEdgeToChapter()` 单连替换，runtime validation 兜底重复单输出。
- [x] 非法连接给中文原因：policy 覆盖开始、结束、辅助、Choice 来源、Choice 输出、未知端口、重复边、文本选项模式互斥。
- [x] Story 专有规则不下沉到 `EditorNodeGraphKit`：grep 仅在 Story adapter/window/tests 命中 Story 类型；NodeGraphKit 未引用 `NodeKind`。

**明确不做逐项核对**

- [x] 未改 Choice 分支合成 step 的完整编译契约，旧 compiler 兼容测试仍保留；正式契约留给 `choice-item-branching-contract`。
- [x] 未新增命令字段 ObjectField、资源 GUID 导出或 typed argument schema。
- [x] 未新增图上红点、错误 badge、validation overlay。
- [x] 未引入官方 Graph Toolkit，也未把 `EditorNodeGraphKit` 改成 Story 专用。
- [x] 未恢复 `unit`、`payload`、owner action/transition 为作者主界面概念。

**关键决策落地**

- [x] D1 Story policy 集中在 Story 语义适配层：`StoryEditorPortPolicy` 位于 StoryEditor adapter 文件内，供 adapter/window 共享。
- [x] D2 单连端口替换旧目标：`StoryEditorWindow.AddEdgeToChapter()` 对非多连端口移除旧同源端口边。
- [x] D3 多连只给明确语义：schema `Multiple` 和文本 completed 到 Choice 是唯一多连来源。
- [x] D4 辅助节点不参与 runtime-required flow：policy 的 `IsAuxiliaryNode()` 禁止来源和目标；runtime validation 仍拒绝 editor-only 节点。

**挂载点反向核对**

- [x] `StoryEditorGraphAdapter.CanConnect()`：graph 拖线语义入口，已调用 policy。
- [x] `StoryEditorWindow` 连线写回容量判断：`AddEdgeToChapter()` 与 `IsMultipleOutputPort()` 复用 policy。
- [x] `NodeSchemaRegistry`：仍是节点端口 schema 来源。
- [x] `StoryModule.ValidateDefinition()` / `ValidateEdges()`：runtime 导入兜底，拒绝未知端口、重复单输出、editor-only 节点。
- [x] 反向 grep：本 feature 新增引用集中在上述挂载点和 tests；无清单外业务挂载点。
- [x] 拔除沙盘推演：移除 adapter policy 会导致拖线规则失效；移除 window 写回会导致 authoring 边容量漂移；移除 runtime validation 会让外部非法 Definition 绕过编辑器。

## 3. 验收场景核对

- [x] N1 Start 无输入、有 `completed` 输出、不在 palette、不可删除：`WindowV4Graph_WhenChapterOpened_KeepsStartEndFixed` + graph fixture 断言。
- [x] N2 连到 Start 失败：`StoryEditorPortPolicy.CanConnect()` 明确返回 `开始节点不能作为目标。`。
- [x] N3 End 无输出：adapter port 渲染跳过 End 输出，policy 从 End 拖出返回 `结束节点没有输出端口。`，`WindowV4GraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason` 覆盖。
- [x] N4 End 可作为目标：policy 未禁止 target End，测试覆盖 `Choice.selected -> End` 可连接。
- [x] N5 文本 completed 连普通节点允许且单连替换：policy 按普通 schema 允许，单连替换由 `AddEdgeToChapter()` 覆盖。
- [x] N6 文本 completed 连多个 Choice 允许，从直连切换会移除旧直连：`AuthoringAsset_WhenLineCompletedTargetsChoices_KeepsChoiceEdges` 和 `WindowV4Graph_WhenLineSwitchesToChoiceMode_RemovesDirectEdgeAndRejectsOrdinaryTarget`。
- [x] N7 非文本节点连到 Choice 失败：`WindowV4GraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason` 覆盖 video.completed -> Choice。
- [x] N8 文本非 completed 端口连到 Choice 失败：policy 要求 `outputPortId == completed`。
- [x] N9 Choice.selected 连普通节点或 End 允许，第二次替换旧目标：policy 允许 selected，window 单连替换覆盖。
- [x] N10 Choice 非 selected 输出失败：`WindowV4GraphAdapter_WhenPortsAreInvalid_ReturnsChineseReason` 覆盖 `choice.help`。
- [x] N11 命令/动作节点 schema outcome 允许，未知端口失败：schema ports + `Register_WhenOutputPortIsUnknown_ThrowsWithNodeEdgeAndPort`。
- [x] N12 条件节点 schema outcome 允许，未知端口失败：schema ports + 同一 runtime 端口声明校验兜底。
- [x] N13 辅助节点参与 runtime flow 失败：policy 禁止辅助来源/目标，runtime `Register_WhenEditorOnlyNodeIsUsed_ThrowsWithNodeAndKind` 兜底。
- [x] N14 单连端口重复连接完全相同目标不新增重复 edge：policy 返回 `这条连线已经存在。`，runtime duplicate single output 测试兜底。
- [x] N15 文本 completed 已连接 Choice 后再直连普通节点失败：`WindowV4Graph_WhenLineSwitchesToChoiceMode_RemovesDirectEdgeAndRejectsOrdinaryTarget` 覆盖。
- [x] N16 单连端口改连不同目标只保留新边：旧 GraphView 单连替换测试与 V4 window 写回逻辑覆盖。
- [x] N17 runtime definition 含未知输出端口失败且含定位：`Register_WhenOutputPortIsUnknown_ThrowsWithNodeEdgeAndPort`。
- [x] N18 runtime definition 含 editor-only 节点失败且含定位：`Register_WhenEditorOnlyNodeIsUsed_ThrowsWithNodeAndKind`。

自动验证：

- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过，保留既有 obsolete/unused event warnings。
- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过，0 warning。
- [x] YAML 校验通过：feature checklist 与 roadmap items。
- [x] `git diff --check` 通过；未跟踪 Story 文件额外扫描行尾空白无命中。
- [x] Unity Test Runner 尝试执行 EditMode 测试，但当前已有 `E:\Black Rain` Unity Editor 实例打开，Unity 报 `Multiple Unity instances cannot open the same project`，未生成 test result XML；本次验收以构建和测试源码证据为自动验证依据。

## 4. 术语一致性

- Story port policy：代码使用 `StoryEditorPortPolicy`，命中位置在 Story adapter/window。
- Port role / Port capacity：通用 graph 仍保留 `EditorGraphPortDirection` / `EditorGraphPortCapacity`，Story role 未下沉。
- Semantic connection result：代码以 `StoryEditorPortPolicyResult` 内部结果转 `EditorGraphConnectionResult`。
- Line node / Choice item node：代码以 `Dialogue` / `Narration` 和 `Choice` 判断，测试 fixture 使用 `line_intro` 与 `choice`。
- Command outcome port：未新增字段类型化命名。
- Auxiliary node：通过 `NodeCategory.Auxiliary` 判定。

防冲突 grep 结论：`ObjectField` / `VideoClip` 未在本 feature 代码中新增；`EditorNodeGraphKit` 未引入 `NodeKind`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已在 Story Editor / Editor Node Graph 章节补充 `StoryEditorPortPolicy`、Start/End/Text/Choice/Command/Condition/Auxiliary 连接规则、写回容量语义和 runtime validation 兜底。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已在硬边界补充 Story 端口策略不得下沉到 `EditorNodeGraphKit` 的约束。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已在变更日志记录 2026-06-20 Story 端口策略现状。

## 6. requirement 回写

- [x] `.codestable/requirements/story-editor.md`：已加入 `implemented_by: 2026-06-20-story-graph-port-policy`。
- [x] `.codestable/requirements/story-editor.md`：已追加实现进展，说明端口语义收紧已完成，但选项分支契约、命令字段类型化和图上校验反馈仍未完成，因此 requirement 保持 `draft`。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml`：`story-graph-port-policy` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md`：第 5 节子 feature 清单中 `story-graph-port-policy` 已改为 `done`。
- [x] roadmap items YAML 已通过 `.codestable/tools/validate-yaml.py --yaml-only`。

## 8. attention.md 候选盘点

- 候选 1：Unity Test Runner batchmode 不能在同一项目已有 Unity Editor 实例打开时运行；需要先关闭当前项目 Editor，或改用现有 Editor 内 Test Runner。

## 9. 遗留

- `choice-item-branching-contract`：下一步优先，固化文本 completed 连接多个 Choice item 后的编译和 runtime `Select(choiceId)` 契约。
- `typed-command-fields`：命令节点字段类型化、资源/对象字段和 runtime 稳定参数导出仍未做。
- `story-graph-validation-feedback`：图上校验 badge / overlay / 定位反馈仍未做。
- `StoryEditorWindow.cs` 仍偏大，后续可拆 story tree、toolbar、asset commands 和 graph workspace；本 feature 未做结构扩大。
