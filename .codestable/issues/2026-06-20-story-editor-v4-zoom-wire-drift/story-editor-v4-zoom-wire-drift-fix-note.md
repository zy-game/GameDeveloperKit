---
doc_type: issue-fix
issue: 2026-06-20-story-editor-v4-zoom-wire-drift
path: fast-track
fix_date: 2026-06-20
tags: [editor, ui-toolkit, node-graph, zoom, wire]
---

# Story Editor v4 缩放后连线与节点脱离修复记录

## 1. 问题描述

Story Editor v4 的节点图使用鼠标滚轮缩放后，节点和连线会出现明显偏移，连线不再贴合端口；缩放视觉中心也会向右下角偏移。

## 2. 根因

节点层 `m_Content` 使用 UI Toolkit 的 `transform.scale` 做缩放，但没有指定 `transformOrigin`。UI Toolkit 默认按元素中心作为 transform origin 进行缩放。

线层 `EditorNodeGraphWireLayer` 则按 `canvas = graph * zoom + pan` 的左上角原点模型绘制。节点层和线层的缩放原点不一致时，缩放比例越偏离 1，端口锚点和线条绘制位置就越明显分离，并表现为视图向右下偏。

## 3. 修复方案

- 将节点内容层的 `transformOrigin` 固定为 `(0, 0)`，让节点层、线层、网格和 `CanvasToGraph` / `GraphToCanvas` 使用同一套左上角原点坐标模型。
- 保留原有 `ZoomAt` 的鼠标锚点算法：缩放前后保持鼠标下同一 graph 坐标不变。
- 增加测试确认内容层 transform origin 被固定到左上角，避免后续改 UI 时回退。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphCanvas.cs`
- `Assets/GameDeveloperKit/Tests/Editor/EditorNodeGraphTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Editor.csproj --no-restore` 通过；存在旧 StoryEditor 迁移代码的过时 API 警告。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过；存在既有 StoryEditorTests 的过时 API 警告。
- `git diff --check` 通过。

Unity Test Runner 未运行：当前环境里之前 batchmode 会因为项目已有 Unity 实例打开而失败，需要关闭现有 Editor 后再跑完整 Editor 测试。

## 6. 遗留事项

需要在 Unity Editor 中实际打开 Story Editor v4，用滚轮在节点和空白画布上分别缩放确认：连线端点应持续贴合端口，鼠标所在图坐标应保持在缩放中心。
