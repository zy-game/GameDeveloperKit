---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "arch-drift-05"
nature: arch-drift
severity: P1
confidence: medium
suggested_action: cs-refactor
status: retracted
---

# Finding 05：已撤销：Editor-only provider 仍放在 Runtime 资源模块

## 速答

已撤销。项目约定确认：`EditorAssetProvider` 中通过 `#if UNITY_EDITOR` 包裹的 `UnityEditor` API 留在 Runtime 目录是合理设计，不需要移动到 Editor assembly。

## 关键证据

- `.codestable/architecture/ARCHITECTURE.md:132` — 明确记录 `EditorSimulatorMode` / `EditorProvider` 当前在 Runtime，后续接入 `UnityEditor` API 时必须先隔离到 Editor-only asmdef。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs:12` — `EditorAssetProvider` 位于 Runtime Resource 目录。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs:83` — `#if UNITY_EDITOR` —— provider 内部已有编辑器分支。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs:94` — `UnityEditor.AssetDatabase.LoadAssetAtPath(...)` —— 已接入 Editor-only API。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs:96` — 非编辑器环境抛 `EditorProvider is only available in Unity Editor.` —— 运行时仍可能被 public mode 选择后才失败。

## 影响

不作为有效问题统计。保留本文件仅用于记录审计结论修正，避免后续重复把该设计误报为架构偏离。

## 修复方向

无需移动到 Editor assembly。

## 建议动作

无。
