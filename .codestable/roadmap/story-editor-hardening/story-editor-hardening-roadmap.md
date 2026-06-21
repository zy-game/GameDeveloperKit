---
doc_type: roadmap
slug: story-editor-hardening
status: active
created: 2026-06-20
last_reviewed: 2026-06-21
tags: [story, editor, node-graph, roadmap, hardening]
---

# Story Editor Hardening Roadmap

## 1. 背景

Story Editor 已经完成首轮运行时契约、编译器、迁移和基于 `EditorNodeGraphKit` 的 ShaderGraph 式编辑器外壳。当前阶段的主要问题不再是“能不能打开”，而是要把图编辑器变成可长期使用的叙事 authoring 工具：

- 画布交互必须经过真实 Unity Editor 手测，而不能只依赖编译通过。
- 端口连接规则需要从临时判断收紧为稳定语义，避免任意节点互连导致运行时不可解释。
- “对白完成后接多个选项节点，每个选项节点代表一个玩家选择项”需要成为编译器和运行时的正式契约。
- 命令节点字段需要类型化，例如播放视频在编辑器里选 `VideoClip` 或资源引用，而不是让作者手填字符串。
- 示例剧情图需要可在 Editor 中真实播放并逐步交互，而不是只通过默认路径自动 smoke，看不到选项、命令、等待和章节跳转的完整流程。

本 roadmap 接在 `features/2026-06-20-story-editor/` 之后，作为后续多条 feature 的共同规划层。

## 2. 范围与明确不做

### 范围

- Story Editor 的画布交互验收和回归证据。
- `EditorNodeGraphKit` 的通用图编辑行为：右键菜单、快捷键、焦点、拖拽、缩放、连线、端口创建节点、框选，以及由外部编辑器提供的节点模板、分组和样式键。
- Story 的节点/端口语义：开始/结束、对白/旁白、选项、媒体/音频/命令、等待和跳转；条件、随机、标记、辅助、Parallel/Merge 不作为默认作者主路径。
- 选项分支 authoring 与 runtime `StoryFrame.Choices -> Select(choiceId)` 的闭环。
- 命令字段类型化，尤其是媒体/资源类字段的编辑器 object 选择与 runtime 参数导出边界。
- 编译与校验错误在 graph node / port / field 上可定位。
- Editor 内运行时播放窗口：编译当前 authoring graph 后使用 `StoryModule` / `StoryRunner` 推进，并让作者手动选择选项、完成命令、推进等待和查看历史。
- Story runtime 多轨帧模型：一个剧情推进点可以同时输出视频、图片、音频、旁白/对白和选项，避免用作者图里的 Parallel/Merge 伪造表现层并行。

### 明确不做

- 不在本 roadmap 中升级 Unity 到 6000.2+ 或切换官方 Graph Toolkit。
- 不引入 Yarn Spinner / Ink 第三方 runtime。
- 不做完整脚本语言、复杂表达式编辑器、协作锁定、热重载或断点调试。
- 不在 `StoryModule` 中播放视频、渲染 UI、加载资源或保存存档。
- 不把播放窗口做成 Player 运行时 UI；它是 Editor-only 验收和调试工具。
- 不把 `UnityEngine.VideoClip`、Graph Toolkit graph asset、UI Toolkit VisualElement 或 Editor 类型写入 `StoryProgram` runtime 契约。
- 不恢复 `unit`、`payload`、owner action/transition 作为作者主界面概念。
- 不把剧情分类、节点语义、命令、交互或条件规则硬编码进 `EditorNodeGraphKit`。
- 不用 Parallel/Merge 作为“播放视频同时显示选项”的主要作者语义；该能力应由 runtime 多轨帧表达。

## 3. 模块拆分

### 3.1 EditorNodeGraphKit 交互层

职责：提供可复用的节点图库能力，和具体业务无关。

覆盖：右键菜单、快捷键、焦点、节点拖拽、端口拖线、palette 拖入、框选、pan/zoom、wire 绘制、minimap、blackboard、选中/删除。

