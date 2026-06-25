---
doc_type: issue-report
issue: 2026-06-25-story-editor-ux-gaps
status: confirmed
severity: P1
summary: 剧情编辑器章节树缺少卷创建与内联改名功能，节点编辑器多选后不支持拖拽和删除
tags: [story-editor, chapter-tree, node-graph, ux, editor]
---

# 剧情编辑器 UX 缺口 Issue Report

## 1. 问题现象

剧情编辑器存在三项功能缺口：

1. **无法创建新卷**：章节树以"卷"（如"第一卷"）为根节点组织，但当前没有创建新卷的入口，用户无法新增卷。
2. **卷名和章节名无法编辑**：双击卷名或章节名时无反应。期望双击后在名称原位出现可编辑输入框，直接在树中修改名称，而非弹出对话框。
3. **多选节点不支持拖拽和删除**：节点编辑器虽支持框选多个节点，但拖拽选中节点时仅移动被鼠标按住的单个节点，其他选中节点不跟随；删除需逐节点操作，不支持批量删除。

## 2. 复现步骤

### 2.1 无法创建新卷

1. 打开剧情编辑器（菜单 `GameDeveloperKit/剧情编辑/编辑器`）
2. 观察左侧章节树
3. 观察到：章节树只有扁平的章节列表，无"新增卷"按钮或右键菜单入口

### 2.2 卷名和章节名无法编辑

1. 在章节树中双击任意卷名或章节名
2. 观察到：双击无反应，名称位置不出现编辑框

### 2.3 多选节点拖拽和删除

1. 在节点编辑器中框选多个节点
2. 尝试拖拽选中节点
3. 观察到：仅被鼠标按住的单个节点移动，其余选中节点停留在原位
4. 尝试按 Delete 键删除选中节点
5. 观察到：（待确认是否生效；研究显示 `DeleteSelection()` 已实现多删，需实测验证）

复现频率：稳定必现。

## 3. 期望 vs 实际

### 3.1 创建新卷

**期望行为**：章节树提供创建新卷的入口（按钮或右键菜单），点击后在树中新增一个卷节点，卷下可容纳章节。

**实际行为**：无任何创建卷的入口，章节直接平铺在树中，无卷层级。

### 3.2 内联改名

**期望行为**：双击卷名或章节名后，名称原位变为可编辑输入框，输入完成后按 Enter 或失焦确认改名。

**实际行为**：双击无任何响应，名称始终为只读文本，无编辑入口。

### 3.3 多选拖拽与删除

**期望行为**：框选多个节点后，拖拽任一选中节点时所有选中节点同步移动；按 Delete 键批量删除所有选中节点。

**实际行为**：拖拽仅移动单个节点，其余选中节点不跟随；删除行为待实测确认。

## 4. 环境信息

- 涉及模块 / 功能：剧情编辑器（Story Editor），具体涉及章节树面板和节点图编辑器
- 相关文件 / 函数：
  - `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.cs` — 章节树 UI（`RefreshTree`、`CreateTreeRow`）、卷/章节增删逻辑
  - `Assets/GameDeveloperKit/Editor/StoryEditor/Model/StoryAuthoringAsset.cs` — 数据模型，无 Volume 类
  - `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphNodeView.cs` — 节点拖拽逻辑（`OnMouseMove` 仅移动自身）
  - `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraphCanvas.cs` — 画布多选和删除调度
  - `Assets/GameDeveloperKit/Editor/StoryEditor/StoryEditorGraphAdapter.cs` — `DeleteSelection()` / `MoveNode()` 适配
- 运行环境：Unity Editor 编辑器模式
- 最近改动：是，修改过剧情编辑器相关代码

## 5. 严重程度

**P1** — 核心编辑功能受损（无法组织卷结构、无法重命名资产、无法高效批量操作节点），虽可通过其他方式绕过（手动编辑资产文件、逐个拖拽节点），但严重影响日常编辑效率。

## 备注

- 数据模型中 `StoryAuthoringAsset` 仅含扁平 `List<StoryAuthoringChapter>`，无 `StoryAuthoringVolume` 类，卷概念在代码层面尚未实现。这是创建新卷功能缺失的根因线索（待阶段 2 确认）。
- `StoryEditorWindow.CreateTreeRow()` 中章节行使用 `Button` 控件渲染，无双击事件注册，这是内联改名缺失的根因线索（待阶段 2 确认）。
- `EditorNodeGraphNodeView.OnMouseMove()` 仅计算并应用自身位移，不向其他选中节点广播 delta，这是多选拖拽失效的根因线索（待阶段 2 确认）。

---

<!-- 状态：等待用户确认后改为 confirmed -->
