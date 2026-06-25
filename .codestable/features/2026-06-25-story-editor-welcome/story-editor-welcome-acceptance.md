# 剧情编辑器欢迎页 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-25
> 关联方案 doc：story-editor-welcome-design.md

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对：**

- [x] `StoryEditorRecentAssets.GetRecentPaths()` — 设计：返回 `IReadOnlyList<string>`，元素为 `"Assets/..."` 格式 → 代码：`StoryEditorRecentAssets.cs:12`，签名一致，内部用 `JsonUtility` 反序列化 + 过滤非 `Assets/` 开头路径 ✓
- [x] `StoryEditorRecentAssets.RecordOpen(string assetPath)` — 设计：null/空/非 Assets 开头忽略 → 代码：`StoryEditorRecentAssets.cs:54`，`string.IsNullOrWhiteSpace` + `StartsWith("Assets/")` 双重校验 ✓
- [x] `StoryEditorWelcomeWindow.Open()` — 设计：`[MenuItem]` 入口 → 代码：`StoryEditorWelcomeWindow.cs:18`，`[MenuItem("GameDeveloperKit/剧情编辑/编辑器")]` ✓
- [x] `StoryEditorWindow.Open(string assetPath)` — 设计：新增重载 → 代码：`StoryEditorWindow.cs:66`，通过 `s_PendingAssetPath` 传递路径到 `CreateGUI()` ✓

**名词层"现状 → 变化"逐项核对：**

- [x] 新增 `StoryEditorWelcomeWindow`：声明为独立 `EditorWindow` → 代码：`StoryEditorWelcomeWindow.cs:9`，`public sealed class StoryEditorWelcomeWindow : EditorWindow` ✓
- [x] 新增 `StoryEditorRecentAssets`：声明为静态辅助类 → 代码：`StoryEditorRecentAssets.cs:8`，`internal static class StoryEditorRecentAssets` ✓
- [x] 修改 `StoryEditorWindow.Open()` 菜单绑定：移除 `[MenuItem]` → grep 确认 `StoryEditorWindow.cs` 中无任何 `[MenuItem]` ✓
- [x] 不变 `StoryEditorWindow.Open()` / `.OpenSample()`：保留为 public static → 代码中两者均为 public static，供 Welcome 窗口调用 ✓

**流程图核对（第 2.2 节 mermaid 图）：**

- [x] 菜单 → `StoryEditorWelcomeWindow.Open()` → grep 确认 `[MenuItem("GameDeveloperKit/剧情编辑/编辑器")]` 在 `StoryEditorWelcomeWindow.cs:18` ✓
- [x] 欢迎页 → 点击最近资源 → 校验 → `StoryEditorWindow.Open(assetPath)` → `HandleOpenRecent` 调用链完整 ✓
- [x] 欢迎页 → 点击新建 → 保存对话框 → `CreateAtPath` → `StoryEditorWindow.Open(path)` → `HandleNew` 调用链完整 ✓
- [x] 欢迎页 → 点击打开 → 文件对话框 → 校验类型 → `StoryEditorWindow.Open(assetPath)` → `HandleOpen` 调用链完整 ✓
- [x] 欢迎页 → 点击示例剧情 → `StoryEditorWindow.OpenSample()` → `HandleOpenSample` 调用链完整 ✓
- [x] 取消操作 → return → `HandleNew`/`HandleOpen` 在 `string.IsNullOrWhiteSpace` 时 return ✓
- [x] 资源无效 → `DisplayDialog` → `HandleOpenRecent` 中 `isValid is false` 时弹窗 ✓

## 2. 行为与决策核对

对照方案第 1 节 + 第 2.2 节：

**需求摘要逐项验证：**

