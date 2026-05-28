---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "arch-drift-04"
nature: arch-drift
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 04：Web 模式未接入 `WebAssetProvider`

## 速答

`ResourceMode.Web` 会创建 `WebGLMode`，但 `WebGLMode` 初始化 package 时仍创建 `BundleAssetProvider`；真正用 `UnityWebRequestAssetBundle` 的 `WebAssetProvider` 在资源模块内没有调用点。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceMode.cs:21` — Web 模式注释说明资源通过 Web 环境加载。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:377` — `ResourceMode.Web => new WebGLMode(_manifest)` —— Web 模式落到 `WebGLMode`。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.InitializePackageOperationHandle.cs:59` — `var provider = new BundleAssetProvider(bundle);` —— WebGL package 初始化仍选择本地 bundle provider。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.InitializeBundleOperationHandle.cs:61` — `Super.File.ReadAsync(bundlePath)` —— `BundleAssetProvider` 读取本地 VFS。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/WebAssetProvider.cs:12` — `public sealed partial class WebAssetProvider` —— Web provider 存在，但 `rg` 只找到类型定义和自身分部文件，没有被 mode 创建。

## 影响

配置 `ResourceMode.Web` 时，资源包不会按 Web URL 路径加载，而是走本地 VFS 读取。除非 bundle 名恰好也是本地文件路径，否则 Web 模式 package 初始化会失败；同时 `WebAssetProvider` 的行为无法通过正常模式路径覆盖到。

## 修复方向

明确 `ResourceMode.Web` / `WebGLMode` / `WebAssetProvider` 的职责关系：Web 模式应创建 Web provider，或删除未接线 provider 并把 Web 加载语义并入现有 mode。

## 修复状态

已修复：`WebGLMode.InitializePackageOperationHandle` 已创建 `WebAssetProvider`，Web bundle 初始化走 `UnityWebRequestAssetBundle`。

## 建议动作

`cs-issue`，因为模式选择和实际 provider 行为不一致，会导致 Web 加载路径不可用。
