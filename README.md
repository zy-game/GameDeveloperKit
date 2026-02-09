# Game Developer Kit

A modular Unity game framework providing comprehensive systems for game development.

## Features

### Core Modules

- **Resource Management** - Asset loading and package management with Addressables support
- **UI System** - Form-based UI management with lifecycle control
- **Audio System** - Audio playback and management with track support
- **Event System** - Decoupled event-driven architecture
- **Data Management** - Type-safe data containers and persistence
- **Config System** - Configuration loading and management
- **Network** - Socket, web requests, and download management
- **Logger** - Advanced logging with debug console and command system
- **Procedure System** - State machine for game flow control
- **World System** - World and entity management
- **Grid System** - Grid-based spatial management
- **VFS (Virtual File System)** - File operations abstraction

### Additional Features

- **PSD to UGUI** - Convert Photoshop files to Unity UI
- **Downloader Plugin** - Robust file download system with resume support
- **Built-in Commands** - Debug console with extensible command system

## Requirements

- Unity 6000.0.0f1 or later
- Dependencies:
  - Unity UI (UGUI) 2.0.0
  - Newtonsoft Json 3.2.1
  - Scriptable Build Pipeline 2.4.3
  - UniTask
  - KinematicCharacterController

## Installation

1. Clone this repository
2. Open the project in Unity 6000.0 or later
3. All dependencies should be automatically resolved via Package Manager

## Quick Start

The framework initializes through the `Startup` component. Core modules are accessed via the static `Game` class:

```csharp
// Access modules
Game.Resource.LoadAssetAsync<GameObject>("AssetPath");
Game.UI.OpenUIForm("UIFormName");
Game.Event.Subscribe<YourEvent>(OnEventHandler);
Game.Audio.PlaySound("SoundName");
Game.Data.GetData<YourDataType>();
```

## Module System

All modules implement the `IModule` interface and are managed through the `Game` class:

```csharp
// Get any module
var customModule = Game.GetModule<ICustomModule>();

// Add custom module
Game.AddModule<CustomModule>();
```

## Project Structure

```
Assets/
├── Runtime/           # Core framework code
│   ├── Audio/        # Audio management
│   ├── Config/       # Configuration system
│   ├── Data/         # Data management
│   ├── Events/       # Event system
│   ├── Files/        # VFS implementation
│   ├── Grid/         # Grid system
│   ├── Logger/       # Logging and debug console
│   ├── Network/      # Network modules
│   ├── Procedure/    # State machine
│   ├── Resource/     # Resource management
│   ├── UIForm/       # UI system
│   ├── World/        # World management
│   └── Game.cs       # Main framework entry
├── Editor/           # Editor tools and extensions
└── Plugins/          # Third-party plugins
```

## License

MIT License - see [LICENSE](LICENSE) file for details

## Author

刺、青
