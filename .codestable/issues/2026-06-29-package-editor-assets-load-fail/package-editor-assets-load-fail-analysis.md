---
doc_type: issue-analysis
issue: 2026-06-29-package-editor-assets-load-fail
status: confirmed
root_cause_type: config
related:
  - package-editor-assets-load-fail-report.md
tags:
  - unity-editor
  - package
  - editor-assets
---

# Package Editor Assets Load Fail 根因分析

## 1. 问题定位

| 关键位置 | 说明 |
|---|---|
| `Assets/GameDeveloperKit/package.json:2` | 包名是 `com.gamedeveloperkit.framework`。该目录作为 UPM 包被其他工程通过 `file:` 引用后，包内资源路径应从 `Assets/GameDeveloperKit/...` 变为 `Packages/com.gamedeveloperkit.framework/...`。 |
| `Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.cs:12` | `UnityBridgeWindow` 把样式表路径固定为 `Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.uss`。 |
| `Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.cs:58` | 打开窗口时直接用上述固定路径 `AssetDatabase.LoadAssetAtPath<StyleSheet>()` 加载样式；包引用环境下该路径不存在，返回 `null`。 |
| `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorWindow.cs:25` | `ResourceEditorWindow` 把 UXML 固定为 `Assets/GameDeveloperKit/Editor/ResourceEditor/UI/ResourceEditorWindow.uxml`。 |
| `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorWindow.cs:121` | 直接用固定 `Assets/...` 路径加载 UXML；包引用环境下会加载失败并进入 `Missing UXML` 分支。 |
| `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherWindow.cs:27` | `ResourcePublisherWindow` 把 UXML 固定为 `Assets/GameDeveloperKit/Editor/ResourcePublisher/UI/ResourcePublisherWindow.uxml`。 |
| `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.cs:20` | `StoryEditorWindow` 把主样式表固定为 `Assets/GameDeveloperKit/Editor/StoryEditor/UI/StoryEditorWindow.uss`。 |
| `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.cs:21` | `StoryEditorWindow` 把 graph 样式表固定为 `Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraph.uss`。 |
| `Assets/GameDeveloperKit/Editor/TagEditor/TagEditorWindow.cs:23` | `TagEditorWindow` 把样式表固定为 `Assets/GameDeveloperKit/Editor/TagEditor/UI/TagEditorWindow.uss`。 |
| `Assets/GameDeveloperKit/Editor/StoryEditor/Model/StoryAuthoringAssetStore.cs:14` | Story 默认作者资源会创建到 `Assets/GameDeveloperKit/Story`，这在包引用工程里会把框架默认数据写到宿主工程 Assets 下，和包内资源路径问题同属路径假设风险。 |
| `Assets/GameDeveloperKit/Editor/StoryEditor/StoryPlayerViewPrefabBuilder.cs:15` | Story 播放器 prefab 生成路径固定到 `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.prefab`，包引用环境下会尝试写入宿主 Assets 路径而不是包路径或用户项目路径。 |

## 2. 失败路径还原

**正常路径**：在本仓库作为 Unity 项目打开时，GameDeveloperKit 源码和资源实际位于 `Assets/GameDeveloperKit/`。用户打开 `GameDeveloperKit/Unity Bridge` 菜单后，`UnityBridgeWindow.CreateGUI()` 调用 `BuildLayout()`，`AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.uss")` 能找到样式表，窗口根元素的 USS class 被正确渲染，布局和样式正常。

**失败路径**：在其他 Unity 工程中通过 `file:` 引用 `Assets/GameDeveloperKit` 这个 UPM 包时，Unity 把包注册为 `com.gamedeveloperkit.framework`。包内 USS/UXML 的可寻址路径变为 `Packages/com.gamedeveloperkit.framework/Editor/...`。用户打开 `UnityBridge` 后，代码仍然请求 `Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.uss`；宿主工程没有这个路径，`AssetDatabase.LoadAssetAtPath` 返回 `null`，后续 UI 只保留未应用样式的 VisualElement 树，表现为窗口布局混乱、样式丢失。

**分叉点**：`Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.cs:58` — 样式加载路径只适配源码直接放在 `Assets/GameDeveloperKit` 的项目内使用方式，没有适配 UPM 包安装后的 `Packages/com.gamedeveloperkit.framework` 路径。

## 3. 根因

**根因类型**：config

**根因描述**：编辑器工具把框架目录位置写死为 `Assets/GameDeveloperKit/...`。这在本仓库开发态成立，但 UPM package 被其他工程通过 `file:` 引用时，Unity 会把包内容暴露在 `Packages/{package-name}/...` 下。代码没有统一解析“框架当前安装根路径”，也没有对 `Assets/...` 与 `Packages/...` 两种安装形态做兼容，因此所有依赖包内 USS、UXML、示例资源或 prefab 路径的编辑器入口都可能加载失败或写入错误位置。

