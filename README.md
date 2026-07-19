# GameDeveloperKit

GameDeveloperKit is an internal Unity framework package for modular runtime services, resource management, Playable media, Story playback, UI, networking, data, and tooling.

This package is `UNLICENSED`, private, and restricted to organization-internal use. Read [NOTICE.md](NOTICE.md) before installing or distributing it.

## Start a project

1. Add `FrameworkStartup` to the startup scene.
2. Select a public, non-abstract, parameterless `ProcedureBase` implementation.
3. Configure Resource initialization and any Config, Data, or Playable modules required before the first Procedure.
4. Enter Play Mode. `FrameworkStartup` initializes `App`, prepares selected modules, then changes to the generated Procedure registration.

Manual startup is also supported:

```csharp
await App.Initialize();
await App.Resource.InitializeAsync(resourceSettings);
await App.Resource.PreloadDefaultPackagesAsync();
await App.Procedure.ChangeAsync<StartupProcedure>();
```

Await framework `UniTask` results. A handler-free `Forget()` is rejected by `GDK_ASY002`.

## Main APIs

- `App.Resource`: EditorSimulator, Offline, signed Online, and Web resource loading.
- `App.Playable`: Audio, Text, Image, and Video playback handles.
- `App.Story`: StoryProgram registration and StoryRunner state.
- `StoryPlayerView`: default UGUI/AVPro Story presentation.
- `App.UI`, `App.Event`, `App.Procedure`, `App.Network`, `App.Data`, `App.File`: framework runtime modules.

See [the developer quickstart](Documentation~/framework-quickstart.md) for resource security, handle ownership, media playback, Story integration, Analyzer rules, and the repository quality command.

For Jenkins channel builds, immutable resource staging, approval, and rollback, see [Jenkins Channel Build Operations](Documentation~/jenkins-channel-build-operations.md).

## Distribution

The package contains third-party components including AVPro Video. Do not publish this package to a public registry or redistribute it to an external legal entity. External delivery requires a separately approved package composition and license review.
