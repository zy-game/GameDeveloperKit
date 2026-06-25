---
doc_type: feature-acceptance
feature: 2026-06-22-startup-component-entry
status: passed
accepted_at: 2026-06-22
design: .codestable/features/2026-06-22-startup-component-entry/startup-component-entry-design.md
roadmap: story-playback-architecture
roadmap_item: startup-component-entry
tags: [runtime, startup, procedure, resource, sound, unity-component]
---

# Startup Component Entry 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-22
> 关联方案 doc：`.codestable/features/2026-06-22-startup-component-entry/startup-component-entry-design.md`

## 1. 接口契约核对

对照方案第 2.1 节：

- [x] `FrameworkStartup` 已作为 Runtime 可挂载组件落地：`Assets/GameDeveloperKit/Runtime/FrameworkStartup.cs:11` 继承 `MonoBehaviour`，不继承 `ProcedureBase`。
- [x] 组件序列化字段落地：`m_TargetProcedureTypeName`、`m_TargetUserData`、`m_Modules`、`m_ShutdownAppOnDestroy` 位于 `FrameworkStartup.cs:14-23`。
- [x] 可观察状态落地：`IsRunning`、`HasStarted`、`LastError`、`TargetProcedureType` 位于 `FrameworkStartup.cs:30-45`。
- [x] 可 await 启动入口落地：`StartupAsync()` 位于 `FrameworkStartup.cs:51`。
- [x] `FrameworkStartupModuleOptions` 已落地：`Assets/GameDeveloperKit/Runtime/FrameworkStartupModuleOptions.cs:12`，持有内嵌 `ResourceSettings` 和 `SoundMixerSettings`。
- [x] 模块配置项只读公开属性落地：`InitializeResource`、`ResourceSettings`、`ResolveConfigModule`、`ResolveDataModule`、`ResolveSoundModule`、`SoundMixerSettings` 位于 `FrameworkStartupModuleOptions.cs:35-60`。
- [x] `FrameworkStartupInspector` 已落地：`Assets/GameDeveloperKit/Editor/Startup/FrameworkStartupInspector.cs:13-14` 使用 `[CustomEditor(typeof(FrameworkStartup))]`。
- [x] 旧 `ResourceInitializeOptions` wrapper 已移除；`ResourceModule.InitializeAsync(ResourceSettings)` 位于 `ResourceModule.cs:79`。

流程图核对：

- [x] `App.Startup -> ResolveTargetProcedureType -> PrepareModulesAsync -> App.Procedure.ChangeAsync` 在 `FrameworkStartup.cs:69-72` 顺序落地。
- [x] Resource ready 节点在 `FrameworkStartup.cs:112` 调用 `App.Resource.InitializeAsync(options.ResourceSettings)`。
- [x] Config/Data shell 节点通过 `App.Config` / `App.Data` 访问落地于 `FrameworkStartup.cs:115-123`。
- [x] Sound 节点在 `FrameworkStartup.cs:127` 调用 `App.Sound.ConfigureMixer(options.SoundMixerSettings)`。

## 2. 行为与决策核对

需求摘要逐项验证：

- [x] 场景组件能解析目标 Procedure 并切换：`FrameworkStartupTests.StartupAsync_WhenTargetProcedureIsValid_ChangesCurrentProcedureAndPassesUserData` 覆盖。
- [x] 资源初始化在目标 Procedure enter 前完成：`FrameworkStartupTests.StartupAsync_WhenResourceInitializationEnabled_ReadiesResourceBeforeTargetEnter` 覆盖，目标 Procedure 记录 `ResourceInitializedOnEnter`。
- [x] Config/Data 只创建同步外壳：`StartupAsync_WhenConfigAndDataResolveEnabled_RegistersConfigAndDataShells` 覆盖，同时确认 Resource 未被隐式 initialized。
- [x] Sound 显式配置入口落地：`StartupAsync_WhenSoundResolveEnabled_RegistersSoundShell` 覆盖 SoundModule 注册；`SoundModule.ConfigureMixer()` 位于 `SoundModule.cs:305`。
- [x] Inspector 下拉只枚举可创建 Procedure：`FrameworkStartupInspector.cs:93` 使用 `TypeCache.GetTypesDerivedFrom<ProcedureBase>()`，`CanCreateProcedure()` 在 `FrameworkStartupInspector.cs:111-116` 过滤 abstract、open generic 和非 Procedure 类型，并在 `FrameworkStartupInspector.cs:60-62` 写入 `AssemblyQualifiedName`。
- [x] 错误目标类型和空目标类型可观察：`StartupAsync_WhenTargetProcedureTypeIsInvalid_ThrowsAndRecordsLastError`、`StartupAsync_WhenTargetProcedureTypeNameIsEmpty_ThrowsAndRecordsLastError` 覆盖。

