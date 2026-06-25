---
doc_type: issue-fix
issue: 2026-06-24-network-waitasync-request-response
path: fast-track
fix_date: 2026-06-24
tags: [network, waitasync, request-response]
---

# Network WaitAsync Request Response 修复记录

## 1. 问题描述

`IChannel.WaitAsync<TResponse>(Message request)` 的现有实现语义不符合调用直觉：调用方期望它发送 `request`，并等待同一 `SequenceId` 的 `TResponse` 回包；旧实现要求先调用 `SendAsync(request)` 创建 pending slot，再调用 `WaitAsync<TResponse>(request)` 只等待已有 slot。

## 2. 根因

`NetworkChannel.SendAsync()` 同时承担了发送和登记 pending response 的职责，`NetworkChannel.WaitAsync<TResponse>()` 只检查 pending slot 是否已存在。这让 API 名称和实际职责错位，也让单次 request-response 调用被拆成两步。

## 3. 修复方案

将职责调整为：

- `SendAsync(request)`：只发送消息，不创建 pending response slot。
- `WaitAsync<TResponse>(request)`：为 request 分配 `SequenceId`，创建 pending response slot，发送 request，再等待同 `SequenceId` 且 `IsResponse == true` 的回包。
- `Receive(message)`：只有 `message.IsResponse == true` 时才完成 pending response；普通消息仍进入 listener 分发。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Network/NetworkChannel.cs`
  - 提取内部 `SendPayloadAsync()` 复用编码和底层发送逻辑。
  - `SendAsync()` 保留连接检查、`SequenceId` 分配和发送，不登记 pending。
  - `WaitAsync<TResponse>()` 登记 pending、发送 request、等待并校验响应类型，异常或完成后清理 pending。
  - `Receive()` 只用 response 消息完成 pending，避免 request 回环误完成等待。
- `Assets/GameDeveloperKit/Tests/Runtime/NetworkModuleTests.cs`
  - 更新旧的 `SendAsync + WaitAsync` 测试为单次 `WaitAsync` 请求响应语义。
  - 增加 `SendAsync` 不创建 pending、`WaitAsync` 未连接不登记 pending、发送失败清理 pending、request 回环不会完成 pending 的覆盖。

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- `git diff --check -- Assets/GameDeveloperKit/Runtime/Network/NetworkChannel.cs Assets/GameDeveloperKit/Tests/Runtime/NetworkModuleTests.cs`：无空白错误，仅提示 Git 下次触碰时会将 LF 替换为 CRLF。

## 6. 遗留事项

- `.codestable/architecture/ARCHITECTURE.md` 仍记录旧的 Network pending 行为，后续应通过 `cs-arch update` 同步架构现状。
- 本次未运行 Unity Test Runner；当前验证覆盖为 .NET 编译层面的 Runtime 与 Runtime.Tests 项目构建。
