[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$FixtureRoot,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [string]$Channel = "dev",
    [string]$Profile = "android-dev",
    [switch]$IncludeTests,
    [switch]$IncludePlayerScene
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-CanonicalPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-ChildPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )

    $parentPath = (Get-CanonicalPath -Path $Parent).TrimEnd('\', '/') +
        [System.IO.Path]::DirectorySeparatorChar
    $childPath = Get-CanonicalPath -Path $Child
    if (-not $childPath.StartsWith($parentPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Fixture project must remain inside its fixture root."
    }

    return $childPath
}

function Write-Utf8Json
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}

$packageRoot = Get-CanonicalPath -Path $PackagePath
$unityPath = Get-CanonicalPath -Path $UnityEditorPath
if (-not [System.IO.File]::Exists($unityPath))
{
    throw "Unity executable does not exist: $unityPath"
}
$unityProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($unityPath).ProductVersion
if ($unityProductVersion -notmatch '^(?<version>[^_]+)_(?<revision>[0-9a-fA-F]+)$')
{
    throw "Unity executable ProductVersion does not contain a version and revision."
}
$unityVersion = $Matches.version
$unityRevision = $Matches.revision

$packageManifestPath = Join-Path $packageRoot "package.json"
if (-not [System.IO.File]::Exists($packageManifestPath))
{
    throw "Package manifest does not exist: $packageManifestPath"
}

$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw | ConvertFrom-Json
if ($packageManifest.name -cne "com.gamedeveloperkit.framework")
{
    throw "Package name must be com.gamedeveloperkit.framework."
}

if ([string]::IsNullOrWhiteSpace($Channel) -or $Channel -notmatch '^[A-Za-z0-9._-]+$')
{
    throw "Channel must be a non-empty safe segment."
}
if ([string]::IsNullOrWhiteSpace($Profile) -or $Profile -notmatch '^[A-Za-z0-9._-]+$')
{
    throw "Profile must be a non-empty safe segment."
}

