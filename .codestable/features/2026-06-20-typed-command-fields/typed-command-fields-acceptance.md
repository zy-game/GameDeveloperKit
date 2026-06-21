# Typed Command Fields 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-typed-command-fields/typed-command-fields-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `ParameterValueType.AssetReference`：已在 `Assets/GameDeveloperKit/Runtime/Story/Definition/NodeType.cs` 增加，`PlayVideo.clip` 使用 `AssetReference`。
- [x] `NodeParameterDefinition`：已包含 `Tooltip`、`ResourceType`、`Options`，并提供 asset / option schema 来源。
- [x] `StoryCommandArgumentDefinition`：已在 `Assets/GameDeveloperKit/Runtime/Story/Program/StorySchema.cs` 落地 key、label、value type、required、resource type、options、tooltip。
- [x] `PlayVideo clip=guid:... wait=true`：`StoryProgramCompiler` 导出 `StoryCommand.Arguments["clip"]` 为 string，`wait` 不进入 arguments，`WaitForCompletion=true`。
- [x] `clip=intro.mp4` 兼容输入：compiler 保留 string argument 并输出兼容 warning。

**名词层“现状 → 变化”逐项核对**：
- [x] Runtime 参数值仍为 `StoryValue` 基础类型，没有新增 Unity object 值类型。
- [x] `StoryCommandDefinition.ArgumentNames` 保留兼容读取，并由 typed `ArgumentDefinitions` 派生。
- [x] Editor graph field model 新增 `AssetReference` 与 `ResourceType`，不依赖 Story 专有类型。

**流程图核对**：
- [x] `NodeSchemaRegistry -> StoryEditorGraphAdapter -> EditorNodeGraphNodeView -> StoryAuthoringNode.Parameters -> StoryProgramCompiler -> StoryCommand.Arguments / StoryCommandSchema -> StoryModule.Program.Validation` 均有代码落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `PlayVideo.clip` 不再只是普通字符串字段：schema 和测试确认 `AssetReference`、required、`UnityEngine.Video.VideoClip` resource type。
- [x] Story Editor 节点图展示资源字段：节点视图包含 `ObjectField("视频片段 *")` 和 `TextField("资源 ID")`。
- [x] authoring 保存稳定资源值：`SetNodeFieldFromGraph("video", "clip", "guid:video_intro")` 后参数保存同一字符串。
- [x] `StoryCommandSchema` 携带 typed metadata：`play_video.ArgumentDefinitions["clip"]` 包含 value type / required / resource type。
- [x] 缺必填或类型错误可定位：compiler 和 runtime validation 都包含 story/chapter/node/field 或 step/command/argument 定位。

**明确不做逐项核对**：
- [x] 未在 `StoryModule` 中播放视频、加载资源或绑定 UI。
- [x] runtime `StoryProgram` / `StoryCommand` / `StoryCommandSchema` 不保存 Unity object 实例。
- [x] 未引入 Addressables / ResourceModule 完整资源管线。
- [x] 未做 graph validation overlay、错误 badge 或字段红框。
- [x] 未做完整命令插件系统或第三方 command handler 自动发现。

**关键决策落地**：
- [x] `NodeParameterDefinition` 仍是节点字段唯一 schema 来源；Story adapter 从 schema 映射 graph field。
- [x] 媒体/资源字段编译为稳定字符串；manual key 兼容并 warning。
- [x] `StoryCommandSchema` 升级为 typed metadata，同时旧 `ArgumentNames` 兼容。
- [x] 字段类型化先覆盖命令节点，并顺手覆盖 `PlayVideo` / `ShowImage` / `PlayAudio` 资源字段。

**编排层“现状 → 变化”逐项核对**：
- [x] schema 层补齐 command typed metadata。
- [x] graph kit 支持业务无关 asset reference 字段视图。
- [x] Story adapter/window 负责 schema 映射与 stable value 写回。
- [x] compiler 按 schema 编译 command arguments 与 command argument definitions。
- [x] runtime validation 按 command schema 校验 arguments。

