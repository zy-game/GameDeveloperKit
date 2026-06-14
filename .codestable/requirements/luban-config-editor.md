---
doc_type: requirement
slug: luban-config-editor
pitch: 在 Unity Editor 里管理 Luban 配置工程、生成代码和校验结果，不再靠手写命令来维护配置流水线。
status: draft
last_reviewed: 2026-06-10
implemented_by: []
tags: [config, editor, luban, tooling]
---

# Luban 配置编辑工具

## 用户故事

- 作为框架维护者，我希望在 Unity Editor 里配置 Luban 工程、目标和输出目录，而不是每次都手写一长串命令。
- 作为配置维护者，我希望点一下就能检查配置表和生成结果，并看到清楚的错误位置，而不是去命令行里翻日志。
- 作为玩法开发者，我希望配置生成后的代码和数据能稳定落到项目约定目录，而不是每个人本地脚本各写一套输出路径。

## 为什么需要

Luban 已经负责配置 schema、数据校验、代码生成和数据导出，但接入到 Unity 项目后，真正容易出错的是工程路径、目标、输出目录和命令参数。只靠手写脚本会让配置流水线分散在个人机器上，出错时也不容易看出是表格问题、Luban 参数问题还是输出路径问题。

## 怎么解决

提供一个 Unity Editor 工具来维护 Luban workspace 和生成 profile：选择或创建配置工程，编辑生成目标和输出目录，调用 Luban 执行检查 / 生成，并把结果、日志和错误集中展示在窗口里。工具只做 Unity 侧编排，不替代 Luban 自己的 schema 和数据处理能力。

## 边界

- 它不重写 Luban 的 schema、加载器、验证器或代码生成器。
- 它不在运行时依赖 Luban，也不让 Player 构建携带 Luban 工具 DLL。
- 它不替代 Excel 或外部表格编辑器；首版重点是 Luban 工程配置、生成 profile 和校验结果管理。
- 它不改变现有 `ConfigModule` 查询 API；是否用 Luban 生成结果替换运行时配置读取另起设计。
- 它不负责资源打包、远端上传、热更新发布或下载缓存。
