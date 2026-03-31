# GameDeveloperKit Framework

一个功能完整、模块化的 Unity 游戏开发框架，提供游戏开发中常用的核心功能模块。

## 目录

- [框架概述](#框架概述)
- [模块说明](#模块说明)
- [使用案例](#使用案例)
- [最小使用案例](#最小使用案例)
- [架构设计](#架构设计)

## 框架概述

GameDeveloperKit 是一个轻量级但功能强大的 Unity 游戏开发框架，采用模块化设计，开发者可以根据需求选择启用或禁用特定模块。框架提供了游戏开发中常见的核心功能，包括资源管理、流程控制、UI管理、事件系统等。

### 主要特性

- **模块化设计**: 各模块相互独立，可按需启用
- **统一入口**: 通过 `Game` 类统一访问所有模块
- **异步支持**: 基于 UniTask 的异步编程模型
- **生命周期管理**: 完善的模块初始化和关闭机制
- **诊断支持**: 内置诊断系统，便于调试和监控
- **事件驱动**: 强大的事件系统，支持同步/异步事件

## 模块说明

### 核心模块

#### Game 类

框架的核心入口类，负责模块的注册、管理和访问。

**主要功能:**
- 模块注册和获取
- 模块生命周期管理
- 统一模块访问接口

**使用示例:**
```csharp
// 获取模块实例
var procedureModule = Game.Procedure;
var resourceModule = Game.Resource;

// 检查模块是否已注册
if (Game.HasModule<ProcedureModule>())
{
    // 使用模块
}
```

#### Startup 类

框架启动器，负责初始化所有模块并处理启动流程。

**主要功能:**
- 自动初始化核心模块和功能模块
- 资源初始化
- 初始流程和场景加载
- 启动状态监控和错误处理

**配置文件位置:** `StreamingAssets/GameDeveloperKit/startup-config.json`

**配置示例:**
```json
{
  "PersistAcrossScenes": true,
  "Modules": {
    "PrepareResourcesOnStartup": true,
    "InitializeUI": true,
    "InitializeSceneModule": true,
    "InitializeProcedureModule": true
  },
  "InitialFlow": {
    "AutoEnterInitialScene": true,
    "AutoEnterInitialProcedure": true,
    "InitialScene": "MainScene",
    "InitialProcedure": "Lobby"
  },
  "Overlay": {
    "ShowOverlay": true,
    "HideOverlayOnComplete": true
  }
}
```

### 流程模块 (Procedure Module)

提供游戏流程状态管理功能，支持状态机模式。

**主要功能:**
- 流程状态注册和管理
- 状态切换和转换保护
- 支持父子状态嵌套
- 状态历史记录

**使用示例:**
```csharp
// 注册流程状态
Game.Procedure.RegisterState<StartupProcedure>();
Game.Procedure.RegisterState<LobbyProcedure>();
Game.Procedure.RegisterState<BattleProcedure>();

// 注册子状态
Game.Procedure.RegisterSubState("Battle", "BattleLoading");

// 切换流程状态
await Game.Procedure.ChangeStateAsync<LobbyProcedure>();
await Game.Procedure.ChangeStateAsync("Battle", userData);

// 获取当前状态
var currentState = Game.Procedure.CurrentState;
```

**自定义流程状态:**
```csharp
public class MyProcedure : ProcedureStateBase
{
    public override string Name => "MyProcedure";
    
    public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
    {
        // 进入状态时的逻辑
        Debug.Log("Enter MyProcedure");
    }
    
    public override async UniTask OnExitAsync(CancellationToken cancellationToken)
    {
        // 退出状态时的逻辑
        Debug.Log("Exit MyProcedure");
    }
    
    public override void OnUpdate(float deltaTime)
    {
        // 每帧更新逻辑
    }
}
```

### 资源模块 (Resource Module)

提供统一的资源加载、管理和更新功能。

**主要功能:**
- 支持多种资源模式 (EditorSimulate、Offline、Host、Web)
- 资源包管理
- 资源加载 (同步/异步)
- 资源更新和热更支持

**使用示例:**
```csharp
// 加载资源 (同步)
var asset = Game.Resource.LoadAsset<Texture2D>("TextureName");
var handle = Game.Resource.LoadAsset<GameObject>("PrefabName");

// 加载资源 (异步)
var handle = await Game.Resource.LoadAssetAsync<Texture2D>("TextureName");
var texture = handle.Asset;

// 加载场景
await Game.Resource.LoadSceneAsync("GameScene", LoadSceneMode.Single);

// 按标签加载资源
var assets = Game.Resource.LoadAssetsByLabel<Sprite>("UI");

// 释放资源
handle.Dispose();
```

**资源包配置:**
```csharp
// 初始化资源包
var options = new ResourcePackageOptions
{
    RootPath = "Resources"
};

Game.Resource.InitializePackage("MainPackage", options);

// 准备资源包
await Game.Resource.PreparePackageAsync("MainPackage");

// 更新资源包
var result = await Game.Resource.UpdatePackageAsync("MainPackage");
```

### UI 模块 (UI Module)

提供统一的 UI 窗口管理功能。

**主要功能:**
- UI 窗口打开/关闭管理
- UI 层级控制
- UI 窗口栈管理
- 内置加载、对话框、提示功能

**使用示例:**
```csharp
// 打开 UI 窗口
var window = await Game.UI.OpenAsync<MyWindow>(param1, param2);

// 关闭 UI 窗口
Game.UI.Close<MyWindow>();
Game.UI.Close(window);

// 检查窗口是否打开
if (Game.UI.IsOpen<MyWindow>())
{
    var window = Game.UI.Get<MyWindow>();
}

// UI 窗口栈管理
Game.UI.Back(); // 返回上一个窗口
Game.UI.BackTo<MyWindow>(); // 返回到指定窗口

// 显示/隐藏层级
Game.UI.Show(UILayer.Popup, UILayer.System);
Game.UI.Hide(UILayer.Overlay);

// 内置 UI 功能
Game.UI.ShowLoading("Loading resources...");
Game.UI.HideLoading();

Game.UI.ShowDialog("Title", "Message", "OK", () => { /* confirm */ });
Game.UI.ShowTips("Operation successful", 2f);
```

**自定义 UI 窗口:**
```csharp
public class MyWindow : UIWindow
{
    public override string AssetName => "MyWindow";
    public override UILayer Layer => UILayer.Popup;
    public override UIMode Mode => UIMode.HideOthers;
    public override bool CacheOnClose => true;
    
    protected override UniTask OnCreateAsync(CancellationToken cancellationToken)
    {
        // 窗口创建逻辑
        return UniTask.CompletedTask;
    }
    
    protected override UniTask OnShowAsync(object[] args, CancellationToken cancellationToken)
    {
        // 窗口显示逻辑
        return UniTask.CompletedTask;
    }
    
    protected override UniTask OnHideAsync(CancellationToken cancellationToken)
    {
        // 窗口隐藏逻辑
        return UniTask.CompletedTask;
    }
}
```

### 场景模块 (Scene Module)

提供场景加载、切换和历史管理功能。

**主要功能:**
- 场景加载 (同步/异步)
- 场景切换
- 持久化场景管理
- 场景历史记录
- 场景流程集成

**使用示例:**
```csharp
// 加载场景
var handle = Game.Scene.LoadAsync("GameScene", LoadSceneMode.Single, true);
await handle;

// 切换场景
await Game.Scene.SwitchAsync("LobbyScene");

// 持久化场景
Game.Scene.RegisterPersistentScene("MainScene");
Game.Scene.UnregisterPersistentScene("MainScene");

// 场景历史
if (Game.Scene.CanGoBack)
{
    await Game.Scene.GoBackAsync();
}

// 获取历史记录
var history = Game.Scene.GetHistory();

// 检查场景是否已加载
if (Game.Scene.IsLoaded("GameScene"))
{
    var scene = Game.Scene.GetLoadedScene("GameScene");
}
```

### 事件模块 (Event Module)

提供强大的事件系统，支持同步和异步事件。

**主要功能:**
- 事件注册和注销
- 支持多种事件键类型 (字符串、整数、枚举)
- 同步/异步事件处理
- 事件上下文管理
- 性能诊断支持

**使用示例:**
```csharp
// 注册事件处理程序
Game.Event.Register("PlayerDied", new PlayerDiedEventHandler());
Game.Event.RegisterAsync("PlayerLevelUp", new PlayerLevelUpAsyncHandler());

// 使用类型安全的方式注册
Game.Event.Register<GameEvents, PlayerDiedHandler>();

// 触发事件
Game.Event.Raise("PlayerDied", this, playerData);
Game.Event.RaiseAsync("PlayerLevelUp", this, level);

// 使用枚举触发事件
Game.Event.Raise<GameEvents>(GameEvents.PlayerDied, this, playerData);

// 注销事件
Game.Event.Unregister("PlayerDied", handler);
Game.Event.Unregister<GameEvents, PlayerDiedHandler>();
```

**自定义事件处理程序:**
```csharp
public class PlayerDiedHandler : IEventHandle
{
    public void Handle(IEventContext context)
    {
        var playerData = context.GetArg<PlayerData>(0);
        Debug.Log($"Player died: {playerData.Name}");
    }
}

public class PlayerLevelUpAsyncHandler : IAsyncEventHandle
{
    public async UniTask HandleAsync(IEventContext context)
    {
        var level = context.GetArg<int>(0);
        await SaveProgressAsync(level);
        Debug.Log($"Player leveled up to {level}");
    }
}

// 事件绑定提供程序
public class GameEventBindings : IEventBindingProvider
{
    public void Register(EventModule eventModule)
    {
        eventModule.Register<GameEvents, PlayerDiedHandler>();
        eventModule.RegisterAsync<GameEvents, PlayerLevelUpAsyncHandler>();
    }
}
```

### 网络模块 (Network Module)

提供网络请求和网络服务的统一管理。

**主要功能:**
- HTTP 请求封装
- 网络服务注册和管理
- 请求重试和错误处理
- 请求统计和诊断

**使用示例:**
```csharp
// 配置网络模块
Game.Network.Configure("https://api.example.com", 30);
Game.Network.SetDefaultHeader("Authorization", "Bearer token");

// 发送 GET 请求
var response = await Game.Network.GetAsync("/api/player");

// 发送 POST 请求
var response = await Game.Network.PostJsonAsync("/api/player", jsonData);

// 自定义网络服务
public interface IPlayerService
{
    UniTask<PlayerData> GetPlayerAsync(string playerId);
}

public class PlayerService : NetworkServiceBase, IPlayerService
{
    public async UniTask<PlayerData> GetPlayerAsync(string playerId)
    {
        var response = await Http.GetAsync($"/api/players/{playerId}");
        return JsonUtility.FromJson<PlayerData>(response.Data);
    }
}

// 注册自定义服务
Game.Network.RegisterService<IPlayerService, PlayerService>();

// 使用自定义服务
var playerService = Game.Network.GetService<IPlayerService>();
var player = await playerService.GetPlayerAsync("123");
```

### 其他功能模块

#### 音频模块 (Audio Module)
提供音频播放和管理功能。

```csharp
// 播放背景音乐
Game.Audio.PlayBGM("MainTheme", loop: true);

// 播放音效
Game.Audio.PlaySFX("ClickSound");

// 停止音频
Game.Audio.StopBGM();
```

#### 输入模块 (Input Module)
提供输入管理功能。

```csharp
// 获取输入状态
var input = Game.Input.GetInput("Player");

// 检查按键
if (input.IsPressed("Jump"))
{
    // 跳跃逻辑
}
```

#### 本地化模块 (Localization Module)
提供多语言支持。

```csharp
// 设置语言
Game.Localization.SetLanguage("en-US");

// 获取本地化文本
var text = Game.Localization.GetText("WelcomeMessage");
```

#### 数据模块 (Data Module)
提供数据持久化功能。

```csharp
// 保存数据
var saveData = new PlayerSaveData { Level = 10 };
Game.Data.Save(saveData, "PlayerSave");

// 加载数据
var saveData = Game.Data.Load<PlayerSaveData>("PlayerSave");
```

#### 对象池模块 (Pool Module)
提供对象池功能。

```csharp
// 预热对象池
Game.Pool.Warmup<GameObject>(prefab, 10);

// 从对象池获取对象
var instance = Game.Pool.Spawn(prefab);

// 将对象返回对象池
Game.Pool.Despawn(instance);
```

#### 下载模块 (Download Module)
提供文件下载功能。

```csharp
// 下载文件
var result = await Game.Download.DownloadAsync("https://example.com/file.zip", 
    "C:/Downloads/file.zip");
```

#### 调度器模块 (Scheduler Module)
提供任务调度功能。

```csharp
// 延迟执行
Game.Schedule.Delay(1f, () => Debug.Log("Delayed action"));

// 重复执行
Game.Schedule.Interval(0.5f, () => Debug.Log("Repeated action"));
```

#### 命令模块 (Command Module)
提供命令模式支持。

```csharp
// 执行命令
Game.Command.Execute(new MoveCommand());

// 撤销命令
Game.Command.Undo();

// 重做命令
Game.Command.Redo();
```

#### 诊断模块 (Diagnostics Module)
提供诊断和日志功能。

```csharp
// 记录日志
Game.Diagnostics.LogInfo("Information message");
Game.Diagnostics.LogWarning("Warning message");
Game.Diagnostics.LogError("Error message");

// 捕获快照
Game.Diagnostics.CaptureSnapshot("PlayerLevel", level.ToString());
```

#### 平台模块 (Platform Module)
提供平台相关功能。

```csharp
// 检查平台
var isMobile = Game.Platform.IsMobile;

// 获取平台信息
var platformInfo = Game.Platform.GetPlatformInfo();
```

## 使用案例

### 案例 1: 游戏流程管理

实现一个完整游戏流程，包含启动、大厅、战斗等状态。

```csharp
public class GameProcedureFlow
{
    private void InitializeProcedures()
    {
        // 注册流程状态
        Game.Procedure.RegisterState<StartupProcedure>();
        Game.Procedure.RegisterState<LobbyProcedure>();
        Game.Procedure.RegisterState<BattleProcedure>();
        Game.Procedure.RegisterState<ResultProcedure>();
    }
    
    public async UniTask StartGameAsync()
    {
        InitializeProcedures();
        
        // 从启动流程开始
        await Game.Procedure.ChangeStateAsync<StartupProcedure>();
    }
}

// 启动流程
public class StartupProcedure : ProcedureStateBase
{
    public override string Name => "Startup";
    
    public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
    {
        // 显示加载界面
        Game.UI.ShowLoading("Initializing...");
        
        try
        {
            // 初始化资源
            await Game.Resource.InitializeAllPackagesAsync(cancellationToken);
            
            // 加载大厅场景
            await Game.Scene.SwitchAsync("LobbyScene");
            
            // 进入大厅流程
            await Game.Procedure.ChangeStateAsync<LobbyProcedure>();
        }
        finally
        {
            Game.UI.HideLoading();
        }
    }
}

// 大厅流程
public class LobbyProcedure : ProcedureStateBase
{
    public override string Name => "Lobby";
    
    public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
    {
        // 打开大厅 UI
        await Game.UI.OpenAsync<LobbyWindow>();
        
        // 注册事件监听
        Game.Event.Register("StartBattle", new StartBattleHandler());
    }
    
    public override async UniTask OnExitAsync(CancellationToken cancellationToken)
    {
        // 关闭大厅 UI
        Game.UI.Close<LobbyWindow>();
        
        // 注销事件监听
        Game.Event.Unregister<StartBattleHandler>();
    }
}

// 开始战斗事件处理
public class StartBattleHandler : IEventHandle
{
    public void Handle(IEventContext context)
    {
        var battleData = context.GetArg<BattleData>(0);
        Game.Procedure.ChangeStateAsync<BattleProcedure>(battleData).Forget();
    }
}
```

### 案例 2: 资源加载和管理

实现资源包管理和热更新功能。

```csharp
public class ResourceManager
{
    public async UniTask InitializeAsync()
    {
        // 配置资源包
        var settings = ScriptableObject.CreateInstance<ResourceSettings>();
        settings.PlayMode = ResourcePlayMode.Host;
        
        var mainPackage = new ResourcePackageDefinition
        {
            PackageName = "MainPackage",
            Role = ResourcePackageRole.Builtin,
            RemoteBaseUrl = "https://cdn.example.com/resources",
            StreamingAssetsRoot = "Resources",
            PersistentRoot = "PersistentResources"
        };
        
        settings.Packages.Add(mainPackage);
        
        // 初始化资源模块
        Game.Resource.Initialize(settings);
        
        // 检查更新
        var updateResult = await Game.Resource.UpdatePackageAsync("MainPackage");
        
        if (updateResult.HasUpdates)
        {
            Game.UI.ShowDialog("Update Available", 
                $"New version available: {updateResult.NewVersion}", 
                "Update", 
                async () => await ApplyUpdateAsync(updateResult),
                "Cancel");
        }
    }
    
    private async UniTask ApplyUpdateAsync(ResourceUpdateResult result)
    {
        Game.UI.ShowLoading("Updating...");
        
        try
        {
            await Game.Resource.PreparePackageAsync("MainPackage");
            Game.UI.ShowTips("Update completed!");
        }
        catch (Exception ex)
        {
            Game.UI.ShowDialog("Update Failed", ex.Message, "OK");
        }
        finally
        {
            Game.UI.HideLoading();
        }
    }
}
```

### 案例 3: UI 窗口管理

实现完整的 UI 窗口管理系统。

```csharp
// 主菜单窗口
public class MainMenuWindow : UIWindow
{
    public override string AssetName => "MainMenuWindow";
    public override UILayer Layer => UILayer.Main;
    public override bool CacheOnClose => true;
    
    private Button startButton;
    private Button settingsButton;
    private Button exitButton;
    
    protected override async UniTask OnCreateAsync(CancellationToken cancellationToken)
    {
        // 初始化 UI 组件
        startButton = GetComponent<Button>("StartButton");
        settingsButton = GetComponent<Button>("SettingsButton");
        exitButton = GetComponent<Button>("ExitButton");
        
        // 绑定事件
        startButton.onClick.AddListener(OnStartClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        exitButton.onClick.AddListener(OnExitClicked);
    }
    
    private void OnStartClicked()
    {
        Game.Event.Raise("StartGame");
    }
    
    private void OnSettingsClicked()
    {
        Game.UI.OpenAsync<SettingsWindow>().Forget();
    }
    
    private void OnExitClicked()
    {
        Game.UI.ShowDialog("Exit Game", "Are you sure you want to exit?",
            "Yes", () => Application.Quit(),
            "No");
    }
}

// 设置窗口
public class SettingsWindow : UIWindow
{
    public override string AssetName => "SettingsWindow";
    public override UILayer Layer => UILayer.Popup;
    public override UIMode Mode => UIMode.Exclusive;
    
    private Slider musicSlider;
    private Slider sfxSlider;
    private Dropdown languageDropdown;
    
    protected override async UniTask OnShowAsync(object[] args, CancellationToken cancellationToken)
    {
        // 加载设置
        var settings = Game.Data.Load<SettingsData>("Settings");
        musicSlider.value = settings.MusicVolume;
        sfxSlider.value = settings.SFXVolume;
        languageDropdown.value = GetLanguageIndex(settings.Language);
    }
    
    private void SaveSettings()
    {
        var settings = new SettingsData
        {
            MusicVolume = musicSlider.value,
            SFXVolume = sfxSlider.value,
            Language = GetSelectedLanguage()
        };
        
        Game.Data.Save(settings, "Settings");
        Game.Localization.SetLanguage(settings.Language);
    }
}
```

### 案例 4: 网络请求和数据同步

实现与服务器通信的功能。

```csharp
// 玩家服务接口
public interface IPlayerService
{
    UniTask<PlayerData> GetPlayerAsync(string playerId);
    UniTask<PlayerData> UpdatePlayerAsync(string playerId, PlayerUpdateData data);
    UniTask<ItemData[]> GetInventoryAsync(string playerId);
}

// 玩家服务实现
public class PlayerService : NetworkServiceBase, IPlayerService
{
    public async UniTask<PlayerData> GetPlayerAsync(string playerId)
    {
        var response = await Http.GetAsync($"/api/players/{playerId}");
        return JsonUtility.FromJson<PlayerData>(response.Data);
    }
    
    public async UniTask<PlayerData> UpdatePlayerAsync(string playerId, PlayerUpdateData data)
    {
        var jsonData = JsonUtility.ToJson(data);
        var response = await Http.PostJsonAsync($"/api/players/{playerId}", jsonData);
        return JsonUtility.FromJson<PlayerData>(response.Data);
    }
    
    public async UniTask<ItemData[]> GetInventoryAsync(string playerId)
    {
        var response = await Http.GetAsync($"/api/players/{playerId}/inventory");
        var wrapper = JsonUtility.FromJson<InventoryWrapper>(response.Data);
        return wrapper.Items;
    }
}

// 网络管理器
public class NetworkManager
{
    public void Initialize()
    {
        // 配置网络模块
        Game.Network.Configure("https://api.gameserver.com", 30);
        
        // 设置认证令牌
        Game.Network.SetDefaultHeader("Authorization", $"Bearer {GetAuthToken()}");
        
        // 注册服务
        Game.Network.RegisterService<IPlayerService, PlayerService>();
    }
    
    public async UniTask<PlayerData> LoadPlayerDataAsync(string playerId)
    {
        try
        {
            Game.UI.ShowLoading("Loading player data...");
            
            var playerService = Game.Network.GetService<IPlayerService>();
            var playerData = await playerService.GetPlayerAsync(playerId);
            
            return playerData;
        }
        catch (Exception ex)
        {
            Game.UI.ShowDialog("Network Error", 
                "Failed to load player data. Please check your connection.", 
                "Retry", 
                () => LoadPlayerDataAsync(playerId).Forget(),
                "Cancel");
            
            return null;
        }
        finally
        {
            Game.UI.HideLoading();
        }
    }
}
```

## 最小使用案例

下面是一个完整的游戏启动最小示例，展示了如何使用 GameDeveloperKit 框架创建一个简单的游戏。

### 步骤 1: 创建启动场景

在 Unity 中创建一个空场景，命名为 "StartupScene"，并添加 Startup 组件。

### 步骤 2: 创建启动配置文件

在 `StreamingAssets/GameDeveloperKit/` 目录下创建 `startup-config.json` 文件：

```json
{
  "PersistAcrossScenes": true,
  "Modules": {
    "PrepareResourcesOnStartup": true,
    "InitializeUI": true,
    "InitializeSceneModule": true,
    "InitializeProcedureModule": true
  },
  "InitialFlow": {
    "AutoEnterInitialScene": false,
    "AutoEnterInitialProcedure": true,
    "InitialProcedure": "Startup"
  },
  "Overlay": {
    "ShowOverlay": true,
    "HideOverlayOnComplete": true,
    "OverlayTitle": "Game Startup"
  },
  "Resource": {
    "PlayMode": 0,
    "Packages": []
  }
}
```

### 步骤 3: 创建流程状态

```csharp
// StartupProcedure.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Game
{
    public class StartupProcedure : ProcedureStateBase
    {
        public override string Name => "Startup";
        
        public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
        {
            Debug.Log("Game Startup Started");
            
            // 显示加载界面
            Game.UI.ShowLoading("Initializing game...");
            
            try
            {
                // 模拟初始化过程
                await UniTask.Delay(1000, cancellationToken);
                
                // 初始化游戏数据
                if (!Game.Data.HasData<PlayerSaveData>("PlayerSave"))
                {
                    var initialData = new PlayerSaveData
                    {
                        PlayerName = "Player",
                        Level = 1,
                        Experience = 0,
                        Gold = 100
                    };
                    Game.Data.Save(initialData, "PlayerSave");
                }
                
                // 切换到主菜单流程
                await Game.Procedure.ChangeStateAsync<MainMenuProcedure>();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Startup failed: {ex.Message}");
                Game.UI.ShowDialog("Startup Error", ex.Message, "OK");
            }
            finally
            {
                Game.UI.HideLoading();
            }
        }
        
        public override async UniTask OnExitAsync(CancellationToken cancellationToken)
        {
            Debug.Log("Game Startup Completed");
        }
    }
}
```

### 步骤 4: 创建主菜单流程

```csharp
// MainMenuProcedure.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Game
{
    public class MainMenuProcedure : ProcedureStateBase
    {
        public override string Name => "MainMenu";
        
        private MainMenuWindow mainMenuWindow;
        
        public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
        {
            Debug.Log("Entered Main Menu");
            
            // 注册事件监听
            Game.Event.Register("StartGame", new StartGameHandler());
            Game.Event.Register("OpenSettings", new OpenSettingsHandler());
            Game.Event.Register("ExitGame", new ExitGameHandler());
            
            // 打开主菜单 UI
            mainMenuWindow = await Game.UI.OpenAsync<MainMenuWindow>();
        }
        
        public override async UniTask OnExitAsync(CancellationToken cancellationToken)
        {
            Debug.Log("Exited Main Menu");
            
            // 关闭主菜单 UI
            if (mainMenuWindow != null)
            {
                Game.UI.Close<MainMenuWindow>();
            }
            
            // 注销事件监听
            Game.Event.Unregister<StartGameHandler>();
            Game.Event.Unregister<OpenSettingsHandler>();
            Game.Event.Unregister<ExitGameHandler>();
        }
        
        public override void OnUpdate(float deltaTime)
        {
            // 每帧更新逻辑（如需要）
        }
    }
    
    // 开始游戏事件处理
    public class StartGameHandler : IEventHandle
    {
        public void Handle(IEventContext context)
        {
            Game.Procedure.ChangeStateAsync<GameProcedure>().Forget();
        }
    }
    
    // 打开设置事件处理
    public class OpenSettingsHandler : IEventHandle
    {
        public void Handle(IEventContext context)
        {
            Game.UI.OpenAsync<SettingsWindow>().Forget();
        }
    }
    
    // 退出游戏事件处理
    public class ExitGameHandler : IEventHandle
    {
        public void Handle(IEventContext context)
        {
            Application.Quit();
        }
    }
}
```

### 步骤 5: 创建游戏流程

```csharp
// GameProcedure.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Game
{
    public class GameProcedure : ProcedureStateBase
    {
        public override string Name => "Game";
        
        public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
        {
            Debug.Log("Game Started");
            
            // 显示加载界面
            Game.UI.ShowLoading("Loading game...");
            
            try
            {
                // 加载游戏场景
                await Game.Scene.SwitchAsync("GameScene");
                
                // 打开游戏 UI
                await Game.UI.OpenAsync<GameWindow>();
                
                // 发送游戏开始事件
                Game.Event.Raise("GameStarted", this);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to start game: {ex.Message}");
                await Game.Procedure.ChangeStateAsync<MainMenuProcedure>();
            }
            finally
            {
                Game.UI.HideLoading();
            }
        }
        
        public override async UniTask OnExitAsync(CancellationToken cancellationToken)
        {
            Debug.Log("Game Ended");
            
            // 关闭游戏 UI
            Game.UI.Close<GameWindow>();
            
            // 返回主菜单
            await Game.Scene.SwitchAsync("StartupScene");
        }
    }
}
```

### 步骤 6: 创建 UI 窗口

```csharp
// MainMenuWindow.cs
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Game
{
    public class MainMenuWindow : UIWindow
    {
        public override string AssetName => "MainMenuWindow";
        public override UILayer Layer => UILayer.Main;
        public override bool CacheOnClose => true;
        
        private Button startButton;
        private Button settingsButton;
        private Button exitButton;
        private Text playerNameText;
        
        protected override async UniTask OnCreateAsync(CancellationToken cancellationToken)
        {
            // 获取 UI 组件引用
            var transform = Root.transform;
            startButton = transform.Find("StartButton").GetComponent<Button>();
            settingsButton = transform.Find("SettingsButton").GetComponent<Button>();
            exitButton = transform.Find("ExitButton").GetComponent<Button>();
            playerNameText = transform.Find("PlayerNameText").GetComponent<Text>();
            
            // 绑定按钮事件
            startButton.onClick.AddListener(OnStartClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            exitButton.onClick.AddListener(OnExitClicked);
            
            // 加载玩家数据
            var saveData = Game.Data.Load<PlayerSaveData>("PlayerSave");
            playerNameText.text = $"Player: {saveData.PlayerName}";
        }
        
        private void OnStartClicked()
        {
            Game.Event.Raise("StartGame", this);
        }
        
        private void OnSettingsClicked()
        {
            Game.Event.Raise("OpenSettings", this);
        }
        
        private void OnExitClicked()
        {
            Game.Event.Raise("ExitGame", this);
        }
        
        protected override async UniTask OnDestroyAsync(CancellationToken cancellationToken)
        {
            // 清理事件绑定
            startButton.onClick.RemoveAllListeners();
            settingsButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();
        }
    }
}

// SettingsWindow.cs
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Game
{
    public class SettingsWindow : UIWindow
    {
        public override string AssetName => "SettingsWindow";
        public override UILayer Layer => UILayer.Popup;
        public override UIMode Mode => UIMode.Exclusive;
        
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private Button closeButton;
        private Button saveButton;
        
        protected override async UniTask OnCreateAsync(CancellationToken cancellationToken)
        {
            var transform = Root.transform;
            musicVolumeSlider = transform.Find("MusicVolumeSlider").GetComponent<Slider>();
            sfxVolumeSlider = transform.Find("SFXVolumeSlider").GetComponent<Slider>();
            closeButton = transform.Find("CloseButton").GetComponent<Button>();
            saveButton = transform.Find("SaveButton").GetComponent<Button>();
            
            // 绑定按钮事件
            closeButton.onClick.AddListener(OnCloseClicked);
            saveButton.onClick.AddListener(OnSaveClicked);
            
            // 加载当前设置
            var settings = Game.Data.Load<SettingsData>("Settings");
            if (settings != null)
            {
                musicVolumeSlider.value = settings.MusicVolume;
                sfxVolumeSlider.value = settings.SFXVolume;
            }
        }
        
        private void OnSaveClicked()
        {
            var settings = new SettingsData
            {
                MusicVolume = musicVolumeSlider.value,
                SFXVolume = sfxVolumeSlider.value
            };
            
            Game.Data.Save(settings, "Settings");
            Game.Audio.SetBGMVolume(settings.MusicVolume);
            Game.Audio.SetSFXVolume(settings.SFXVolume);
            
            Game.UI.ShowTips("Settings saved!");
            Game.UI.Close<SettingsWindow>();
        }
        
        private void OnCloseClicked()
        {
            Game.UI.Close<SettingsWindow>();
        }
    }
}

// GameWindow.cs
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Game
{
    public class GameWindow : UIWindow
    {
        public override string AssetName => "GameWindow";
        public override UILayer Layer => UILayer.Game;
        
        private Text levelText;
        private Text experienceText;
        private Text goldText;
        private Button pauseButton;
        private Button returnButton;
        
        protected override async UniTask OnCreateAsync(CancellationToken cancellationToken)
        {
            var transform = Root.transform;
            levelText = transform.Find("LevelText").GetComponent<Text>();
            experienceText = transform.Find("ExperienceText").GetComponent<Text>();
            goldText = transform.Find("GoldText").GetComponent<Text>();
            pauseButton = transform.Find("PauseButton").GetComponent<Button>();
            returnButton = transform.Find("ReturnButton").GetComponent<Button>();
            
            // 绑定按钮事件
            pauseButton.onClick.AddListener(OnPauseClicked);
            returnButton.onClick.AddListener(OnReturnClicked);
            
            // 更新 UI 显示
            UpdateUI();
            
            // 注册游戏更新事件
            Game.Event.Register("PlayerLevelUp", new PlayerLevelUpHandler());
            Game.Event.Register("PlayerExperienceGained", new PlayerExperienceGainedHandler());
        }
        
        private void UpdateUI()
        {
            var saveData = Game.Data.Load<PlayerSaveData>("PlayerSave");
            levelText.text = $"Level: {saveData.Level}";
            experienceText.text = $"EXP: {saveData.Experience}/{GetNextLevelExp(saveData.Level)}";
            goldText.text = $"Gold: {saveData.Gold}";
        }
        
        private int GetNextLevelExp(int level)
        {
            return level * 1000;
        }
        
        private void OnPauseClicked()
        {
            Time.timeScale = 0f;
            Game.UI.OpenAsync<PauseWindow>().Forget();
        }
        
        private void OnReturnClicked()
        {
            Game.UI.ShowDialog("Return to Menu", 
                "Your progress will be saved. Continue?", 
                "Yes", 
                () => 
                {
                    Game.Data.Save(Game.Data.Load<PlayerSaveData>("PlayerSave"), "PlayerSave");
                    Game.Procedure.ChangeStateAsync<MainMenuProcedure>().Forget();
                },
                "No");
        }
        
        protected override async UniTask OnDestroyAsync(CancellationToken cancellationToken)
        {
            // 恢复时间缩放
            Time.timeScale = 1f;
            
            // 注销事件监听
            Game.Event.Unregister<PlayerLevelUpHandler>();
            Game.Event.Unregister<PlayerExperienceGainedHandler>();
        }
    }
    
    public class PlayerLevelUpHandler : IEventHandle
    {
        public void Handle(IEventContext context)
        {
            if (Game.UI.TryGet<GameWindow>(out var gameWindow))
            {
                // 触发 UI 更新
                gameWindow.SendMessage("UpdateUI");
            }
        }
    }
    
    public class PlayerExperienceGainedHandler : IEventHandle
    {
        public void Handle(IEventContext context)
        {
            if (Game.UI.TryGet<GameWindow>(out var gameWindow))
            {
                // 触发 UI 更新
                gameWindow.SendMessage("UpdateUI");
            }
        }
    }
}
```

### 步骤 7: 创建数据模型

```csharp
// PlayerSaveData.cs
using System;

namespace Game
{
    [Serializable]
    public class PlayerSaveData
    {
        public string PlayerName;
        public int Level;
        public int Experience;
        public int Gold;
        public int LastPlayTime;
    }
}

// SettingsData.cs
using System;

namespace Game
{
    [Serializable]
    public class SettingsData
    {
        public float MusicVolume = 0.8f;
        public float SFXVolume = 0.8f;
        public int GraphicsQuality = 2;
        public bool FullScreen = true;
        public string Language = "en";
    }
}
```

### 步骤 8: 注册流程状态

创建一个游戏初始化脚本来注册所有流程状态：

```csharp
// GameInitializer.cs
using UnityEngine;
using GameDeveloperKit.Runtime;

namespace Game
{
    public class GameInitializer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterProcedures()
        {
            // 注册所有流程状态
            if (Game.HasModule<ProcedureModule>())
            {
                Game.Procedure.RegisterState<StartupProcedure>();
                Game.Procedure.RegisterState<MainMenuProcedure>();
                Game.Procedure.RegisterState<GameProcedure>();
            }
        }
    }
}
```

### 步骤 9: 创建 UI 资源

在 Unity 编辑器中创建对应的 UI Prefab：

1. **MainMenuWindow**: 包含开始游戏、设置、退出按钮
2. **SettingsWindow**: 包含音乐音量、音效音量滑块
3. **GameWindow**: 包含等级、经验、金币显示，暂停和返回按钮

### 运行游戏

1. 在 Unity 中打开 "StartupScene"
2. 运行游戏
3. 框架将自动启动并按照配置的流程运行

## 架构设计

### 设计模式

GameDeveloperKit 框架采用了多种设计模式：

1. **模块化模式**: 各模块相互独立，通过统一接口访问
2. **单例模式**: `Game` 类作为框架的唯一入口
3. **工厂模式**: 模块的创建和注册使用工厂方法
4. **观察者模式**: 事件系统实现发布-订阅机制
5. **状态模式**: 流程模块使用状态机模式管理游戏流程
6. **命令模式**: 命令模块支持操作撤销和重做
7. **对象池模式**: 对象池模块提高资源复用效率

### 模块依赖关系

```
┌─────────────────────────────────────────────────────────┐
│                      Game (Core)                         │
└─────────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Procedure  │  │   Resource   │  │      UI      │
│   Module     │  │   Module     │  │   Module     │
└──────────────┘  └──────────────┘  └──────────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          ▼
                  ┌──────────────┐
                  │    Event     │
                  │   Module     │
                  └──────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│    Scene     │  │   Network    │  │     Audio    │
│   Module     │  │   Module     │  │   Module     │
└──────────────┘  └──────────────┘  └──────────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          ▼
                  ┌──────────────┐
                  │ Diagnostics  │
                  │   Module     │
                  └──────────────┘
```

### 生命周期管理

每个模块都遵循统一的生命周期：

1. **Created**: 模块已创建但未初始化
2. **Initializing**: 模块正在初始化
3. **Ready**: 模块已就绪，可以正常使用
4. **ShuttingDown**: 模块正在关闭
5. **Disposed**: 模块已释放

## 扩展和定制

### 自定义模块

```csharp
public class MyCustomModule : IGameFrameworkLifecycleModule
{
    private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
    
    public GameFrameworkModuleStatus Status => _status;
    
    public UniTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 初始化逻辑
        _status = GameFrameworkModuleStatus.Ready;
        return UniTask.CompletedTask;
    }
    
    public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // 清理逻辑
        _status = GameFrameworkModuleStatus.Disposed;
        return UniTask.CompletedTask;
    }
    
    public void Dispose()
    {
        // 释放资源
    }
}

// 注册自定义模块
Game.RegisterModule(new MyCustomModule());
```

### 扩展现有模块

```csharp
// 继承并扩展流程状态
public class ExtendedProcedure : ProcedureStateBase
{
    public override string Name => "ExtendedProcedure";
    
    public override async UniTask OnEnterAsync(object userData, CancellationToken cancellationToken)
    {
        // 调用基类方法
        await base.OnEnterAsync(userData, cancellationToken);
        
        // 自定义逻辑
    }
}

// 继承并扩展 UI 窗口
public class ExtendedWindow : UIWindow
{
    protected override async UniTask OnCreateAsync(CancellationToken cancellationToken)
    {
        await base.OnCreateAsync(cancellationToken);
        
        // 自定义创建逻辑
    }
}
```

## 性能优化建议

1. **资源加载**: 尽量使用异步加载方法，避免阻塞主线程
2. **对象池**: 对于频繁创建销毁的对象，使用对象池
3. **UI 缓存**: 对于频繁打开关闭的 UI 窗口，启用缓存
4. **事件处理**: 避免在事件处理程序中执行耗时操作
5. **场景管理**: 合理使用持久化场景，减少加载时间

## 故障排查

### 常见问题

1. **模块未初始化**: 检查模块是否正确注册和初始化
2. **资源加载失败**: 检查资源路径和资源包配置
3. **UI 窗口无法打开**: 检查窗口资源和窗口配置
4. **事件未触发**: 检查事件处理程序是否正确注册

### 调试工具

框架提供了诊断模块，可以用于调试：

```csharp
// 获取诊断信息
var snapshots = Game.Diagnostics.GetSnapshots();
foreach (var snapshot in snapshots)
{
    Debug.Log($"{snapshot.Key}: {snapshot.Value}");
}

// 记录自定义诊断信息
Game.Diagnostics.CaptureSnapshot("CustomKey", "CustomValue");
```

## 许可证

本项目采用 MIT 许可证。

## 贡献

欢迎提交 Issue 和 Pull Request！

## 联系方式

如有问题或建议，请通过以下方式联系：

- 提交 GitHub Issue
- 发送邮件到项目维护者

---

**GameDeveloperKit** - 让 Unity 游戏开发更简单、更高效！
