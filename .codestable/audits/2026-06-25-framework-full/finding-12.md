---
doc_type: audit-finding
finding_id: "12"
slug: framework-full
dimension: bug
severity: P1
confidence: high
action: cs-issue
---

# Finding 12: ProcedureModule.Shutdown 同步阻塞 async 有死锁风险

## 证据

`Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs` 第 79-93 行，`Shutdown()`:

```csharp
public override void Shutdown()
{
    // ...
    try
    {
        if (Current != null)
        {
            try
            {
                Current.OnLeaveAsync(null, null).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                firstException = exception;
            }
            finally
            {
                Current = null;
            }
        }
    // ...
}
```

`Shutdown()` 是 `void` 同步方法（`IGameModule` 契约），但内部调用 `OnLeaveAsync` 的 async 版本并用 `.GetAwaiter().GetResult()` 同步等待。

## 问题

- 在 Unity 中，`UniTask.GetAwaiter().GetResult()` 在主线程调用时，如果 async 方法内部有 `await UniTask.Yield()` 或 `await UniTask.SwitchToMainThread()`，会导致死锁（等待的回调需要主线程，但主线程被 `GetResult()` 阻塞）
- `ProcedureBase.OnLeaveAsync` 的实现可能包含此类 await（尤其是业务 Procedure 的实现）
- 架构设计文档的约束说模块生命周期是同步的，但 Procedure 的 leave 是 async——这两个约束在这里冲突

## 建议动作

选项 A：修改 `IGameModule.Shutdown()` 签名为 async（较大改动）。选项 B：Procedure 内部在 Startup 时预注册同步 leave 回调，避免在 Shutdown 中调用 async。选项 C：为 `ProcedureModule.Shutdown` 提供安全的同步 fallback（如检测到未完成的 async 则跳过 leave 直接释放）。走 `cs-issue`。
