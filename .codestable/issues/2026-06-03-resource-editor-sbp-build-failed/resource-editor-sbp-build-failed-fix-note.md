---
doc_type: issue-fix
slug: resource-editor-sbp-build-failed
date: 2026-06-03
status: fixed
severity: high
tags:
  - resource-editor
  - sbp
---

# Resource Editor SBP Build Failed Fix Note

## Symptom

Clicking the Resource Editor build button could only report a generic `SBP build failed.` result, making resource builds unusable or opaque when SBP returned a failure.

## Root Cause

`ResourceBuildExecutor` used `CompatibilityBuildPipeline.BuildAssetBundles`. That compatibility wrapper returns `null` for every negative SBP `ReturnCode`, so the editor lost the actual failure category. The executor also passed the generated plan directly to SBP without checking invalid asset entries first.

## Fix

- Switched the executor to `ContentPipeline.BuildAssetBundles` so build failures return explicit SBP `ReturnCode` values.
- Added preflight validation for duplicate bundle names, missing resources, folder entries, missing assets, and assets assigned to multiple bundles.
- Preserved the compatibility manifest file by writing `CompatibilityAssetBundleManifest` from SBP `BundleInfos`.
- Added unsupported build target and unsaved-scene handling before invoking SBP.
- Improved exception failure text to include the exception type.

## Verification

Compiled the editor assembly with:

```powershell
& 'D:\unitycn\editor\2022.3.62f2\Editor\Data\NetCoreRuntime\dotnet.exe' exec 'D:\unitycn\editor\2022.3.62f2\Editor\Data\DotNetSdkRoslyn\csc.dll' '@Library/Bee/artifacts/1900b0aEDbg.dag/GameDeveloperKit.Editor.rsp'
```

Result: compile passed.
