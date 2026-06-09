---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "bug-09"
nature: bug
severity: P2
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 09：`LoadRawAssetsByLabelAsync` 只读取第一个命中的资源模式

## 速答

`ResourceModule.LoadAssetsByLabelAsync` 会遍历所有命中 mode，但 `LoadRawAssetsByLabelAsync` 只取 `FirstOrDefault`，同一 label 分布在 builtin 和当前资源模式时会漏掉后续 raw assets。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:184` 到 `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:205` — 普通 asset 按 label 加载时使用 `modes.Where(...)` 并聚合每个 mode 的结果。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:267` 到 `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:281` — raw asset 按 label 加载时使用 `FirstOrDefault(...)`，然后直接返回单个 mode 的结果。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:49` 到 `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:62` — runtime 通常同时存在 `BuiltinMode` 和配置选择的资源 mode，因此 label 跨 mode 分布是正常状态。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.cs:155` 到 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.cs:164`、`Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.cs:156` 到 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.cs:165` — 各 mode 自身都支持返回多个 raw handle，漏聚合发生在 `ResourceModule` 门面层。

## 影响

同一个 label 同时标在内置 raw 配置和远端/StreamingAssets raw 配置时，调用者只拿到第一个 mode 的结果。因为返回值是成功的列表而不是错误，缺失资源可能在业务层表现为配置不完整或热更数据没有生效。

## 修复方向

对齐 `LoadAssetsByLabelAsync` 的门面逻辑：遍历所有 `modes.Where(pm => pm.HasAsset(label))`，聚合每个 mode 的 raw handle。

## 建议动作

`cs-issue`，因为这是公开资源查询 API 的遗漏。
