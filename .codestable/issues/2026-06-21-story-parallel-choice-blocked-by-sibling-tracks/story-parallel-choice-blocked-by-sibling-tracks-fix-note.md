---
doc_type: issue-fix
issue: 2026-06-21-story-parallel-choice-blocked-by-sibling-tracks
status: fixed
severity: P1
summary: 并行轨道中选择选项后仍被其它未完成轨道阻塞
---

# story-parallel-choice-blocked-by-sibling-tracks fix note

## 现象

并行节点同时启动视频、音频、对白后，对白下方出现选项。选择某个选项后，运行时仍等待同一并行块中上一轮视频/音频等其它轨道完成，才播放选项后的节点。

## 根因

`StoryRunner.SelectParallel()` 只替换被选中选项所在的分支，并保留 `m_CurrentParallelFrame` 中其它未完成分支。后续 `ResolveParallelBranches()` 会继续组合并行帧，因此选项后的节点被 sibling 轨道的 `WaitForCompletion` 命令阻塞。

## 修复

- `StoryRunner.SelectParallel()` 改为选择后清空当前并行帧并 `JumpTo(choice.Target)`，再 `ResolveFrameUntilStop()`。
- 新增回归测试 `ProgramCompiler_WhenParallelChoiceSelected_DoesNotWaitForOtherTracks`：验证并行帧同时等待 choice 和 command 时，选择选项后立即进入所选目标，不再保留视频轨和未选中选项。

## 验证

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过，0 warning / 0 error。