---
doc_type: audit-finding
audit: 2026-06-26-resource-commercial-readiness
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 02：资源编辑器构建配置被忽略

## 速答

`ResourceBuildSettings` 看起来可配置，但实际构建时不读这些配置：`OutputRoot`、`Target`、`ManifestFileName` 的 getter 直接返回常量或空串，`EnsureDefaults()` 还会把 backing field 重置掉。最终 `ResourceBuildExecutor` 继续使用 `activeBuildTarget`、固定输出根目录和固定 manifest 名，用户在 Editor 里改的值不会影响真实构建。

## 关键证据

- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorWindow.cs:1004-1009` — 窗口保存时确实把 `OutputRoot`、`Target`、`CleanOutput`、`ManifestFileName` 传回设置对象，说明 UI 层是按“可配置”设计的。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorSettings.cs:190-223` — `OutputRoot` 返回 `OUTPUT_ROOT`，`Target` 返回空串，`ManifestFileName` 返回 `ResourceSettings.MANIFEST_NAME`，getter 不反映 backing field。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorSettings.cs:256-271` — `EnsureDefaults()` 每次都会把 `m_OutputRoot`、`m_Target`、`m_CleanOutput`、`m_ManifestFileName` 重置掉。
- `Assets/GameDeveloperKit/Editor/ResourceEditor/Build/ResourceBuildExecutor.cs:56-57,409-410` — 实际 build 仍取 `EditorUserBuildSettings.activeBuildTarget`、固定 `ResourceBuildSettings.OUTPUT_ROOT` 和 `ResourceSettings.MANIFEST_NAME`。

## 影响

用户在资源编辑器里修改的输出路径、目标平台和 manifest 名称不会真正生效，CI 或多渠道发布会生成和界面预期不一致的产物，属于直接影响交付的功能性错误。

## 修复方向

让 getter 返回 backing field，停止在 `EnsureDefaults()` 里无条件覆盖用户输入，并让 build executor 以 `context.BuildSettings` 为准，而不是硬编码常量和当前 Unity active build target。

## 建议动作

`cs-issue`，因为这是公开 Editor 配置与真实构建行为不一致的确定性 bug。
