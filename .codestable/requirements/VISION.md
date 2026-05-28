---
doc_type: requirements-index
last_reviewed: 2026-05-28
---

# Requirements Vision

## Draft

- `config-module` — 在运行时统一读取和查询配置表，不再让业务关心表来自 XML、CSV、JSON 还是 Unity 资产。
- `command-module` — 把可撤销的操作统一成命令历史，让编辑、建造和工具流程可以执行、撤销、重做，而不是各自维护状态回退。
- `logger` — 让运行时日志有统一入口、等级和输出去向，排查问题时不用到处找 Debug.Log。
- `resource-editor` — 在 Unity Editor 里维护资源包配置和收集规则，而不用手写 manifest JSON。
- `sound-module` — 统一播放背景音乐、音效和音轨效果，让业务不用反复手写 AudioSource 管理。
- `ui-module` — 在运行时按窗口类型打开 UI、适配安全区并生成组件绑定代码，让业务不再手写 prefab 加载、层级和节点查找。

## Current

暂无。

## Outdated

暂无。
