---
doc_type: feature-acceptance
feature: 2026-06-20-editor-graph-manual-acceptance
status: passed
date: 2026-06-20
tags: [story, editor, node-graph, uitoolkit, acceptance]
---

# Story Editor 图交互真实验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-editor-graph-manual-acceptance/editor-graph-manual-acceptance-design.md`

## 1. 接口契约核对

- [x] `EditorNodeGraphCanvas` 仍是复用画布入口：代码位于 `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphCanvas.cs`，通过 `IEditorNodeGraphAdapter` 获取 nodes/wires/templates，负责 pan/zoom、wire 绘制、右键菜单、快捷键、palette 拖入和框选。
- [x] `EditorNodeGraphNodeView` 仍负责节点视图、节点拖拽、端口 dot 和节点内字段。
- [x] `EditorNodeGraphPaletteView` 仍负责节点库模板展示和拖拽事件；单击节点库不创建节点。
- [x] `StoryEditorWindow` 仍通过 `StoryEditorGraphAdapter` 接入通用画布。
- [x] 接口示例“节点库拖入创建节点”：`TryCreateTemplateFromPaletteDrop` / `CreateTemplateAt` 只调用 adapter `CreateNode`。
- [x] 接口示例“Delete 删除选中项”：`EditorNodeGraphCanvas.DeleteSelection()` 只调用 adapter `DeleteSelection()`，Story 删除由 `StoryEditorWindow` 执行。
- [x] 新增框选接口保持业务无关：`IEditorNodeGraphAdapter.SelectNodes(IReadOnlyList<string> nodeIds)` 只传 node id 集合，Story 语义仍在 adapter/window 层。

## 2. 行为与决策核对

- [x] 需求摘要满足：真实 Unity Editor 中 N1-N15 已由用户确认正常。
- [x] 关键决策 1：真实 Editor 手测是一等输出；已写入 `editor-graph-manual-acceptance-manual-record.md`。
- [x] 关键决策 2：自动测试只覆盖稳定离屏契约；已补 `EditorNodeGraphTests` / `StoryEditorTests` 覆盖框选命中、多选委托和多选删除。
- [x] 关键决策 3：graph kit 保持业务无关；grep `Assets/GameDeveloperKit/Editor/NodeGraph` 未命中 `GameDeveloperKit.Story` / `StoryEditor` / `NodeKind` / `GraphView`。
- [x] 错误语义：N1-N15 全部有真实结果记录；没有把失败或阻塞项写成通过。
- [x] 顺序约束：`editor-graph-manual-acceptance` 已回写 done，`story-graph-port-policy` 可作为下一项启动。
- [x] 挂载点核对：入口仍是 `GameDeveloperKit/剧情编辑器`、`EditorNodeGraphCanvas` 和 `IEditorNodeGraphAdapter`。
- [x] 反向核对：Runtime grep 未命中 `EditorNodeGraph` / `UnityEditor.Experimental.GraphView`；NodeGraphKit 未引入 Story 语义。

## 3. 验收场景核对

| 编号 | 证据 | 结果 |
|---|---|---|
| N1 | 用户真实 Editor 手测 | 通过 |
| N2 | 用户真实 Editor 手测 | 通过 |
| N3 | 用户真实 Editor 手测 | 通过 |
| N4 | 用户真实 Editor 手测 | 通过 |
| N5 | 用户真实 Editor 手测 + palette 测试 | 通过 |
| N6 | 用户真实 Editor 手测 | 通过 |
| N7 | 用户真实 Editor 手测 + adapter 连接测试 | 通过 |
| N8 | 用户真实 Editor 手测 | 通过 |
| N9 | 用户真实 Editor 手测 + zoom 锚点测试 | 通过 |
| N10 | 用户真实 Editor 手测 | 通过 |
| N11 | 用户真实 Editor 手测 | 通过 |
| N12 | 用户真实 Editor 手测 + Delete 委托/多选删除测试 | 通过 |
| N13 | 用户真实 Editor 手测 | 通过 |
| N14 | 用户真实 Editor 手测 | 通过 |
| N15 | 用户真实 Editor 手测 + 框选命中/多选委托测试 | 通过 |

自动验证：

- [x] `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过；仅有既有 obsolete / unused warning。
- [x] 本 feature 涉及文件 `git diff --check` 通过。
- [x] 全仓 `git diff --check` 仍被既有无关文件尾随空格阻塞：`Assets/GameDeveloperKit/Simples/UI/Test.prefab`、`UserSettings/Layouts/default-2022.dwlt`。

## 4. 术语一致性

- [x] `Editor Node Graph Kit`：代码目录为 `Assets/GameDeveloperKit/Editor/NodeGraph/`，与方案一致。
- [x] `graph area`：实现落点为 `EditorNodeGraphCanvas` 内 `m_GraphArea`。
- [x] `manual acceptance`：手测结果写入 `editor-graph-manual-acceptance-manual-record.md`。
- [x] `palette drag`：节点库拖拽由 `EditorNodeGraphPaletteView` 内部事件流实现。
- [x] `box selection`：框选由 `EditorNodeGraphCanvas` 处理，业务多选通过 `SelectNodes` 交给 adapter。
- [x] 禁用/边界 grep：NodeGraphKit 未引入 Story 业务名词或 GraphView。

## 5. 架构归并

- [x] 已更新 `.codestable/architecture/ARCHITECTURE.md`：新增 `Story Editor / Editor Node Graph` 小节，记录 `EditorNodeGraphKit`、`StoryEditorGraphAdapter`、`IEditorNodeGraphAdapter.SelectNodes`、N1-N15 手测通过和 runtime 隔离约束。

## 6. requirement 回写

- [x] 已更新 `.codestable/requirements/story-editor.md`：将 `2026-06-20-editor-graph-manual-acceptance` 追加到 `implemented_by`，并在“实现进展”记录 Story Editor 图交互最小闭环。
- [x] requirement 保持 `draft`：本 feature 只完成图交互验收，端口语义、选项分支契约、命令字段类型化和图上校验反馈仍未完成。

## 7. roadmap 回写

- [x] 已更新 `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml`：`editor-graph-manual-acceptance` 状态从 `in-progress` 改为 `done`。
- [x] 已同步 `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md`：子 feature 清单状态改为 `done`，并补入框选和 `SelectNodes` 契约。
- [x] yaml 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。本次使用的 `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 属于本 feature 验收命令，不是所有 feature 必踩规则；既有 attention 已记录 Runtime 快速编译命令。

## 9. 遗留

- 后续 roadmap 项：
  - `story-graph-port-policy`
  - `choice-item-branching-contract`
  - `typed-command-fields`
  - `story-graph-validation-feedback`
  - `sample-story-graph-fixture`
- 已知限制：Unity Editor 手测为用户确认记录；没有在命令行跑 Unity Test Runner，因为项目当前已有 Editor 实例时 batchmode 会被锁。
- 顺手发现：全仓 `git diff --check` 被既有无关文件尾随空格阻塞，未在本 feature 中修改。
