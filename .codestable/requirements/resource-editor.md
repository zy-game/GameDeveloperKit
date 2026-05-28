---
doc_type: requirement
slug: resource-editor
pitch: 在 Unity Editor 里维护资源包配置和收集规则，而不用手写 manifest JSON。
status: draft
last_reviewed: 2026-05-28
implemented_by: []
tags: [resource, editor, unity, manifest]
---

# 在 Unity Editor 里管理资源包配置

## 用户故事

- 作为维护资源模块的开发者，我希望先在项目级配置里维护 package、bundle、资源收集器和打包方式，而不是直接手写 manifest JSON。
- 作为配置热更新资源的人，我希望在 package 列表里一眼看出哪些包参与热更，而不是点进每个包查字段。
- 作为整理 bundle 的人，我希望选中 package 后展开查看 bundle、group、收集到的资源和检查结果，而不是自己在散落的配置里比对。

## 为什么需要

资源模块已经有运行时清单模型，但编辑阶段更需要一份稳定的项目配置来描述“要收集什么、用哪个收集器、怎么分包、哪些包热更”。如果直接把 manifest 当人工编辑入口，构建规则、热更标记、资源归组和资源检查会混在运行时数据里，后续很难维护。

## 怎么解决

提供一个 Unity Editor 工具窗口，先创建并维护项目级资源配置。编辑器启动时收集可用的资源收集器、打包方式和资源检查器；配置里按 package 展示热更状态，选中 package 后管理 bundle 列表、收集器类型、打包方式、group、资源集合和检查结果。运行时 manifest 由这份配置派生，不作为首要人工新建对象。

## 边界

- 它不负责打 AssetBundle、上传资源、下载缓存或热更新发布。
- 它不替代运行时资源模块，也不改变现有 manifest 数据结构。
- 它不把 manifest JSON 当作首要编辑入口；需要导出 manifest 时按现有运行时字段生成。
- 它管理的是编辑期资源配置；需要新增运行时字段时另起 manifest schema 设计。
- 它要求用户已经理解资源 package / bundle / asset 的基本含义，不做面向非开发者的向导式教学。