约束：不得引用 `GameDeveloperKit.Story`、`StoryEditor`、`NodeKind` 或 `UnityEditor.Experimental.GraphView`；不得内置“流程/命令/交互/条件/辅助”等剧情分类，也不得在 palette 内自行决定业务分类的默认折叠、颜色或合法连接。

### 3.2 Story Graph 语义适配层

职责：把 Story 的节点类型、端口、字段、连接规则和中文文案映射到 `EditorNodeGraphKit`。

覆盖：`StoryEditorGraphAdapter`、`NodeSchemaRegistry`、`StoryAuthoringAsset` 边界维护、开始/结束节点保护、动态选项端口、命令字段模板。

约束：所有 Story 专有规则集中在 Story adapter/schema，不下沉到通用节点图库；Story adapter 负责把自己的节点分类、中文分组、样式键和端口策略注册给 NodeGraph。

### 3.3 Story Compiler / Runtime Bridge

职责：把 authoring graph 编译成 `StoryProgram`，并保证 runtime 输出能被 UI/表现层消费。

覆盖：Line、Choice、Command、Branch、Jump、Wait、End 的编译；`StoryFrame` 输出；`Select(choiceId)` 推进；`CompleteCommand(commandId, outcomeId)` 推进；多轨帧输出。

约束：运行时只消费 `StoryProgram` 和 bridge 接口，不消费 editor graph、layout 或 Unity Editor 资源对象。

### 3.4 Validation / Acceptance 证据层

职责：把手测结论、自动测试和编译错误定位沉淀为可追踪证据。

覆盖：Unity Editor 手测清单、Editor tests、runtime/compiler tests、`git diff --check`、错误消息定位、示例 story graph。

约束：验收证据不能只写“编译通过”；涉及画布手感的项目必须有手测记录或截图/操作清单。

## 4. 接口契约

### 4.1 通用节点图库契约

`EditorNodeGraphKit` 对外只暴露业务无关接口：

```csharp
public interface IEditorNodeGraphAdapter
{
    IReadOnlyList<EditorGraphNodeModel> Nodes { get; }
    IReadOnlyList<EditorGraphWireModel> Wires { get; }
    IReadOnlyList<EditorGraphNodeTemplate> Templates { get; }

    VisualElement CreateBlackboard();
    EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input);
    void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom);
    void MoveNode(string nodeId, Vector2 graphPosition);
    void SelectNode(string nodeId);
    void SelectNodes(IReadOnlyList<string> nodeIds);
    void SelectWire(string wireId);
    void Connect(EditorGraphPortRef output, EditorGraphPortRef input);
    void Disconnect(string wireId);
    void DeleteSelection();
    void SetNodeField(string nodeId, string fieldId, string value);
}
```

后续 feature 修改图交互时必须守住：

- 删除键只调用 `DeleteSelection()`，不直接操作业务模型。
- 框选只调用 `SelectNodes(nodeIds)`，批量删除仍通过 `DeleteSelection()` 进入业务适配层。
- 连接合法性只通过 `CanConnect(output, input)` 判断。
- 创建节点只通过 `CreateNode(template, graphPosition, connectFrom)`，palette / 右键 / 端口拖出都走同一入口。
- 节点库分组来自 `EditorGraphNodeTemplate.Category`，NodeGraph 不解释分组含义。
- 节点与模板颜色类来自 `StyleKey` 这种通用样式键，具体含义由宿主编辑器的 USS 决定。
- 画布坐标和 graph 坐标通过 `CanvasToGraph` / `GraphToCanvas` 转换，缩放后 wire 和 node 不允许脱离。

### 4.2 Story 节点连接语义契约

Story adapter 必须集中维护以下规则：

