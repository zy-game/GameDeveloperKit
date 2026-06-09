---
doc_type: roadmap
slug: runtime-scheduling-diagnostics
status: active
created: 2026-06-08
last_reviewed: 2026-06-08
tags: [runtime, timer, diagnostics, debug, scheduling, network]
related_requirements: [timer-module, runtime-diagnostics, combat-module, procedure-module, logger]
related_architecture: [ARCHITECTURE]
---

# Runtime Scheduling And Diagnostics

## 1. 背景

当前 Runtime 已经有 Timer、Debug、Combat、Procedure 等模块，但时间推进和诊断状态还没有完全收束。TimerModule 已经具备 Unity `Update`、`LateUpdate`、`FixedUpdate` 三类 phase trigger，并通过显式 update handle 选择推进口径；Timer 全局 clock 只由 Update 推进，Late/Fixed 可做 handle 级 fps 门控。Debug profile/metrics 刷新仍由 `DebugGuiDriver.Update()` 直接读取 Unity delta；Combat 和 Procedure 仍各自创建 `MonoBehaviour` driver。2026-06-08 Debug 审计也指出 `DebugModule` 聚合过多子域，profile/redaction/日志输出边界互相牵动。

这份 roadmap 规划一条更统一的运行时编排路线：Timer 作为 Runtime `Update` / `LateUpdate` / `FixedUpdate` 的统一 phase 调度入口，模块通过注册 `UpdateTimerHandle`、`LateUpdateTimerHandle` 或 `FixedUpdateTimerHandle` 选择需要的 phase；DebugModule 收敛为诊断门面和 profile registry 生命周期，日志与内存状态由内建 `ProfileHandle` 承接；日志的网络发送不再由 Debug 模块持有 transport，实际 Network bridge 由未来 Network 模块读取已脱敏日志并提供实现。

> 说明：部分 item slug 保留历史命名里的 `consumer`，但当前已拍板的实现契约是显式 Timer update handle，不再使用 `ITimerUpdateConsumer` / `TimerUpdatePhase` / subscription 模型。

## 2. 范围与明确不做

### 本 roadmap 覆盖

- Timer 增加 Runtime update handle 合约，让业务或框架模块注册到 `Update`、`LateUpdate`、`FixedUpdate` 三类 phase，而不是各自创建 driver。
- Debug profile/metrics 刷新改为 Timer update handle；`DebugGuiDriver` 只保留 IMGUI `OnGUI` 桥接。
- CombatModule / ProcedureModule 迁移到 Timer update handle，删除各自 runtime update driver。
- DebugModule 内部状态通过多张 `ProfileHandle` 暴露，优先压住 profile getter 异常、redaction `ToString()` 异常和 Debug 自身状态可观测缺口；日志入口由 `DebugProfileHandle` 承载，内存/metrics 由 `MemoryProfileHandle` 承载。
- 用既有 `ProfileHandle` 为 Debug、Timer、Combat、Procedure 等模块暴露诊断表。
- 定义 Network-backed debug log bridge 的接口契约：Debug 侧只提供已脱敏日志记录，Network 模块负责 bridge、payload 和具体发送实现。

### 明确不做

- 不在本 roadmap 内实现完整 NetworkModule、服务器协议、鉴权、重试、断线缓存或批量上传策略；这里只定义 Network 如何读取 Debug 已脱敏日志的接线契约。
- 不替代 Unity coroutine、UniTask、OperationModule 或业务异步流程；Timer 只负责运行时 tick/update 与简单时间调度。
- 不删除 `Startup` bootstrap，也不尝试把 IMGUI `OnGUI` 改成 Timer 调用；Unity 生命周期桥接仍允许存在。
- 不把 Combat 变成网络同步、锁步或回滚确定性系统；Combat 仍只负责本地 ECS 世界推进。
- 不把 Debug Console 改成 UGUI/UI Toolkit，也不引入业务 UI 模块依赖。
- 不要求所有模块一次性迁移；先迁移有明确 runtime driver 的 Debug/Procedure/Combat。

## 3. 模块拆分（概设）