- [x] 点击菜单看到欢迎页 → 代码：`StoryEditorWelcomeWindow.Open()` 创建窗口并 `Show()` ✓
- [x] 欢迎页显示最近打开的资源列表（按时间倒序）→ 代码：`RecordOpen` 插入到 index 0，`RefreshRecentList` 按 paths 顺序渲染 ✓
- [x] 提供新建、打开、示例入口按钮 → 代码：三个 `Button` 分别绑定 `HandleNew`/`HandleOpen`/`HandleOpenSample` ✓
- [x] 显示快速开始引导文字 → 代码：`BuildGuide()` 包含 5 步骤引导 ✓
- [x] 从欢迎页进入编辑器后行为一致 → 代码：`StoryEditorWindow.CreateGUI()` 在有/无 `s_PendingAssetPath` 时均调用 `BuildLayout()` + `RefreshAll()` ✓

**明确不做逐项核对（用第 3 节"反向核对项"）：**

- [x] 不做资源模板市场 → grep 确认 Welcome 目录代码中无 `marketplace`/`template` 相关 UI 或逻辑 ✓
- [x] 不做版本更新日志 → grep 确认无 `changelog`/`版本更新` 字样 ✓
- [x] 不做云端同步 → grep 确认无 `cloud`/`云端`/网络请求 ✓
- [x] 不改变编辑器窗口本身行为 → 代码：`StoryEditorWindow.CreateGUI()` 逻辑仅在入口处增加路径判断，后续 `BuildLayout()`/`RefreshAll()` 未变 ✓
- [x] 不在欢迎页做资源删除/重命名管理 → grep 确认 Welcome 目录代码无文件级 CRUD 操作 ✓

**关键决策落地：**

- [x] 决策 1: 欢迎页是独立 `EditorWindow` → 代码：`StoryEditorWelcomeWindow : EditorWindow`，用自己的 `GetWindow<>()` ✓
- [x] 决策 2: 菜单入口从 `StoryEditorWindow.Open()` 改为 `StoryEditorWelcomeWindow.Open()` → grep 确认 `StoryEditorWindow.cs` 无 `[MenuItem]`，`StoryEditorWelcomeWindow.cs` 有 ✓
- [x] 决策 3: 最近资源用 `EditorPrefs` 持久化 JSON → 代码：`StoryEditorRecentAssets` 使用 `EditorPrefs.GetString/SetString` + `JsonUtility` ✓
- [x] 决策 4: 最近资源只在编辑器打开时记录 → 代码：`RecordOpen` 在 `HandleNew`/`HandleOpen`/`HandleOpenRecent` 中调用，`OpenSample` 不记录 ✓
- [x] 决策 5: UI 风格与现有 Story Editor 一致 → 代码：USS 复用深色背景色值、圆角面板、hover 效果，`StoryEditorWelcomeWindow.cs` 采用相同的 `m_` 前缀 + 4 空格缩进风格 ✓

**编排层"现状 → 变化"逐项核对：**

- [x] 在现有线性流程最前面插入欢迎页 → 代码：菜单 → `Open()` → `CreateGUI()` → 用户选择 → `StoryEditorWindow.Open(...)` → 编辑器 `CreateGUI()` ✓
- [x] 菜单项不再直接调用 `StoryEditorWindow.Open()` → grep 确认菜单绑定在 Welcome 窗口 ✓
- [x] `StoryEditorWindow` 新增接受资源路径的重载 → 代码：`Open(string assetPath)` 设置 `s_PendingAssetPath` ✓

**流程级约束核对：**

- [x] 窗口关系：欢迎页和编辑器是独立 `EditorWindow` → 代码：各自 `GetWindow<>()`，独立生命周期 ✓
- [x] 重复打开：`GetWindow<>()` 保证单例 → Unity `EditorWindow.GetWindow<T>()` 内建单例 ✓
- [x] 取消操作：`HandleNew`/`HandleOpen` 在取消时 return → 代码：`string.IsNullOrWhiteSpace(path)` 时直接 return ✓
- [x] 资源校验：`IsValidAsset` 检查 `LoadAssetAtPath<StoryAuthoringAsset>` → 代码：`StoryEditorRecentAssets.cs:79` ✓
- [x] 错误处理：打开失败时 `DisplayDialog` → 代码：`HandleOpen` 和 `HandleOpenRecent` 中均有 dialog ✓

