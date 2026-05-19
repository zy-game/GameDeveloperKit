---
doc_type: audit-finding
audit: 2026-05-19-filesystem-bugs
finding_id: "bug-05"
nature: bug
severity: P2
confidence: medium
suggested_action: cs-refactor
status: open
---

# Finding 05：Release 对不存在路径静默无操作并浪费一次 SaveAsync

## 速答

`VfsManifest.Release(string path)` 在 `path` 不匹配任何条目时静默跳过 `Unused()` 调用，但仍执行 `SaveAsync()` 将清单写回磁盘。这既是静默吞没无效输入，也是不必要的 I/O 操作。

## 关键证据

- `VfsManifest.cs:42-50` —
  ```csharp
  public UniTask Release(string path)
  {
      var entry = m_Entries.Find(e => e.FilePath == path);
      if (entry != null)
      {
          entry.Unused();
      }
      return SaveAsync();
  }
  ```
  - `path` 未找到时，`entry` 为 null，跳过 `Unused()`，但仍调用 `SaveAsync()` 写入未变化的 JSON
  - `path` 为 null 时，`e.FilePath == null` 会匹配到已被 `Unused()` 置为 `FilePath=null` 的条目，导致错误地标记一个已释放条目——虽然调用 `Unused()` 是幂等操作，但语义上是错误的
  - 与 `DeleteAsync`（FileModule.cs:96）对比：`DeleteAsync` 在条目不存在时直接 `return` 不调用 `SaveAsync`

## 影响

低影响：无效输入被静默忽略，浪费一次文件 I/O。`path=null` 的边缘情况可能导致意外回收一个已释放条目，但 `Unused()` 调用是幂等的，实际后果轻微。

## 修复方向

在 `entry == null` 时提前 `return` 而不调用 `SaveAsync()`，或对 null/empty path 抛出 `ArgumentException`。同时对 `path` 做 null 校验以匹配项目编码约定。

## 建议动作

`cs-refactor`，因为属于低影响的代码改进，不涉及紧急修复。