$fixtureParent = Get-CanonicalPath -Path $FixtureRoot
$fixtureRoot = Assert-ChildPath -Parent $fixtureParent -Child $ProjectPath
$packagePrefix = $packageRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($fixtureRoot.StartsWith($packagePrefix, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Fixture project cannot be nested inside the package checkout."
}
if ([System.IO.Directory]::Exists($fixtureRoot))
{
    [System.IO.Directory]::Delete($fixtureRoot, $true)
}

$assetsPath = Join-Path $fixtureRoot "Assets"
$packagesPath = Join-Path $fixtureRoot "Packages"
$projectSettingsPath = Join-Path $fixtureRoot "ProjectSettings"
$settingsPath = Join-Path $fixtureRoot "ProjectSettings\GameDeveloperKit"
[System.IO.Directory]::CreateDirectory($assetsPath) | Out-Null
[System.IO.Directory]::CreateDirectory($packagesPath) | Out-Null
[System.IO.Directory]::CreateDirectory($projectSettingsPath) | Out-Null
[System.IO.Directory]::CreateDirectory($settingsPath) | Out-Null

$packageDependencyPath = $packageRoot.Replace('\', '/')
$manifest = [ordered]@{
    dependencies = [ordered]@{
        "com.gamedeveloperkit.framework" = "file:$packageDependencyPath"
    }
}
if ($IncludeTests)
{
    $manifest.testables = @("com.gamedeveloperkit.framework")
}
$profiles = [ordered]@{
    schemaVersion = 1
    profiles = @(
        [ordered]@{
            id = $Profile
            channel = $Channel
        }
    )
}

Write-Utf8Json -Path (Join-Path $packagesPath "manifest.json") -Value $manifest
[System.IO.File]::WriteAllText(
    (Join-Path $projectSettingsPath "ProjectVersion.txt"),
    "m_EditorVersion: $unityVersion`n" +
        "m_EditorVersionWithRevision: $unityVersion ($unityRevision)`n",
    [System.Text.UTF8Encoding]::new($false))
Write-Utf8Json `
    -Path (Join-Path $settingsPath "channel-build-profiles.json") `
    -Value $profiles

if ($IncludePlayerScene)
{
    $settingsScriptMeta = Join-Path $packageRoot "Editor\ResourceEditor\ResourceEditorSettings.cs.meta"
    if (-not [System.IO.File]::Exists($settingsScriptMeta))
    {
        throw "Resource editor settings script metadata is missing."
    }
    $guidMatch = Select-String -LiteralPath $settingsScriptMeta -Pattern '^guid: ([0-9a-f]{32})$' | Select-Object -First 1
    if ($null -eq $guidMatch)
    {
        throw "Resource editor settings script GUID is invalid."
    }
    $settingsScriptGuid = $guidMatch.Matches[0].Groups[1].Value
    $resourceSettings = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_ObjectHideFlags: 61
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $settingsScriptGuid, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Packages:
  - m_Name: BUILTIN
    m_Version: 1.0.0
    m_IsHotUpdate: 0
    m_CollectorId:
    m_BuildStrategyId: single-bundle
    m_Bundles:
    - m_Name: Resources
      m_Group: Resources
      m_Dependencies: []
      m_Labels: []
      m_AssetPaths: []
      m_ProviderId: resources
      m_Entries: []
      m_CollectorId:
      m_SourceFolder:
      m_CollectorParameter:
  - m_Name: LOCAL
    m_Version: 1.0.0
    m_IsHotUpdate: 0
    m_CollectorId:
    m_BuildStrategyId: single-bundle
    m_Bundles:
    - m_Name: LOCAL
      m_Group: LOCAL
      m_Dependencies: []
      m_Labels: []
      m_AssetPaths: []
      m_ProviderId: asset-bundle
      m_Entries: []
      m_CollectorId:
      m_SourceFolder:
      m_CollectorParameter:
  m_ManifestOutputPath: Assets/StreamingAssets/manifest.json
  m_BuildSettings:
    m_OutputRoot: Build/ResourceBundles
    m_Target: Android
    m_Channel: $Channel
    m_CleanOutput: 1
    m_Compression: 1
    m_ManifestFileName: manifest.json
    m_Version: 1.0.0
    m_Scope: 1
  m_SelectedPackageIndex: 0
"@
    [System.IO.File]::WriteAllText(
        (Join-Path $projectSettingsPath "GameDeveloperKitResourceEditorSettings.asset"),
        $resourceSettings,
        [System.Text.UTF8Encoding]::new($false))

    $scenePath = Join-Path $assetsPath "ChannelBuild.unity"
    $scene = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/ChannelBuild.unity
    guid: 00000000000000000000000000000000
  m_configObjects: {}
"@
    [System.IO.File]::WriteAllText(
        (Join-Path $projectSettingsPath "EditorBuildSettings.asset"),
        $scene,
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        $scenePath,
        "%YAML 1.1`n%TAG !u! tag:unity3d.com,2011:`n--- !u!29 &1`nOcclusionCullingSettings:`n  m_ObjectHideFlags: 0`n  serializedVersion: 2`n  m_OcclusionBakeSettings:`n    smallestOccluder: 5`n    smallestHole: 0.25`n    backfaceThreshold: 100`n  m_SceneGUID: 00000000000000000000000000000000`n  m_OcclusionCullingData: {fileID: 0}`n--- !u!104 &2`nRenderSettings:`n  m_ObjectHideFlags: 0`n  serializedVersion: 9`n  m_Fog: 0`n--- !u!157 &3`nLightmapSettings:`n  m_ObjectHideFlags: 0`n  serializedVersion: 12`n  m_GIWorkflowMode: 1`n--- !u!196 &4`nNavMeshSettings:`n  serializedVersion: 2`n  m_ObjectHideFlags: 0`n  m_BuildSettings:`n    serializedVersion: 3`n  m_NavMeshData: {fileID: 0}`n",
        [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Channel build smoke fixture ready: $fixtureRoot"
