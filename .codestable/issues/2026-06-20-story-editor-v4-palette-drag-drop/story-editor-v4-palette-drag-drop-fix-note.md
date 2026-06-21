---
doc_type: issue-fix
issue: 2026-06-20-story-editor-v4-palette-drag-drop
path: fast-track
fix_date: 2026-06-20
tags: [editor, ui-toolkit, node-graph, story-editor]
---

# Story Editor v4 节点库拖不到 Graph 修复记录

## 1. 问题描述

Story Editor v4 的节点库条目不能像 ShaderGraph 一样拖到 graph 画布中创建节点。

## 2. 根因

节点库之前依赖 UnityEditor 的 `DragAndDrop` 泛型数据和 `DragPerformEvent` 作为创建节点的唯一通道，但拖拽起点仍由 palette item 捕获鼠标。UI Toolkit 鼠标捕获和 Editor 原生拖拽事件交接不稳定时，graph area 收不到 drop 事件，因此不会创建节点。

## 3. 修复方案

将节点库到画布的拖放改为可复用 graph kit 内部事件流：

- `EditorNodeGraphPaletteView` 在拖拽开始、移动、释放、取消时发出模板和 panel 坐标。
- `EditorNodeGraphCanvas` 接收内部拖拽事件，显示拖拽预览，并在释放位置落入 graph area 时创建节点。
- 保留“单击节点库不会创建节点”的行为，只有拖到画布中释放才创建。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphPaletteView.cs`
- `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphCanvas.cs`
- `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraph.uss`
- `Assets/GameDeveloperKit/Tests/Editor/EditorNodeGraphTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Editor.csproj --no-restore` 通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过，只有既有 Story v2 迁移类型过时警告。
- `git diff --check` 通过。

Unity Editor 真实拖放事件未在本轮自动化中运行，需要在已打开的 Story Editor v4 窗口里手动确认：从“节点库”拖一个节点到画布后，松手应创建对应节点，并且单击节点库不创建节点。

## 6. 遗留事项

如果手动确认后仍不能创建节点，下一步应在 palette drag started / released 和 canvas drop 判断处加临时日志，检查当前 Editor 版本下 `LocalToWorld` / `WorldToLocal` 的实际坐标是否与 graph area 边界一致。
