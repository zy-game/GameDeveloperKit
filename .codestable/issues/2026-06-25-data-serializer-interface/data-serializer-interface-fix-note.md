---
doc_type: issue-fix
issue: 2026-06-25-data-serializer-interface
path: fast-track
fix_date: 2026-06-25
tags: []
---

# DataModuleTests.CustomDataSerializer 未实现 IDataSerializer 新接口 修复记录

## 1. 问题描述

`Assets\GameDeveloperKit\Tests\Runtime\DataModuleTests.cs` 中的两个本地 serializer 只实现了泛型 `Serialize<T>` / `Deserialize<T>`，但 `IDataSerializer` 现在还要求非泛型 `Serialize(Type, object)` / `Deserialize(Type, byte[])`。

## 2. 根因

测试桩接口签名落后于 `IDataSerializer` 的当前定义，导致编译期接口实现检查失败。

## 3. 修复方案

为 `CustomDataSerializer` 和 `FailingDataSerializer` 补齐 `Serialize(Type, object)` / `Deserialize(Type, byte[])`，并让泛型方法直接转发到非泛型实现，保持测试行为不变。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Tests/Runtime/DataModuleTests.cs`

## 5. 验证结果

- `dotnet test GameDeveloperKit.Runtime.Tests.csproj --no-restore` 返回 0。

## 6. 遗留事项

- 当前只修复了测试桩与接口签名不一致的问题，没有改动 `IDataSerializer` 或 `DataModule` 的运行时逻辑。
