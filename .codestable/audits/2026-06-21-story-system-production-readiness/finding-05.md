---
doc_type: audit-finding
id: F05
severity: P2
nature: maintainability
confidence: high
suggested_action: cs-refactor
---

# F05 Story schema/compiler 仍保留大量不再支持的节点残留

## 证据

- 默认 authoring 节点只允许 Start/End/JumpChapter/Parallel/Merge/Wait/Dialogue/Narration/PlayVideo/ShowImage/PlayAudio/EmitEvent/Choice/MiniGame：`Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeSchemaRegistry.cs:69`
- 但同一个 registry 仍注册 Branch/Switch/Sequence/Random/StopAudio/Camera/Animation/SetFlag/ClearFlag/ExternalAction/QTE/Hotspot/InputWait 等节点：`Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeSchemaRegistry.cs:98`
- 条件和辅助节点也仍被注册：`Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeSchemaRegistry.cs:134`
- compiler 已经会拒绝非默认节点：`Assets/GameDeveloperKit/Editor/StoryEditor/Compiler/StoryProgramCompiler.cs:124`
- 但 compiler 仍保留 FlagCheck/Once/Cooldown/Compare/Not 的条件 helper：`Assets/GameDeveloperKit/Editor/StoryEditor/Compiler/StoryProgramCompiler.cs:1568`

## 影响

这些节点不会出现在默认 palette，但仍存在于公共 enum/schema 和 compiler 私有逻辑里。后续维护者会误以为这些节点半支持，继续在旧 taxonomy 上补功能。对当前“降低系统复杂度”的目标来说，这是明显的维护负担。

## 建议

破坏式清理 Story 自己的 schema：

- `NodeKind` 只保留当前 production authoring 节点。
- 删除不再支持节点的 schema 注册。
- 删除 compiler 中对应 unreachable helper。
- 如确实要保留实验节点，放到单独 experimental registry，并不进入默认 Story authoring 编译路径。

