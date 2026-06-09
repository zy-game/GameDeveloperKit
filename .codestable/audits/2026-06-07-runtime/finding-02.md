---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "bug-02"
nature: bug
severity: P2
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 02：`ReadAllStringAsync` 公开 API 永远返回空字符串

## 速答

`FileModule.ReadAllStringAsync(path)` 是公开方法，但当前实现忽略 `path` 并固定返回 `string.Empty`，调用方会把真实文件内容误判为空内容。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:73` — 声明公开异步方法 `ReadAllStringAsync(string path)`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:75` — 方法体直接 `return string.Empty;`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:128` 到 `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:142` — 同模块已经有二进制读取 API，可用于实现字符串读取，但当前未调用。

## 影响

任何依赖该 API 读取配置、存档或文本资源的 runtime 代码都会拿到空字符串。因为它不是抛错而是返回合法空值，问题会延迟到 JSON 解析、业务默认值或数据校验阶段才暴露。

## 修复方向

复用 `ReadAsync(path)`，对 `null` 结果按约定返回 `null` 或抛错，并用 UTF-8 解码字节内容。

## 建议动作

`cs-issue`，因为这是明确的错误实现。