```text
Runtime Scheduling And Diagnostics
├── Timer Scheduler：统一 Runtime Update/LateUpdate/FixedUpdate phase 调度入口
├── Runtime Update Handles：跨模块 update handle 合约与状态快照
├── Debug Diagnostics Profiles：Debug 门面下的 DebugProfileHandle/MemoryProfileHandle/profile registry 状态表
├── Module Profile Handles：Timer/Debug/Combat/Procedure 等模块的诊断表
├── Module Update Adapters：Debug/Procedure/Combat 的 Timer update handle 接入
└── Network Debug Log Bridge：Network 实现的 realtime Debug log bridge
```

### Timer Scheduler

- **职责**：维护 Timer 的全局 tick/time/unscaled time/delta，提供 `Update`、`LateUpdate`、`FixedUpdate` 三类 phase trigger，Delay/Countdown/Interval 默认随 Update 全局 clock 推进，并在每类 phase 内推进匹配的 update handles。
- **承载的子 feature**：`timer-update-consumer-contract`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs`、`TimerModule.Timer.cs`、`TimerSnapshot`

### Runtime Update Handles

- **职责**：给 Combat、Procedure、Debug 和业务模块提供统一的 Timer update handle 接入点；模块注册对应 handle 类型选择 tick，Timer 负责注册/移除、异常隔离和状态快照。
- **承载的子 feature**：`timer-update-consumer-contract`、`procedure-timer-consumer`、`combat-timer-consumer`
- **触碰的现有代码 / 模块**：Timer、Combat、Procedure、Debug

### Debug Diagnostics Profiles

- **职责**：保留 `DebugModule` 公开门面和 profile registry 生命周期，把日志接收、脱敏、过滤和内存 buffer 下沉到 `DebugProfileHandle`，把内存/metrics 采样下沉到 `MemoryProfileHandle`；P1 行为风险先在 registry/redaction 层修稳。
- **承载的子 feature**：`debug-profilehandle-hardening`、`debug-timer-refresh`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Debug/**/*.cs`、`DebugModuleTests`

### Module Profile Handles

- **职责**：复用既有 `ProfileHandle` 扩展点，让 Timer/Debug/Combat/Procedure 等模块把自身状态注册进 Debug Profiles tab。
- **承载的子 feature**：`runtime-module-profile-handles`
- **触碰的现有代码 / 模块**：Debug Profiles、Timer、Combat、Procedure

### Module Update Adapters

- **职责**：把已有独立 runtime driver 迁移为 Timer update handle，必要时保留只处理 Unity 生命周期的薄桥接。
- **承载的子 feature**：`debug-timer-refresh`、`procedure-timer-consumer`、`combat-timer-consumer`
- **触碰的现有代码 / 模块**：`DebugGuiDriver`、`ProcedureRuntimeDriver`、`CombatRuntimeDriver`

### Network Debug Log Bridge

- **职责**：保持 Debug 侧只暴露已脱敏日志记录；未来 Network 模块提供具体 bridge，把 `DebugLogRecord` 转换为网络 payload 并发送到服务端。
- **承载的子 feature**：`network-debug-log-transport-contract`
- **触碰的现有代码 / 模块**：未来 Network 模块；必要时只读取 DebugProfileHandle/DebugLogRecord 公开表面

## 4. 模块间接口契约 / 共享协议（架构层详设）

### 4.1 Timer update handle 合约

**方向**：Runtime 模块 / 业务 → TimerModule

**形式**：显式 handle 类型 + TimerModule 注册 API

**契约**：

```csharp
namespace GameDeveloperKit.Timer
{
    public readonly struct TimerUpdateContext
    {
        public long Tick { get; }
        public double Time { get; }
        public double UnscaledTime { get; }
        public float DeltaTime { get; }
        public float UnscaledDeltaTime { get; }
    }

    public abstract class TimerUpdateHandle : TimerHandle
    {
        public bool Enabled { get; set; }
        public long LastTick { get; }
        public Exception LastException { get; }
        public bool HasError { get; }
    }

    public sealed class UpdateTimerHandle : TimerUpdateHandle { }
    public sealed class LateUpdateTimerHandle : TimerUpdateHandle { }
    public sealed class FixedUpdateTimerHandle : TimerUpdateHandle { }
}
```

