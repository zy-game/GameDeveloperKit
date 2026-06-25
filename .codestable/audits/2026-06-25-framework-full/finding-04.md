---
doc_type: audit-finding
finding_id: "04"
slug: framework-full
dimension: arch-drift
severity: P2
confidence: medium
action: cs-refactor
---

# Finding 04: FileSystem 目录名与命名空间不匹配

## 现状

- **目录**: `Runtime/FileSystem/`（含 `FileModule.cs`、`VfsManifest.cs` 等）
- **命名空间**: 全部使用 `namespace GameDeveloperKit.File`
- **对照**: 其他所有 Runtime 模块目录名 = 命名空间后缀（`Runtime/Timer/` → `GameDeveloperKit.Timer`，`Runtime/UI/` → `GameDeveloperKit.UI`，等等，Debug 例外见 Finding 01）

## 问题

目录叫 `FileSystem` 但命名空间叫 `File`，对外 API 是 `App.File`，`using GameDeveloperKit.File`。目录名比命名空间长，这是命名不一致。虽然属于审美层面，但让新开发者困惑："这个模块到底叫 FileSystem 还是 File？"

## 建议动作

二选一：(a) 目录重命名为 `Runtime/File/` 与命名空间一致；(b) 命名空间改为 `GameDeveloperKit.FileSystem`，同步更新所有引用（约 6 个源文件 + App.cs using）。走 `cs-refactor`。
