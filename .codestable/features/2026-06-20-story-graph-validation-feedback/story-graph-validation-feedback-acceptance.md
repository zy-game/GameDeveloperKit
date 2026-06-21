# Story Graph 校验反馈验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-story-graph-validation-feedback/story-graph-validation-feedback-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `EditorGraphDiagnostic`：已在 `EditorNodeGraphModels.cs` 落地，包含 `DiagnosticId`、`Severity`、`TargetKind`、`NodeId`、`FieldId`、`PortId`、`WireId`、`Message`、`Tooltip`、`Stale`；target kind 只包含 `Graph`、`Node`、`Field`、`Port`、`Wire`。
- [x] graph model 挂载：`EditorGraphNodeModel`、`EditorGraphFieldModel`、`EditorGraphPortModel`、`EditorGraphWireModel` 均可携带 diagnostics，`EditorNodeGraphNodeView` 和 `EditorNodeGraphWireLayer` 消费它们渲染样式和 tooltip。
- [x] Story source 映射：`StoryEditorDiagnostics` 解析 `story:/chapter:/node:/field:/port:/edge:` source，映射到 field、port、wire、node 或 graph；source 同时带 edge 和 port 时优先 wire，指定 edge 不可见时回退到 port，不借用其它 wire。

**名词层“现状 -> 变化”逐项核对**：
- [x] `StoryValidationReport` 仍保持原有 `Severity/Source/Message` 契约，未破坏旧调用方。
- [x] `StoryEditorWindow` 的左侧问题列表不再只显示 `issue.ToString()`，而是使用 `StoryEditorDiagnosticItem.SummaryText` 中文摘要，并可点击定位。
- [x] `EditorNodeGraphKit` 新增通用 diagnostics 能力，但不理解 Story source。
- [x] Story-specific source parser 与中文摘要集中在 Story helper，窗口不直接解析 source 字符串。

**流程图核对**：
- [x] 作者编辑字段或连线会触发轻量 authoring 校验与 stale 标记：`MarkDirty()` -> `MarkGraphChanged()`，`RefreshDiagnostics()` 合并本地和 compiler diagnostics。
- [x] 编译按钮仍走 `StoryProgramCompiler.Compile()`，正式 `StoryValidationReport` 再投影到 graph 和 summary。
- [x] summary 点击调用 `FocusDiagnostic()`，可选择 node 或 wire。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 缺必填字段、字段类型错误、手填资源 key warning 可显示到节点内字段。
- [x] Choice selected 缺目标、文本 completed 混连、未知输出端口、辅助节点进入 runtime flow、缺目标节点/章节可定位到端口、连线、节点或 summary。
- [x] 编译失败和迁移 warning 仍走左侧问题列表，并通过 Story 展示层中文化常见错误。
- [x] 点击 summary 能定位 node/wire；field/port issue 选中对应节点并保留字段/端口高亮。
- [x] GraphKit 保持业务无关；Runtime 未引入 editor graph、UI Toolkit graph 或诊断模型。

**明确不做逐项核对**：
- [x] 未重新设计端口连接策略，仍复用 `StoryEditorPortPolicy`。
- [x] 未改变 Choice item 合成 runtime choice step 的语义。
- [x] 未扩展命令字段 schema 或资源 `ObjectField` 契约。
- [x] 未增加表达式编辑器、变量黑板校验、运行时断点调试或自动修复。
- [x] 未把 `unit`、`payload`、owner action/transition 恢复成主 graph 或 summary 概念。
- [x] 未切换官方 Graph Toolkit。

**关键决策落地**：
- [x] D1 GraphKit 只接收业务无关 diagnostics：grep `Assets/GameDeveloperKit/Editor/NodeGraph` 未命中 Story/NodeKind/GraphView 专有引用。
- [x] D2 Story helper 是 source mapping 入口：source 解析和中文摘要位于 `StoryEditorDiagnostics`。
- [x] D3 校验分两层：`BuildLocal()` 做当前章节即时校验，`FromReport()` 投影 compiler report。
- [x] D4 中文摘要统一由 Story 展示层整理，原始 source/message 保留在 tooltip。

**流程级约束核对**：
- [x] 作者可见主文案是中文；原始 source/message 在 tooltip。
- [x] 定位优先级已覆盖：field、port、wire、node、chapter、graph；edge+port 优先 wire，指定坏 edge 不可见时回退 port。
- [x] 重复校验通过稳定 key 去重，不累积重复 diagnostics。
- [x] 轻量校验只遍历当前 asset/chapter 和 stable value，不加载资源。
- [x] Runtime 不依赖 editor 诊断模型。