| 规则 | 契约 |
|---|---|
| 开始节点 | 每章恰好一个；无输入；允许单个流程输出；不出现在节点库；不可删除 |
| 结束节点 | 每章恰好一个；允许作为流程目标；无输出；不出现在节点库；不可删除 |
| 文本节点 | `Dialogue` / `Narration` 输出 `completed`；可连接普通流程节点；可多连到多个 `Choice` 节点形成一次玩家选择 |
| 选项节点 | 一个 `Choice` 节点代表一个玩家可选项；输入只能来自文本节点的 `completed`；输出 `selected` 单连到分支目标或结束 |
| 命令节点 | 输出由 command schema 的 outcome 定义；等待型命令必须通过 `CompleteCommand(commandId, outcomeId)` 推进 |
| 条件节点 | 不再作为默认作者主路径；如保留，只用于高级 branch 或边条件编辑，不进入新手节点库 |
| 辅助节点 | 不再作为默认作者主路径；注释/分组等应作为图编辑器装饰能力或高级节点，而不是运行时流程节点 |
| Parallel/Merge | 不再作为“同时播放媒体和显示选项”的默认方案；该需求由多轨帧模型表达 |

连接失败必须给中文原因，且至少包含端口语义，例如“开始节点不能作为目标”“选项节点只能接在对白或旁白完成后”。

### 4.3 选项分支编译契约

作者图表达：

```text
Dialogue.completed
  -> Choice(textKey=choice.help).selected -> Branch A
  -> Choice(textKey=choice.leave).selected -> Branch B
```

编译结果：

- 文本节点仍编译为 `StoryStepKind.Line`。
- 多个连接到同一个文本 `completed` 的 `Choice` 节点合成为一个 runtime `StoryStepKind.Choice`。
- 合成 step id 使用稳定规则：`{lineNodeId}_choices`。
- 每个 `Choice` 节点生成一个 `StoryChoice`：
  - `ChoiceId = choiceNode.NodeId`
  - `TextKey = choiceNode.Parameters["textKey"]`
  - `Condition = choiceNode condition / edge condition`
  - `Target = choiceNode.selected` 对应目标
- 选项节点本身不再作为独立 runtime step 执行。

运行时输出：

```csharp
StoryFrame.CurrentFrame
IReadOnlyList<StoryChoice> StoryFrame.Choices
```

UI / 表现层只读取 `CurrentFrame.Choices` 渲染按钮；玩家选择后调用：

```csharp
runner.Select(choiceId);
```

运行时不得反向依赖具体 UI。

### 4.4 多轨帧输出契约

“一边播放视频同时显示选项”“显示图片 + 播放音频 + 显示选项”“旁白 + 音效 + 选项”“对话 + 选项”不应依赖作者手动搭 Parallel/Merge。后续 runtime 应把这些表达为一次可观察输出帧：

```csharp
public sealed class StoryFrame
{
    public IReadOnlyList<StoryFrameTrack> Tracks { get; }
    public IReadOnlyList<StoryChoice> Choices { get; }
    public bool WaitsForChoice { get; }
    public bool WaitsForCommand { get; }
}
```

契约：

- 一个 frame 可以同时包含 media、audio、text、command 和 choices。
- 表现层按 track 类型并发呈现；`StoryModule` 只产出数据，不直接播放视频或音频。
- 选项仍通过 `Select(choiceId)` 推进；等待型 command 仍通过 `CompleteCommand(commandId, outcomeId)` 推进。
- 作者图中的节点应表达内容组合和下一步，而不是用 Parallel/Merge 表达表现层并发。
- 播放窗口应能显示同一 frame 的全部 tracks 和 choices，避免“点击播放直接完成但看不到流程”。

### 4.5 命令字段类型化契约

命令 schema 是字段定义的唯一来源，字段至少包含：

```csharp
public sealed class NodeParameterDefinition
{
    public string Key { get; }
    public string Label { get; }
    public ParameterValueType ValueType { get; }
    public bool Required { get; }
    public string Tooltip { get; }
}
```

后续扩展媒体/资源字段时遵守：