**TimerModule API**：

```csharp
public sealed partial class TimerModule : GameModuleBase
{
    public TimerHandle Register(TimerHandle handle, object owner = null, string tag = null);
    public T Register<T>(T handle, object owner = null, string tag = null) where T : TimerHandle;
    public bool Unregister(TimerHandle handle);

    public UpdateTimerHandle OnUpdate(Action callback, object owner = null, string tag = null);
    public UpdateTimerHandle OnUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null);
    public LateUpdateTimerHandle OnLateUpdate(Action callback, object owner = null, string tag = null);
    public LateUpdateTimerHandle OnLateUpdate(Action callback, float fps, object owner = null, string tag = null);
    public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null);
    public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null);
    public FixedUpdateTimerHandle OnFixedUpdate(Action callback, object owner = null, string tag = null);
    public FixedUpdateTimerHandle OnFixedUpdate(Action callback, float fps, object owner = null, string tag = null);
    public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null);
    public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null);
}
```

**约束**：

- `handle == null` 抛 `ArgumentNullException`。
- 同一个 active handle 重复注册返回已有 handle，不重复调用。
- Timer driver 提供 Unity `Update()`、`LateUpdate()`、`FixedUpdate()` 三个入口；每个入口只检查匹配 phase 的 handles。
- `Update` 使用 Unity `Time.deltaTime` / `Time.unscaledDeltaTime` 推进唯一全局 Timer clock；`LateUpdate` / `FixedUpdate` 不推进全局 clock，只把 phase unscaled delta 提供给 handle 级 fps gate。
- Delay/Countdown/Interval 默认随 Update 全局 clock 推进；同 phase update handles 按注册顺序调用。
- Late/Fixed handle 未传 `fps` 时每个匹配 phase 都调用；传入 `fps` 时按 phase unscaled delta 累积到 `1 / fps` 后调用，callback context 仍使用全局 Timer clock。
- 调用前取 dispatch buffer；回调内注册/移除其他 handle 不影响当前 tick。
- 单个 update handle 抛异常时，Timer 写入该 handle 的 `LastException`，继续推进后续 handle。
- Timer shutdown 时取消并清空全部 handles。

**旧调度 API 兼容**：

```csharp
public TimerDelayHandle Delay(float delay, Action callback, bool useUnscaledTime = false, object owner = null, string tag = null);
public TimerCountdownHandle Countdown(float duration, Action<float> onTick = null, Action onComplete = null, bool useUnscaledTime = false, object owner = null, string tag = null);
public TimerIntervalHandle Interval(float interval, Action<float> callback, bool useUnscaledTime = false, object owner = null, string tag = null);
```

Delay/Countdown/Interval 默认属于全局 Update clock。需要 LateUpdate/FixedUpdate 的持续回调时使用对应 update handle；如果未来确实要让 Delay/Countdown/Interval 选择 phase，应另起 feature 评估 API 形态。

### 4.2 Timer handle 状态快照

**方向**：TimerModule → Debug Timer tab / ProfileHandle

**形式**：只读结构

**契约**：

```csharp
public readonly struct TimerSnapshot
{
    public long Tick { get; }
    public double Time { get; }
    public double UnscaledTime { get; }
    public float DeltaTime { get; }
    public float UnscaledDeltaTime { get; }
    public IReadOnlyList<TimerDelayHandle> Delays { get; }
    public IReadOnlyList<TimerCountdownHandle> Countdowns { get; }
    public IReadOnlyList<TimerIntervalHandle> Intervals { get; }
    public IReadOnlyList<TimerUpdateHandle> Updates { get; }
}
```

**约束**：

- snapshot 只暴露 active handles，不暴露内部 `_handles` list。
- update handle 的 `LastTick` 是该 handle 最近一次成功或失败回调所在 tick。
- `LastException` 只记录最近一次异常；错误历史如果需要列表，另起 feature。

### 4.3 Debug ProfileHandle 合约

