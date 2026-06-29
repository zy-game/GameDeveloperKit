---
doc_type: issue-fix
issue: 2026-06-29-package-editor-assets-load-fail
path: standard
fix_date: 2026-06-29
related:
  - package-editor-assets-load-fail-analysis.md
tags:
  - unity-editor
  - package
  - editor-assets
---

# Package Editor Assets Load Fail 修复记录

## 1. 实际采用方案

采用分析阶段确认的方案 A：新增统一 Editor 路径解析入口，优先使用当前 assembly 对应的 UPM package 路径，回退到 `Packages/com.gamedeveloperkit.framework` 和 `Assets/GameDeveloperKit`。包内只读 USS / UXML 加载点统一通过该入口加载，兼容框架源码开发态和 `file:` package 引用态。

同时按用户补充要求，把 Story 示例资产、StoryPlayerView prefab 生成路径、运行时示例 prefab 路径、Luban 默认输出路径和测试里的路径假设一并收口：宿主工程生成/示例资源写到 `Assets/Bundles/...` 或 `Assets/Generated/...`；框架源码/包内 fixture 读取通过 package-aware fallback 解析。

实现时把 `GameDeveloperKitEditorPaths` 放在已参与当前 `GameDeveloperKit.Editor.csproj` 编译的 `UnityBridgeWindow.cs` 文件末尾，避免手动修改 Unity 生成的 csproj；Unity 重新生成项目文件后可再独立拆成单文件。

## 2. 改动文件清单

- `Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.cs` — 新增 `GameDeveloperKitEditorPaths`，并将 UnityBridge USS 加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorWindow.cs` — ResourceEditor UXML / USS 加载改为 package-aware 加载，缺失提示显示解析后的实际路径。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorCheckResultWindow.cs` — 结果窗口 USS 加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceBuildPublishResultWindow.cs` — 构建结果窗口 USS 加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherWindow.cs` — ResourcePublisher UXML 加载改为 package-aware 加载，缺失提示显示解析后的实际路径。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherResultWindow.cs` — 发布结果窗口 USS 加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/TagEditor/TagEditorWindow.cs` — TagEditor USS 加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.cs` — StoryEditor 主样式和 NodeGraph 样式加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/StoryEditor/Playback/StoryEditorPlaybackWindow.cs` — Story 播放窗口样式加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/StoryEditor/Welcome/StoryEditorWelcomeWindow.cs` — Story 欢迎窗口样式加载改为 package-aware 加载。
- `Assets/GameDeveloperKit/Editor/StoryEditor/Model/StoryAuthoringAssetStore.cs` — Story 默认 authoring asset 和示例图路径迁到 `Assets/Bundles/Story`；打开示例时按需把包内图片/音频复制到宿主 `Assets/Bundles/Story`。
- `Assets/GameDeveloperKit/Editor/StoryEditor/StoryPlayerViewPrefabBuilder.cs` — StoryPlayerView prefab 生成路径使用 `Assets/Bundles/Playback/StoryPlayerView.prefab`。
- `Assets/GameDeveloperKit/Editor/LubanConfigEditor/LubanEditorProfiles.cs` — Luban 默认输出目录迁到 `Assets/Generated/Luban/...`，避免默认写入框架目录。
- `Assets/GameDeveloperKit/Runtime/UI/Custom/Loading/LoadingWindow.Design.g.cs` — Loading 示例 UI prefab 路径迁到 `Assets/Bundles/UI/Loading.prefab`。
- `Assets/GameDeveloperKit/Runtime/Config/ConfigModule.cs` — 编辑器环境下为 `Assets/GameDeveloperKit/...` / `Packages/com.gamedeveloperkit.framework/...` 配置 fixture 文件读取增加窄 fallback。
- `Assets/GameDeveloperKit/Tests/Editor/*.cs`、`Assets/GameDeveloperKit/Tests/Runtime/*.cs` — 测试中的 Story 示例路径改到 `Assets/Bundles/Story`；源码扫描和包内 fixture 读取改为根据当前安装形态解析。

## 3. 验证结果

- `rg 'AssetDatabase\.LoadAssetAtPath<StyleSheet>|AssetDatabase\.LoadAssetAtPath<VisualTreeAsset>|"Assets/GameDeveloperKit/Editor' Assets/GameDeveloperKit/Editor -g '*.cs'`：无命中，包内 USS / UXML 固定路径加载点已清理。
- `rg -n "Assets/GameDeveloperKit" Assets/GameDeveloperKit/Editor Assets/GameDeveloperKit/Runtime Assets/GameDeveloperKit/Tests -g '*.cs'`：仅剩 package-aware fallback / 测试源码根解析用途。
- `git diff --check -- <本次修改的代码文件>`：通过；仅输出 Git 关于 LF 将来可能替换为 CRLF 的提示。
- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 errors。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore --disable-build-servers`：通过，0 errors；保留既有 UnityBridge / TextureImporter 警告。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore --disable-build-servers`：通过，0 errors；保留既有 Timer 过时 API 警告。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore --disable-build-servers`：通过，0 errors。

未能在当前环境中实际打开一个外部 Unity 工程按 `file:` 引用复现；本次用代码路径、检索和 Unity 生成工程编译验证覆盖修复点。

## 4. 遗留事项

- `Assets/GameDeveloperKit/Story/*.asset` 和 `ProjectSettings/GameDeveloperKitResourceEditorSettings.asset` 里仍存在历史序列化路径。这些是已落盘 Unity 资产 / ProjectSettings，不在本次代码修复中手动搬迁，避免破坏 GUID 或改变现有项目配置；需要单独做一次 Unity 内资产迁移时再处理。
- `Assets/GameDeveloperKit/Simples/*` 仍作为包内只读示例源保留。打开 Story 示例时会按需复制图片/音频到宿主工程 `Assets/Bundles/Story`，不会覆盖用户已有文件。
- `Assets/Bundles/UI/Loading.prefab` 的实体迁移未手动执行；当前只修正运行时示例窗口的声明路径。若要让示例 UI 在干净宿主工程中直接可加载，需要用 Unity 资产迁移流程把现有 Loading prefab 移到目标路径并保留/重建 meta。
