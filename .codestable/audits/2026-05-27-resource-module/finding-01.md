---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "bug-01"
nature: bug
severity: P0
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 01：`Startup()` 无法完成资源模块自举

## 速答

`ResourceModule.Startup()` 在任何 mode 创建前调用自己的 `LoadAssetAsync()`，会命中 `modes.Count == 0` 的保护并抛错；后续清单 operation 还在 `_setting` 赋值前执行，启动链路存在多处硬阻断。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:35` — `var handle = await LoadAssetAsync("Resources/ResourceSettings");` —— 启动第一步走公开加载 API。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:136` — `if (modes.Count == 0)` —— 公开加载 API 在 mode 列表为空时直接抛 `GameException`。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:41` — `Super.Operation.Execute<ManifestOperationHandle>(_setting)` —— `_setting` 在 `ResourceModule.cs:50` 才从 handle 赋值。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.ManifestOperationHandle.cs:25` — `string url = args[0] as string;` —— Manifest operation 期望收到 URL 字符串，但调用侧没有传入该参数。

## 影响

只要 `ResourceModule.Startup()` 被正常调用，资源模块就会在加载 `ResourceSettings` 前失败。即使绕过第一处失败，清单下载也会因为 `_setting` 尚未赋值和参数类型不匹配继续失败。

## 修复方向

把 settings/manifest 的 bootstrap 加载改成不依赖 `ResourceModule.LoadAssetAsync()` 的独立路径，并明确 `ManifestOperationHandle` 的 key 与 URL 参数契约。

## 修复状态

已修复：`Startup()` 直接通过 Unity `Resources.Load<ResourceSettings>("ResourceSettings")` 读取设置，再以 `ResourceSettings.ManifestLocation` 执行 `ManifestOperationHandle`；manifest operation 支持 HTTP/HTTPS 与本地 / StreamingAssets 读取。

## 建议动作

`cs-issue`，因为这是启动路径上的确定性运行时 bug。