**方向**：DebugModule → ProfileHandle / Runtime 模块

**形式**：现有 `ProfileHandle` 扩展点 + 内建 profile handles

**ProfileHandle 当前公开契约**：

```csharp
public abstract class ProfileHandle
{
    public abstract string Name { get; }
    protected internal abstract void Draw();
}
```

**新增约束**：

- `DebugProfileRegistry` 只维护注册顺序，安全读取 `Name`，Profiles tab 绘制时调用派生类 `Draw()`；单个 profile draw 抛异常时，只影响对应 profile 区域。
- Debug 内建 profile 使用已有 `ProfileHandle`，不新增第二套 profile 接口。
- `DebugProfileHandle` 负责接收日志、脱敏、category/minimum level 过滤并持有 `DebugLogBuffer`；`DebugModule.Trace/Debug/Info/Warning/Error/Fatal` 只转发到该 handle，Debug 状态不再默认作为 Profiles tab profile 展示。
- `MemoryProfileHandle` 负责 metrics/memory 采样，持有 `DebugMetricSnapshot` 和固定容量 sample buffer，并自行绘制 memory 曲线。
- `DeviceInfoProfileHandle` 负责自行绘制非敏感设备/平台/图形信息。
- `DebugModule` 只管理内建/外部 profile 注册、模块生命周期、Unity log capture 和 Console/Command 薄门面；不持有 sink、transport 或 analytics 列表。
- 旧 sink、analytics sink、transport API 已退场；Debug 不再提供独立输出扩展点。

### 4.4 Network-backed Debug log bridge 合约

**方向**：Network 模块 → DebugProfileHandle / DebugLogRecord

**形式**：Network 定义 bridge / payload / sender，Debug 不持有 transport 列表

**建议契约**：

```csharp
namespace GameDeveloperKit.Network
{
    public readonly struct DebugLogPayload
    {
        public long Sequence { get; }
        public DateTimeOffset Timestamp { get; }
        public int FrameCount { get; }
        public long TimerTick { get; }
        public string Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string Exception { get; }
        public string Context { get; }
        public IReadOnlyList<string> Tags { get; }
    }

    public interface IDebugLogNetworkSender
    {
        UniTask SendDebugLogAsync(DebugLogPayload payload);
    }
}
```

**新增约束**：

- Network bridge 只能读取已完成 redaction 的 `DebugLogRecord`，不要求 DebugModule 持有 sender 或 transport。
- payload 从已 redacted 的 `DebugLogRecord` 转换而来；Network 不负责二次脱敏。
- 鉴权、连接、重试、批量发送、断线缓存和限流属于 NetworkModule 后续 feature。
- Bootstrap 或业务初始化负责在 Network ready 后启动 bridge；Debug 不感知网络生命周期。

### 4.5 Debug refresh handle 合约

**方向**：TimerModule → DebugModule

**形式**：Debug 内部 `UpdateTimerHandle`

**契约**：

```csharp
internal sealed class DebugRefreshHandle : UpdateTimerHandle
{
    public DebugRefreshHandle(DebugModule module);
}
```

**约束**：

- Debug refresh 默认使用 `UpdateTimerHandle`；若需要 LateUpdate 采样口径，应由 feature-design 明确改为 `LateUpdateTimerHandle`。
- refresh callback 只推进 profile refresh 和 metrics sample，不做 IMGUI 绘制。
- `DebugGuiDriver.OnGUI()` 仍由 Unity 调用，仅负责 `DrawGui()`；`DebugGuiDriver.Update()` 不再推进 metrics/profile。
- Timer 未注册时 Debug 可保留最小 fallback，但默认启动顺序下 Timer 先于 Debug。

### 4.6 Module update adapter 合约

**方向**：TimerModule → CombatModule / ProcedureModule

**形式**：模块内部保存注册到 Timer 的 update handle

**Combat 约束**：

```csharp
private FixedUpdateTimerHandle m_UpdateHandle;
```

