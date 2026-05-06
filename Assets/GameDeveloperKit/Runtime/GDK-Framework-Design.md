# GameDeveloperKit (GDK) 框架设计文档

> **状态**: 草稿 · 待审核  
> **版本**: v0.2  
> **命名空间**: `GameDeveloperKit`

---

## 一、概述

**GameDeveloperKit (GDK)** 是一个基于 Unity 的轻量级游戏开发框架，借鉴 Unreal Engine 的核心设计理念：

- **EActor** — 所有对象的根基类，类似 UE 的 `AActor`，整个框架对象体系从这里派生
- **Super** — 框架的超类/总入口，类似 UE 的全局子系统集合，所有框架模块通过 `Super.XXX` 访问
- **ReferencePool** — 通用对象池，所有 `IReference` 对象统一管理

### 1.1 设计目标

| 目标 | 说明 |
|------|------|
| **Actor 体系** | 以 `EActor` 为根，构建统一的游戏对象层级树 |
| **模块化入口** | 通过 `Super` 静态类统一暴露所有子系统（`Super.Event`、`Super.Resource`、`Super.Command` 等） |
| **池化优先** | 框架内所有对象通过 `ReferencePool` 管理，减少 GC 压力 |
| **最小依赖** | Unity + UniTask + Newtonsoft.Json |

### 1.2 依赖关系

```
GameDeveloperKit.Runtime
├── Unity (Engine)
├── UniTask (异步)
└── Newtonsoft.Json (序列化)
```

---

## 二、核心架构

### 2.1 架构全景

```
                        ┌─────────────────────────┐
                        │         Super            │
                        │    (框架超类·总入口)       │
                        │                         │
                        │  Super.Event             │
                        │  Super.Resource          │
                        │  Super.Command           │
                        │  Super.Audio             │
                        │  Super.Scene             │
                        │  ...                     │
                        └──────────┬──────────────┘
                                   │ (暴露)
                                   ▼
        ┌──────────────────────────────────────────────────┐
        │                  模块子系统层                       │
        │                                                  │
        │  EventManager  ResourceManager  CommandManager    │
        │       │              │              │            │
        │       └──────────────┼──────────────┘            │
        │                      │                           │
        │               IGameModule                        │
        │         (统一生命周期: Startup / Shutdown)         │
        └──────────────────────────────┬───────────────────┘
                                       │
        ┌──────────────────────────────┼───────────────────┐
        │                       对象体系层                    │
        │                                                  │
        │                   EActor                          │
        │              (所有对象的根基类)                     │
        │              (完全由业务层派生)                     │
        │                                                  │
        │              IReference                          │
        │         (引用协议 + 对象池支持)                     │
        └──────────────────────────────┬───────────────────┘
                                       │
        ┌──────────────────────────────┼───────────────────┐
        │                       基础设施层                    │
        │                                                  │
        │     ReferencePool      GameFrameworkException     │
        │      (通用对象池)          (框架统一异常)            │
        └──────────────────────────────────────────────────┘
```

### 2.2 两条主线

| 主线 | 核心角色 | 职责 |
|------|---------|------|
| **Super 主线** | `Super` 静态类 | 框架总入口，聚合所有子系统模块，提供全局访问点 |
| **EActor 主线** | `EActor` 抽象类 | 对象体系根基，所有游戏对象从其派生 |

```
Super (静态入口)              EActor (对象根基)
    │                             │
    ├─ Super.Event                └─ (完全由业务层按需派生)
    ├─ Super.Resource
    ├─ Super.Command
    ├─ Super.Audio
    └─ ...
```

---

## 三、核心模块详细设计

### 3.1 Super — 框架超类·总入口

**文件**: `Super.cs`  
**职责**: 框架的根节点，所有子系统模块的访问入口。类似 UE 中各处全局子系统 (`GEngine` / `GWorld` 等) 的统一集合。

```
static class Super
{
    // 每个子系统模块以静态属性的方式暴露
    // 模块在框架初始化时注册到 Super，Shutdown 时注销
}
```

**设计模式**: Facade + Service Locator