**挂载点反向核对**：
- [x] `EditorNodeGraphKit` 挂载点：`EditorNodeGraphModels.cs`、`EditorNodeGraphNodeView.cs`、`EditorNodeGraphWireLayer.cs`、`EditorNodeGraph.uss`。
- [x] Story 映射挂载点：`StoryEditorDiagnostics` 位于 `StoryEditorGraphAdapter.cs`。
- [x] Window 交互挂载点：`StoryEditorWindow.RefreshDiagnostics()`、`FocusDiagnostic()`、`MarkGraphChanged()`、summary 渲染。
- [x] 正式问题来源：`StoryProgramCompiler` / `StoryValidationReport` 未改契约。
- [x] 反向 grep：本 feature 相关引用均落在上述挂载点；GraphKit 无 Story 专有引用，Runtime 无 editor graph 引用。
- [x] 拔除推演：移除 GraphKit diagnostics 会失去通用图上样式；移除 Story diagnostics helper 会失去 source 映射和本地校验；移除 Window summary 逻辑会失去点击定位和 stale 提示。

## 3. 验收场景核对

- [x] **N1** PlayVideo 缺 `clip`：`WindowV4Graph_WhenRequiredFieldMissing_ShowsFieldDiagnosticAndChineseSummary` 覆盖节点、字段 error 和中文 summary。
- [x] **N2** `Wait.duration=fast`：`WindowV4Graph_WhenNumberFieldInvalid_ShowsFieldDiagnostic` 覆盖 duration 字段 error 和中文 summary。
- [x] **N3** 布尔字段非法：`WindowV4Graph_WhenBooleanFieldInvalid_ShowsFieldDiagnostic` 覆盖 `wait=maybe` 的字段 error 和中文 summary。
- [x] **N4** 资源字段手填字符串：`WindowV4Graph_WhenAssetFieldIsManualString_ShowsWarningDiagnostic` 覆盖 warning 样式和“建议改为资源选择”。
- [x] **N5** Choice 缺 selected 目标：`WindowV4Graph_WhenChoiceMissingSelectedTarget_ShowsPortDiagnostic` 覆盖 Choice 节点、selected 端口和 summary。
- [x] **N6** 文本 completed 混连：`WindowV4Graph_WhenLineMixesChoiceAndDirectTargets_ShowsCompletedPortDiagnostic` 覆盖 completed 端口和中文提示。
- [x] **N7** 未知输出端口：`WindowV4Graph_WhenEdgeUsesUnknownOutputPort_PrioritizesWireDiagnostic` 覆盖 edge+port 优先 wire。
- [x] **N8** 辅助节点参与 runtime flow：`WindowV4Graph_WhenAuxiliaryNodeParticipatesInRuntimeFlow_ShowsNodeDiagnostic` 覆盖辅助节点 error 和 summary。
- [x] **N9** 目标节点不存在：`WindowV4Graph_WhenEdgeTargetIsMissing_ShowsWireDiagnostic` 覆盖找不到可绘制 wire 时回退到来源 port；目标章节不存在由同一 `AddEdgeDiagnostics()` 规则覆盖。
- [x] **N10** 点击 field issue：`WindowV4Graph_WhenSummaryClicked_SelectsDiagnosticNode` 覆盖点击后选中对应节点，字段样式仍由 diagnostics 保留。
- [x] **N11** 点击 wire issue：`WindowV4Graph_WhenSummaryClickedForWireIssue_SelectsDiagnosticWire` 覆盖点击后选中对应 edge；`Canvas_WhenDiagnosticsProvided_RendersElementClassesAndTooltips` 覆盖 wire error 颜色。
- [x] **N12** 图编辑后 compiler diagnostics stale：`WindowV4Graph_WhenEditedAfterCompile_MarksCompilerDiagnosticsStale` 覆盖 stale class 和“图已修改，请重新编译确认”。
- [x] **N13** 修复字段后本地校验刷新：`WindowV4Graph_WhenFieldFixed_RemovesLocalFieldDiagnostic` 覆盖 clip 修复后 field diagnostic 消失。
- [x] **N14** 当前章节外 issue：`WindowV4Graph_WhenIssueTargetsOtherChapter_KeepsItInSummaryOnly` 覆盖 summary 显示章节、不挂当前画布 node。
- [x] **N15** NodeGraphKit grep：`NodeGraphKit_WhenScanned_DoesNotReferenceStoryOrGraphView` 和命令行 grep 均通过。
- [x] **N16** Runtime grep：Runtime 未引用 `EditorNodeGraph`、UI Toolkit editor graph、GraphView、ObjectField 或 VideoClip 具体类型；广义 `UnityEditor/AssetDatabase` 命中既有 `Runtime/Resource/Provider/EditorAssetProvider.cs`，这是 architecture 已登记的 `#if UNITY_EDITOR` 资源模块例外，不属于本 feature 新增依赖。

