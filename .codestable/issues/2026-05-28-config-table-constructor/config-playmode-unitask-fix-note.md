---
doc_type: issue-fix
issue: 2026-05-28-config-table-constructor
path: fast-track
fix_date: 2026-05-28
tags: [config, unitask, playmode-tests]
---

# Config PlayMode UniTask 异常修复记录

## 1. 问题描述

运行 Runtime/PlayMode 测试时，`ConfigModule.LoadTableBySourceAsync` 附近反复出现 `NullReferenceException`，同时预期失败用例会通过 `UniTaskScheduler.PublishUnobservedTaskException` 打印异常。

## 2. 根因

`ConfigModule.LoadTableAsync` 将 `LoadTableBySourceAsync(source).Preserve()` 缓存在 pending 字典中，PlayMode 测试又通过 `GetAwaiter().GetResult()` 同步等待会让步的 UniTask，导致 UniTask 状态机和 memoized source 被不安全地消费。

部分测试只检查失败句柄的 `Status/Error`，没有观察 `OperationHandle` 内部完成源上的失败/取消状态，GC 后会被 UniTask 视为未观察异常。

## 3. 修复方案

- `ConfigModule` 改用 `UniTaskCompletionSource<IConfigTable>` 共享 pending load，首个调用方负责真实加载并写回结果或异常，后续调用方 await 同一个 completion source。
- `ConfigModuleTests` 改为 `UnityTest` + `UniTask.ToCoroutine`，所有异步加载和异步异常断言都使用 `await`。
- `OperationModule` 对同步执行抛出的异常完成源做内部观察；测试中外部设置失败/取消的场景显式观察完成源。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Config/ConfigModule.cs`
- `Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs`
- `Assets/GameDeveloperKit/Runtime/Operation/OperationModule.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/ConfigModuleTests.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/OperationModuleTests.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/DownloadOperationHandleTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 error。

## 6. 遗留事项

Unity Editor 当前已有多个 Unity 进程运行，未强行启动 batchmode 抢项目锁；仍建议在已打开的 Editor 中重跑 PlayMode Test Runner 做最终运行验证。
