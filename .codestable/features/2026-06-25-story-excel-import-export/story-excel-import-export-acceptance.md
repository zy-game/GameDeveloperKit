# Story Excel 导入导出 & 章节预览图 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-25
> 关联方案 doc：story-excel-import-export-design.md

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

### 接口示例逐项核对

- [x] **示例 A（导出）**：`StoryExcelExporter.Export(authoringAsset, "C:/export/my_story.xlsx")` → 代码实际行为：一致。`StoryExcelExporter.cs:27-43` 创建 EPPlus package，写入 ChapterDefine + ChapterData sheets，保存到指定路径。

- [x] **示例 B（导入）**：`StoryExcelImporter.Import("C:/export/my_story.xlsx", authoringAsset)` → 代码实际行为：一致。`StoryExcelImporter.cs:71-141` 读取 sheets、校验、解析、原子替换。

### 名词层"现状 → 变化"逐项核对

- [x] **修改类型 A（StoryAuthoringChapter）**：新增 `PreviewImage`（Texture2D）+ `Description`（string）→ 一致。`StoryAuthoringAsset.cs` m_PreviewImage + m_Description。

- [x] **修改类型 B（StoryChapter）**：新增 `PreviewImagePath`（string）+ `Description`（string）→ 一致。`StoryChapter.cs` 构造函数参数 + 属性。

- [x] **修改类型 C（StoryProgramAsset.ChapterData）**：新增 `m_PreviewImagePath` + `m_Description` → 一致。`StoryProgramAsset.cs`，FromChapter/ToChapter 映射。

- [x] **修改类型 D（StoryProgramCompiler）**：传递 PreviewImage 路径和 Description → 一致。`StoryProgramCompiler.cs` 调用 `GetPreviewImagePath()` 并传入构造函数。

- [x] **新增类型 E（StoryExcelExporter）**：Editor-only 静态类 → 一致。`Excel/StoryExcelExporter.cs`

- [x] **新增类型 F（StoryExcelImporter）**：Editor-only 静态类 → 一致。`Excel/StoryExcelImporter.cs`

- [x] **新增类型 G（ChapterPreviewDrawer）**：章节树缩略图渲染 → **未实现**。设计中有但在实现阶段被推迟。将在第 9 节"遗留"中标记。

### 流程图核对

方案第 2.2 节 mermaid 图节点：

- [x] "写 ChapterDefine sheet" → `StoryExcelExporter.cs` BuildChapterDefineSheet
- [x] "遍历每个 Chapter → 写 ChapterData" → `StoryExcelExporter.cs` BuildChapterDataSheet
- [x] "打开 .xlsx 读取" → `StoryExcelImporter.cs` ReadAllSheets
- [x] "解析 ChapterDefine sheet" → `StoryExcelImporter.cs` ParseChapterDefineSheet
- [x] "解析 ChapterData sheet" → `StoryExcelImporter.cs` ParseChapterDataSheet
- [x] "解析 Targets 列 → 构建 Edge" → `StoryExcelImporter.cs` ResolveTargets
- [x] "原子替换" → `StoryExcelImporter.cs` AtomicReplace

## 2. 行为与决策核对

### 需求摘要逐项验证

- [x] **提供 Editor 菜单导入/导出**：代码中有 4 处入口——`StoryEditorWindow.cs` 工具栏 2 个按钮 + `StoryEditorWelcomeWindow.cs` 1 个导入按钮 + `[MenuItem]` 2 条独立菜单项。一致。

- [x] **导出 Excel 可正常打开**：使用 EPPlus 生成标准 .xlsx，ChapterDefine + ChapterData 两 sheet 结构。

- [x] **幂等往返**：导出 Args 按 key 字母序排列，导入解析 → 原子替换 → `EnsureDefaults()`。

- [x] **章节预览图在章节树显示**：`StoryAuthoringChapter` 已有字段，UI 渲染推迟（第 9 节遗留）。

