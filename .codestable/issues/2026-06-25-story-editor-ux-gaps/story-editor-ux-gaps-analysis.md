---
doc_type: issue-analysis
issue: 2026-06-25-story-editor-ux-gaps
status: confirmed
root_cause_type: logic
related: [story-editor-ux-gaps-report.md]
tags: [story-editor, chapter-tree, node-graph, ux, editor]
---

# 剧情编辑器 UX 缺口 根因分析

## 1. 问题定位

### 问题 A：无法创建新卷

| 关键位置 | 说明 |
|---|---|
| `StoryAuthoringAsset.cs:14` | 模型层仅有 `List<StoryAuthoringChapter> m_Chapters`，无 Volume 类或 volumes 列表 |
| `StoryEditorWindow.cs:226-256` (`RefreshTree`) | 构建单个 `Foldout("章节")`，直接遍历 `m_Asset.Chapters` 平铺渲染，无卷级包装 |
| `StoryEditorWindow.cs:441-443` (`BuildChapterGroupContextMenu`) | 右键菜单仅有"新增章节"，无"新增卷"入口 |
| `StoryEditorWindow.cs:652` (`AddChapter`) | 创建章节后直接追加到 `m_Asset.Chapters`（扁平整列），无卷归属逻辑 |

### 问题 B：卷名和章节名无法内联编辑

| 关键位置 | 说明 |
|---|---|
| `StoryEditorWindow.cs:1616-1623` (`CreateTreeRow`) | 章节行用 `new Button(click) { text = text }` 渲染，无双击事件注册 |
| `StoryEditorWindow.cs:1648-1653` (`FormatChapterLabel`) | 仅格式化显示文本，无编辑态切换逻辑 |
| `StoryEditorWindow.cs:246-252` (`RefreshTree`) | 调用 `CreateTreeRow` 后仅绑定右键菜单，无重命名入口 |

### 问题 C：多选节点不支持拖拽

| 关键位置 | 说明 |
|---|---|
| `EditorNodeGraphNodeView.cs:331-342` (`OnMouseMove`) | 拖动仅计算自身 delta，调用 `m_Moved?.Invoke(NodeId, Position)` 只传自身 |
| `EditorNodeGraphCanvas.cs:217-224` (`OnNodeMoved`) | 接收单个 nodeId，调用 `m_Adapter?.MoveNode(nodeId, position)` |
| `StoryEditorWindow.cs:762-771` (`MoveNodeFromGraph`) | 仅更新单个节点的 `GetLayout(node).Position` |
| `IEditorNodeGraphAdapter.cs:21` | 接口仅有 `MoveNode(string nodeId, Vector2)` ——无多节点移动 API |

### 补充发现：多选删除实际可用

`EditorNodeGraphCanvas.OnKeyDown()`（第 302 行）监听 Delete/Backspace 调用 `DeleteSelection()` → `StoryEditorGraphAdapter.DeleteSelection()` → `StoryEditorWindow.DeleteSelectionFromGraph()` → `RemoveSelection()`。`RemoveSelection()`（第 977 行）在 `m_SelectedNodeIds.Count > 1` 时正确批量删除所有非 Start/End 节点及其边。**多选删除已实现**。问题可能在于用户未尝试 Delete 键，或删除后 UI 未刷新。

## 2. 失败路径还原

### 问题 A：无法创建新卷

**期望路径**：用户在章节树右键 → 菜单中有"新增卷" → 点击后在树中新增卷节点 → 卷下可容纳章节。

**实际路径**：`RefreshTree()` 不渲染卷层级 → `BuildChapterGroupContextMenu` 无"新增卷"选项 → 数据模型 `StoryAuthoringAsset` 不支持卷 → **整条路径不存在**。

**分叉点**：`StoryAuthoringAsset.cs:14` —— 从数据模型开始就缺失卷概念，上层 UI 自然无法渲染。

### 问题 B：卷名和章节名无法内联编辑

**期望路径**：双击章节行 → `MouseDownEvent.clickCount == 2` 触发 → 将 `Button` 替换为 `TextField` → 编辑完成（Enter / 失焦）→ 写回 `chapter.Title` → 刷新树。

