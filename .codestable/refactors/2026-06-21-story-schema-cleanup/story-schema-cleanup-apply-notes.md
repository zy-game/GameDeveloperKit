---
doc_type: refactor-apply-notes
refactor: 2026-06-21-story-schema-cleanup
related:
  - ../../audits/2026-06-21-story-system-production-readiness/finding-05.md
---

# story-schema-cleanup apply notes

## 步骤 1: 清理 Story authoring 旧节点 schema

- 完成时间: 2026-06-21
- 改动文件:
  - `Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeKind.cs`
  - `Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeSchema.cs`
  - `Assets/GameDeveloperKit/Runtime/Story/AuthoringSchema/NodeSchemaRegistry.cs`
  - `Assets/GameDeveloperKit/Editor/StoryEditor/StoryEditorGraphAdapter.cs`
  - `Assets/GameDeveloperKit/Editor/StoryEditor/Compiler/StoryProgramCompiler.cs`
  - `Assets/GameDeveloperKit/Editor/StoryEditor/Window/StoryEditorWindow.cs`
  - `Assets/GameDeveloperKit/Tests/Runtime/StoryModuleTests.cs`
  - `Assets/GameDeveloperKit/Tests/Editor/StoryEditorTests.cs`
- 验证结果:
  - `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning / 0 error。
  - `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning / 0 error。
  - `dotnet build GameDeveloperKit.Editor.csproj --no-restore`：通过，0 warning / 0 error。
  - `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`：通过，0 warning / 0 error。
- 偏离: 本次来自审计 F05 的定向清理，未另起完整 scan/design；范围仅限删除已退出生产 authoring 路径的节点 enum、schema 注册、schema 分类残留和 compiler 私有 helper。

## 遗留

- Runtime `StoryStepKind.Branch`、`StoryExpression` 条件能力仍保留，因为 edge/choice condition 仍需要运行时表达式支持。
- F06 已在 `.codestable/refactors/2026-06-21-story-editor-naming-doc-cleanup/story-editor-naming-doc-cleanup-apply-notes.md` 处理。
