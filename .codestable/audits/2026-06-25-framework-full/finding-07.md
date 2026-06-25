---
doc_type: audit-finding
finding_id: "07"
slug: framework-full
dimension: maintainability
severity: P1
confidence: high
action: cs-refactor
---

# Finding 07: StoryRunner.cs 超长文件

## 证据

`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs` — **1590 行，68.2 KB**

其他超长文件：
- `StoryPlayerView.cs` — 1380 行（57.2 KB）
- `SoundModule.cs` — 899 行（34.6 KB）
- `DataModule.cs` — 753 行（32.0 KB）
- `DownloadHandler.cs` — 682 行（27.7 KB）

## 问题

AGENTS.md 虽未设硬性行数上限，但业界共识是单文件超过 500 行显著降低可维护性。StoryRunner（1590 行）混合了：program 注册、Step 推进、Choice/Command/Wait/Jump/Gate 分支处理、变量解析、条件求值、函数调用、Snapshot 捕获和诊断信息——至少 6 种职责在一个文件里。

架构设计文档中已经为 `StoryEditorWindow.cs`（1689 行）写了"超出范围的观察"建议拆 partial，StoryRunner 同理。

## 建议动作

拆为 partial 文件：`StoryRunner.cs`（核心推进）、`StoryRunner.Variables.cs`（变量/函数解析）、`StoryRunner.Snapshot.cs`（快照/诊断）。走 `cs-refactor`。
