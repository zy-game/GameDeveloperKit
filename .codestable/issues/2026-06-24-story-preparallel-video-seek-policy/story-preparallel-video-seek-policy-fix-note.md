---
doc_type: issue-fix
issue: 2026-06-24-story-preparallel-video-seek-policy
path: fast-track
fix_date: 2026-06-24
tags: [story, editor, seek, compiler]
---

# 前置过渡视频进入 Parallel 时 seek policy 误判修复记录

## 1. 问题描述

Story Editor 图里存在两个视频：左侧视频先播放，完成后进入 `Parallel`；右侧视频位于 `Parallel` 的视频轨内。期望左侧前置视频可以 seek，右侧并行互动视频不可 seek。

## 2. 根因

`StoryProgramCompiler.CanInferTransitionVideo()` 已优先排除了位于 `Parallel` 分支内部的视频，但 `IsTransitionVideoTarget()` 没有把 `PlayVideo -> Parallel` 视为安全后续目标，导致前置视频完成后进入 Parallel 时被误判为不可 seek。

## 3. 修复方案

把 seek 推导里的目标判断改为 seek-safe continuation，并将 `NodeKind.Parallel` 纳入其中。这样前置视频完成后进入后续编排时可以 seek，而 `IsInsideParallelBranch()` 仍会让 `Parallel` 分支内部的视频保持不可 seek。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Editor/StoryEditor/Compiler/StoryProgramCompiler.cs`
- `Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过，0 warning / 0 error。
- 新增回归测试 `ProgramCompiler_WhenVideoTargetsParallel_WritesHiddenSeekPolicyOnlyForPreParallelVideo`：同一张图中 `intro_video -> parallel` 写入 `__videoSeekPolicy=transition`，`branch_video` 位于 `Parallel` 分支内则不写入该参数。
- 未运行 Unity Test Runner；当前只完成 .NET Editor Tests 项目构建验证。

## 6. 遗留事项

- 无。本次只修正 compiler seek 推导边界，不改播放 UI、Story runtime seek 或 editor interaction template。