**实际路径**：`CreateTreeRow()` 创建 `Button` 但未注册 `MouseDownEvent` 监听 → 双击事件被 `Button` 默认行为消费 → 仅触发 `click` Action → `SelectChapter(chapter)` → 切换选中章节 → **无编辑行为**。

**分叉点**：`StoryEditorWindow.cs:1616-1623` —— `CreateTreeRow` 不注册双击检测，无双击→编辑的路径。

### 问题 C：多选拖拽

**期望路径**：框选 3 个节点 → 拖拽其中 1 个 → 3 个节点同步移动 → 3 个节点的布局同时更新。

**实际路径**：
1. 框选：`EditorNodeGraphCanvas.EndBoxSelection()` → `SelectNodesInGraphRect()` → `SelectNodes(nodeIds)` → `m_Adapter.SelectNodes(nodeIds)` → `StoryEditorWindow.SelectNodesFromGraph(nodeIds)` → 正确填充 `m_SelectedNodeIds`（含 3 个 ID）。
2. 拖拽：按住某个节点 → `EditorNodeGraphNodeView.OnMouseDown()` → `m_Dragging = true`。
3. 移动：`EditorNodeGraphNodeView.OnMouseMove()` → `Position += delta` → `m_Moved?.Invoke(NodeId, Position)` —— **仅传入被拖拽节点的 NodeId**。
4. 布局更新：`EditorNodeGraphCanvas.OnNodeMoved(nodeId, position)` → `m_Adapter.MoveNode(nodeId, position)` → `StoryEditorWindow.MoveNodeFromGraph(nodeId, position)` → 仅更新一个节点的 `StoryNodeLayout.Position`。
5. 其他 2 个选中节点的布局未被修改，画布刷新后仍留在原位。

**分叉点**：`EditorNodeGraphNodeView.cs:338-339` —— `m_Moved?.Invoke(NodeId, Position)` 只传自身 NodeId，不传播 delta。

## 3. 根因

**根因类型**：logic（逻辑缺失——功能入口和代码路径不存在）

三个子问题各有独立根因，互不依赖：

| 子问题 | 根因 | 根因类型 |
|---|---|---|
| A：无法创建新卷 | `StoryAuthoringAsset` 模型只支持扁平章节列表，无 Volume 数据结构；`RefreshTree` 和上下文菜单均未实现卷创建 | 逻辑缺失 |
| B：无法内联改名 | `CreateTreeRow` 使用 `Button` 渲染章节行，未注册 `MouseDownEvent` 监听双击，无双击→TextField 替换的编辑交互 | 逻辑缺失 |
| C：多选拖拽失效 | `EditorNodeGraphNodeView.OnMouseMove` 只移动自身节点并只上报自身 `NodeId`，画布未将 delta 传播给其他选中节点 | 逻辑缺失 |

三个根因间无因果关系，可独立修复；但问题 A 和 B 都涉及 `StoryEditorWindow.cs` 的章节树 UI，有共同改动区域。

## 4. 影响面

### 问题 A：无法创建新卷

- **影响范围**：所有剧情编辑器的用户。缺少卷层级意味着多卷故事只能通过章节名约定来组织，无法在编辑器 UI 中体现卷结构。
- **潜在受害模块**：
  - `StoryValidationReport.cs:VolumeCount` —— 已有 `VolumeCount` 属性但当前始终为 0（死代码），引入卷后需同步更新。
  - `Simples/chapters.csv` —— CSV 数据已包含卷概念（`volume_black_rain,第一卷：黑雨,...`），模型对齐 CSV 后可简化导入逻辑。
- **数据完整性风险**：无。新增模型字段（添加 `List<StoryAuthoringVolume>`）是向前兼容的，旧数据序列化后 `volumes` 列表为空，可做迁移。
- **严重程度复核**：维持 P1。

### 问题 B：无法内联改名

