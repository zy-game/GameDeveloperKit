---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "bug-06"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 06：不同 URL 的同名文件会写入同一个下载临时文件

## 速答

下载模块按完整 URL 复用 handler，但临时文件名只取 URL path 的最后一段；两个不同 URL 如果文件名相同，会拥有不同 handler 却写同一个 `{name}.download`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Download/DownloadModule.cs:83` 到 `Assets/GameDeveloperKit/Runtime/Download/DownloadModule.cs:95` — `m_Downloads` 以完整 `url` 为 key，不同 URL 会创建不同 handler。
- `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:216` 到 `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:218` — `TempPath` 使用 `GetFileName(url) + ".download"`。
- `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:698` 到 `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:702` — `GetFileName` 只取 `uri.LocalPath` 的文件名；只有文件名为空时才用 `uri.GetHashCode()`。
- `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:622` 到 `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs:628` — cancel 会删除该 handler 的 `TempPath` 和分片文件。

## 影响

例如 `https://cdn-a/game/config.json` 和 `https://cdn-b/patch/config.json` 会同时写 `config.json.download`。并发下载会互相覆盖或锁文件；一个 handler cancel 时会删除另一个 handler 正在使用的 temp 文件；断点续传还可能把 A 的残留当作 B 的已有内容。

## 修复方向

临时文件名应基于完整 URL 的稳定 hash 或 URL hash + 原文件名组合，保证不同 URL 不共享同一 temp path。

## 建议动作

`cs-issue`，因为这是下载数据错写和取消误删问题。