验证命令：

```powershell
dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore
dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore
python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-20-story-graph-validation-feedback/story-graph-validation-feedback-checklist.yaml --yaml-only
python .codestable/tools/validate-yaml.py --file .codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml --yaml-only
rg -n "GameDeveloperKit\.Story|StoryEditor|NodeKind|StoryCommand|PlayVideo|play_video|GraphView" Assets/GameDeveloperKit/Editor/NodeGraph
rg -n "EditorNodeGraph|UIElements|UnityEditor\.Experimental\.GraphView|ObjectField" Assets/GameDeveloperKit/Runtime
```

## 4. 术语一致性

- `EditorGraphDiagnostic` / `Diagnostic target`：通用层命名一致，目标仍是 graph/node/field/port/wire。
- `Story validation source`：只在 Story helper/window 侧处理，GraphKit 不出现 Story source 解析。
- `Issue badge`：节点/字段/端口/连线只表达状态和 tooltip，不替代 summary。
- `Validation summary`：使用中文摘要，点击定位。
- `Stale diagnostics`：编译结果过期时 message/tooltip 与 summary 样式均表达“需重新编译确认”。
- 防冲突 grep：NodeGraphKit 无 Story/NodeKind/GraphView；Runtime 无 EditorNodeGraph/UI Toolkit editor graph/ObjectField。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已把 `EditorGraphDiagnostic`、Story diagnostics helper、node/field/port/wire 诊断渲染、summary 点击定位和 stale compiler diagnostics 写入 Story Editor / Editor Node Graph 现状。
- [x] 已把 Story 图诊断边界写入“已知约束 / 硬边界”：通用 graph diagnostics 只表达通用目标；Story source 解析、中文化、当前章节过滤和本地 authoring 校验只落 Story 层。
- [x] Runtime 隔离已补充为现状边界，且保留 Resource `EditorAssetProvider` 的既有 `#if UNITY_EDITOR` 例外解释。

## 6. requirement 回写

- [x] `requirement: story-editor` 已更新 `.codestable/requirements/story-editor.md`。
- [x] `implemented_by` 已追加 `2026-06-20-story-graph-validation-feedback`。
- [x] “实现进展”已追加本次图上校验反馈能力。
- [x] requirement 保持 `draft`：`sample-story-graph-fixture` 仍是 roadmap 中未完成的能力闭环。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml` 中 `story-graph-validation-feedback` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md` 第 5 节表格已同步为 `done`。
- [x] 第 6 节排期建议已改为下一步推进 `sample-story-graph-fixture`。
- [x] roadmap items yaml 校验通过。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 `attention.md` 的新通用注意事项。

说明：`GameDeveloperKit.Editor.csproj` 显式包含 `.cs` 文件导致新 helper 文件不被 dotnet quick build 自动纳入，这个细节本次通过把 helper 留在 `StoryEditorGraphAdapter.cs` 内规避。但这是当前生成工程文件/Unity 工程同步的局部现象，是否作为长期注意事项还需用户确认。

## 9. 遗留

- 后续优化点：`sample-story-graph-fixture` 仍应继续，提供多章节、对白、旁白、选项、命令和分支的完整示例 authoring 数据。
- 已知限制：当前仍基于项目内 `EditorNodeGraphKit`，未切换官方 Graph Toolkit；这符合本 feature 的明确不做。
- 已知限制：compiler message 源文案仍有英文，Story Editor 展示层对常见错误做中文摘要；未要求本 feature 一次性重写所有 compiler message。
- 实现阶段发现并修复：指定坏 edge 无法绘制 wire 时，不再 fallback 到同端口其它健康 wire，避免错误高亮错线。