- **影响范围**：所有使用章节树的用户。重命名章节是目前唯一通过程序化 ID 生成（`MakeUnique`）控制的入口，用户无法给章节起有意义的名字。
- **潜在受害模块**：无。内联编辑是纯 UI 交互，不改数据模型。
- **数据完整性风险**：无。
- **严重程度复核**：维持 P1。

### 问题 C：多选拖拽

- **影响范围**：所有使用节点编辑器的用户。布局调整是大批量节点操作的高频场景，缺失多拖会严重影响编辑效率。
- **潜在受害模块**：
  - `EditorNodeGraphNodeView` 和 `EditorNodeGraphCanvas` 是通用节点图框架，任何使用 `IEditorNodeGraphAdapter` 的模块都受益于此修复。当前仅有 Story Editor 使用该框架，无其他消费者。
  - `EditorNodeGraphMiniMap` 依赖 `m_Adapter.Nodes` 渲染缩略图，修复后迷你地图中的选中高亮需保持正确。
- **数据完整性风险**：无。布局数据 `StoryNodeLayout.Position` 是 `Vector2` 值类型，批量更新无竞态风险。
- **严重程度复核**：维持 P1。

## 5. 修复方案

### 方案 A：全量模型驱动（推荐问题 A + B）

**问题 A（卷）**：
1. 新增 `[Serializable] StoryAuthoringVolume` 类，含 `m_VolumeId`、`m_Title`、`List<StoryAuthoringChapter> m_Chapters`
2. `StoryAuthoringAsset` 新增 `List<StoryAuthoringVolume> m_Volumes`，保留旧的 `List<StoryAuthoringChapter> m_Chapters` 做向后兼容（加载时若旧字段有数据则迁移到第一卷）
3. `RefreshTree()` 改为两层 Foldout：外层卷名 + 内层章节列表
4. `BuildChapterGroupContextMenu` 新增"新增卷"菜单项，`AddVolume()` 方法
5. `AddChapter()` 改为在选中卷下创建章节

**问题 B（内联改名）**：
1. `CreateTreeRow` 新增 `renameCallback` 参数，注册 `RegisterCallback<MouseDownEvent>` 检测 `clickCount == 2`
2. 双击时将 `Button` 替换为 `TextField`（借位替换，非弹窗）
3. `TextField` 注册 `RegisterCallback<FocusOutEvent>` 和 `RegisterCallback<KeyDownEvent>`（Enter 键），失焦或回车时确认改名
4. 回调 `chapter.Title = newName; MarkDirty(); RefreshTree()`

**问题 C（多选拖拽）**：
1. `EditorNodeGraphNodeView` 构造函数新增 `Func<IReadOnlyList<string>> getSelectedNodeIds` 参数
2. `OnMouseMove` 中计算 delta 后，遍历其他选中节点调用 `m_Moved?.Invoke(otherNodeId, nodeView.Position + delta)`
3. 更干净的方案：`IEditorNodeGraphAdapter` 新增 `void MoveNodes(IReadOnlyList<(string nodeId, Vector2 position)> moves)` 批量接口
4. `EditorNodeGraphCanvas.OnNodeMoved` 改为检测多选后调用批量接口，一次性更新所有选中节点

**优点**：数据模型完整、UI 层次清晰、交互完备

**缺点 / 风险**：
- 问题 A 涉及模型变更，需要向后兼容迁移逻辑；Volume 数据结构与 CSV 不一致（CSV 把卷作为章节属性而非容器），需进一步对齐
- 问题 C 修改框架层 `IEditorNodeGraphAdapter` 接口，影响所有适配器实现者（当前仅有 StoryEditorGraphAdapter）

**影响面**：
- `StoryAuthoringAsset.cs`（新增 Volume 类 + 迁移逻辑）
- `StoryEditorWindow.cs`（卷 CRUD + 内联编辑 + 批量移动）
- `EditorNodeGraphNodeView.cs`（多选拖拽 delta 传播）
- `EditorNodeGraphCanvas.cs`（批量移动调度）
- `IEditorNodeGraphAdapter.cs`（新增 MoveNodes 接口）
- `StoryEditorGraphAdapter.cs`（实现 MoveNodes）
- `StoryValidationReport.cs`（同步 VolumeCount）

