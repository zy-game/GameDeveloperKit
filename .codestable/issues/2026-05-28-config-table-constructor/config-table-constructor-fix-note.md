---
doc_type: issue-fix
issue: 2026-05-28-config-table-constructor
path: fast-track
fix_date: 2026-05-28
tags: [config, reflection, runtime]
---

# ConfigTable Constructor MissingMethod 修复记录

## 1. 问题描述

运行配置模块相关测试或加载配置表时，控制台出现多次 `NullReferenceException`，并伴随 `MissingMethodException: Constructor on type 'GameDeveloperKit.Config.ConfigTable`1[...]' not found.`。异常栈指向 `ConfigTableBuilder.Build` 通过反射创建 `ConfigTable<T>` 的位置。

## 2. 根因

`ConfigTableBuilder.Build` 使用 `Activator.CreateInstance(tableType, args)` 创建泛型配置表，但 `ConfigTable<T>` 的构造函数是 `internal`。该 `Activator` 重载只匹配 public 构造函数，因此运行时找不到可用构造函数并抛出 `MissingMethodException`。

## 3. 修复方案

保留 `ConfigTable<T>` 构造函数的 `internal` 可见性，只在 `ConfigTableBuilder` 内部创建表实例时改用带 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic` 的 `Activator.CreateInstance` 重载，允许反射调用非公开实例构造函数。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Config/Internal/ConfigTableBuilder.cs`

## 5. 验证结果

- 通过：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore`，0 warning，0 error。
- 通过：`dotnet build GameDeveloperKit.Runtime.Tests.csproj`，测试程序集编译通过；保留 1 个既有 `NoKeyConfig.Name` 未赋值 warning。
- 未完成：`dotnet test GameDeveloperKit.Runtime.Tests.csproj --filter "FullyQualifiedName~ConfigModuleTests"` 和 `--list-tests` 没有列出或执行 Unity Test Runner 测试。
- 未完成：尝试使用 `D:\unitycn\editor\2022.3.62f2\Editor\Unity.exe -batchmode -runTests -testPlatform EditMode -testFilter GameDeveloperKit.Tests.ConfigModuleTests`，未生成 `Temp\ConfigModuleTestsResults.xml` 或日志文件；为避免残留后台进程，已停止本次启动的 Unity batchmode 进程。

## 6. 遗留事项

- 仍需在 Unity Editor Test Runner 中运行 `GameDeveloperKit.Tests.ConfigModuleTests` 做最终测试确认。
