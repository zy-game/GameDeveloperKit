---
doc_type: audit-index
audit: 2026-05-19-filesystem-bugs
scope: Assets/GameDeveloperKit/Runtime/FileSystem/ (5 .cs files) — bug 隐患维度
created: 2026-05-19
status: resolved
total_findings: 5
---

# filesystem-bugs 审计报告

## 范围

`Assets/GameDeveloperKit/Runtime/FileSystem/` 下全部 5 个源文件：`VFSteaming.cs`、`VfsManifest.cs`、`VfsConstants.cs`、`FileModule.cs`、`VFSMeta.cs`。仅扫描 **bug 隐患**（空值路径、边界条件、竞态条件、异常吞没）。

## 总评

共发现 5 条 bug 隐患：2 条 P0、2 条 P1、1 条 P2。
最严重的是 `VFSteaming.WriteAsync` 的边界检查逻辑会阻止所有向新 Bundle 文件的首次写入，这意味着当前 VFS 写入路径在实际上无法正常工作。其次是 `FileModule.WriteAsync` 对同一 Bundle 重复创建 `VFSteaming` 实例，配合 `FileShare.None` 会产生文件锁冲突。
整体代码结构清晰，但边界条件处理和资源生命周期管理存在明显缺口。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | bug | P0 | high | WriteAsync 边界检查阻止写入新 Bundle 文件 | [finding-01.md](finding-01.md) |
| 2 | bug | P0 | high | WriteAsync 重复创建 VFSteaming 导致 FileShare.None 冲突 | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | medium | ReadAsync 边界检查可能拒绝稀疏 Bundle 中的合法读取 | [finding-03.md](finding-03.md) |
| 4 | bug | P1 | medium | GetFileInfo 未找到时静默返回 null | [finding-04.md](finding-04.md) |
| 5 | bug | P2 | medium | Release 对不存在路径静默无操作并浪费一次 SaveAsync | [finding-05.md](finding-05.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 2 | 2 | 1 | 5 |
| **合计** | **2** | **2** | **1** | **5** |

## 下一步建议

- **P0 立刻修**：#1 VFSteaming.WriteAsync 边界检查、#2 FileModule.WriteAsync 重复 VFSteaming —— 当前 VFS 写入路径几乎不可用，建议立刻开 `cs-issue` 修复。
- **P1 本迭代修**：#3 ReadAsync 边界检查、#4 GetFileInfo 静默 null —— 可能导致调用方 NRE 或数据读取失败。
- **P2 有空再看**：#5 Release 静默 no-op —— 低影响但属于浪费 I/O 的不良行为。