**典型使用场景**:

```csharp
// 发送事件
Super.Event.Fire("PlayerDied", new PlayerDeathArgs());

// 加载资源
var asset = await Super.Resource.LoadAsync<GameObject>("Prefabs/Player");

// 执行命令
Super.Command.Execute(new SpawnEnemyCommand());

// 播放音效
Super.Audio.PlaySFX("explosion_01");
```

**模块注册机制**（待实现）:

```csharp
// Super 内部维护模块注册表
internal static class Super
{
    private static Dictionary<Type, IGameModule> s_Modules;

    internal static void Register<T>(T module) where T : IGameModule;
    internal static void Unregister<T>();

    // 公开的模块访问属性
    public static EventManager Event => Get<EventManager>();
    public static ResourceManager Resource => Get<ResourceManager>();
    public static CommandManager Command => Get<CommandManager>();
}
```

**计划中的子系统模块**:

| 模块 | 访问路径 | 职责 |
|------|----------|------|
| EventManager | `Super.Event` | 全局事件总线，解耦模块间通信 |
| ResourceManager | `Super.Resource` | 资源加载/卸载/引用计数 |
| CommandManager | `Super.Command` | 命令队列/撤销重做/输入映射 |
| AudioManager | `Super.Audio` | 音效/BGM 管理 |
| SceneManager | `Super.Scene` | 场景加载/切换/叠加 |
| UIManager | `Super.UI` | UI 层级/弹窗栈管理 |
| ConfigManager | `Super.Config` | 配置表加载与缓存 |

---

### 3.2 EActor — 对象体系根基类

**文件**: `Core/EActor.cs`  
**职责**: 所有游戏对象的抽象基类，类似 UE 的 `AActor`。定义对象最基本属性（id/name/HideFlags）和生命周期。

```
abstract class EActor : IReference
{
    int id              { get; set; }
    string name         { get; set; }
    HideFlags hideFlags { get; set; }

    void IDisposable.Dispose()  → Release()
    virtual void Release()      → 重置 id=0, name="", hideFlags=None
}
```

**HideFlags 枚举**: 控制对象在 Unity 中的显示和持久化行为

**继承层级设计**:

`EActor` 不预设派生层级，完全由业务层按需派生。

```csharp
// 业务层自由派生
public class PlayerActor : EActor { ... }
public class EnemyActor  : EActor { ... }
public class BulletActor : EActor { ... }
// 等...
```

**派生示例**:

```csharp
public class PlayerActor : EActor
{
    public float moveSpeed { get; set; }
    public float health    { get; set; }

    public override void Release()
    {
        moveSpeed = 0f;
        health = 0f;
        base.Release();
    }
}
```

---

### 3.3 IReference — 引用协议

**文件**: `Core/IReference.cs`  
**职责**: 所有可池化对象的基础协议

```
interface IReference : IDisposable
{
    void Release();            // 重置状态，归还池
    void IDisposable.Dispose()  // 默认实现 → Release()
}
```

**与 EActor 的关系**: `EActor` 实现 `IReference`，因此所有 Actor 都可以被 `ReferencePool` 管理。

---

### 3.4 ReferencePool — 通用对象池

**文件**: `Core/ReferencePool.cs`  
**职责**: 按类型管理 `IReference` 实例的创建/获取/回收/销毁

#### 内部结构

```
ReferencePool (static)
│
├── Dictionary<Type, ReferenceCollection>
│
└── ReferenceCollection (private sealed)
    ├── Queue<IReference>      // 空闲对象队列
    ├── UsingReferenceCount    // 使用中数量
    ├── AcquireReferenceCount  // 累计获取
    ├── ReleaseReferenceCount  // 累计释放
    ├── AddReferenceCount      // 累计预分配
    └── RemoveReferenceCount   // 累计移除
```

#### API

| 方法 | 说明 |
|------|------|
| `Acquire<T>()` | 从池获取（泛型 `new()`, 零反射） |
| `Acquire(Type)` | 从池获取（反射创建） |
| `Release(IReference)` | 归还实例 |
| `Add<T>(count)` | 预分配 N 个 |
| `Remove<T>(count)` | 移除 N 个空闲实例 |
| `RemoveAll<T>()` / `ClearAll()` | 清空池 |
| `EnableStrictCheck` | 严格模式开关 |

