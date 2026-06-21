---
doc_type: audit-index
status: active
scope: story-system-production-readiness
date: 2026-06-21
---

# Story System Production Readiness Audit

## 范围

- `Assets/GameDeveloperKit/Runtime/Story`
- `Assets/GameDeveloperKit/Editor/StoryEditor`
- `Assets/GameDeveloperKit/Editor/NodeGraph`
- `Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs`
- `Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs`
- `Assets/GameDeveloperKit/Tests/Editor/StorySampleGraphFixtureTests.cs`
- `.codestable/architecture/ARCHITECTURE.md`

## 总评

当前剧情系统已经具备可用的核心雏形：Story runtime 与 Editor/AVPro 隔离，NodeGraph 没有 Story 业务泄漏，authoring 资源以 `ScriptableObject` 保存，媒体参数编译后保存 `Assets/...` 字符串，编辑器预览窗口能通过 runtime `StoryModule` 推进。

但按“实际投入使用”标准，目前还不能直接作为生产级剧情系统交付。主要阻塞在运行时状态模型：并行帧、等待、命令、选项的快照恢复不完整；等待时间语义不适合真实 Update；运行时注册校验没有覆盖所有 target；Player 侧还缺少正式 presenter/command 执行层。编辑器层可继续使用和打磨，但运行时必须先修 P1。

## 发现矩阵

| 编号 | 严重度 | 性质 | 置信度 | 发现 | 建议动作 |
|---|---|---|---|---|---|
| F01 | P1 | bug | high | 快照只保存单个 stepId，恢复并行/等待/命令状态会重放或丢状态 | cs-issue |
| F02 | P1 | bug | high | Wait 使用绝对时间比较，没有记录进入等待的起点 | cs-issue |
| F03 | P1 | bug | high | `Register(StoryProgram)` 未校验普通跳转 target 是否存在 | cs-issue |
| F04 | P1 | maintainability | high | 运行时没有正式 command/presenter 执行层，`IStoryCommandHandler` 是未接线接口 | cs-issue |
| F05 | P2 | maintainability | high | Story schema/compiler 仍保留大量已排除节点和条件 helper | cs-refactor |
| F06 | P2 | arch-drift | high | 架构文档和测试命名仍大量保留 V4，且部分描述与现状冲突 | cs-refactor |

## 通过项

- Runtime Story 目录没有引用 `UnityEditor`、`AssetDatabase`、`ObjectField`、AVProVideo 或 Unity `VideoPlayer`。
- `EditorNodeGraph` 未扫到 `Story`、`NodeKind`、`Command`、`Condition` 等业务关键字，通用节点图库边界成立。
- 示例资源参数使用 `Assets/...` 路径，不保存 Unity object 或 `guid:` 作为 runtime 参数。
- 编辑器播放窗口是 Editor-only harness，视频预览只在 Editor playback 层引用 AVProVideo。

## 建议优先级

1. 先开 issue 修 F01、F02、F03。这三条直接影响存档、真实时间推进和运行时数据安全。
2. 再补 F04：定义 Player 侧 `StoryPresenter` / command executor 契约，明确 AVPro、音频、图片、UI 选项如何从 `StoryFrame` 驱动。
3. 最后做 F05、F06：删掉不再支持的节点枚举/schema/compiler helper，并清掉 V4 命名和过期架构描述。

## 修复进度

- 2026-06-21：F01、F02、F03 已按审计顺序修复，记录见 `.codestable/issues/2026-06-21-story-runtime-p1-readiness/story-runtime-p1-readiness-fix-note.md`。
- 2026-06-21：F04 已补运行时 Player 表现桥接，记录见 `.codestable/issues/2026-06-21-story-player-presentation-bridge/story-player-presentation-bridge-fix-note.md`。
- 2026-06-21：F05 已清理 Story authoring 旧节点 schema/compiler 残留，记录见 `.codestable/refactors/2026-06-21-story-schema-cleanup/story-schema-cleanup-apply-notes.md`。
- 2026-06-21：F06 已清理 Story Editor V4 命名和过期架构描述，记录见 `.codestable/refactors/2026-06-21-story-editor-naming-doc-cleanup/story-editor-naming-doc-cleanup-apply-notes.md`。
- F01-F06 已全部处理。
