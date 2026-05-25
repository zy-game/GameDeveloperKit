# event-module 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-20
> 关联方案 doc：`.codestable/features/2026-05-20-event-module/event-module-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `Super.Event`：`Assets/GameDeveloperKit/Runtime/Super.cs` 已暴露 `public static EventModule Event => Get<EventModule>();`。
- [x] `EventModule`：`Assets/GameDeveloperKit/Runtime/Event/EventModule.cs` 已提供 `Startup`、`Shutdown`、`Release`、`Subscribe<THandle>`、`Subscribe<TEvent>(Action<TEvent>)`、`Unsubscribe<THandle>`、`Unsubscribe<TEvent>(Action<TEvent>)`、`Fire<TEvent>`、`Clear`。

**名词层"现状 → 变化"逐项核对**：
- [x] `BindingAttribute`：已落地，构造时校验 key，并暴露 `Key`。
- [x] `IEventArgs`：已定义 `Use()` / `HasUse()`；`EventArgsBase` 提供默认实现。
- [x] `IEventHandle` / `IEventHandle<TEvent>`：已定义 `Handle(object sender, object args)` 与 `Handle(object sender, TEvent eventData)`。
- [x] `Subscription`：已暴露 `IsActive`、`Cancel()`、`Release()`。
- [x] `EventListener`：内部记录事件类型、handle 或 delegate、active 状态。

**流程图核对**：
- [x] `Super.Event -> Subscribe -> listener`：`EventModule.Subscribe` 已写入 `m_Listeners`。
- [x] `Super.Event -> Fire -> Dispatch snapshot -> Handlers`：`EventModule.Fire` 使用 `m_DispatchCache` 快照派发。
- [x] `Auto subscribe`：`EventBindingGenerated.RegisterAll(this)` 与 source generator 生成 `module.Subscribe(new Handle())` 对齐。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 订阅 / 取消 / 发布：`EventModuleTests` 覆盖 handle 和 action 两类订阅、取消、触发。
- [x] source generator 自动绑定：生成器识别 `BindingAttribute` 与 `IEventHandle`，生成 `EventModule` partial hook 和 `FireXxx` 扩展方法。
- [x] 重复订阅去重：`Subscribe_WhenSameHandleAddedTwice_InvokesOnce` 与 `SubscribeAction_WhenSameActionAddedTwice_InvokesOnce` 覆盖。

**明确不做逐项核对**：
- [x] 无异步事件 API：事件目录 grep 未发现 `UniTask<` / `async UniTask` 订阅发布 API；仅 `IGameModule` 生命周期使用 `UniTask`。
- [x] 无跨进程 / 网络事件：事件目录 grep 未发现 `Http`、`Socket`、`WebRequest`。
- [x] 无事件持久化 / 编辑器面板：事件目录 grep 未发现 `Editor` 或文件持久化逻辑。
- [x] 无事件队列 / 优先级 / 限流 / 复杂调度：事件目录 grep 未发现 `Queue`、`Priority`。

**关键决策落地**：
- [x] 命名采用 `EventModule`：Runtime 入口与 source generator 输出均改为 `EventModule`。
- [x] 强类型 API 为主路径：`Fire<TEvent>`、`IEventHandle<TEvent>`、`Action<TEvent>` 均约束 `TEvent : IEventArgs`。
- [x] 字符串 key 仅用于 `BindingAttribute` / generator：Runtime 发布入口无 string key。
- [x] Fire 立即同步派发：`Fire<TEvent>` 直接遍历快照并调用 handle / action。
- [x] 快照保护与消费中断：派发使用 `m_DispatchCache`；每次调用前检查 `eventData.HasUse()`。

**流程级约束核对**：
- [x] null handle 抛 `ArgumentNullException`。
- [x] null eventData 抛 `ArgumentNullException`。
- [x] 无订阅事件不抛异常。
- [x] handle 抛异常向调用方传播。
- [x] `Shutdown()` / `Release()` 清理 listener。

**挂载点反向核对**：
- [x] `Super.Event`：代码实际落点在 `Super.cs`。
- [x] `Assets/GameDeveloperKit/Runtime/Event/`：事件模块 Runtime 类型集中落地。
- [x] `EventBindingSourceGenerator`：生成器目标已从旧 `EventManager` / `EventBindingAttribute` / `Raise` / `IEventHandler` 迁移。
- [x] `GameDeveloperKit.Runtime.csproj`：新增 Runtime/Event 文件已纳入编译项。
- [x] 反向核查：grep `EventModule|BindingAttribute|IEventArgs|IEventHandle|Subscription|Super.Event|Fire...`，命中均落在挂载点或测试内。
- [x] 拔除沙盘：删除 `Runtime/Event/`、移除 `Super.Event`、回滚 source generator 事件输出、移除事件测试，即可拔除该 feature。

## 3. 验收场景核对

- [x] N1：注册 `EventModule` 后访问 `Super.Event` → 类型入口已落地；Runtime build 通过。
- [x] N2：`Subscribe(handle)` 后 `Fire(new MyEvent())` → `SubscribeAndFire_WhenHandleRegistered_InvokesHandle`。
- [x] N2a：`Subscribe<MyEvent>(action)` 后 `Fire(new MyEvent())` → `SubscribeActionAndFire_WhenActionRegistered_InvokesAction`。
- [x] N3：`Subscription.Cancel()` 后再次发布 → `SubscriptionCancel_WhenFireAgain_DoesNotInvokeHandle`。
- [x] N4：重复 handle 订阅只触发一次 → `Subscribe_WhenSameHandleAddedTwice_InvokesOnce`。
- [x] N5：`[Binding]` handle 自动订阅 → source generator 生成 `module.Subscribe(new Handle())`；generator build 通过。
- [x] N6：模块清理取消内部订阅 → `Shutdown_WhenCalled_ClearsListeners`。
- [x] N7：手写取消 handle / action → `Unsubscribe_WhenFireAgain_DoesNotInvokeHandle`、`UnsubscribeAction_WhenFireAgain_DoesNotInvokeAction`。
- [x] N8：`Use()` 后停止后续 handle → `Fire_WhenHandleUsesEvent_StopsDispatchingFollowingHandles`。
- [x] N9：事件声明类型生成扩展方法 → source generator 生成 `FireXxx(this EventModule module, TEvent eventData, object sender = null)`；generator build 通过。
- [x] B1：无订阅事件不抛异常 → `Fire_WhenEventHasNoListeners_DoesNotThrow`。
- [x] B2：派发中取消不破坏当前枚举 → `Fire_WhenHandleUnsubscribesDuringDispatch_KeepsCurrentSnapshotStable`。
- [x] B3：`Shutdown()` 清空 listener → `Shutdown_WhenCalled_ClearsListeners`。
- [x] E1：null handle 抛异常 → `Subscribe_WhenHandleIsNull_Throws`。
- [x] E2：null eventData 抛异常 → `Fire_WhenEventDataIsNull_Throws`。
- [x] E3：handle 内异常向外抛 → `Fire_WhenHandleThrows_PropagatesException`。

## 4. 术语一致性

- `EventModule`：Runtime、Super、source generator 生成目标一致。
- `BindingAttribute`：Runtime 类型与 generator metadata name 一致。
- `IEventArgs`：Runtime、测试、设计一致，包含 `Use()` / `HasUse()`。
- `IEventHandle` / `IEventHandle<TEvent>`：Runtime、generator、测试一致。
- `Subscription`：Runtime 与设计一致。
- 防冲突：grep 未发现旧 `EventBindingAttribute`、`EventManager`、`IEventHandler`、`Raise`、`Register<` 残留在事件 Runtime / generator 中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已新增 Event 子系统，记录入口、核心类型和派发策略。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已补充事件模块同步派发、事件消费中断、无异步/队列语义等约束。

## 6. requirement 回写

- [x] design frontmatter 未指定 `requirement`，项目当前无 `.codestable/requirements/` 产物；本次作为框架基础能力已在 architecture 归并。是否后续 backfill requirement 留给用户决定，本次验收不阻塞。

## 7. roadmap 回写

- [x] design frontmatter 无 `roadmap` / `roadmap_item`，非 roadmap 起头；跳过 roadmap 回写。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露新的通用环境 / 工具 / 工作流注意事项；已有 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 仍适用。

## 9. 遗留

- 已知限制：未实际运行 Unity Test Runner；当前证据为 Runtime build、source generator build、IDE diagnostics 和测试代码覆盖。
- 已知限制：`dotnet build GameDeveloperKit.Runtime.Tests.csproj` 失败，原因是 generated csproj 引用多份当前不存在的历史测试文件，非本次事件模块改动引入。
- 顺手发现：`UserSettings/Layouts/default-2022.dwlt` 有既有未相关修改，本 feature 未触碰。