**挂载点反向核对（可卸载性）：**

- [x] 挂载点 M1: `[MenuItem("GameDeveloperKit/剧情编辑/编辑器")]` → 代码落点：`StoryEditorWelcomeWindow.cs:18` ✓
- [x] 挂载点 M2: `[MenuItem("GameDeveloperKit/剧情编辑/打开示例剧情图")]` → 代码落点：`StoryEditorWelcomeWindow.cs:27` ✓
- [x] 挂载点 M3: `EditorPrefs` key `StoryEditor.RecentAssets` → 代码落点：`StoryEditorRecentAssets.cs:10` ✓
- [x] **反向核查（grep）**：Welcome 目录以外的代码无引用 `StoryEditorWelcomeWindow` 或 `StoryEditorRecentAssets` → grep 确认 ✓
- [x] **拔除沙盘推演**：删除 Welcome 目录 + 恢复 `StoryEditorWindow.cs` 的 `[MenuItem]` + 删除 `EditorPrefs` key → 系统回到原始状态，无残留 ✓

## 3. 验收场景核对

对照方案第 3 节关键场景清单：

- [x] **N1**: 菜单打开欢迎窗口，显示引导、按钮、最近资源区域
  - 证据：`BuildLayout()` 包含 title/subtitle/actions/recent list/guide 完整布局，`[MenuItem]` 绑定正确
  - 结果：通过 ✓

- [x] **N2**: 新建 → 选择路径 → 确认 → 编辑器窗口打开新资源
  - 证据：`HandleNew()` → `SaveFilePanelInProject` → `CreateAtPath` → `StoryEditorWindow.Open(path)`
  - 结果：通过（代码逻辑完整，编译通过）✓

- [x] **N3**: 打开 → 选择 .asset → 确认 → 编辑器窗口加载所选资源
  - 证据：`HandleOpen()` → `OpenFilePanel` → `LoadAssetAtPath<StoryAuthoringAsset>` 校验 → `StoryEditorWindow.Open(assetPath)`
  - 结果：通过 ✓

- [x] **N4**: 打开示例剧情 → 编辑器窗口加载示例剧情图
  - 证据：`HandleOpenSample()` → `StoryEditorWindow.OpenSample()` 调用链完整
  - 结果：通过 ✓

- [x] **N5**: 打开资源后关闭编辑器，重新打开欢迎页，最近列表第一条是刚打开的资源
  - 证据：`RecordOpen` 去重后插入 index 0，`RefreshRecentList` 按序渲染
  - 结果：通过（代码逻辑验证）✓

- [x] **N6**: 反复打开同一资源，最近列表去重且位置在最前
  - 证据：`RecordOpen` 中 `paths.RemoveAll(x => x == assetPath)` 再去重 + `paths.Insert(0, ...)`
  - 结果：通过 ✓

- [x] **N7**: 最近资源超过 10 条时裁剪
  - 证据：`RecordOpen` 中 `while (paths.Count > MaxCount) paths.RemoveAt(...)`，`MaxCount = 10`
  - 结果：通过 ✓

- [x] **N8**: 点击已删除/移动的最近资源，提示不可用
  - 证据：`HandleOpenRecent` 中 `isValid is false` 时 `DisplayDialog("资源不可用", ...)`
  - 结果：通过 ✓

- [x] **N9**: 首次使用（最近列表为空），显示"暂无最近资源"
  - 证据：`GetRecentPaths()` 返回空时 `m_RecentEmpty.style.display = DisplayStyle.Flex`
  - 结果：通过 ✓

- [x] **N10**: 欢迎页已打开时再次点击菜单，聚焦已有窗口
  - 证据：`GetWindow<StoryEditorWelcomeWindow>()` Unity 内建单例
  - 结果：通过（类型系统保证）✓

