---
doc_type: issue-fix
issue: 2026-06-25-story-editor-ux-gaps
path: standard
fix_date: 2026-06-25
related: [story-editor-ux-gaps-analysis.md]
tags: [story-editor, chapter-tree, node-graph, ux, editor]
---

# 剧情编辑器 UX 缺口 修复记录

## 1. 实际采用方案

方案 A（全量模型驱动），三项问题一并修复：

- **卷创建**：新增 `StoryAuthoringVolume` 序列化类（VolumeId / Title / List\<Chapter\>），`StoryAuthoringAsset` 新增 `List\<StoryAuthoringVolume\> m_Volumes` 字段，旧 `m_Chapters` 保留做向后兼容。`EnsureDefaults()` 自动迁移旧扁平章节到默认卷。
- **内联改名**：章节树行注册双击事件（`MouseDownEvent.clickCount == 2`），原位替换 Button 为 TextField，失焦/Enter 确认改名并写回模型。
- **多选拖拽**：框架层新增 `IEditorNodeGraphAdapter.MoveNodes` 批量接口 + `EditorNodeGraphMove` 结构体，`EditorNodeGraphNodeView` 新增 delta 广播回调，`EditorNodeGraphCanvas` 处理多选 delta 传播 + 批量持久化。

## 2. 改动文件清单

| 文件 | 改动说明 |
|---|---|
| `Model/StoryAuthoringAsset.cs` | 新增 `StoryAuthoringVolume` 类；`StoryAuthoringAsset` 加 `m_Volumes` 字段、`Volumes`/`SelectedVolume` 属性；`Chapters` 改为跨卷聚合；`EnsureDefaults` 增加卷迁移逻辑；`FindChapter` 遍历所有卷 |
| `Window/StoryEditorWindow.cs` | `RefreshTree` 改为卷→章节两层 Foldout；新增 `AddVolume`/`RemoveVolume`/`MoveNodesFromGraph`/`BeginInlineRename`/`CommitRename`/`CancelRename`/`GetAllChapters`/`FindVolumeIndexOfChapter`/`GetChapterCount`；更新 `AddChapter`(volumeIndex)/`RemoveSelectedChapter`/`EnsureSelection`/`SelectDefaults`；上下文菜单增加"新增卷"/"删除卷" |
| `Model/StoryAuthoringAssetStore.cs` | `Create()` 改为通过 `SelectedVolume.Chapters` 写入示例章节 |
| `NodeGraph/IEditorNodeGraphAdapter.cs` | 新增 `MoveNodes(IReadOnlyList<EditorNodeGraphMove>)` 批量移动接口 |
| `NodeGraph/EditorNodeGraphModels.cs` | 新增 `EditorNodeGraphMove` 结构体（NodeId + Position） |
| `NodeGraph/EditorNodeGraphNodeView.cs` | 新增 `m_MoveDeltaApplied` 回调参数；`OnMouseMove` 同时广播 delta；`Position` setter 改为 `internal`；`ApplyPosition()` 改为 `internal` |
| `NodeGraph/EditorNodeGraphCanvas.cs` | `OnNodeMoved` 检测多选后走 `MoveNodes` 批量接口；新增 `OnNodeMoveDelta` 将 delta 应用到其他选中节点视图 |
| `StoryEditor/StoryEditorGraphAdapter.cs` | 实现 `MoveNodes`，委托到 `m_Window.MoveNodesFromGraph` |

## 3. 验证结果

- [x] 编译通过：`dotnet build GameDeveloperKit.Editor.csproj` — 0 errors, 0 warnings
- [ ] Unity Editor 运行验证：需在 Unity Editor 中手动执行（当前环境无法运行）。验证步骤见 issue report 第 2 节。

## 4. 遗留事项

- 无。三项修复均在同一轮改动中完成。
- 多选删除在代码中已正确实现（`RemoveSelection()` 支持 `m_SelectedNodeIds.Count > 1`），用户可尝试按 Delete 键验证。
