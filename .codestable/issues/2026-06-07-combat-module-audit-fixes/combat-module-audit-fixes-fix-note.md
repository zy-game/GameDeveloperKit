---
doc_type: issue-fix
issue: 2026-06-07-combat-module-audit-fixes
path: fast-track
fix_date: 2026-06-07
status: fixed
tags:
  - combat
  - audit
  - runtime
  - architecture
---

# Combat Module Audit Fixes 修复记录

## 1. 问题描述

来源：`.codestable/audits/2026-06-07-combat-module/` 的 5 条 finding。用户确认“修复全部”后，按审计清单一次性处理 Combat 模块生命周期、系统回调重入、组件错误口径、组件变更热路径和架构档案漂移。

## 2. 根因

- `World.Dispose()` 只清理底层状态并设置 `m_Disposed`，公开 API 没有 disposed guard。
- `SystemManager` 多个路径直接枚举 `m_Registrations`，用户系统回调中可以通过保存的 world 引用加载/卸载系统。
- `GetComponent<T>()` 和反射版组件查询依赖 massive 的条件断言和底层数据集行为，Combat 层没有统一 guard。
- 实体组件变更时 `Capture(entity)` / `NotifyChanged(entity)` 会逐一检查全部系统，缺少 component-to-system 影响索引。
- Combat feature 已落地，但 `.codestable/architecture/ARCHITECTURE.md` 没有同步记录 Combat 子系统。

## 3. 修复方案

- `World`：为公开 API 增加 disposed guard；`IsAlive` 在 world disposed 后返回 false；`Dispose()` 内部走不抛异常的清理路径。
- `SystemManager`：注册表枚举改为快照；`Registration` 增加 active 标记；移除系统时先标记 inactive 并从索引/列表移除，避免当前枚举继续更新已卸载系统。
- `EntityManager`：缺失组件读取先显式 `Has<T>()`；反射组件查询统一校验 `ComponentBase` 派生和 `IDataSet`，错误口径收束为 `GameException` / `ArgumentException`。
- 性能：`Registration` 缓存 include/exclude 涉及的组件类型，`SystemManager` 维护组件类型到系统注册的索引；组件增删只捕获/通知受影响系统。
- 架构：在 `ARCHITECTURE.md` 新增 Combat 小节和已落地硬边界。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Combat/World.cs`
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs`
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.Registration.cs`
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/CombatModuleTests.cs`
- `.codestable/architecture/ARCHITECTURE.md`
- `.codestable/audits/2026-06-07-combat-module/`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning，0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning，0 error。
- `dotnet build GameDeveloperKit.PlayMode.Tests.csproj --no-restore`：未通过，缺少 `Temp/obj/GameDeveloperKit.PlayMode.Tests/project.assets.json`，需要 restore 或 Unity 生成资产文件。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：未通过，缺少 `Temp/obj/GameDeveloperKit.Editor.Tests/project.assets.json`，需要 restore 或 Unity 生成资产文件。
- `python .codestable/tools/validate-yaml.py --dir .codestable/audits/2026-06-07-combat-module`：通过，6 个文件全部有效。

## 6. 遗留事项

未运行 Unity Test Runner；本次完成了 runtime 与 runtime tests C# 构建验证。PlayMode / Editor 测试项目的 `--no-restore` 构建因缺 NuGet assets 文件被阻塞。