#### 待修复

| 问题 | 说明 |
|------|------|
| `Release()` 在 lock 内引用 `ReferencePool.EnableStrictCheck` | 应改为局部缓存或 `s_EnableStrictCheck` |

---

### 3.5 IGameModule — 模块生命周期协议

**文件**: `Core/IGameModule.cs`  
**职责**: 所有框架子系统的生命周期协议

```
interface IGameModule : IReference
{
    UniTask Startup();   // 异步初始化
    UniTask Shutdown();  // 异步销毁
}
```

**模块生命周期的完整流程**:

```
框架启动
  │
  ├─ Super 创建各模块实例
  │    ├─ new EventManager()
  │    ├─ new ResourceManager()
  │    ├─ new CommandManager()
  │    └─ ...
  │
  ├─ 按依赖顺序调用 Startup()
  │    ├─ ResourceManager.Startup()    ← 最先（无依赖）
  │    ├─ ConfigManager.Startup()     ← 依赖 Resource
  │    ├─ AudioManager.Startup()      ← 依赖 Resource
  │    ├─ EventManager.Startup()      ← 无依赖
  │    ├─ CommandManager.Startup()    ← 依赖 Event
  │    └─ ...
  │
  └─ 框架就绪，开始游戏逻辑

框架关闭
  │
  ├─ 按逆依赖顺序调用 Shutdown()
  │    └─ ... → Shutdown()
  │
  └─ Super 清空所有模块引用
```

---

### 3.6 GameFrameworkException — 框架统一异常

**文件**: `Core/GameFrameworkException.cs`  
**职责**: 框架内所有异常的统一类型

```
sealed class GameFrameworkException : Exception
```

- `sealed` 防止外部滥用继承
- 业务层统一 `catch (GameFrameworkException)` 处理框架异常

---

### 3.7 ResourceManager — 资源管理系统

**文件**: `Resource/ResourceManager.cs`、`Resource/AssetOperationHandle.cs`、`Resource/IAssetProvider.cs`、`Resource/ResourcesAssetProvider.cs`、`Resource/ResourceManifest.cs`、`Resource/AssetEntry.cs`  
**职责**: 参照 YooAsset / Addressables 设计模式，提供完整的资源管理系统

#### 架构分层

```
ResourceManager (对外 API：Super.Resource)
    │
    ├── ResourceManifest (资源配置清单 — 对标 YooAsset PackageManifest)
    │   ├── AssetEntry[] Entries           // 所有资源条目
    │   ├── TryGetEntry(loc, out entry)    // location → AssetEntry
    │   ├── GetEntriesByLabel(label)       // label → AssetEntry[]
    │   ├── SerializeToJson() / FromJson() // JSON 序列化
    │   └── AssetEntry
    │       ├── Location          // 逻辑地址
    │       ├── Path              // 物理路径
    │       ├── AssetTypeName     // 类型
    │       ├── Labels[]          // 标签
    │       └── Dependencies[]    // 依赖项
    │
    ├── IAssetProvider (Provider 抽象层)
    │   └── ResourcesAssetProvider (默认实现，可替换为 AB/Addressable)
    │
    └── AssetOperationHandle (操作句柄)
        ├── 状态机 (None → Loading → Succeed/Failed)
        ├── 进度追踪 + 自动依赖加载
        ├── 引用计数
        ├── awaitable / Completed 回调
        └── Instantiate / Release
```

#### ResourceManifest — 资源配置清单

```
class ResourceManifest
{
    string Version                    // Manifest 版本号
    List<AssetEntry> Entries          // 所有条目

    AddEntry(location, path[, labels])  → AssetEntry
    TryGetEntry(location, out entry)    → bool
    GetEntriesByLabel(label)            → AssetEntry[]
    GetEntriesByLabels(labels[])        → AssetEntry[]
    SerializeToJson()                   → string
    static DeserializeFromJson(json)    → ResourceManifest
}
```

