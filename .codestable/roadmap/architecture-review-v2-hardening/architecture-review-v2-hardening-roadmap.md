---
doc_type: roadmap
slug: architecture-review-v2-hardening
status: active
created: 2026-06-25
last_reviewed: 2026-06-25
tags: [runtime, hardening, core, combat, data, config, story, ui, download, code-quality]
related_architecture: [ARCHITECTURE]
---

# Architecture Review v2 Hardening

## 1. 背景

2026-06-25 的架构深度审查（v2）修正了 v1 的 4 个误报后，剩余 12 个真实问题：3 个 P1、5 个 P2、4 个 P3。分布在 Core、Combat、Data、Config、Story/UI、Download 六个模块，不影响正确运行，但补齐后框架成熟度从 4.1 → 4.5。

审计来源：`.codestable/audits/2026-06-25-framework-full/`

## 2. 范围与明确不做

### 覆盖

- P1: World.Step() 系统级异常隔离、StoryPlayerView 与 UIModule 集成、超大文件拆分、XML 注释清理
- P2: DataModule 反射序列化消除、ConfigModule 加载超时、ModuleLifecycle 竞态窗口、TryGetRegistered 二级索引、DebugModule 构造时序、EntityManager.Rebuild 快照 diff
- P3: DownloadModule 显式依赖、UI 层级枚举可扩展

### 明确不做

- 不新建模块或重写架构（只加固现有）
- 不扩大 ReferencePool 使用范围（探索建议先修安全性，另起评估）
- 不改 requirements / architecture 文档（各子 feature acceptance 时会回写）
- 不引入第三方 DI 容器、程序集扫描或 App 动态属性注册
- 不引入 IModuleFactory / 模块工厂化

## 3. 模块拆分（概设）

```
architecture-review-v2-hardening
├── Core Hardening         P2: 竞态窗口、二级索引、构造时序
├── Combat Hardening       P1/P2: 系统异常隔离、Rebuild快照diff
├── Data & Config Hardening P1/P2: 反射消除、加载超时
├── Module Boundary Hardening P1/P3: StoryPlayerView集成、Download依赖、UI枚举
└── Code Quality           P1: 超大文件拆分、XML噪音清理
```

### Core Hardening

- **职责**: 加固 `ModuleLifecycle`、`ModuleRegistry`、`DebugModule` 三个核心类型的健壮性
- **子 feature**: `module-lifecycle-race-window`、`module-registry-secondary-index`、`debug-module-construction-order`

### Combat Hardening

- **职责**: 加固 `World.Step()` 和 `EntityManager.Rebuild()` 两个热路径的健壮性和性能
- **子 feature**: `world-step-exception-isolation`、`entity-manager-rebuild-snapshot`

### Data & Config Hardening

- **职责**: 消除 `DataModule` 反射序列化调用、给 `ConfigModule` 加载加超时保护
- **子 feature**: `datamodule-reflection-elimination`、`config-loading-timeout`

### Module Boundary Hardening

- **职责**: 收口跨模块隐式耦合——StoryPlayerView 接入 UI 层级、DownloadModule 声明依赖、UI 层级枚举可扩展
- **子 feature**: `story-player-view-ui-integration`、`download-module-explicit-dependency`、`ui-layer-enum-extensible`

### Code Quality

- **职责**: 拆分 >300 行超大文件、清理无效 XML 注释噪音
- **子 feature**: `large-file-splitting`、`xml-comment-noise-cleanup`

## 4. 接口契约（架构层详设）

大多数子 feature 是模块内部改动，无跨模块接口。需要定义契约的有：

### 4.1 StoryPlayerView → UIModule 集成契约

目标：`StoryPlayerView` 不再直接继承 `MonoBehaviour`，改为通过 `UIModule` 或通过 `UILayer.StoryPlayback` 挂载，但保持独立于窗口栈。

**方案**: 新增 `IStoryPlaybackHost` 接口，`StoryPlayerView` 实现 `IStoryFramePresenter` 并挂在 `UILayer.StoryPlayback` 下。

```csharp
// 新接口，位于 GameDeveloperKit.StoryPlayback
public interface IStoryPlaybackHost
{
    Transform GetPlaybackRoot();
    void OnPlaybackStarted();
    void OnPlaybackStopped();
}
```

