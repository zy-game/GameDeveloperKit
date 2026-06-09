---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "bug-03"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 03：非 Bundle 资源模式用 bundle 名判断 package，导致包卸载不可达

## 速答

`StreamingAssetMode`、`EditorSimulatorMode`、`WebGLMode` 的 `HasPackage(package)` 把 `ProviderBase.Info.Name` 当 package 名比较，但 `Info` 实际是 `BundleInfo`，因此 package 名和 bundle 名不同的时候 `ResourceModule.UninitializePackageAsync(package)` 找不到目标模式。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:146` — 卸载入口用 `modes.FirstOrDefault(x => x.HasPackage(package))` 选择 play mode。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.cs:39` 到 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.cs:42` — `HasPackage` 比较 `x.Info.Name == package`。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/EditorSimulatorMode.cs:40` 到 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/EditorSimulatorMode.cs:42` — 同样比较 provider 的 `Info.Name`。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.cs:39` 到 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.cs:42` — 同样比较 provider 的 `Info.Name`。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.InitializePackageOperationHandle.cs:44` 与 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.InitializePackageOperationHandle.cs:59` — 初始化时先按 `PackageInfo.Name` 找 package，再为 package 内每个 `BundleInfo` 创建 provider；provider 的 `Info` 是 bundle 而不是 package。

## 影响

常见清单中 package 名通常是逻辑资源组，bundle 名通常是构建产物名。二者不同会导致非 Bundle 模式下已初始化 package 无法通过公开 API 卸载，资源包和内部 provider 长期驻留，热更新/切包/场景切换时尤其容易累积。

## 修复方向

按 `Manifest.Packages` 查 package，再判断 package 的 bundle 集合是否有已初始化 provider；或在 mode 内维护 package 到 provider 的显式索引。

## 建议动作

`cs-issue`，因为公开 package 生命周期 API 的行为会直接失败。
