---
doc_type: feature-manual-acceptance
feature: 2026-06-20-editor-graph-manual-acceptance
status: passed
date: 2026-06-20
tags: [story, editor, node-graph, manual-acceptance]
---

# Story Editor Graph 手测记录

## 1. 当前状态

用户已确认 N1-N15 均正常。本轮 Story Editor graph 基础交互手测通过。

说明：此前命令行 batchmode Test Runner 因当前项目已有 Unity Editor 实例打开而无法执行；真实手测以用户在当前 Editor 中操作结果为准。

## 2. 已完成证据

- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 此前通过。
- 用户确认 N1-N15 手测场景正常。
- 自动测试侧已有覆盖：graph 渲染、创建节点委托、连接校验委托、缩放锚点、Delete 委托、palette 外部释放、runtime 不引用 editor graph。

## 3. 手测清单

| 编号 | 操作 | 结果 | 备注 |
|---|---|---|---|
| N1 | 打开 `GameDeveloperKit/剧情编辑器` | passed | 用户确认正常 |
| N2 | 选择章节 | passed | 用户确认正常 |
| N3 | graph 空白处右键 | passed | 用户确认正常 |
| N4 | graph 获得焦点后按 Space | passed | 用户确认正常 |
| N5 | 从节点库拖节点到画布释放 | passed | 用户确认正常 |
| N6 | 拖拽节点标题或空白区域 | passed | 用户确认正常 |
| N7 | 从输出端口拖到合法输入端口 | passed | 用户确认正常 |
| N8 | 从输出端口拖到空白处 | passed | 用户确认正常 |
| N9 | 滚轮缩放 | passed | 用户确认正常 |
| N10 | 缩放后观察已有连线 | passed | 用户确认正常 |
| N11 | 中键或 Alt+左键拖动画布 | passed | 用户确认正常 |
| N12 | 选中节点或连线后按 Delete / Backspace | passed | 用户确认正常 |
| N13 | 端口拖线过程中按 Esc | passed | 用户确认正常 |
| N14 | 选中节点后按 F | passed | 用户确认正常 |
| N15 | graph 空白处左键拖出框选矩形 | passed | 用户确认正常 |

## 4. 下一步

N1-N15 已通过；本 feature 可进入验收闭环并回写 roadmap item。