**流程级约束核对**：
- [x] 错误定位：compiler 用 `story:.../chapter:.../node:.../field:...`，runtime 用 `story/chapter/step/command/argument`。
- [x] 兼容性：`clip=intro.mp4` 不失败，并产生 warning。
- [x] Runtime 边界：runtime 只保存 resource type 字符串 metadata 和 `StoryValue` 基础值，不引用 Editor API / ObjectField / AssetDatabase。
- [x] GraphKit 边界：`EditorNodeGraphKit` 不引用 `GameDeveloperKit.Story`、`NodeKind`、`StoryCommand` 或 Story command id。

**挂载点反向核对**：
- [x] `NodeSchemaRegistry`：命令节点 schema 与默认参数注册已更新。
- [x] `EditorNodeGraphKit`：field model / node view 是 asset reference 字段渲染挂载点。
- [x] `StoryEditorGraphAdapter` / `StoryEditorWindow`：Story schema 映射与 stable value 写回落点明确。
- [x] `StoryProgramCompiler`：command 编译与 typed argument definitions 导出落点明确。
- [x] `StoryModule.Program.Validation`：runtime command arguments 兜底校验落点明确。
- [x] 反向 grep 未发现 feature 引用落在清单外的新增业务挂载点。
- [x] 拔除沙盘推演：移除上述五个挂载点即可撤掉本 feature 行为；没有 residual business branch 留在通用 graph kit。

## 3. 验收场景核对

- [x] **N1**：`NodeSchemaRegistry.Get(NodeKind.PlayVideo)` 中 `clip` 为 `AssetReference`、必填、resource type 为 `UnityEngine.Video.VideoClip`。证据：`StoryEditorTests.ProgramCompiler_WhenDefinitionIsValid_BuildsStoryProgram`。
- [x] **N2**：V4 graph node 中 `clip` 字段为资源选择 UI，tooltip 中文并包含参数键。证据：`StoryEditorTests.WindowV4Graph_WhenNodeSelected_ShowsInlineSchemaFieldsNotLegacyFields`。
- [x] **N3**：graph 字段写入 `guid:{guid}` 后保存 stable value。证据：`StoryEditorTests.WindowV4Graph_WhenAssetFieldWritesStableId_StoresStringParameterOnly`。
- [x] **N4**：`clip=guid:... wait=true` 编译为 string argument，`wait` 不进入 arguments，`WaitForCompletion=true`。证据：`StoryEditorTests.ProgramCompiler_WhenCommandAssetReferenceIsGuid_ExportsStableStringArgument`。
- [x] **N5**：旧字符串 `clip=intro.mp4` 编译通过并 warning。证据：`StoryEditorTests.ProgramCompiler_WhenCommandAssetReferenceIsManualString_CompilesWithWarning`。
- [x] **N6**：缺 `clip` compiler 失败并定位 field。证据：`StoryEditorTests.ProgramCompiler_WhenCommandRequiredFieldMissing_ReturnsLocatedError`。
- [x] **N7**：`Wait.duration=fast` compiler 失败并定位 `field:duration`。证据：`StoryEditorTests.ProgramCompiler_WhenWaitDurationIsInvalid_ReturnsLocatedError`。
- [x] **N8**：`wait=maybe` compiler 失败并定位 `field:wait`。证据：`StoryEditorTests.ProgramCompiler_WhenCommandBooleanFieldIsInvalid_ReturnsLocatedError`。
- [x] **N9**：`play_video` command schema 包含 typed argument definition，`ArgumentNames` 仍包含 `clip`。证据：`StoryEditorTests.ProgramCompiler_WhenDefinitionIsValid_BuildsStoryProgram`。
- [x] **N10**：runtime 注册 command step 缺 required `clip` 失败并定位 argument。证据：`StoryModuleTests.StoryProgram_WhenRequiredCommandArgumentMissing_RegistrationFails`。
- [x] **N11**：runtime 注册 Number schema 但 argument 为 String 时失败并定位 argument。证据：`StoryModuleTests.StoryProgram_WhenCommandArgumentTypeMismatches_RegistrationFails`。
- [x] **N12**：Runtime Story 程序不引用 UnityEditor / ObjectField / AssetDatabase 或 VideoClip 类型。证据：`EditorNodeGraphTests.Runtime_WhenScanned_DoesNotReferenceEditorNodeGraphKit`。
- [x] **N13**：EditorNodeGraphKit 不引用 Story 专有语义。证据：`EditorNodeGraphTests.NodeGraphKit_WhenScanned_DoesNotReferenceStoryOrGraphView`。
- [x] **N14**：旧 compiler PlayVideo 测试仍可用 `GetString("clip")` 读取。证据：`StoryEditorTests.ProgramCompiler_WhenDefinitionIsValid_BuildsStoryProgram`。

