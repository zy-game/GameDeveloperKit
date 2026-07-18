[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$FixtureRoot,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [string]$Channel = "dev",
    [string]$Profile = "android-dev"
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

Write-Host "Channel build smoke fixture ready: $fixtureRoot"
