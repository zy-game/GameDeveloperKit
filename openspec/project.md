# Project Context

## Purpose
GameDeveloperKit is a modular Unity game framework providing a comprehensive set of systems for game development including resource management, UI, ECS-based world simulation, networking, audio, configuration, and more. It aims to accelerate game development by providing reusable, well-structured modules.

## Tech Stack
- **Engine**: Unity (URP 17.3.0, Unity 6+)
- **Language**: C# (.NET)
- **Async**: UniTask (Cysharp) - Zero-allocation async/await for Unity
- **ECS**: Massive-ECS - Lightweight Entity Component System
- **LINQ**: ZLinq - High-performance zero-allocation LINQ
- **Build Pipeline**: Scriptable Build Pipeline (SBP) for asset bundles
- **Rendering**: Universal Render Pipeline (URP)

## Project Conventions

### Code Style
- **Namespaces**: `GameDeveloperKit.[Module]` (e.g., `GameDeveloperKit.UI`, `GameDeveloperKit.World`)
- **Naming**: PascalCase for public members, `_camelCase` for private fields with underscore prefix
- **Interfaces**: Prefix with `I` (e.g., `IModule`, `ISystem`, `IUIForm`)
- **XML Documentation**: Chinese comments for public APIs (游戏框架 style)
- **Attributes**: Use attributes for metadata (e.g., `[UIFormAttribute]`, `[ProcedureAttribute]`)

### Architecture Patterns
- **Module System**: All major systems implement `IModule` with `OnStartup()`, `OnUpdate()`, `OnClearup()` lifecycle
- **Static Game Facade**: `Game` class provides static access to all modules (e.g., `Game.UI`, `Game.Resource`, `Game.Event`)
- **MVP Pattern**: UI uses Model-View-Presenter with `UIFormBase<TData, TView>`
- **ECS Pattern**: World module uses Entity-Component-System with `ISystem`, `IComponent`, `GameWorld`
- **Reference Pool**: Object pooling via `ReferencePool` and `IReference` interface
- **Procedure/State Machine**: Game flow managed via `ProcedureManager` and `StateBase`
- **Event System**: Decoupled communication via `EventModule` with `GameEventArgs`

### Testing Strategy
- Unity Test Framework for unit tests
- Debug panels for runtime inspection (registered via `LoggerModule.RegisterPanel()`)

### Git Workflow
- Not currently using Git (no .git repository detected)

## Domain Context
- **Modules**: Self-contained systems (Logger, Resource, UI, World, Audio, Config, Data, Event, Procedure, Network, Grid, File/VFS)
- **World/ECS**: `GameWorld` manages entities, components, and systems. Systems implement interfaces like `IUpdateSystem`, `ISetupSystem`, `ITeardownSystem`
- **Resource System**: Supports multiple modes (EditorSimulator, Builtin, Bundle, Remote) with manifest-based asset management
- **UI System**: Layer-based UI with stack management, supports animations and safe area handling
- **Procedure System**: Async state machine for game flow (e.g., `InitializeFrameworkProcedure`, `LoadingDefaultPackageProcedure`)

## Important Constraints
- **Unity Lifecycle**: Must respect Unity's MonoBehaviour lifecycle and main thread requirements
- **Mobile Support**: Safe area handling for notched devices
- **Performance**: Zero-allocation patterns preferred (UniTask, ZLinq)
- **IL2CPP**: Code must be AOT-compatible for IL2CPP builds

## External Dependencies
- **Asset Bundles**: Remote resource server for hot updates (`ResourceUpdateUrl`)
- **Web Server**: Backend API endpoint (`WebServerUrl`)
- **Network**: TCP/UDP/WebSocket channels for multiplayer