本 feature 没有浏览器前端；Editor UI 证据通过 Unity Editor tests 覆盖，真实手测不属于本 feature 新增必需项。

## 4. 术语一致性

- Command node：代码继续使用独立 command step / command schema，没有恢复 owner action。
- Typed field：落在 `NodeParameterDefinition`、`EditorGraphFieldModel`、Story adapter 中，命名一致。
- Asset reference field：落在 `ParameterValueType.AssetReference` / `EditorGraphFieldValueType.AssetReference`，命名一致。
- Runtime argument：仍为 `StoryCommand.Arguments` + `StoryValue`，未引入 Unity object。
- Command argument schema：落在 `StoryCommandArgumentDefinition` / `ArgumentDefinitions`。
- 防冲突：通用 `EditorNodeGraphKit` 未出现 `NodeKind`、`StoryCommand`、`PlayVideo` 或 `play_video`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已归并 Story Editor / Editor Node Graph 现状，补充 typed command fields、asset reference graph UI、stable command argument 导出、runtime typed schema validation 和资源播放边界。
- [x] 已知约束：已补充 Story 命令资源字段边界。
- [x] 变更日志：已追加 2026-06-20 typed command fields 记录。

## 6. requirement 回写

- [x] `requirement: story-editor` 指向 `.codestable/requirements/story-editor.md`。
- [x] 该 requirement 仍是 draft，因为 roadmap 中 `story-graph-validation-feedback` 与 `sample-story-graph-fixture` 仍未完成。
- [x] 已追加 `implemented_by: 2026-06-20-typed-command-fields`。
- [x] 已在实现进展中记录命令节点字段类型化、资源字段、稳定 arguments 和 runtime typed validation。

## 7. roadmap 回写

- [x] `roadmap: story-editor-hardening` / `roadmap_item: typed-command-fields` 已核对。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml` 中 `typed-command-fields` 已从 `in-progress` 改为 `done`，feature 保持 `2026-06-20-typed-command-fields`。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md` 子 feature 表格已同步为 `done`。
- [x] 排期建议已更新为下一步优先 `story-graph-validation-feedback`。

## 8. attention.md 候选盘点

- [x] 候选：本次确认 `ConvertFrom-Yaml` 在当前 PowerShell 环境不可用；CodeStable YAML 校验应使用项目脚本 `python .codestable/tools/validate-yaml.py --file ... --yaml-only`。建议通过 `cs-note` 追加到 `.codestable/attention.md` 的“命令与脚本陷阱”或“测试”分节。

## 9. 遗留

- 后续优化点：
  - `story-graph-validation-feedback`：在图上显示缺字段、断边、非法连接、未知命令等中文定位反馈。
  - `sample-story-graph-fixture`：补 3-4 章示例剧情图 fixture。
  - 后续 refactor：`NodeType.cs` 仍偏胖，可单独拆 schema registry 与 enum/definition；`StoryEditorWindow.cs` 后续可拆 asset commands / graph workspace / story tree。
- 已知限制：
  - 本 feature 只建立 authoring / compiler / runtime schema 契约，不实现资源加载、视频播放或 Addressables 管线。
  - 手填资源 key 仍兼容并 warning，后续图上校验反馈可把 warning 更直观地呈现在节点上。
- 实现阶段顺手发现：
  - Unity 实际视频类型全名是 `UnityEngine.Video.VideoClip`，schema 用资源类型字符串 metadata 避免 runtime 直接依赖具体 VideoClip 类型。