**三层索引**: location → entry (O(1)), label → entries (O(1)), `m_Dirty` 延迟重建索引

#### 典型使用 — 三种 Manifest 构建方式

```csharp
// 方式 1: 代码构建 Manifest
var manifest = new ResourceManifest();
manifest.AddEntry("Player", "Prefabs/Player", new[] { "Characters" });
manifest.AddEntry("Enemy",  "Prefabs/Enemy",  new[] { "Characters" })
       .SetDependencies("SharedMaterials");
manifest.AddEntry("SharedMaterials", "Materials/Shared");
Super.Resource.SetManifest(manifest);

// 方式 2: 从 JSON 加载 Manifest（策划/工具生成）
var json = Resources.Load<TextAsset>("Configs/resource_manifest");
var manifest = ResourceManifest.DeserializeFromJson(json.text);
Super.Resource.SetManifest(manifest);

// 方式 3: 运行时动态追加
Super.Resource.Manifest.AddEntry("ExtraUI", "UI/ExtraPanel");
```

#### JSON Manifest 文件示例

```json
{
  "version": "1.0.0",
  "entries": [
    {
      "location": "Player",
      "path": "Prefabs/Player",
      "assetType": "UnityEngine.GameObject",
      "labels": ["Characters", "Player"],
      "dependencies": ["SharedMaterials", "PlayerAnim"]
    },
    {
      "location": "SharedMaterials",
      "path": "Materials/Shared",
      "assetType": "UnityEngine.Material",
      "labels": ["Materials"]
    }
  ]
}
```

#### ResourceManager API

| 方法 | 说明 |
|------|------|
| `SetManifest(manifest)` | 设置资源配置清单 |
| `SetAssetProvider(provider)` | 替换底层 Provider |
| `LoadAssetAsync<T>(location)` | 异步加载资源（自动解析 manifest 依赖） |
| `LoadSubAssetsAsync<T>(location)` | 加载子资源集合 |
| `LoadSceneAsync(location, mode)` | 异步加载场景 |
| `LoadAssetsByLabelAsync<T>(label)` | 按标签批量加载 |
| `InstantiateAsync(location, parent)` | 直接异步实例化 GameObject |
| `Release(handle)` | 释放句柄 |
| `Manifest` | 获取当前 Manifest，用于运行时追加条目 |

---

## 四、目录结构规划

```
Runtime/
├── GameDeveloperKit.Runtime.asmdef
│
├── Super.cs                              # ★ 框架超类·总入口
│
├── Core/                                 # 核心抽象层
│   ├── EActor.cs                         # ★ 对象体系根基类
│   ├── IReference.cs                     # 引用协议
│   ├── ReferencePool.cs                  # 通用对象池
│   ├── IGameModule.cs                    # 模块生命周期协议
│   └── GameFrameworkException.cs         # 框架异常
│
├── Event/                                # 事件子系统 (NEW)
│   ├── EventManager.cs                   # 事件管理器
│   ├── EventArgs.cs                      # 事件参数基类
│   └── IEventHandler.cs                  # 事件处理器接口
│
├── Resource/                             # 资源子系统
│   ├── IAssetProvider.cs                 # Provider 抽象层
│   ├── ResourcesAssetProvider.cs         # 默认 Resources Provider
│   ├── ResourceManifest.cs               # 资源配置清单（对标 YooAsset）
│   ├── AssetEntry.cs                     # Manifest 条目
│   ├── AssetOperationHandle.cs           # 操作句柄（状态机+引用计数）
│   └── ResourceManager.cs                # 资源管理器
│
├── Command/                              # 命令子系统 (NEW)
│   ├── CommandManager.cs                 # 命令管理器
│   └── ICommand.cs                       # 命令接口
│
├── Audio/                                # 音频子系统 (NEW)
│   └── AudioManager.cs                   # 音频管理
│
├── Scene/                                # 场景子系统 (NEW)
│   └── SceneManager.cs                   # 场景管理
│
├── UI/                                   # UI 子系统 (NEW)
│   └── UIManager.cs                      # UI 管理
│
├── Config/                               # 配置子系统 (NEW)
│   └── ConfigManager.cs                  # 配置管理
│
└── ObjectPool/                           # GameObject 池 (NEW)
    └── GameObjectPool.cs                 # Unity 对象池
```

