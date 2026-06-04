---
doc_type: requirements-index
last_reviewed: 2026-06-04
---

# Requirements Vision

## Draft

- `config-module` — 在运行时统一读取和查询配置表，不再让业务关心表来自 XML、CSV、JSON 还是 Unity 资产。
- `command-module` — 把可撤销的操作统一成命令历史，让编辑、建造和工具流程可以执行、撤销、重做，而不是各自维护状态回退。
- `combat-module` — 用统一的战斗世界、实体、组件和系统编排战斗逻辑，让业务不用直接散用底层 ECS 库。
- `data-module` — 把运行中会变化的数据集中保存、读取和回滚，让业务不用各自维护一套缓存、key 和本地保存逻辑。
- `framework-startup` — 让框架在 Unity 场景里一键按依赖启动和关闭，不再让业务手动排列模块注册顺序。
- `logger` — 让运行时日志有统一入口、等级和输出去向，排查问题时不用到处找 Debug.Log。
- `procedure-module` — 用一个全局流程状态机管理游戏启动、检查更新、登录、主菜单、战斗等顶层阶段，让业务不用把主流程散落在场景、UI 和异步回调里。
- `resource-editor` — 在 Unity Editor 里维护资源包配置和收集规则，而不用手写 manifest JSON。
- `resource-build-publish` — 在编辑器里把资源配置一键构建成本地包并发布到远端。
- `runtime-diagnostics` — 把日志模块升级成运行时调试入口，让日志、埋点、性能指标、崩溃线索和命令工具集中可看、可上传、可执行。
- `sound-module` — 统一播放背景音乐、音效和音轨效果，让业务不用反复手写 AudioSource 管理。
- `tag-management` — 在一个地方维护项目标签并让运行时读取同一份标签目录，避免标签散落在资源、Unity 设置和手写常量里。
- `ui-module` — 在运行时按窗口类型打开 UI、适配安全区并生成组件绑定代码，让业务不再手写 prefab 加载、层级和节点查找。

## Current

暂无。

## Outdated

暂无。
