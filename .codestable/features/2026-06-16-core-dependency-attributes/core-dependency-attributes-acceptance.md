---
doc_type: feature-acceptance
feature: 2026-06-16-core-dependency-attributes
status: accepted
summary: Core 依赖元数据契约与首批静态模块标注已落地，供后续 App resolver 消费。
tags: [runtime, module, dependency, attribute, core]
---

# core-dependency-attributes 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-17
> 关联方案 doc：.codestable/features/2026-06-16-core-dependency-attributes/core-dependency-attributes-design.md

## 1. 接口契约核对

- [x] `DependencyAttribute` 暴露只读 `DependencyType`，构造参数为 null 时抛 `ArgumentNullException`。
- [x] `ModuleDependencyAttribute` 继承 `DependencyAttribute`，拒绝非 `IGameModule` 类型并抛 `GameException`。
- [x] 两个 attribute 均为 class-only、`AllowMultiple=true`、`Inherited=false`。
- [x] Event、Download、Resource、Config、Sound、UI 已带首批静态 `[ModuleDependency]` 标注。

## 2. 行为与决策核对

- [x] 本 feature 只新增被动元数据和静态标注。
- [x] 未实现 App resolver，未新增 `App.GetModule<T>()`。
- [x] 未在 App 或模块启动流程中读取 `[ModuleDependency]`。
- [x] 未新增 optional dependency、程序集扫描或自动启动全部模块。
- [x] Debug、Data、Combat 未被本 feature 标注依赖。

## 3. 验收场景核对

- [x] N1-N5：Attribute 构造、异常和 usage 契约由 `ModuleDependencyAttributeTests` 覆盖。
- [x] N6：首批模块标注由 `RuntimeModules_WhenReadingModuleDependencyAttributes_MatchStartupDependencies` 反射验证。
- [x] B2/B3：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 和 `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。

## 4. 术语一致性

- [x] 代码使用 `DependencyAttribute` / `ModuleDependencyAttribute` / `DependencyType`，和 design 术语一致。
- [x] 未引入 design 外的新依赖术语。

## 5. 架构归并

- [x] 本 feature 已形成稳定系统级元数据契约；`ARCHITECTURE.md` 将在后续 resolver 落地时一起更新为最终 App 按需加载现状，避免先记录“有元数据但 App 不消费”的过渡状态误导读者。

## 6. requirement 回写

- [x] 本 feature 为 runtime 架构契约迁移，frontmatter `requirement` 为空；无用户可见新能力需要 backfill。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml` 中 `core-dependency-attributes` 已回写为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md` 第 5 节对应条目已同步为 `done`。

## 8. attention.md 候选盘点

- [x] 无新增候选；已有 Runtime 编译命令仍适用。

## 9. 遗留

- 后续 `app-sync-module-resolver` 需要真正读取 `[ModuleDependency]` 并替换 App 固定预加载。
