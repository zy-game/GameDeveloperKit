---
doc_type: audit-finding
finding_id: "11"
slug: framework-full
dimension: bug
severity: P0
confidence: high
action: cs-issue
---

# Finding 11: ReferencePool 生产构建中重复释放静默损坏池状态

## 证据

`Assets/GameDeveloperKit/Runtime/Core/ReferencePool.cs` 第 165-182 行，`ReferenceCollection.Release`:

```csharp
public void Release(IReference reference)
{
    reference.Release();

    bool strictCheck = IsStrictCheckEnabled;
    lock (m_References)
    {
        if (strictCheck && m_References.Contains(reference))
        {
            throw new InvalidOperationException("The reference has already been released.");
        }

        m_References.Enqueue(reference);
    }

    m_ReleaseReferenceCount++;
    m_UsingReferenceCount--;
}
```

`IsStrictCheckEnabled` 在非 `UNITY_EDITOR && !DEVELOPMENT_BUILD` 下始终返回 `false`（第 30-37 行）：

```csharp
private static bool IsStrictCheckEnabled
{
    get
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return s_EnableStrictCheck;
#else
        return false;
#endif
    }
}
```

## 问题

在生产构建（Release）中，同一个 reference 被 Release 两次时：
1. `reference.Release()` 执行两次（可能清空状态）
2. 同一个对象被 `Enqueue` 两次到 `m_References` 队列
3. `m_UsingReferenceCount` 减两次 → 变成负数
4. `Acquire()` 可能 Dequeue 出已释放的脏对象

这是静默状态损坏：不会崩溃，但后续 Acquire 可能返回已被释放/复用的对象，导致难以排查的逻辑错误。

## 复现

任何实现 `IReference` 的业务对象，如果调用方错误地 Release 两次，在 Release 构建中无任何反馈。

## 建议动作

将重复释放检查移出 `#if` 条件编译，改为始终启用（`m_References.Contains` 是 O(n) 但 pool 通常很小）。或者在 Release 构建中至少用 `Debug.LogError` 记录。走 `cs-issue`。