- [x] **N11**: 文件对话框中取消，留在欢迎页
  - 证据：`HandleNew`/`HandleOpen` 在路径为空时直接 return，不调用 `Close()`
  - 结果：通过 ✓

- [x] **N12**: 从欢迎页进入编辑器后关闭编辑器，下次菜单正常打开欢迎页
  - 证据：两个窗口独立生命周期，欢迎页已在进入编辑器时 `Close()`，下次菜单重新创建
  - 结果：通过 ✓

- [x] **N13**: 最近资源指向非 StoryAuthoringAsset 类型，灰显标注
  - 证据：`IsValidAsset` 用 `LoadAssetAtPath<StoryAuthoringAsset>` 校验类型，`--invalid` CSS 类灰显
  - 结果：通过 ✓

**反向核对项全部守住：**
- [x] 代码中无模板市场/版本日志/云端同步/网络请求
- [x] 欢迎页不修改 StoryAuthoringAsset/StoryAuthoringChapter 数据（只读 `LoadAssetAtPath`）
- [x] StoryEditorWindow 核心逻辑（CreateGUI/BuildLayout/RefreshAll/编译/播放）未改

## 4. 术语一致性

对照方案第 0 节 + 第 2.1 节命名 grep 代码：

- `StoryEditorWelcomeWindow` — 代码中 3 处引用，类名/文件名/Open() 返回类型一致 ✓
- `StoryEditorRecentAssets` — 代码中 3 处引用，类名/文件名一致 ✓
- `最近资源 (Recent Resources)` — 代码使用 `StoryEditor.RecentAssets` 作为 EditorPrefs key ✓
- `快速开始引导 (Quick Start Guide)` — `BuildGuide()` 对应 ✓
- 防冲突：grep 确认项目中无同名 `StoryEditorWelcomeWindow`/`StoryEditorRecentAssets` ✓

## 5. 架构归并

对照方案第 4 节：

**需要更新的架构 doc：** `architecture/ARCHITECTURE.md` 的 "Story Editor / Editor Node Graph" 节

- [x] 补充菜单入口描述：菜单现先经过 `StoryEditorWelcomeWindow`，用户选择资源后才进入编辑器窗口
- [x] 新增 `StoryEditorWelcomeWindow` 简要描述

**归并动作：**

- [x] `ARCHITECTURE.md` 第 37-40 行：更新 Story Editor 入口描述，补充 `StoryEditorWelcomeWindow` 作为菜单门面的说明，新增 `StoryEditorWelcomeWindow` 和 `StoryEditorRecentAssets` 核心类型条目 ✓
- [x] 无系统级 Runtime 影响，无需更新其他架构 doc ✓

## 6. requirement 回写

方案 frontmatter `requirement: story-editor`，指向 `.codestable/requirements/story-editor.md`（draft）。

- [x] `implemented_by` 列表已追加 `2026-06-25-story-editor-welcome` ✓
- [x] 实现进展节已追加 2026-06-25 条目 ✓
- [x] requirement 保持 `draft`（因导入导出等更大编辑器愿景未完成）✓

## 7. roadmap 回写

方案 frontmatter `roadmap` / `roadmap_item` 均为空 → 非 roadmap 起头，跳过。

## 8. attention.md 候选盘点

本 feature 为纯 Editor 层 UI 功能，不涉及编译/运行/环境配置的特殊约定。无需要补入 attention.md 的内容。

## 9. 遗留

- 后续优化点：无
- 已知限制：
  - 最近资源列表不显示资源文件名以外的信息（如修改时间）
  - 欢迎页不提供"从此列表中移除"单条记录的功能
- 实现阶段"顺手发现"：
  - `StoryEditorWindow.cs`（1716 行）：已在 design 2.5 节记录，混合了布局/工具栏/树视图/画布/CRUD/诊断/选择管理等多类职责，建议后续 `cs-refactor` 拆 partial