明确不做逐项核对：

- [x] 不新增 Startup Procedure：grep `StartupProcedureBase|DefaultStartupProcedure|ProcedureStartup` 在 `Assets/GameDeveloperKit/Runtime` 无命中。
- [x] 不修改 `App.Startup()` 语义：本 feature 未修改 `App.cs`，architecture 仍记录 `App.Startup()` 只切 lifecycle。
- [x] 不新增 Config/Data `InitializeAsync()`：grep `ConfigModule.InitializeAsync|DataModule.InitializeAsync` 无命中。
- [x] 不引入 StoryPlayback/AVPro/Loading/章节预加载逻辑到 `FrameworkStartup`：`FrameworkStartup.cs` 只引用 `Cysharp.Threading.Tasks`、`GameDeveloperKit.Procedure` 和 `UnityEngine`。
- [x] 不恢复 Resources asset 自举：grep `Resources.Load<ResourceSettings>|Resources.Load<SoundMixerSettings>` 在 Runtime 目标文件无命中。

关键决策落地：

- [x] 命名为 `FrameworkStartup`，不叫 `Startup`，且不恢复旧 `Startup.cs`。
- [x] 进入业务流程用 `App.Procedure.ChangeAsync(targetProcedureType, m_TargetUserData)`，不调用 `RequestChange()`。
- [x] Resource/Sound 设置内嵌到 `FrameworkStartupModuleOptions`，`ResourceSettings` / `SoundMixerSettings` 是 `[Serializable]` 普通对象。
- [x] Config/Data 首版只做同步外壳解析。

挂载点核对：

- [x] Runtime 组件挂载点：`FrameworkStartup.cs`。
- [x] serialized configuration 挂载点：`FrameworkStartup.cs:14-23` 和 `FrameworkStartupModuleOptions.cs:15-30`。
- [x] Editor authoring 挂载点：`Editor/Startup/FrameworkStartupInspector.cs`。
- [x] 模块配置对象挂载点：`FrameworkStartupModuleOptions.cs`。
- [x] 拔除沙盘推演：移除上述四项后，StoryPlayback、Runtime/Story、ProcedureModule、ResourceModule 和 SoundModule 仍可由业务自行调用；本 feature 不把启动职责塞回播放层或 Procedure 内部。

## 3. 验收场景核对

- [x] **N1** 有效目标 Procedure 调用 `StartupAsync()` 后成为 `App.Procedure.Current`。
  - 证据：`StartupAsync_WhenTargetProcedureIsValid_ChangesCurrentProcedureAndPassesUserData`。
- [x] **N2** `m_TargetUserData` 为 Unity Object 时目标 Procedure 收到同一对象引用。
  - 证据：同一测试断言 `Assert.AreSame(userData, RecordingProcedure.LastUserData)`。
- [x] **N3** 启用 Resource 初始化时目标 Procedure enter 前 `App.Resource.IsInitialized == true`。
  - 证据：`StartupAsync_WhenResourceInitializationEnabled_ReadiesResourceBeforeTargetEnter`。
- [x] **N4** 不启用 Resource 初始化时组件不隐式初始化 Resource。
  - 证据：`StartupAsync_WhenResourceInitializationDisabled_DoesNotInitializeResource`。
- [x] **N5** 启用 Config/Data 同步外壳解析后对应模块按配置注册。
  - 证据：`StartupAsync_WhenConfigAndDataResolveEnabled_RegistersConfigAndDataShells`。
- [x] **N6** 空或非法目标 Procedure type name 使 `StartupAsync()` 抛异常并记录 `LastError`。
  - 证据：两个无效目标测试。
- [x] **N7** 启动中重复调用 `StartupAsync()` 不重复进入目标 Procedure。
  - 证据：`StartupAsync_WhenCalledWhileRunning_ReusesInFlightStartup`。
- [x] **N8** `m_ShutdownAppOnDestroy == true` 时组件销毁触发 `App.Shutdown()`。
  - 证据：`OnDestroy_WhenShutdownEnabled_ShutsDownApp`。
