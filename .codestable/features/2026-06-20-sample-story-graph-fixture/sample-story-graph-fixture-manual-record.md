---
doc_type: feature-manual-acceptance
feature: 2026-06-20-sample-story-graph-fixture
status: passed
date: 2026-06-20
tags: [story, editor, fixture, node-graph]
---

# 示例剧情图手测记录

## 1. 打开入口

- 菜单：`GameDeveloperKit/剧情编辑器/打开示例剧情图`
- 窗口按钮：打开 `GameDeveloperKit/剧情编辑器` 后点击工具栏 `打开样例`
- 生成位置：`Assets/GameDeveloperKit/Story/SampleStoryGraph.asset`
- CSV 样例目录：`Assets/GameDeveloperKit/Simples/`

## 2. 待确认清单

| 编号 | 操作 | 预期 | 结果 | 备注 |
|---|---|---|---|---|
| S1 | 打开示例剧情图 | 左侧显示 `剧情  sample_story_graph` | passed | 用户确认示例都没问题 |
| S2 | 展开章节 | 看到 `雨夜抵达`、`旧车站`、`暗巷`、`余波` 四章 | passed | 用户确认示例都没问题 |
| S3 | 选择 `雨夜抵达` | graph 布局从左到右可读，包含开场视频、守卫对白、两个选择、条件、显示图片、等待、跳转节点 | passed | 用户确认示例都没问题 |
| S4 | 选择 `旧车站` | graph 包含旁白、播放环境音、列车员对白、两个选择、设置标记、比较、跳转节点 | passed | 用户确认示例都没问题 |
| S5 | 选择 `暗巷` | graph 包含陌生人对白、两个选择、小游戏、清除标记、跳转节点 | passed | 用户确认示例都没问题 |
| S6 | 选择 `余波` | graph 包含旁白、主角对白、停止音频、发送事件、等待、结束节点 | passed | 用户确认示例都没问题 |
| S7 | 点击 `编译` | 状态显示编译通过，无红色 error | passed | 用户确认示例都没问题 |
| S8 | 点击黑板 `播放当前章节` | 预览能自动推进并显示播放通过 | passed | 用户确认示例都没问题 |

## 3. 自动证据

- `dotnet build GameDeveloperKit.Editor.csproj --no-restore` 通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore` 通过。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。
- 当前项目已有 Unity Editor 实例打开，未从命令行运行 batchmode Test Runner；真实手测应在当前 Editor 内完成。
