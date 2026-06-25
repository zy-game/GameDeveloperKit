# GameDeveloperKit

Unity 模块化游戏开发框架。按需加载、依赖自动解析、同步生命周期。

## 快速开始

```csharp
// 场景挂载 FrameworkStartup 组件，或手动调用：
await App.Initialize();

// 访问模块（首次访问自动创建并递归启动依赖）
App.UI.OpenAsync<MyWindow>().Forget();
App.Resource.LoadAssetAsync<Texture2D>("icon").Forget();
App.Event.Fire(new PlayerDiedArgs(), this);

// 关闭
await App.Shutdown();
```

## 入口

`App` 是静态入口，提供 18 个模块属性：

| 属性 | 模块 | 说明 |
|------|------|------|
| `App.Timer` | TimerModule | 全局时钟、延迟/倒计时/间隔/Update 回调 |
| `App.Event` | EventModule | 强类型事件订阅/派发（Fire 队列 + FireNow 即时） |
| `App.Procedure` | ProcedureModule | 顶层流程状态机（Startup → Lobby → Battle） |
| `App.Resource` | ResourceModule | 多模式资源加载（Builtin/Editor/StreamingAsset/Bundle/WebGL） |
| `App.UI` | UIModule | 窗口生命周期、层级管理、窗口栈、安全区 |
| `App.Config` | ConfigModule | 配置表加载（JSON/资源/HTTP，支持 Luban） |
| `App.Data` | DataModule | 版本化数据持久化（JSON 序列化，多版本回滚） |
| `App.Sound` | SoundModule | 音频播放（BGM/SFX，支持 Mixer） |
| `App.Network` | NetworkModule | Socket 长连接 + HTTP 请求封装 |
| `App.Download` | DownloadModule | 文件下载（断点续传/分片/批量） |
| `App.File` | FileModule | 虚拟文件系统（VFS，小文件合并 Bundle） |
| `App.Input` | InputModule | 输入管理（Action Map + 绑定） |
| `App.Localization` | LocalizationModule | 多语言支持 |
| `App.Debug` | DebugModule | 运行时控制台（日志/Profiles/Timers/Tools） |
| `App.Command` | CommandModule | GM 命令注册/执行 |
| `App.Operation` | OperationModule | 异步操作句柄（Execute/Wait/Complete/Cancel） |
| `App.Story` | StoryModule | 剧情引擎（节点图编译 → StoryProgram 运行时播放） |
| `App.Combat` | CombatModule | 固定帧率 ECS 战斗世界（确定性回滚、系统匹配） |

## 模块生命周期

```csharp
public class MyModule : GameModuleBase
{
    public override void Startup()
    {
        // 同步轻量初始化：创建字典、GameObject、注册回调
    }

    public override void Shutdown()
    {
        // 同步释放：销毁 GameObject、清空集合
    }
}
```

- `Startup()` 只做同步轻量外壳。需要异步准备（资源、网络、文件）的能力通过显式 async API 承接。
- `Shutdown()` 释放 Startup 中创建的资源。模块也实现 `IReference.Release()` → 默认调用 `Shutdown()`。

## 依赖解析

```csharp
[ModuleDependency(typeof(TimerModule))]      // Event 依赖 Timer（用于队列派发）
[ModuleDependency(typeof(OperationModule))]  // Download 依赖 Operation
public sealed class EventModule : GameModuleBase { }
```

- 首次访问 `App.Event` 时，resolver 递归启动 `TimerModule` → `EventModule`
- 循环依赖检测，启动失败自动回滚已创建模块
- `App.Shutdown()` 按反序关闭

## 核心模块

### Timer

```csharp
App.Timer.Delay(2f, () => Debug.Log("2秒后执行"));
App.Timer.Interval(0.5f, dt => UpdateLogic(dt));
App.Timer.Countdown(10f, onTick: p => ShowProgress(p), onComplete: () => OnTimeout());
App.Timer.OnUpdate(ctx => { /* 每帧 Update */ });
App.Timer.OnFixedUpdate(ctx => { /* FixedUpdate */ });
```

### Event

```csharp
// 订阅
var sub = App.Event.Subscribe<PlayerDiedArgs>(args => HandleDeath(args));
// 延迟派发（入队，Timer Update 驱动）
App.Event.Fire(new PlayerDiedArgs { PlayerId = 1 }, sender: this);
// 即时派发（同步，16 层递归保护）
App.Event.FireNow(new PlayerDiedArgs { PlayerId = 1 }, sender: this);
// 取消
sub.Cancel();
```

### Procedure