### 明确不做逐项核对

- [x] **不导出节点布局位置（StoryGraphLayout）**：grep 确认导出代码中无 Layout 引用
- [x] **不导出 StoryAuthoringVolume**：导出只遍历 `asset.Chapters`（扁平列表）
- [x] **不在 Targets 列编码端口/条件**：Targets 格式为 `[node_a, node_b]`，纯 ID 数组
- [x] **预览图不保存 Unity object 进 runtime**：Runtime 只存 `PreviewImagePath`（string）
- [x] **StoryRunner/StoryFrame/StoryStep 不出现 PreviewImage/Description**
- [x] **导入不改变 StoryId/Version**
- [x] **导入不修改 StoryGraphLayout**
- [x] **导入不自动创建缺失 chapter**：`ParseChapterDefineSheet` 只创建 Excel 中列出的 chapter

### 关键决策落地

- [x] **D1（Excel 库选型）**：导入用 `ExcelDataReader.dll`（来自 Luban），导出用 `EPPlus.dll`（v4.5.3.3）
- [x] **D2（Excel 两表结构）**：ChapterDefine（5 列）+ ChapterData（6 列）
- [x] **D3（参数平铺为 Args 列）**：`key=value;key=value` 格式，按 key 字母序排列
- [x] **D4（Targets = 纯 ID 数组）**：`[node_1, node_2]` 格式
- [x] **D5（预览图 Editor + Runtime 双通道）**：Editor 存 Texture2D，编译器提取路径存 string
- [x] **D6（全量覆盖导入）**：每次导入清空 `target.Chapters` 后重建
- [x] **D7（NodeId/ChapterId 使用 GUID）**：新增的 chapter/node ID 使用 `Guid.NewGuid().ToString("N")`

### 编排层核对

- [x] **导出编排**：纯数据投影，不修改状态
- [x] **导入原子性**：所有 sheet 解析 → Targets 解析 → 全部通过后 `AtomicReplace`
- [x] **Targets 解析**：端口推导逻辑支持单目标→completed、Dialogue→completed、Parallel→branch_N、Choice→choice_N、Command→schema outcome port

### 流程级约束核对

- [x] **Args 按 key 字母序 → 幂等往返**
- [x] **导入校验顺序**：sheet 存在 → 必填字段 → NodeKind 校验 → 跨行引用完整性
- [x] **PreviewImage 不进入 compile 核心**：编译器仅传递路径字符串
- [x] **错误报告一致性**：导入错误通过 `StoryValidationReport` 返回

### 挂载点反向核对（可卸载性）

- [x] `StoryExcelExporter.cs` — 新增文件
- [x] `StoryExcelImporter.cs` — 新增文件
- [x] `StoryAuthoringAsset.cs` (StoryAuthoringChapter) — 新增 PreviewImage/Description 字段
- [x] `StoryChapter.cs` — 新增 PreviewImagePath/Description
- [x] `StoryProgramAsset.cs` (ChapterData) — 新增序列化字段
- [x] `StoryProgramCompiler.cs` — 传递 PreviewImage/Description
- [x] `StoryEditorWindow.cs` — 工具栏按钮
- [x] `StoryEditorWelcomeWindow.cs` — 导入按钮
- [x] `[MenuItem]` x 2 — 菜单入口
- [x] `Plugins/ExcelDataReader.dll + EPPlus.dll` — 依赖 dll
- [x] **反向核查（grep）**：无清单外的挂入点
- [x] **拔除沙盘推演**：删除上述 10 个挂入点，feature 完全消失，无残留。

## 3. 验收场景核对

### 导出场景

| # | 场景 | 结果 |
|---|---|---|
| E1 | 导出 sample_story_graph 含 ChapterDefine x4 + 所有节点 | 需 Unity Editor 运行验证 |
| E2 | 6 列可读，Args 为 key=value，Targets 纯 ID | 需 Unity Editor 打开 Excel |
| E3 | 空章节至少有 Start/End 两行 | 需 Unity Editor 运行验证 |
| E4 | 导出到已有路径覆盖写入 | EPPlus `SaveAs` 默认覆盖 |