**不破坏**: `StoryTestProcedure`、`UIModule.GetLayerRoot()`、现有运行时行为。

### 4.2 DownloadModule 依赖声明

```csharp
[ModuleDependency(typeof(OperationModule))]
public sealed class DownloadModule : GameModuleBase
```

纯标记改动，不改变运行时行为。`GetOrCreateHandler` 中 `App.Operation.ExecuteWithKey` 由 resolver 保证 `OperationModule` 已启动。

### 4.3 UILayer 枚举可扩展

```csharp
public enum UILayer
{
    Background = 0,
    // ... 现有层 ...
    StoryPlayback = 100,
    CustomBase = 1000,
}

public Transform GetOrCreateLayerRoot(int layerId, string layerName);
```

P3 优先级，保留接口设计，可在业务需求来临时再实现。

## 5. 子 Feature 拆解

### 最小闭环

`world-step-exception-isolation` — 改动局限在 `SystemManager` / `World.Step()`，不依赖其他子 feature，改完能在 Unity Editor PlayMode 手测。

### 完整清单

| # | slug | 描述 | 依赖 | 优先级 |
|---|---|---|---|---|
| 1 | `world-step-exception-isolation` | World.Step() 中每个系统 OnUpdate 独立 try-catch + 异常收集，一个系统异常不阻断其他系统 | 无 | P1 |
| 2 | `module-lifecycle-race-window` | ModuleLifecycle.Initialize() 去掉无意义 async，加注释说明 UniTask 单线程保证 | 无 | P2 |
| 3 | `module-registry-secondary-index` | TryGetRegistered 加 Type→IGameModule 二级缓存，避免线性扫描 | 无 | P2 |
| 4 | `debug-module-construction-order` | DebugModule 构造函数中 Settings 在构造时为空，理顺为 Startup() 中统一配置 | 无 | P2 |
| 5 | `entity-manager-rebuild-snapshot` | EntityManager.Rebuild() 记录上次快照实体集合，回滚时只对比差异 | 无 | P2 |
| 6 | `datamodule-reflection-elimination` | DataModule 中反射序列化调用替换为编译期已知类型的分发 | 无 | P1 |
| 7 | `config-loading-timeout` | ConfigModule 加载操作加 CancellationToken/超时参数 | 无 | P2 |
| 8 | `download-module-explicit-dependency` | DownloadModule 加 [ModuleDependency(typeof(OperationModule))] | 无 | P3 |
| 9 | `story-player-view-ui-integration` | StoryPlayerView 接入 UILayer 管理，通过 IStoryPlaybackHost 解耦 | 无 | P1 |
| 10 | `ui-layer-enum-extensible` | UILayer 枚举预留自定义层范围 + GetOrCreateLayerRoot API | 无 | P3 |
| 11 | `large-file-splitting` | 拆分 >300 行文件。策略：优先用内部嵌套类（如 `TimerModule.Timer` 用 `private class Timer : MonoBehaviour` 放在独立 `.cs` 中通过 partial 接入），只在跨职责边界且嵌套不自然时用独立类 | 9 | P1 |
| 12 | `xml-comment-noise-cleanup` | 清理无效 XML 注释（如"获取引用对象，返回一个指定..."这类无信息量注释），保留有价值的 API 文档注释 | 无 | P1 |

### 依赖图

```
1-8: 全部独立，可并行
9 ──→ 11（StoryPlayerView 集成后再拆它）
10, 12: 独立
```

## 6. 排期建议

1. 先做独立项（1-8、10、12）：改动小、可独立验证、不互相阻塞
2. Story 线（9 → 11）：StoryPlayerView 集成有架构风险，先小改后大拆

## 7. 观察项

- **ReferencePool 使用率低**: 探索文档指出 ReferencePool 在业务代码中基本未使用，但 v2 报告未列为问题。建议后续独立评估。
- **超大文件拆分可能触发编译错误**: StoryPlayerView(870行) 拆分需配合增量验证策略。
- **XML 注释清理范围**: 实际操作前需精确统计，建议先跑脚本统计再做。