---

### 方案 B：轻量 UI 补丁（最小改动）

**问题 A（卷）**：不引入 `StoryAuthoringVolume` 类。利用 `StoryAuthoringChapter.Title` 的前缀约定（如 `第一卷：黑雨` / `第一卷：雨夜抵达`）实现视觉分组：
1. `RefreshTree()` 解析章节 Title，按 `：` 前缀分组渲染多个 Foldout
2. 右键菜单新增"新增卷"，创建空标记章节作为卷头部（`Title = "第N卷"`，`NodeKind = Placeholder`）
3. 不修改序列化模型

**问题 B（内联改名）**：同方案 A。

**问题 C（多选拖拽）**：
1. 不修改 `IEditorNodeGraphAdapter` 接口
2. `EditorNodeGraphNodeView` 新增 `Action<Vector2> m_BroadcastDelta` 回调
3. `EditorNodeGraphCanvas` 在创建 NodeView 时传入回调，回调内遍历所有选中 NodeView 调用 `m_Adapter.MoveNode` 逐个更新
4. 在 `OnNodeMoved` 内添加多选感知逻辑

**优点**：
- 问题 A 不动数据模型，零序列化风险
- 问题 C 不改框架接口，改动范围最小

**缺点 / 风险**：
- 问题 A：卷分组纯靠字符串约定，无数据完整性保证；章节移动/重命名可能打破分组；与已有 CSV 数据中对齐的卷结构不一致
- 问题 C：逐个调用 `MoveNode` 可能触发多次 `MarkDirty` 和刷新，性能略差

**影响面**：
- `StoryEditorWindow.cs`（树分组渲染 + 内联编辑 + 多拖）
- `EditorNodeGraphNodeView.cs`（多拖）
- `EditorNodeGraphCanvas.cs`（多拖）

---

### 方案 C：保守增量

逐一最小改动：

**问题 A（卷）**：仅添加"新增卷"右键菜单项，创建一个 `StoryAuthoringChapter` 作为卷级占位（`Title = "第N卷"`），在 `RefreshTree` 中通过 `ChapterId` 前缀识别卷并渲染为单独 Foldout。这是方案 B 的子集。

**问题 B（内联改名）**：同方案 A。

**问题 C（多选拖拽）**：仅在 `EditorNodeGraphCanvas.OnNodeMoved` 内添加逻辑：若被移动节点在选中集中，对 `m_NodeViews` 中所有选中节点应用相同 delta（直接操作 `view.Position` + `ApplyPosition` + `m_Adapter.MoveNode`）。

**优点**：每项改动最小化，互不干扰

**缺点 / 风险**：
- 问题 A 和方案 B 相同——卷概念不持久化
- 问题 C 框架层缺少批量接口，未来其他消费者无法复用

**影响面**：与方案 B 几乎相同

---

### 推荐方案

**推荐方案 A（全量模型驱动）**，理由：

1. **问题 A 走模型路线是正确的**——CSV 数据（`chapters.csv`）和 `StoryValidationReport.VolumeCount` 已预设卷概念，模型对齐后消除技术债务而非堆积变通方案。字符串前缀分组的方案 B/C 在数据完整性上不可靠。
2. **问题 C 的批量接口是框架级改进**——`IEditorNodeGraphAdapter` 作为通用节点图框架，批量移动是基本操作，应纳入接口契约而非在消费侧 hack。
3. **三个问题改动区域有重叠**（`StoryEditorWindow.cs`），合并修复减少来回刷新。
4. 改动范围可控：约 6-7 个文件，均在 Story Editor 和 NodeGraph 模块内，无跨系统影响。

序列化向后兼容建议：`StoryAuthoringAsset` 同时保留 `m_Chapters`（旧）和新增 `m_Volumes`（新），加载时若 `m_Volumes.Count == 0 && m_Chapters.Count > 0`，自动将旧章节迁移到默认卷 `volume_default`。
