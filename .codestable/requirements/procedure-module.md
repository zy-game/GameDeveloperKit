---
doc_type: requirement
slug: procedure-module
pitch: 用一个全局流程状态机管理游戏启动、检查更新、登录、主菜单、战斗等顶层阶段，让业务不用把主流程散落在场景、UI 和异步回调里。
status: draft
last_reviewed: 2026-06-04
implemented_by: []
tags: [procedure, runtime, state-machine, flow]
---

# 全局流程状态机

## 用户故事

- 作为玩法或框架开发者，我希望把启动、检查版本、登录、主菜单、战斗等顶层流程定义成明确的 procedure，而不是散落在 MonoBehaviour、场景脚本和 UI 回调里。
- 作为维护框架的人，我希望一次只存在一个当前顶层流程，并且流程切换有统一的 enter / leave / update 顺序，而不是每个业务点自己约定切换时机。
- 作为做启动链路的人，我希望 procedure 可以协调 Resource、UI、Data、Command、Event、Logger 等模块，但不把这些模块的职责吞进去。
- 作为排查流程问题的人，我希望能观察当前流程、上一个流程、是否正在切换和切换失败原因，而不是只看到一串异步调用。

## 为什么需要

游戏顶层阶段天然是全局互斥的：启动时不会同时处在登录、主菜单和战斗主流程里。没有统一流程状态机时，状态经常被 UI 打开关闭、场景加载、资源初始化和异步回调分散维护，最后容易出现重复进入、旧流程没清理、切换中又触发切换、以及不同模块互相猜当前阶段的问题。

## 怎么解决

提供一个运行时 `ProcedureModule`：业务可以直接通过 `Super.Procedure.ChangeAsync<TProcedure>()` 切换当前流程；目标 procedure 未提前注册但可无参创建时，由模块首次切换时自动创建并初始化。需要传入依赖或复用已有实例时，业务也可以先 `RegisterProcedure(instance)`。模块负责维护当前 procedure、串行执行 leave / enter、在 Unity Update 中驱动当前流程，并暴露本地状态变化通知。具体资源加载、UI 打开、命令执行、事件派发仍由 procedure 内部调用已有模块完成。

## 边界

- 它不替代 `OperationModule`；长耗时下载、资源加载和可等待任务仍走 operation 或对应模块，Procedure 只编排顶层阶段。
- 它不替代 `UIModule`；窗口层级、返回栈、安全区和 UI 资源释放仍归 UI 模块。
- 它不替代 `CommandModule`；撤销 / 重做历史仍归命令模块。
- 它不替代 `EventModule`；事件订阅和派发仍归事件模块，ProcedureModule 的状态变化首版只提供本地事件。
- 首版不做通用 FSM 图编辑器、嵌套状态机、并行状态、pushdown state stack、可视化调试面板或流程持久化恢复。
- 首版不负责加载 Unity Scene；如果 procedure 需要切场景，应在自身 enter / leave 中调用资源或场景能力。