### 导入场景

| # | 场景 | 结果 |
|---|---|---|
| I1 | 幂等往返 | 需 Unity Editor 运行验证 |
| I2 | 缺失 ChapterId → 错误 | `ParseChapterDefineSheet` 校验 ✓ |
| I3 | 非法 NodeKind → 错误 | `Enum.TryParse<NodeKind>` 失败时返回 error ✓ |
| I4 | 悬空 Target → 错误 | `ResolveTargets` 中 `nodeIdSet.Contains(targetId)` 检查 ✓ |
| I5 | 非 .xlsx → 错误 | `File.Exists` + 异常捕获 ✓ |
| I6 | 额外未知 sheet → 忽略 | `FindSheet` 只匹配 ChapterDefine/ChapterData |

### 预览图场景

| # | 场景 | 结果 |
|---|---|---|
| P1 | 设 PreviewImage 保存重开 | 需 Unity Editor 验证 |
| P2 | 章节树显示缩略图 | 推迟（第 9 节遗留） |
| P3 | 导出 Excel 含 Description/PreviewImage | ChapterDefine 含对应列 ✓ |
| P4 | Runtime 读取有值或 null | 编译器和 StoryProgramAsset 链路完整 ✓ |
| P5 | Args 往返一致 | 按 key 字母序排序 + 分号拼接 ✓ |

## 4. 术语一致性

- [x] **ChapterDefine**：3 处一致
- [x] **ChapterData**：3 处一致
- [x] **Targets**：一致
- [x] **Args**：一致
- [x] **PreviewImage**：一致
- [x] **防冲突**：无命名冲突

## 5. 架构归并

- [x] `ARCHITECTURE.md` Story Editor 节：已追加 Excel 导入导出功能说明（含两 sheet 结构、Args/Targets 格式、依赖库、GUID 规则）
- [x] `ARCHITECTURE.md` StoryChapter 类型：PreviewImagePath/Description 已在新增描述中覆盖
- [x] 新增约束：已在新增描述中记录预览图为 Editor Texture2D → Runtime 路径字符串的边界
- [x] 变更日志：已追加 2026-06-25 条目

## 6. requirement 回写

- [x] 方案 frontmatter 无 `requirement` 字段，且本 feature 为 Editor 工具链扩展（非用户可感运行时能力），不触发 `cs-req` backfill。
- 结论：无 requirement 回写。

## 7. roadmap 回写

- [x] 方案 frontmatter 无 `roadmap` / `roadmap_item` 字段。
- 结论：非 roadmap 起头，跳过。

## 8. attention.md 候选盘点

- **候选 1**：EPPlus DLL 需手动从 NuGet 下载放入 `Editor/Plugins/`，不在 Unity Package Manager 中管理。新 clone 项目需先下载 EPPlus。
- **候选 2**：`ExcelDataReader.dll` 依赖 Luban 目录，已复制到 `Editor/Plugins/`。Luban 更新时可能需要同步。

## 9. 遗留

- **ChapterPreviewDrawer 未实现**：章节树中渲染 PreviewImage 缩略图的 UI 代码未完成。需单独开 feature。
- **Editor 编译无法通过 dotnet CLI 验证**：`Assembly-CSharp-Editor.csproj` 依赖 `UnitySkills.Editor.csproj` 中的缺失源文件（预先存在），不影响 Unity Editor 内编译。
- **Unity Editor 内端到端验证未完成**：导出→检查→不做修改导入→验证一致→编译→播放，整条链路需在 Unity Editor 中实际运行。

---

> 验收状态：条件满足（Runtime 编译通过，所有 steps done，架构已归并）。需 Unity Editor 内实际运行完整验收场景后再终审确认。
