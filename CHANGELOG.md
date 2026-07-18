# Changelog

All notable GameDeveloperKit package changes are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## [2.0.0-preview.1] - 2026-07-18

### Breaking

- Removed the legacy `TimerModule.SetTimer` and `TimerModule.ClearTimer` APIs and the `TimerDelayHandle(Action<float>)` constructor. Use `Delay`, `Interval`, `Register`, and `Cancel`, and retain the returned handle when cancellation is required.

### Changed

- The commercial quality gate now treats managed and Unity script compiler warnings as errors.

### Migration

1. Replace one-shot `SetTimer` calls with `Delay` and repeating calls with `Interval`.
2. Store the returned `TimerHandle` and pass it to `Cancel`; callback identity is no longer a timer ownership mechanism.

## [1.0.0-preview.1] - 2026-07-18

### Breaking

- Removed `App.Sound` and `SoundModule`. Audio playback now uses `App.Playable.PlayAudioAsync` and `AudioPlayableHandle`.
- Removed the `GameDeveloperKit.StoryPlayback` assembly. Story runtime, default presentation, and Playable composition now live in `GameDeveloperKit.Runtime`; remove the old asmdef reference from consumers.
- Resource manifests now require `FormatVersion = 1`. Online/Web publishing requires HTTPS and a signed publish protocol v1 pointer with client build range.
- Procedure and Network runtime type discovery now uses generated registration. Valid Procedure types must be public, non-abstract, and parameterless.
- Resource batch APIs are all-or-fail and use bounded concurrency configured by `ResourceSettings.MaxConcurrentBatchLoads`.
- Data persistence now requires explicit `[DataKey]` and `[DataSchema]` contracts and writes container format 2. Format 1 containers are not loaded.

### Added

- Text, Audio, Image, Video, and Story Playable abstractions.
- Consumer-default Roslyn Analyzer errors and generated Event/Procedure/Network registration.
- CI-side `Tools/ResourceSigning` command for publish pointer signing.
- Provider-independent `Tools/Quality/quality-gate.ps1` for CodeAnalysis, Unity tests, mandatory Windows IL2CPP build, and generated-registration Player smoke verification.
- Package README, developer quickstart, internal-only distribution notice, and public API baseline.
- Consecutive Data schema migrations with explicit serializer-aware payloads, plus `GDK_DAT001` consumer analysis.

### Migration

1. Remove `GameDeveloperKit.StoryPlayback` from business asmdef references; reference `GameDeveloperKit.Runtime` only.
2. Replace Sound calls with `App.Playable` Audio APIs and dispose playback handles.
3. Rebuild resource manifests with the current Resource Editor/build workflow; do not reuse an old manifest.
4. Configure Online/Web `ClientBuild` and `TrustedKeys`, then sign CI publish metadata with `Tools/ResourceSigning`.
5. Fix Analyzer errors in consumer business code; do not disable framework analysis scope.
6. Add `[DataKey]` and `[DataSchema]` to every persisted type. Register every required `N -> N+1` `IDataMigration` before loading older schema data; rebuild or deliberately migrate format 1 containers outside Runtime.