- [x] **N9** Inspector 目标 Procedure 下拉只列可创建 `ProcedureBase` 派生类型。
  - 证据：`FrameworkStartupInspector.cs:93-116` 的 `TypeCache` 枚举和过滤逻辑，Editor 编译通过。未做 Unity Inspector 肉眼手测。
- [x] **N10** 启用 Sound 同步外壳解析后 `SoundModule` 按配置注册。
  - 证据：`StartupAsync_WhenSoundResolveEnabled_RegistersSoundShell`；`SoundModule.ConfigureMixer()` 编译落地。

反向核对项：

- [x] `FrameworkStartup` 不调用 `App.Procedure.RequestChange()`。
- [x] `FrameworkStartup` 不在 `GameDeveloperKit.StoryPlayback` 程序集内。
- [x] `ResourceSettings` / `SoundMixerSettings` 不继承 `ScriptableObject`，不通过 Resources 默认 asset 自举。
- [x] 本次未手写新增 Unity `.meta` 文件；Unity 可后续生成缺失 meta。

## 4. 术语一致性

- `FrameworkStartup`：代码、design、architecture、roadmap 用词一致。
- `目标 Procedure`：runtime 字段保存 AssemblyQualifiedName；Inspector 下拉也写入 AssemblyQualifiedName。
- `模块 ready`：Resource 使用显式 `InitializeAsync(ResourceSettings)`；Config/Data 只解析同步外壳；Sound 显式 `ConfigureMixer()`。
- 禁用词 grep：`StartupProcedureBase`、`DefaultStartupProcedure`、`ProcedureStartup` 无运行时代码命中。
- 旧 `ResourceInitializeOptions`：运行时代码和当前 feature/roadmap 契约无命中；历史 6 月 17 feature 文档作为当时快照保留。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已新增 `Framework Startup（场景启动组件）` 小节，记录组件形态、序列化配置、启动顺序、错误语义和 Inspector 边界。
- [x] Module Lifecycle 小节已区分“无自动默认 `Startup.cs`”和“可显式挂载 `FrameworkStartup`”。
- [x] Resource 小节已更新为 `InitializeAsync(ResourceSettings)`，并记录 `ResourceSettings` 是 `[Serializable]` 普通配置对象，不再通过 Resources asset 自举。
- [x] 已知约束已补 `FrameworkStartup` 边界：不是 Procedure，不注册剧情、不播放剧情、不打开 LoadingWindow、不做章节预加载。
- [x] 变更日志已追加 2026-06-22 Startup 与 Resource/Sound 配置来源变化。

## 6. requirement 回写

- [x] `.codestable/requirements/framework-startup.md` 已从 `status: draft` 升级为 `status: current`。
- [x] pitch、用户故事、为什么需要、怎么解决和边界已更新为“显式挂载 + 按需模块 ready + 目标 Procedure 切换”的当前能力。
- [x] `implemented_by` 已指向 `ARCHITECTURE`。
- [x] `.codestable/requirements/VISION.md` 已把 `framework-startup` 从 Draft 移入 Current。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-items.yaml` 中 `startup-component-entry` 已从 `in-progress` 改为 `done`，feature 保持 `2026-06-22-startup-component-entry`。
- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-roadmap.md` 第 5 节对应条目已同步为 `状态：done` / `对应 feature：2026-06-22-startup-component-entry`。
- [x] roadmap 第 4.6 节 Startup 契约已同步为当前 `FrameworkStartup` / `FrameworkStartupModuleOptions`：Resource 直接接收 `ResourceSettings`，Config/Data 只解析同步外壳，Sound 使用内嵌 `SoundMixerSettings`。
- [x] roadmap items yaml 校验通过。

## 8. attention.md 候选盘点

- 候选：本次没有新的长期命令陷阱。已有 attention 中“Runtime 快速编译命令”和“Unity Test Runner batchmode 与已打开 Editor 冲突”仍适用。

## 9. 遗留

- Unity Inspector 下拉目前以代码和 Editor 编译验证为证据，未进行真实 Inspector 肉眼手测。
- `story-test-scripts-entry` 现在依赖已满足，是 roadmap 中建议下一步推进的功能。
- `story-playback-video-prewarm` 仍是独立 planned 功能，本次没有实现 AVPro 预热队列。