- Editor authoring 可使用 `ObjectField` 选择 `VideoClip` 或其他 Unity object。
- Authoring asset 可以保存 editor object reference 或 asset guid，但编译后的 `StoryProgram` 只保存稳定的命令参数，如 asset guid、address/key 或业务资源 id。
- `StoryCommand.Arguments` 不保存 Unity object 实例。
- 缺失必填 object/resource 字段时，编译失败并定位到 chapter / node / field。
- Resource/Localization/UI/Video playback 仍由业务 command handler 处理，不进入 `StoryModule`。

### 4.6 验收证据契约

每条子 feature 至少提供一种证据：

- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore`
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`
- 针对 `EditorNodeGraphKit` / compiler / runtime 的自动测试
- Unity Editor 手测清单，至少包含：右键、Space、Delete、Esc、F、拖拽节点、端口拖线、palette 拖入、框选、缩放后 wire 对齐

## 5. 子 feature 清单

| 顺序 | slug | 状态 | 依赖 | 说明 |
|---|---|---|---|---|
| 1 | `editor-graph-manual-acceptance` | done | 无 | 最小闭环：真实 Unity Editor 手测并修正图交互明显问题 |
| 2 | `story-graph-port-policy` | done | 1 | 收紧 Story 节点/端口连接规则和错误提示 |
| 3 | `choice-item-branching-contract` | done | 2 | 固化“对白完成 -> 多个选项节点 -> 分支目标”的 authoring、编译和 runtime 输出 |
| 4 | `typed-command-fields` | done | 2 | 让播放视频等命令字段支持类型化编辑和稳定导出 |
| 5 | `story-graph-validation-feedback` | done | 2, 3, 4 | 在图上暴露缺字段、断边、非法连接、未知命令等校验反馈 |
| 6 | `sample-story-graph-fixture` | done | 3, 4 | 提供 3-4 章示例剧情图，覆盖旁白、对白、选项、命令和分支 |
| 7 | `story-playback-window` | done | 6 | 提供 Editor-only 运行时播放窗口，真实推进当前剧情并交互选择、命令和等待 |
| 8 | `node-graph-generic-boundary` | done | 1 | 收紧 NodeGraph 通用边界，移除剧情分类和业务样式硬编码 |
| 9 | `story-runtime-multitrack-frame` | done | 7, 8 | 重做 runtime 可观察输出，让一个剧情帧能同时包含视频、图片、音频、文字和选项 |
| 10 | `story-editor-node-simplification` | in-progress | 9 | 基于多轨帧模型精简节点库，移除或隐藏高理解成本节点 |

技术依赖顺序如上；产品优先级可由用户在实际启动 feature 前调整。

## 6. 排期建议

第一批只做最小闭环：

1. `editor-graph-manual-acceptance`
2. `story-graph-port-policy`
3. `choice-item-branching-contract`

`editor-graph-manual-acceptance`、`story-graph-port-policy`、`choice-item-branching-contract`、`typed-command-fields`、`story-graph-validation-feedback`、`sample-story-graph-fixture`、`story-playback-window`、`node-graph-generic-boundary` 和 `story-runtime-multitrack-frame` 已完成。当前正在推进 `story-editor-node-simplification`，基于多轨帧模型精简剧情节点库和作者主路径。

## 7. 观察项

- 当前 `story-editor` feature checklist 已全部 `done`，不再回写为 pending；后续工作从本 roadmap 派生新 feature。
- `story-editor` / `story-module` requirement 仍是 draft，后续验收通过后应由 `cs-req` 或 `cs-feat-accept` 视实际能力回填 current 状态。
- architecture 只记现状；本 roadmap 的规划内容不直接写入 `ARCHITECTURE.md`。

## 8. 自查

- 模块拆分：已按通用节点图库、Story 语义适配、Compiler/Runtime bridge、Validation/Acceptance 分层。
- 接口契约：已写到 adapter 方法、连接规则、选项编译规则、命令字段边界级别。
- 子 feature 粒度：每条都可独立进入 `cs-feat-design`。
- 依赖关系：DAG，无循环。
- 最小闭环：`editor-graph-manual-acceptance`。
- 明确不做：已列出 Unity 升级、第三方 runtime、runtime 播放表现、Editor 类型进入 StoryProgram 等边界。