- `m_UpdateHandle` callback 调用 `World.Update(context.DeltaTime)`。
- Combat 默认使用 `FixedUpdateTimerHandle`，因为战斗 world 当前内部有固定步推进语义；如业务希望显式用 Update，应由 feature-design 提供配置开关。
- CombatModule shutdown 必须 unregister handle，然后 dispose world。
- Combat 首版仍按需注册，不进入默认启动计划；若 Timer 未注册，Combat startup 应明确失败或创建受控 fallback，不再静默创建独立 runtime driver。

**Procedure 约束**：

```csharp
private UpdateTimerHandle m_UpdateHandle;
```

- `m_UpdateHandle` callback 调用当前 procedure 的 `OnUpdate(context.DeltaTime, context.UnscaledDeltaTime)`。
- Procedure 默认使用 `UpdateTimerHandle`；如流程需要 LateUpdate，应注册单独 handle 或显式配置。
- ProcedureModule shutdown 必须 unregister handle，再执行当前流程 leave/release。
- Procedure 不吞并 Resource/UI/Command/Event 的职责。

## 5. 子 feature 清单

1. **timer-update-consumer-contract** — 为 TimerModule 增加 `Update` / `LateUpdate` / `FixedUpdate` 三类显式 update handle、context、异常隔离和 snapshot。
   - 所属模块：Timer Scheduler / Runtime Update Handles
   - 依赖：无
   - 状态：done
   - 对应 feature：`2026-06-08-timer-update-consumer-contract`
   - 备注：最小闭环已完成；历史 slug 保留 `consumer`，实际契约是 `UpdateTimerHandle` / `LateUpdateTimerHandle` / `FixedUpdateTimerHandle`。2026-06-09 后 Timer clock 语义已修正为单一全局 clock，Late/Fixed 只作为 phase trigger。

2. **debug-profilehandle-hardening** — 修正 ProfileHandle 元数据异常隔离和 redaction `ToString()` 兜底，并把 Debug 日志/内存状态拆为内建 profile handle。
   - 所属模块：Debug Diagnostics Profiles / Module Profile Handles
   - 依赖：无
   - 状态：done
   - 对应 feature：`2026-06-08-debug-profilehandle-hardening`
   - 备注：已落地 `DebugProfileHandle` 接收日志、脱敏、过滤和内存 buffer；`ProfileHandle` 已收敛为 Name-only + 派生类自绘；`MemoryProfileHandle` 自绘 memory 曲线，`DeviceInfoProfileHandle` 自绘非敏感设备信息；旧 sink/analytics/transport 扩展点已移除，DebugModule 只管理 profile lifecycle 和薄入口。

3. **debug-timer-refresh** — 让 Debug metrics sampling 注册为 Timer update handle，`DebugGuiDriver` 只保留 `OnGUI` 绘制桥接。
   - 所属模块：Module Update Adapters / Debug Diagnostics Profiles
   - 依赖：timer-update-consumer-contract, debug-profilehandle-hardening
   - 状态：planned
   - 对应 feature：未启动
   - 备注：对应 2026-06-08 Debug 审计 finding-04。

4. **procedure-timer-consumer** — ProcedureModule 删除独立 update driver，改为 Timer update handle 推进当前 procedure。
   - 所属模块：Module Update Adapters
   - 依赖：timer-update-consumer-contract
   - 状态：planned
   - 对应 feature：未启动
   - 备注：迁移后 Procedure startup 需要明确 Timer 缺失语义。

5. **combat-timer-consumer** — CombatModule 删除独立 update driver，改为 Timer update handle 推进默认 world。
   - 所属模块：Module Update Adapters
   - 依赖：timer-update-consumer-contract
   - 状态：planned
   - 对应 feature：未启动
   - 备注：保留 Combat 按需注册，不引入网络同步或多 world 调度。

6. **runtime-module-profile-handles** — 为 Timer、Procedure、Combat 等运行时模块补充自绘 ProfileHandle，统一暴露 tick、handle、driver、world/procedure 等运行时状态。
   - 所属模块：Module Profile Handles
   - 依赖：timer-update-consumer-contract, debug-profilehandle-hardening, procedure-timer-consumer, combat-timer-consumer
   - 状态：planned
   - 对应 feature：未启动
   - 备注：后续模块 profile 必须接入 Name-only + 派生类自绘契约；Debug 默认只保留 Memory 曲线和 Device Info，不再要求 Debug 状态 profile。

