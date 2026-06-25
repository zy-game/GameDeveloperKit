---
doc_type: audit-index
slug: framework-full
date: 2026-06-25
scope: Assets/GameDeveloperKit/Runtime/ (20 modules, ~200+ .cs files)
dimensions: [maintainability, bug, arch-drift]
status: active
---

# 框架全量审计速览

## 范围

`Assets/GameDeveloperKit/Runtime/` 下全部 20 个模块：Core、App、Event、Resource、Operation、Command、Procedure、Timer、Network、Data、Config、Debug、FileSystem、Input、Download、Localization、UI、Combat、Story、StoryPlayback、Sound、Utility。

## 总评

框架整体架构设计清晰：模块按需解析 (`[ModuleDependency]`)、Timer 统一驱动、Debug Profile 软接入、显式 Resource ready 等关键决策一致落地。架构文档详尽记录了每个模块的核心类型、关键行为和已知约束。

主要问题集中在三个方面：(1) 字段命名约定碎片化——`_` 和 `m_` 前缀在模块间混用且 AGENTS.md 规范与实际不一致；(2) Debug Profile 注册/注销模式在 Timer/Procedure/Combat 三个模块中逐字复制；(3) ReferencePool 在生产构建中关闭重复释放校验，存在静默状态损坏隐患。

## 发现清单（交叉分类）

| # | 维度 | 严重度 | 置信度 | 摘要 | 建议动作 |
|---|---|---|---|---|---|
| 01 | arch-drift | P1 | high | Debug 模块目录 `Debug/` 与命名空间 `GameDeveloperKit.Logger` 不一致 | cs-refactor |
| 02 | arch-drift | P1 | high | StoryPlayback 目录下部分文件使用 `GameDeveloperKit.Story` 而非 `GameDeveloperKit.StoryPlayback` 命名空间 | cs-refactor |
| 03 | arch-drift | P2 | high | 字段命名约定在模块间碎片化（`_` vs `m_`），AGENTS.md 声称 `m_` 但多数新模块用 `_` | cs-refactor |
| 04 | arch-drift | P2 | medium | FileSystem 目录名与命名空间 `GameDeveloperKit.File` 不一致（其他模块目录=命名空间后缀） | cs-refactor |
| 05 | arch-drift | P2 | medium | `ResourceModule.modes` 字段无前缀，同一类内 `_manifest`/`_setting` 有 `_` 前缀 | cs-refactor |
| 06 | maintainability | P1 | high | Debug Profile 注册/注销模式在 TimerModule、ProcedureModule、CombatModule 中逐字复制 | cs-refactor |
| 07 | maintainability | P1 | high | `StoryRunner.cs` 1590 行，超出 500 行可维护阈值 | cs-refactor |
| 08 | maintainability | P2 | medium | `TimerModule.OnUpdate/OnLateUpdate/OnFixedUpdate` 12 个重载，参数组合爆炸 | cs-refactor |
| 09 | maintainability | P2 | high | `ResourceModule.UnloadAsset/UnloadRawAsset/UnloadSceneAsset` 三方法结构相同，差仅类型 | cs-refactor |
| 10 | maintainability | P2 | low | `App.Startup()` 内生命周期状态重复检查 | cs-refactor |
| 11 | bug | P0 | high | `ReferencePool` 严格检查关闭时重复释放静默损坏池状态（`m_UsingReferenceCount` 变负，引用重复入队） | cs-issue |
| 12 | bug | P1 | high | `ProcedureModule.Shutdown()` 中 `.GetAwaiter().GetResult()` 同步阻塞 async 有死锁风险 | cs-issue |
| 13 | bug | P1 | high | `ResourceModule.LoadAssetsByLabelAsync` 对 LINQ `Where()` 结果做 `== null` 检查——`Where` 永不为 null，空匹配静默返回而非抛错 | cs-issue |
| 14 | bug | P2 | medium | `ConfigModule.LoadTableAsync` 异常处理中 `TrySetException` 后 `await completionSource.Task` 只为重抛同异常，逻辑冗余 | cs-refactor |
| 15 | bug | P2 | low | `TimerModule.SetTimer(Action<float>, float, bool)` 遗留 API 与 `Delay/Countdown/Interval` 语义路径不一致 | cs-refactor |

## 下一步建议

### 立即处理（P0）
- **#11** ReferencePool 重复释放 → `cs-issue`：在生产构建中也应至少记录错误或抛异常，当前静默损坏不可接受。

### 近期处理（P1，建议下个迭代）
- **#12** ProcedureModule.Shutdown 死锁风险 → `cs-issue`
- **#13** ResourceModule 空值检查无效 → `cs-issue`
- **#01** Debug 命名空间迁移 → `cs-refactor`
- **#02** StoryPlayback 命名空间修正 → `cs-refactor`
- **#06** Debug Profile 模式去重 → `cs-refactor`
- **#07** StoryRunner 拆分 → `cs-refactor`

### 远期处理（P2，有空再看）
- **#03** 字段命名统一
- **#04** FileSystem → File 目录重命名
- **#05** ResourceModule modes 字段前缀
- **#08** TimerModule 重载收敛
- **#09** ResourceModule 卸载方法泛型化
- **#10** App.Startup 状态检查简化
- **#14** ConfigModule 异常处理简化
- **#15** TimerModule 遗留 API 清理
