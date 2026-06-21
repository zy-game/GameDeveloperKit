---
doc_type: audit-finding
id: F06
severity: P2
nature: arch-drift
confidence: high
suggested_action: cs-refactor
---

# F06 架构文档和测试命名仍保留 V4 且部分描述过期

## 证据

- 架构文档标题仍是 `Story Editor v4 / Editor Node Graph`：`.codestable/architecture/ARCHITECTURE.md:27`
- 文档仍引用菜单 `GameDeveloperKit/剧情编辑器 v4` 和 `StoryEditorV4GraphAdapter`：`.codestable/architecture/ARCHITECTURE.md:29`
- 文档仍引用 `StoryEditorV4PlaybackWindow`：`.codestable/architecture/ARCHITECTURE.md:46`
- 文档说 palette 不显示并行/合流，但当前测试断言 palette 包含“并行”和“等待全部完成”：`.codestable/architecture/ARCHITECTURE.md:49`、`Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs:548`
- Editor 测试方法名仍大量使用 `WindowV4...`：`Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs:523`

## 影响

这不会直接导致运行时 bug，但会影响后续设计和实现判断。当前代码已经从 `StoryEditorV4` 收敛到 `StoryEditor`，文档和测试名却仍使用 V4，会让后续任务继续沿用已废弃命名和过期节点策略。

## 建议

- 更新 `.codestable/architecture/ARCHITECTURE.md` 的 Story 章节，删除 V4 命名。
- 把测试方法名从 `WindowV4...` 改成 `StoryEditor...` 或 `WindowGraph...`。
- 同步当前默认节点集和 palette 策略，特别是 Parallel/Merge 是否正式保留。