```csharp
public class LobbyProcedure : ProcedureBase
{
    public override async UniTask OnEnterAsync(ProcedureBase previous, object userData)
    {
        await App.UI.OpenAsync<LobbyWindow>();
    }

    public override async UniTask OnLeaveAsync(ProcedureBase next, object userData)
    {
        App.UI.Close<LobbyWindow>();
    }

    public override void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
}

// 注册并切换
App.Procedure.RegisterProcedure(new LobbyProcedure());
await App.Procedure.ChangeAsync<LobbyProcedure>();
```

### Resource

```csharp
// 显式初始化（传入 ResourceSettings）
await App.Resource.InitializeAsync(settings);

// 加载资源
var rawAsset = await App.Resource.LoadRawAssetAsync("config/game_data");
var texture = await App.Resource.LoadAssetAsync<Texture2D>("sprites/icon");
var scene = await App.Resource.LoadSceneAsync("scenes/battle");

// 卸载
rawAsset.Dispose();
```

支持模式：`Builtin` / `EditorSimulator` / `StreamingAsset` / `Bundle` / `WebGL`。

### UI

```csharp
[UIOption("UI_MyWindow", UILayer.Window)]
public partial class MyWindow : UIWindow
{
    protected override async UniTask OnOpenAsync(object userData)
    {
        // Bindings 由 UIDocumentGenerator 生成
        Bindings.label_title.text = "Hello";
    }
}

// 打开/关闭
await App.UI.OpenAsync<MyWindow>("user data");
App.UI.Close<MyWindow>();
```

层级：`Background` → `Main` → `Window` → `Loading` → `Message` → `StoryPlayback`。

### Config

```csharp
[TableOption("configs/item_table")]
public class ItemRow : IConfig
{
    public int Id;
    public string Name;
    public int Price;
}

var table = await App.Config.LoadTableAsync<ItemRow>();
var item = table.Find(r => r.Id == 1001);
```

### Data

```csharp
var data = await App.Data.LoadDataAsync<PlayerSaveData>("slot1");
data.Gold += 100;
await App.Data.SaveDataAsync<PlayerSaveData>("slot1");

// 版本管理
var versions = await App.Data.GetVersionsAsync<PlayerSaveData>("slot1");
var oldData = await App.Data.LoadVersionAsync<PlayerSaveData>("slot1", versions[0].Version);
```

### Combat

```csharp
var world = new World(frameRate: 50); // 默认 50 FPS 固定帧

var entity = world.Create();
world.AddComponent<HealthComponent>(entity);
world.LoadSystem<DamageSystem>();

world.Update(Time.deltaTime);  // 累积真实时间 → 推进 0-N 个固定步
world.SaveFrame();             // 保存快照
world.Rollback(3);             // 回滚 3 帧
```

### Story

剧情编辑器和运行时引擎。编辑器中通过节点图（`剧情编辑器` 菜单）创建章节 → 编译为 `StoryProgram` → 运行时播放：

```csharp
var program = StoryProgramAsset.Load("sample_story_graph");
var view = StoryPlayerView.CreateDefault(App.UI.GetLayerRoot(UILayer.StoryPlayback));
view.Play(program, "chapter_01");
```

## 场景启动

```csharp
// 挂载 FrameworkStartup 组件到场景 GameObject
// Inspector 中设置目标 Procedure、Resource/Sound 配置
// 运行时自动调用 App.Initialize() → 准备模块 → ChangeAsync(targetProcedure)
```

不需要 `Startup.cs` 或启动 JSON 配置。

## 架构

```
App（静态入口，100行）
  ├── ModuleLifecycle（状态机：Stopped/Started/ShuttingDown）
  └── ModuleRegistry（依赖解析、循环检测、回滚、可赋值类型缓存）
        └── IGameModule / GameModuleBase（同步生命周期契约）
              ├── TimerModule        ── 全局时钟
              ├── EventModule        ── 依赖 Timer
              ├── ProcedureModule    ── 依赖 Timer
              ├── ResourceModule     ── 依赖 Operation/Download/File
              ├── UIModule
              ├── ConfigModule       ── 依赖 Resource/Download
              ├── DataModule         ── 依赖 File
              ├── CombatModule       ── 依赖 Timer
              ├── StoryModule
              ├── NetworkModule
              ├── SoundModule
              ├── DownloadModule     ── 依赖 Operation
              ├── FileModule
              ├── DebugModule        ── 依赖 Timer
              ├── InputModule
              ├── LocalizationModule
              ├── CommandModule
              └── OperationModule
```

## 测试

```bash
# Runtime 模块编译验证
dotnet build GameDeveloperKit.Runtime.csproj --no-restore
```

Unity Test Runner 测试位于 `Assets/GameDeveloperKit/Tests/`。同一项目已有 Editor 实例打开时，batchmode 不可用；在 Editor 内跑 Test Runner。
