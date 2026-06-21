---
doc_type: issue-fix
issue: 2026-06-21-story-runtime-p1-readiness
path: fast-track
fix_date: 2026-06-21
related:
  - ../../audits/2026-06-21-story-system-production-readiness/index.md
tags: [story, runtime, snapshot, wait, validation]
---

# Story runtime P1 readiness 修复记录

## 1. 问题描述

本次按生产可用性审计顺序修复前三个 P1：

- F01：快照无法还原并行运行状态。
- F02：Wait 使用绝对时间比较，真实播放中后续等待可能立即完成。
- F03：`StoryModule.Register(StoryProgram)` 没有完整校验跳转目标。

## 2. 根因

- `StorySnapshot` 只保存单个 stepId，缺少 runner state、wait elapsed 和 parallel branch cursor。
- `StoryRunner.Evaluate(double)` 把参数当当前绝对时间使用，未记录等待进入点或等待累计值。
- `StoryModule.Program.Validation` 只校验 target 非空，未校验 target chapter/step 是否存在。

## 3. 修复方案

- 扩展 `StorySnapshot`：增加 `StorySnapshotState`、当前 wait elapsed、parallel branch snapshots。
- 调整 `StoryRunner`：`Evaluate(double)` 改为 delta time 语义；普通 wait 和并行 wait 都累计 elapsed；快照恢复时按分支 cursor 重建当前 frame。
- 强化 `StoryModule.Register()` 校验：注册期校验 Line、Choice、Command outcome/fallback、Branch、Jump、Wait、Merge 的 target。
- 增加 Runtime tests 覆盖 wait delta、wait snapshot、parallel snapshot、parallel wait delta 和缺失 target 注册失败。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/Story/Runtime/StorySnapshot.cs`
- `Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs`
- `Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.cs`
- `Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.Validation.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过，0 warning / 0 error。

## 6. 后续处理

- F04 已在 `.codestable/issues/2026-06-21-story-player-presentation-bridge/story-player-presentation-bridge-fix-note.md` 处理。
- F05 已在 `.codestable/refactors/2026-06-21-story-schema-cleanup/story-schema-cleanup-apply-notes.md` 处理。
- F06 已在 `.codestable/refactors/2026-06-21-story-editor-naming-doc-cleanup/story-editor-naming-doc-cleanup-apply-notes.md` 处理。