7. **network-debug-log-transport-contract** — 由 Network 模块定义 Debug log bridge 和 payload 字段，读取 DebugProfileHandle 中已脱敏日志做实时发送。
   - 所属模块：Network Debug Log Bridge
   - 依赖：debug-profilehandle-hardening
   - 状态：planned
   - 对应 feature：未启动
   - 备注：不实现完整 NetworkModule；Debug 不再保留旧日志 transport 接口，具体网络发送、重试、批量和断线缓存归 Network 模块实现。

**最小闭环**：第 1 条 `timer-update-consumer-contract` 已完成后，Runtime 已具备统一 update handle 端到端路径：注册 `UpdateTimerHandle` / `LateUpdateTimerHandle` / `FixedUpdateTimerHandle` → 对应 Timer phase trigger 检查 → callback 收到全局 clock 口径的 `TimerUpdateContext` → snapshot 可被 Debug/测试读取。

## 6. 排期思路

先完成 Timer update handle 合约，因为它是 Debug/Procedure/Combat 收敛 driver 的共同前置。随后先把 DebugModule 收敛为 profile-centric 门面：日志由 `DebugProfileHandle` 接收，内存/metrics 由 `MemoryProfileHandle` 暴露，再把 Debug refresh 接到 Timer。Procedure 和 Combat 的迁移可以在 Timer 合约稳定后独立推进；最后再统一补 module profile handles 和 Network-backed log bridge。Network 相关工作放后面，是因为当前仓库没有独立 NetworkModule，不能让 Debug 重构依赖一个尚未落地的模块。

## 7. 观察项

- 当前没有独立 NetworkModule；如果后续要完整做网络层，应另起 requirement/roadmap 或 feature design，覆盖连接、鉴权、重试、断线缓存和限流。
- `.codestable/architecture/ARCHITECTURE.md` 只记录已落地现状；Debug/Procedure/Combat 接入 Timer update handle 后再由对应 feature acceptance 回写。
- `logger` requirement 已标 outdated，但 Debug 源码命名空间仍是 `GameDeveloperKit.Logger`；本 roadmap 不强制迁移 namespace，除非后续 feature 评估公开 API 兼容。
- 当前 `DebugProfileHandle` / `MemoryProfileHandle` 暂与 `ProfileHandle` 同编译单元，便于未刷新 Unity csproj 时保持命令行验证；Unity 刷新工程文件后可拆成同名文件。

## 8. 变更日志

- 2026-06-09：按用户反馈修正 Timer clock 语义：TimerModule 只有一份全局 clock，LateUpdate/FixedUpdate 只作为 phase trigger，并支持 handle 级 fps 门控。
- 2026-06-09：按用户反馈移除 Debug 旧 sink/analytics/transport 扩展点，`DebugProfileHandle` 只负责日志接收、脱敏、过滤和内存 buffer；Network 实时日志改为未来 Network 模块自己的 bridge 契约。
- 2026-06-08：按用户反馈把 DebugModule 改为 profile-centric 门面，新增 `DebugProfileHandle` 接收日志，新增 `MemoryProfileHandle` 承载 metrics snapshot；GUI 绘制迁到 `DebugGuiDriver`。
- 2026-06-08：完成 `debug-profilehandle-hardening`，Debug profile registry 已安全读取 metadata/snapshot，redaction 已对 exception/context `ToString()` 加兜底，并注册内建 Runtime/Debug 状态 profile。
- 2026-06-08：按用户反馈把 Timer 契约从 `ITimerUpdateConsumer` / phase 模型改为显式 `UpdateTimerHandle`、`LateUpdateTimerHandle`、`FixedUpdateTimerHandle`；`TimerTickKind` 保留为内部调度实现细节。`timer-update-consumer-contract` 已验收完成。
- 2026-06-08：初版 roadmap 建立 Runtime scheduling 与 diagnostics 收敛路线。
