---
doc_type: audit-finding
audit: 2026-05-18-filesystem
finding_id: "security-01"
nature: security
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 01：路径校验不足可逃逸 VFS 根目录

## 速答

`FileModule` 只禁止开头 `/` 和字符串包含 `..`，没有禁止 Windows 绝对路径、盘符路径或反斜杠变体，调用方可把 standalone 文件写到 VFS 根目录之外。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:49` — `var physicalPath = Path.Combine(m_RootPath, path);` —— 在 .NET 中如果 `path` 是根路径或盘符路径，组合结果可能丢弃 `m_RootPath`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:195` — `if (path.StartsWith("/"))` —— 只挡 Unix 风格绝对路径，没有挡 `\foo`、`C:\foo` 等 Windows 路径。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:200` — `if (path.Contains(".."))` —— 字符串包含检查既不做规范化，也不校验最终 full path 是否仍在 `m_RootPath` 下。

## 影响

如果上层把用户可控路径传给 `WriteFileAsync` / `ReadFileAsync` / `DeleteFileAsync`，VFS 可能写入、读取或删除持久化目录之外的文件；在 Unity Windows 环境风险更明确。

## 修复方向

对虚拟路径做统一规范化，禁止 rooted/drive-qualified 路径，并用 `Path.GetFullPath` 校验最终物理路径必须位于 VFS 根目录内。

## 建议动作

`cs-issue`，因为这是可触发的数据边界/安全问题，需要按缺陷修复并补边界用例。