---

## 五、实现路线图

### Phase 1 — 基础设施加固 ✅ 已完成

| # | 任务 | 优先级 | 状态 |
|---|------|--------|------|
| 1 | 修复 `ReferencePool.Release` strict check bug | **P0** | ✅ |
| 2 | 改造 `Super` — 从空壳变为模块注册中心 | **P0** | ✅ |
| 3 | 增加 `ReferencePool.GetPoolInfo<T>()` 统计接口 | **P1** | ✅ |

### Phase 2 — Event 事件系统 ✅ 已完成

| # | 任务 | 优先级 | 状态 |
|---|------|--------|------|
| 4 | 实现 `EventManager`（`IGameModule`） | **P0** | ✅ |
| 5 | 在 `Super` 中暴露 `Super.Event` | **P0** | ✅ |

### Phase 3 — Resource 资源系统 ✅ 已完成

| # | 任务 | 优先级 | 状态 |
|---|------|--------|------|
| 6 | 实现 `ResourceManager`（`IGameModule`） | **P0** | ✅ |
| 7 | 在 `Super` 中暴露 `Super.Resource` | **P0** | ✅ |

### Phase 4 — Command 命令系统（当前 →）

| # | 任务 | 优先级 |
|---|------|--------|
| 8 | 实现 `CommandManager`（`IGameModule`） | **P1** |
| 9 | 在 `Super` 中暴露 `Super.Command` | **P1** |

### Phase 5 — 其他子系统（按需）

| # | 任务 | 优先级 |
|---|------|--------|
| 10 | Audio、Scene、UI、Config 等子系统 | **P2** |
| 11 | `GameObjectPool` — Unity GameObject 对象池 | **P2** |

---

## 六、设计原则

| 原则 | 说明 |
|------|------|
| **Super 即入口** | 一切框架能力都通过 `Super.XXX` 访问，不暴露分散的全局变量 |
| **EActor 即根** | 所有游戏对象继承自 `EActor`，统一对象生命周期 |
| **模块即服务** | 每个子系统实现 `IGameModule`，通过 Super 注册/访问 |
| **池化优先** | 任何可复用对象走 `ReferencePool`，避免热路径 `new` |
| **零反射热路径** | `Acquire<T>()` 走泛型 `new()` 约束而非 `Activator` |
| **线程安全** | 对象池操作 `lock` 保护 |
| **异常统一** | 框架异常统一用 `GameFrameworkException` |

---

## 七、待确认问题

> 以下问题的默认方案已按最合理假设填入，如无异议可直接确认开始实现。

1. **EventManager 的事件路由模式**:
   - [x] **字符串 Key**（`Fire("PlayerDied", args)`）— 灵活、易调试 *(默认推荐)*
   - [ ] 泛型 Key / int Id — 类型安全、性能更好

2. **Command 系统的范围**:
   - [x] **轻量命令模式** — 封装可撤销操作 + 队列执行 *(默认推荐)*
   - [ ] 完整输入系统 — 按键绑定/动作映射/输入缓冲

3. **EActor 的第二层派生**:
   - [ ] LogicActor / ViewActor / UIActor 三大分支
   - [x] **不预设，完全由业务层派生** ✔ 已确认

4. **ResourceManager 的资源释放策略**:
   - [x] **引用计数自动释放** — 引用归零自动卸载 *(默认推荐)*
   - [ ] 手动释放 + 场景切换时清空
   - [ ] 其他：__________

5. **Phase 1 中 `Super` 的重构粒度**:
   - [x] **一次性搭建模块注册框架** — 定义 `Register`/`Get` 范式，写出 Event 和 Resource 的属性骨架 *(默认推荐)*
   - [ ] 仅写注册框架，模块属性等实现时再加

---

> **请确认后我将进入 Phase 1 实现阶段。**
