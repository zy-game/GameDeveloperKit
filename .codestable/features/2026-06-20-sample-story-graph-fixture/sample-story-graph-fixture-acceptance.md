---
doc_type: feature-acceptance
feature: 2026-06-20-sample-story-graph-fixture
status: passed
summary: 示例剧情图 fixture 已完成，覆盖四章中文剧情图、CSV v3 round-trip、compiler/runtime smoke 和 Story Editor 手测。
tags: [story, editor, fixture, node-graph, csv]
---

# Sample Story Graph Fixture 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-20
> 关联方案 doc：`.codestable/features/2026-06-20-sample-story-graph-fixture/sample-story-graph-fixture-design.md`

## 1. 接口契约核对

- [x] Sample story graph fixture：`StorySampleGraphFixture` 已提供 canonical 样例构造，StoryId 为 `sample_story_graph`，Version 为 `1.0.0`，EntryVolumeId 为 `volume_black_rain`。
- [x] CSV v3 fixture：`Assets/GameDeveloperKit/Simples/` 提供 `story/chapters/nodes/edges/parameters/conditions/layout/warnings` 8 个 CSV sheet；测试断言不生成 `volumes.csv` / `units.csv`。
- [x] Authoring asset fixture：入口可创建 `Assets/GameDeveloperKit/Story/SampleStoryGraph.asset`；实现不手写 Unity serialized asset 或 `.meta`。
- [x] Canonical graph：四章为 `chapter_arrival`、`chapter_station`、`chapter_alley`、`chapter_final`，节点标题使用中文语义标题。
- [x] Runtime smoke path：测试通过 `StoryModule.Register` / `StartProgram` 推进样例 program，媒体和小游戏只由 `CompleteCommand` 模拟表现层完成。

## 2. 行为与决策核对

- [x] 需求摘要：样例覆盖多章节、旁白、对白、选项、命令、条件/分支、等待、跳转章节和固定布局。
- [x] 关键决策 1：样例使用 Story 语义和 `NodeSchemaRegistry` 字段，不新增 legacy authoring 表达。
- [x] 关键决策 2：样例不是节点大全，优先覆盖真实作者常用闭环；未把 Switch/Parallel/Random 等专项节点塞入本 fixture。
- [x] 关键决策 3：数据可重复生成并可 CSV v3 round-trip；自动测试覆盖关键 ID/数量一致。
- [x] 关键决策 4：`Assets/GameDeveloperKit/Simples/` 已替换为完整 CSV v3 样例。
- [x] 明确不做：未新增剧情表现播放器、视频播放、图片显示、音频播放、小游戏实现或 UI 按钮渲染。
- [x] 明确不做：未引入 Yarn Spinner / Ink，未把样例改成脚本语言。
- [x] 明确不做：未改 CSV v3 schema，未恢复 legacy sheet 作为默认样例。
- [x] 明确不做：未恢复 `unit`、`payload`、owner action/transition 作为作者主界面概念。
- [x] 明确不做：runtime `StoryProgram` 仍只保存稳定字符串等基础值，不保存 Unity object 实例或 Editor 类型。
- [x] 挂载点反向核对：本 feature 的用户可见入口集中在 sample fixture builder、CSV v3 样例、Story Editor 样例入口、fixture tests 和手测记录。

## 3. 验收场景核对

- [x] A1 canonical asset 构造：测试断言 story/version/entry volume/entry chapter 均为预期值。
- [x] A2 章节覆盖：测试断言存在四章，且章节 ID 与 volume 章节列表一致。
- [x] A3 选项契约：入口章节包含文本 completed 到多个 Choice item，再由 Choice.selected 单连到不同目标。
- [x] A4 命令字段：PlayVideo/ShowImage/PlayAudio 等资源字段使用 schema key，值为稳定字符串。
- [x] A5 CSV 导出：导出包含 8 个 CSV sheet，无 legacy `volumes.csv` / `units.csv`。
- [x] A6 CSV 导入回归：导入后关键 story/chapter/node/edge/parameter/condition/layout ID 保持。
- [x] A7 编译：canonical asset 编译为 `StoryProgram` 无 error，可注册到 `StoryModule`。
- [x] A8 runtime smoke：能观察 line/choice/command/wait/chapter jump 或 completed；表现层命令通过 `CompleteCommand` 模拟。
- [x] A9 编辑器手测：`.codestable/features/2026-06-20-sample-story-graph-fixture/sample-story-graph-fixture-manual-record.md` 已记录 S1-S8 全部 passed，用户确认示例都没问题。
- [x] E1 反向范围：grep 命中 legacy/unit/payload 主要来自旧兼容代码和旧编辑器测试，不是本 fixture 新增作者主界面概念；runtime editor graph 隔离仍由现有测试守护。
- [x] E2 不做自动布局：fixture 提供固定 layout，不声称提供自动布局算法。

## 4. 术语一致性

- `Sample story graph fixture`：实现名为 `StorySampleGraphFixture`，与方案一致。
- `CSV v3 fixture`：使用现有 8 sheet 命名，未新增 Excel sheet。
- `Canonical graph`：代码和手测均使用四章中文语义节点。
- 防冲突：`unit` / `payload` / owner action/transition 仍仅作为旧兼容、旧窗口或旧 CSV 迁移概念存在，不作为 Story Editor 样例主界面概念。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已在 Story Editor / Editor Node Graph 章节补充 `StorySampleGraphFixture`、`sample_story_graph` 四章样例、CSV v3 fixture、手测入口和 runtime smoke 边界。
- [x] 架构边界保留：fixture 不改变 `StoryModule` 播放职责，资源字段仍是稳定字符串，真实媒体播放仍由业务表现层处理。

## 6. requirement 回写

- [x] `.codestable/requirements/story-editor.md`：已补 `implemented_by: 2026-06-20-sample-story-graph-fixture`，并追加示例剧情图 fixture 实现进展。
- [x] `.codestable/requirements/story-module.md`：已补 runtime smoke 实现进展，明确这不是 runtime 真实媒体播放器能力。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-items.yaml`：`sample-story-graph-fixture` 已改为 `done`，YAML 校验通过。
- [x] `.codestable/roadmap/story-editor-hardening/story-editor-hardening-roadmap.md`：子 feature 清单和排期建议已同步，下一步指向 `story-playback-window`。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 attention.md 的新内容。已有注意事项“Unity Editor 打开时不跑 batchmode Test Runner”仍适用。

## 9. 遗留

- 后续优化点：`story-playback-window` 已登记为下一条 roadmap item，用运行时模块在 Editor 窗口中真实播放和交互完整流程。
- 已知限制：样例资源 ID 是稳定字符串，不要求项目内真实存在对应视频、图片或音频资源。
- 已知限制：fixture 提供固定 layout，不提供自动布局算法。
