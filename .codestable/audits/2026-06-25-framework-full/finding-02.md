---
doc_type: audit-finding
finding_id: "02"
slug: framework-full
dimension: arch-drift
severity: P1
confidence: high
action: cs-refactor
---

# Finding 02: StoryPlayback 目录下部分文件使用错误的命名空间

## 现状

- **目录**: `Runtime/StoryPlayback/`
- **问题文件**:
  - `ModuleInteractionExtensions.cs` → `namespace GameDeveloperKit.Story` (行 1)
  - `StoryAvProVideoCommandPlayer.cs` → `namespace GameDeveloperKit.Story` (行 1)
- **同目录正常文件**: 其余 20+ 个文件均使用 `namespace GameDeveloperKit.StoryPlayback`

## 问题

StoryPlayback 是独立的 asmdef（`GameDeveloperKit.StoryPlayback`），与 `Runtime/Story`（`GameDeveloperKit.Story`）是不同的程序集。两个文件错误地声明在 `GameDeveloperKit.Story` 命名空间下，虽然编译可能通过（同一 asmdef 内允许任意命名空间），但：

- 违反目录=命名空间的约定
- 混淆模块边界——`ModuleInteractionExtensions` 扩展的是 StoryPlayback 的 interaction 概念，不是 Story runtime 核心
- 架构文档将 `IInteractionChannel` 等概念明确归于 StoryPlayback 子系统

## 建议动作

将这两个文件的命名空间改为 `GameDeveloperKit.StoryPlayback`。走 `cs-refactor`。
