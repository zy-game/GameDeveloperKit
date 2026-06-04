---
doc_type: issue-fix
slug: resource-editor-settings-scriptable-singleton
date: 2026-06-03
status: fixed
severity: high
tags:
  - resource-editor
  - unity-editor
  - scriptableobject
---

# Resource Editor Settings ScriptableSingleton Fix Note

## Symptom

Unity logged:

```text
ScriptableSingleton already exists. Did you query the singleton in a constructor?
UnityEditor.ScriptableSingleton`1<GameDeveloperKit.ResourceEditor.ResourceEditorSettings>:.ctor ()
GameDeveloperKit.ResourceEditor.ResourceEditorSettings:.ctor () (...)
```

## Root Cause

`ResourceEditorSettings` inherited `ScriptableSingleton<T>`. The settings asset only needed a `ScriptableObject` persisted under `ProjectSettings`, and the singleton base made Unity's construction / domain reload path fragile.

## Fix

- Changed `ResourceEditorSettings` from `ScriptableSingleton<T>` to a plain `ScriptableObject`.
- Kept the same ProjectSettings path and implemented explicit load / save through `InternalEditorUtility.LoadSerializedFileAndForget` and `SaveToSerializedFileAndForget`.
- Applied the same pattern to `ResourcePublisherSettings` to avoid the same constructor-time singleton failure there.
- Moved serialized defaults into `EnsureDefaults()` so object construction stays side-effect free.

## Verification

Compiled the editor assembly with:

```powershell
& 'D:\unitycn\editor\2022.3.62f2\Editor\Data\NetCoreRuntime\dotnet.exe' exec 'D:\unitycn\editor\2022.3.62f2\Editor\Data\DotNetSdkRoslyn\csc.dll' '@Library/Bee/artifacts/1900b0aEDbg.dag/GameDeveloperKit.Editor.rsp'
```

Result: compile passed.
