---
doc_type: audit-finding
finding_id: "14"
slug: framework-full
dimension: bug
severity: P2
confidence: medium
action: cs-refactor
---

# Finding 14: ConfigModule.LoadTableAsync 异常处理绕路重抛

## 证据

`Assets/GameDeveloperKit/Runtime/Config/ConfigModule.cs` 第 72-96 行：

```csharp
catch (Exception exception)
{
    var loadException = exception is GameException
        ? exception
        : new GameException($"Failed to load config table '{rowType.Name}' from '{path}'. {exception.Message}", exception);

    completionSource.TrySetException(loadException);
    try
    {
        await completionSource.Task;   // <-- 等待自己刚设为异常的任务
    }
    catch
    {
    }

    throw loadException;               // <-- 然后重抛同一异常
}
finally
{
    m_PendingLoads.Remove(rowType);
}
```

## 问题

逻辑正确但路径迂回：
1. 捕获异常 → 包装
2. `TrySetException` → completion source 进入 faulted 状态
3. `await completionSource.Task` → 立即抛出（因为已 faulted）
4. 空的 catch 吃掉这个异常
5. `throw loadException` → 重新抛出原始包装异常

目的似乎是确保 waiting 的并发调用方能感知到异常（通过 completionSource），同时当前调用方也能拿到异常。但 `await completionSource.Task` + 空 catch 的模式让意图不清晰，且增加了一次不必要的 async 状态机开销。

## 建议动作

简化为：设置 `completionSource.TrySetException(loadException)` 后直接 `throw`，不 await 自己的 completion。走 `cs-refactor`。
