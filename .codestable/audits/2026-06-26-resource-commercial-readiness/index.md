---
doc_type: audit-index
audit: 2026-06-26-resource-commercial-readiness
scope: Assets/GameDeveloperKit/Runtime/Resource + Assets/GameDeveloperKit/Editor/ResourceEditor + Assets/GameDeveloperKit/Editor/ResourcePublisher
created: 2026-06-26
status: active
total_findings: 4
---

# resource-commercial-readiness 审计报告

## 范围

审计 `Assets/GameDeveloperKit/Runtime/Resource/`、`Assets/GameDeveloperKit/Editor/ResourceEditor/`、`Assets/GameDeveloperKit/Editor/ResourcePublisher/`。重点检查可商业化交付必须稳定的资源加载、场景卸载、编辑器构建配置、远端发布和密钥存储。

## 总评

本轮保留 4 条有效可行动问题，全部为 bug × P1。`SecretId` / `SecretKey` 本地明文保存已按用户确认从可行动项中略过：当前只是本地便利用途，后续不会提交。当前仍需优先处理构建配置不生效、场景卸载不真正释放、删除远端版本会留下过期 `publish.json` 指针，以及 package 卸载会误伤共享 bundle。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 2 | bug | P1 | high | 资源编辑器构建配置被忽略，输出 / 目标 / manifest 名称不生效 | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | high | 场景句柄卸载只清引用，不会真正 `UnloadSceneAsync` | [finding-03.md](finding-03.md) |
| 4 | bug | P1 | high | 删除远端版本后没有同步清理 `publish.json` 当前指针 | [finding-04.md](finding-04.md) |
| 5 | bug | P1 | medium | 解除 package 初始化会误删其他 package 仍共享的 bundle | [finding-05.md](finding-05.md) |

已略过：`finding-01.md`，`SecretId` / `SecretKey` 本地保存不作为本轮可行动项。

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 4 | 0 | 4 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 0 | 0 | 0 |
| maintainability | 0 | 0 | 0 | 0 |
| arch-drift | 0 | 0 | 0 | 0 |
| **合计** | **0** | **4** | **0** | **4** |

## 下一步建议

- **先开 issue**：2、3、4、5 应走 `cs-issue`。
- **优先级**：先修 4，再修 2、3、5。
- **原因**：4 直接影响线上版本指向，2/3/5 会在构建、场景切换和包卸载时制造不可预测故障。
