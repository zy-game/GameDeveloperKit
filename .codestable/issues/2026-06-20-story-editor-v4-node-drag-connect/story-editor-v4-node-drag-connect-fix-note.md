---
doc_type: issue-fix
issue: 2026-06-20-story-editor-v4-node-drag-connect
status: fixed
severity: high
root_cause_type: editor-interaction-state
tags: [editor, ui-toolkit, node-graph, story-editor]
---

# Story Editor v4 节点拖拽与连线失效修复

## 问题

Story Editor v4 中节点无法像 graph 一样拖拽，也无法从输出端口拖线连接到输入端口。

## 根因

`EditorNodeGraphNodeView.OnMouseDown` 在按下节点或端口时会触发 adapter 的 `SelectNode`。Story Editor v4 的 `SelectNodeFromGraph` 之前直接调用 `SelectNode(node)`，而 `SelectNode` 会 `RefreshAll()` 并重建整个 graph canvas。

结果是 mouse down 的同一帧里原节点 VisualElement 被销毁，后续 mouse move / mouse up 捕获不到同一个节点，拖拽状态和端口拖线状态都会丢失。

同时节点/端口拖拽逻辑混用了 `evt.mousePosition`，在 UI Toolkit mouse capture 和 transform 环境下坐标不稳定，容易造成连线预览和命中偏移。

## 修复

- `StoryEditorV4Window.SelectNodeFromGraph` / `SelectWireFromGraph` 改为只更新选择状态和报告，不重建画布。
- `EditorNodeGraphNodeView` 的节点拖拽和端口拖线坐标统一通过 `LocalToWorld(evt.localMousePosition)` 转为 panel/world 坐标。
- 增加回归测试：图内选择节点不会替换节点 VisualElement，避免再次在 mouse down 时重建画布。

## 验证

- `dotnet build GameDeveloperKit.Editor.csproj --no-restore` 通过。
- 临时纳入 `EditorNodeGraphTests.cs` 的 editor tests csproj 编译通过。

Unity Test Runner 未运行：当前项目已有 Unity 实例打开时 batchmode 会报 multiple instances，需关闭现有 Editor 后再跑。