**是否有多个根因**：是。主根因是包内编辑器资源加载使用硬编码 `Assets/GameDeveloperKit/...` 路径；次要根因是部分编辑器生成/默认资源路径也写死到 `Assets/GameDeveloperKit/...`，包引用环境下可能把框架资产写入宿主工程 Assets，或引用不存在的示例资源。

## 4. 影响面

- **影响范围**：不只影响 `UnityBridge`。检索到的固定框架路径覆盖 UnityBridge、ResourceEditor、ResourcePublisher、TagEditor、StoryEditor、Story 播放窗口、ResourceEditor 结果窗口、ResourcePublisher 结果窗口、Story 示例图和 StoryPlayerView prefab 生成。
- **潜在受害模块**：`GameDeveloperKit.UnityBridge`、`GameDeveloperKit.ResourceEditor`、`GameDeveloperKit.ResourcePublisher`、`GameDeveloperKit.TagEditor`、`GameDeveloperKit.StoryEditor`、`GameDeveloperKit.StoryPlayback` 的编辑器 prefab 构建工具，以及运行时 `UIOption` 中引用示例 prefab 的生成代码。
- **数据完整性风险**：有。样式/UXML 加载失败主要是显示问题；但 Story 默认资源和 prefab 生成路径固定到 `Assets/GameDeveloperKit/...`，在宿主工程里可能创建与框架目录同名的项目资产，造成用户工程被框架工具写入不期望的默认资源。
- **严重程度复核**：维持 P0。框架作为包分发是核心集成方式之一，路径假设会导致多个编辑器工具不可用或写入错误位置，影响所有通过 package 方式集成框架的工程。

## 5. 修复方案

### 方案 A：新增统一包路径解析 helper，替换包内资源加载路径

- **做什么**：在 Editor assembly 新增一个内部 helper，例如 `GameDeveloperKitEditorPaths`。它以 `Packages/com.gamedeveloperkit.framework` 为优先路径，必要时回退 `Assets/GameDeveloperKit`，并提供 `EditorAsset(relativePath)`、`LoadEditorAsset<T>(relativePath)` 等 API。将 USS/UXML/prefab 示例等包内只读资源引用改为通过 helper 解析；把用户生成资产路径和包内只读资源路径明确分开。
- **优点**：直接消除两种安装形态的路径分叉；改动集中；不要求所有调用点理解 UPM 路径规则；后续新增编辑器资源可以复用同一入口。
- **缺点 / 风险**：需要批量替换现有路径；必须小心区分“加载包内资源”和“写入宿主工程资产”，否则可能把用户应写入的文件错误写到包目录或继续写到固定框架目录。
- **影响面**：会动 Editor 路径 helper、新的调用点，以及 UnityBridge、ResourceEditor、ResourcePublisher、TagEditor、StoryEditor 等使用固定路径加载包内资源的文件。Runtime 代码只在涉及生成的 `UIOption` 路径时另行评估。

### 方案 B：仅为现有硬编码路径加 `Assets` / `Packages` 双路径 fallback

- **做什么**：保留每个窗口当前 `Assets/GameDeveloperKit/...` 常量，在加载失败时把前缀替换为 `Packages/com.gamedeveloperkit.framework/...` 再加载。例如 `LoadStyleSheet(assetsPath)` 内部先试 `Assets/...`，再试 `Packages/...`。
- **优点**：改动相对小，风险低，能快速修复样式和 UXML 加载失败。
- **缺点 / 风险**：仍然把 package name 和路径规则散落在各处；容易漏掉后续新增路径；对 Story 默认资源和 prefab 生成这类“写路径”问题没有自然边界，后续还会出现同类问题。
- **影响面**：主要影响编辑器窗口样式/UXML加载代码；对生成资产路径需要额外补丁。

### 方案 C：把框架改为只支持 UPM 包路径，全面迁移到 `Packages/com.gamedeveloperkit.framework/...`

- **做什么**：将所有包内资源路径直接改成 `Packages/com.gamedeveloperkit.framework/...`，并要求开发态也通过 package 方式引用框架，不再支持源码放在 `Assets/GameDeveloperKit` 下直接运行。
- **优点**：路径模型单一，包分发语义最明确。
- **缺点 / 风险**：会破坏当前仓库作为 Unity 项目直接打开的开发工作流；测试、示例和现有架构文档中大量 `Assets/GameDeveloperKit/...` 路径需要同步迁移；改动范围大。
- **影响面**：影响整个仓库布局和开发方式，不适合作为当前 P0 的定点修复。

### 推荐方案

**推荐方案 A**，理由：它直接修复根因，同时保留当前仓库开发态和 UPM 包引用态两种路径。方案 B 虽然最快，但会继续制造散落的路径特殊分支；方案 C 改动面过大，不适合当前 issue。方案 A 的关键约束是必须把“包内只读资源路径”和“宿主工程生成/配置资产路径”拆开处理：USS/UXML/包内 prefab 走 package-aware helper，用户生成的 Story 资源、资源构建输出等保留明确的宿主工程 Assets 路径或让用户选择路径。
