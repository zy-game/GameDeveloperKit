---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "bug-03"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 03：批量加载路径未保护空 `Assets` 列表

## 速答

`BundleInfo.Assets` 是可空字段，单资源查询路径有 null 保护，但多处按标签/类型批量加载直接对 `Info.Assets` 调 `Where()`，清单里某个 bundle 没有资源列表时会空引用。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/Manifest/BundleInfo.cs:40` — `public List<AssetInfo> Assets;` —— 资源列表不是初始化为空列表的字段。
- `Assets/GameDeveloperKit/Runtime/Resource/Manifest/BundleInfo.cs:55` — `if (Assets == null)` —— `TryGetAsset()` 承认并处理了空列表。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs:137` — `this.Info.Assets.Where(...)` —— bundle provider 批量按标签加载未判空。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs:177` — `this.Info.Assets.Where(...)` —— bundle provider 按类型加载未判空。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/WebAssetProvider.cs:133`、`EditorAssetProvider.cs:155`、`BuiltinAssetProvider.cs:86` —— 其他 provider 存在同类路径。

## 影响

清单生成器只要允许空 bundle、依赖 bundle 或资源列表字段缺省，`LoadAssetsByLabelAsync()` / `LoadAssetsByTypeAsync<T>()` / `LoadRawAssetsByLabelAsync()` 就可能在运行时崩溃。触发条件取决于清单数据，因此置信度为 medium。

## 修复方向

统一把 `BundleInfo.Assets` 初始化为空列表，或在 provider 批量查询前使用同一套空列表保护。

## 修复状态

已修复：`BundleInfo.Assets` / `Dependencies` 默认初始化为空列表，Provider 批量查询统一上移到 `ProviderBase` 的安全枚举。

## 建议动作

`cs-issue`，因为这是清单边界数据导致的运行时 bug。
