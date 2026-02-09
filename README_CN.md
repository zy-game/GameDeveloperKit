# Game Developer Kit

一个模块化的 Unity 游戏框架，为游戏开发提供全面的系统支持。

## 功能特性

### 核心模块

- **资源管理** - 基于 Addressables 的资源加载和包管理
- **UI 系统** - 基于表单的 UI 管理，支持生命周期控制
- **音频系统** - 音频播放和管理，支持音轨
- **事件系统** - 解耦的事件驱动架构
- **数据管理** - 类型安全的数据容器和持久化
- **配置系统** - 配置加载和管理
- **网络模块** - Socket、Web 请求和下载管理
- **日志系统** - 高级日志记录，带调试控制台和命令系统
- **流程系统** - 用于游戏流程控制的状态机
- **世界系统** - 世界和实体管理
- **网格系统** - 基于网格的空间管理
- **虚拟文件系统** - 文件操作抽象层

### 附加功能

- **PSD 转 UGUI** - 将 Photoshop 文件转换为 Unity UI
- **下载插件** - 强大的文件下载系统，支持断点续传
- **内置命令** - 可扩展的调试控制台命令系统

## 环境要求

- Unity 6000.0.0f1 或更高版本
- 依赖项：
  - Unity UI (UGUI) 2.0.0
  - Newtonsoft Json 3.2.1
  - Scriptable Build Pipeline 2.4.3
  - UniTask
  - KinematicCharacterController

## 安装

1. 克隆此仓库
2. 在 Unity 6000.0 或更高版本中打开项目
3. 所有依赖项将通过 Package Manager 自动解析

## 快速开始

框架通过 `Startup` 组件初始化。核心模块通过静态 `Game` 类访问：

```csharp
// 访问模块
Game.Resource.LoadAssetAsync<GameObject>("资源路径");
Game.UI.OpenUIForm("UI表单名称");
Game.Event.Subscribe<你的事件>(事件处理器);
Game.Audio.PlaySound("音效名称");
Game.Data.GetData<你的数据类型>();
```

## 模块系统

所有模块都实现 `IModule` 接口，并通过 `Game` 类管理：

```csharp
// 获取任意模块
var customModule = Game.GetModule<ICustomModule>();

// 添加自定义模块
Game.AddModule<CustomModule>();
```

## 项目结构

```
Assets/
├── Runtime/           # 核心框架代码
│   ├── Audio/        # 音频管理
│   ├── Config/       # 配置系统
│   ├── Data/         # 数据管理
│   ├── Events/       # 事件系统
│   ├── Files/        # VFS 实现
│   ├── Grid/         # 网格系统
│   ├── Logger/       # 日志和调试控制台
│   ├── Network/      # 网络模块
│   ├── Procedure/    # 状态机
│   ├── Resource/     # 资源管理
│   ├── UIForm/       # UI 系统
│   ├── World/        # 世界管理
│   └── Game.cs       # 框架主入口
├── Editor/           # 编辑器工具和扩展
└── Plugins/          # 第三方插件
```

## 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 作者

刺、青
