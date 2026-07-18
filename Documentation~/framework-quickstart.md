---
doc_type: dev-guide
slug: framework-quickstart
component: framework
status: current
summary: Integrate GameDeveloperKit startup, resources, Playable media, Story, Analyzer, and quality gates.
tags: [unity, framework, resource, playable, story]
last_reviewed: 2026-07-18
---

# Framework Quickstart

## Overview

GameDeveloperKit exposes runtime modules through `App`. Modules are created on first access, their declared dependencies start first, and asynchronous readiness remains explicit.

## Prerequisites

- Unity 2021.3 or later.
- An organization-internal installation covered by [the package distribution notice](../NOTICE.md).
- AVPro Video license coverage for every permitted internal user of this package.
- UniTask and the package dependencies declared in `package.json`.

## Start the framework

For a scene-driven game, add `FrameworkStartup` to the startup scene. Select a public, non-abstract, parameterless Procedure and configure the modules that must be ready before it enters.

For a custom bootstrap:

```csharp
await App.Initialize();
await App.Resource.InitializeAsync(resourceSettings);
await App.Resource.PreloadDefaultPackagesAsync();
await App.Procedure.ChangeAsync<StartupProcedure>();
```

Call `await App.Shutdown()` when the application-owned framework lifetime ends. Do not use handler-free `Forget()` for framework work; `GDK_ASY002` reports it as an Error.

## Configure resources

`ResourceSettings.Mode` selects one current path:

| Mode | Source |
|---|---|
| `EditorSimulator` | Current Resource Editor authoring snapshot; no prior bundle build |
| `Offline` | Local packaged manifest and bundles |
| `Online` | Local base plus signed remote manifest and VFS bundle cache |
| `Web` | Local base plus signed remote manifest and non-persistent web bundles |

`ManifestName` is the local manifest location. The remote manifest filename is fixed by publish protocol v1 and does not reuse a local path. `MaxConcurrentBatchLoads` defaults to 8 and accepts 1 through 32.

```csharp
var resourceSettings = new ResourceSettings
{
    Mode = ResourceMode.EditorSimulator,
    MaxConcurrentBatchLoads = 8
};
await App.Resource.InitializeAsync(resourceSettings);
```

Online/Web also require:

- an absolute HTTPS `ServerUrl`;
- a positive `ClientBuild`;
- one or more `TrustedKeys` entries containing key id, RSA modulus, and exponent.

The client verifies publish protocol, channel, platform, client build range, RSA signature, manifest SHA-256, manifest format, and resource version before candidate commit. There is no HTTP or failed-signature fallback.

Release every successful Resource handle:

```csharp
var handle = await App.Resource.LoadAssetAsync("ui/icon");
try
{
    Use(handle.Asset);
}
finally
{
    handle.Release();
}
```

Label/type batch APIs are all-or-fail, preserve manifest order, and release successful in-flight handles when any item fails.

## Persist versioned data

Every persisted type has an explicit stable identity and current business schema. Container format, schema version, and snapshot version are separate values:

```csharp
[DataKey("player-profile")]
[DataSchema(2)]
public sealed class PlayerProfile
{
    public string DisplayName;
    public int Level;
}

App.Data.RegisterMigration<PlayerProfile>(new PlayerProfileV1ToV2Migration());
var profile = await App.Data.LoadDataAsync<PlayerProfile>();
await App.Data.SaveDataAsync<PlayerProfile>();
```

`IDataMigration` must declare one consecutive `FromVersion -> ToVersion` step. Its `DataMigrationPayload` carries both serializer identity and bytes, so a step can deliberately change serializers. Loading rejects missing chains, future schemas, failed migrations, and a final serializer that differs from the active serializer without changing the existing cache or version index. Container format 1 has no Runtime fallback.

## Play media

`PlayableModule` registers Audio, Text, Image, and Video implementations at startup:

```csharp
using var audio = await App.Playable.PlayAudioAsync("audio/bgm");
using var image = await App.Playable.PlayImageAsync("images/title", SetImage);
using var video = await App.Playable.PlayVideoAsync(videoPath);
```

Keep a handle while playback is owned and dispose it when that ownership ends. Image and Audio load through Resource; Video is backed by AVPro Video.

## Run Story content

Compile Story authoring data to `StoryProgramAsset`, convert it to a program, and use the default view or a custom `IStoryFramePresenter`:

```csharp
var asset = Resources.Load<StoryProgramAsset>("Story/sample");
var program = asset.ToProgram();
var view = StoryPlayerView.CreateDefault(App.UI.GetLayerRoot(UILayer.StoryPlayback));
await view.PlayAsync(program, "chapter_01");
```

`StoryModule` owns state progression. `StoryPlayable` maps text/audio/image/video commands to Playable implementations; a future presentation layer should compose those Playables instead of adding media logic to StoryProgram.

## Work with Analyzer errors

The package's Roslyn analyzers run against consumer business code under `Assets` by default. Rules for naming, async observation, exceptions, module boundaries, Editor APIs, Runtime physical I/O, and persisted Data contracts are Errors. Runtime file access outside FileModule is rejected; `GDK_DAT001` rejects persistence calls whose data type lacks `[DataKey]` or `[DataSchema]`. Business code should use framework module APIs rather than suppressing a rule.

## Run the commercial gate

In the framework repository, close the Unity Editor for this project and run:

```powershell
$env:UNITY_EDITOR_PATH = 'D:\unity2022.3.62f2c1\Editor\Unity.exe'
pwsh Tools/Quality/quality-gate.ps1
```

The command fails if the project is already open, verifies CodeAnalysis, builds Runtime/Editor/Test assemblies serially, runs EditMode and PlayMode with fresh XML, isolates and restores local VFS data, builds a Windows IL2CPP Player, and launches an AOT registration smoke. It rejects a missing IL2CPP platform module before the expensive stages and never leaves a stale success summary after failure. It does not run package testables or Samples workflows.

## Limits

- Unity Publisher is not part of the supported release chain. CI/CD signs, uploads, and approves resource releases.
- The current package is internal-only because it bundles commercial third-party content.
- The matching Windows Build Support (IL2CPP) module is mandatory. The quality gate does not fall back to Mono or the project's selected backend.
