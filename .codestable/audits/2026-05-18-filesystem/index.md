---
doc_type: audit-index
audit: 2026-05-18-filesystem
scope: Assets/GameDeveloperKit/Runtime/FileSystem plus Super.FileSystem accessor
created: 2026-05-18
status: active
total_findings: 5
---

# filesystem 审计报告

## 范围

扫描 `Assets/GameDeveloperKit/Runtime/FileSystem/` 下 VFS 实现，以及 `Assets/GameDeveloperKit/Runtime/Super.cs` 中 `Super.FileSystem` 公开入口；对照 `.codestable/architecture/ARCHITECTURE.md` 中 FileSystem 记录，覆盖 bug、安全、性能、可维护性、架构偏离 5 个维度。

## 总评

共发现 5 条问题：P1 3 条、P2 2 条。最值得优先处理的是路径校验不完整导致的 VFS 根目录逃逸风险，以及 `m_Manifest` 未初始化时公开 API 可直接空引用崩溃；此外 bundle 读取缺少格式校验、packed 覆盖写入持续追加导致空间膨胀、读取逻辑未使用 manifest 中的 `BundleName` 与架构元数据表达不一致。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | security | P1 | high | 路径校验不足可逃逸 VFS 根目录 | [finding-01.md](finding-01.md) |
| 2 | bug | P1 | high | Startup 前调用公开 API 会空引用崩溃 | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | medium | Bundle 读取信任 manifest offset/size，损坏清单可读错或异常 | [finding-03.md](finding-03.md) |
| 4 | performance | P2 | high | Packed 文件覆盖写入只追加不回收，bundle 会持续膨胀 | [finding-04.md](finding-04.md) |
| 5 | arch-drift | P2 | medium | Manifest 记录 BundleName 但读取固定使用默认 bundle | [finding-05.md](finding-05.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 2 | 0 | 2 |
| security | 0 | 1 | 0 | 1 |
| performance | 0 | 0 | 1 | 1 |
| maintainability | 0 | 0 | 0 | 0 |
| arch-drift | 0 | 0 | 1 | 1 |
| **合计** | **0** | **3** | **2** | **5** |

## 下一步建议

- **P0 立刻修**：无。
- **P1 本迭代修**：finding-01、finding-02、finding-03 建议走 `cs-issue`，分别补路径规范化/根目录约束、生命周期守卫、bundle 记录校验。
- **P2 有空再看**：finding-04 建议走 `cs-refactor` 设计压缩/回收策略；finding-05 建议走 `cs-issue` 或随 bundle 扩展一起修正读取路径。
