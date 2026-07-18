[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$FixtureRoot,
    [Parameter(Mandatory = $true)][string]$Channel,
    [Parameter(Mandatory = $true)][ValidateSet("dev", "test", "staging", "prod")][string]$Environment,
    [Parameter(Mandatory = $true)][string]$BuildTarget,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][int]$PlayerBuildNumber,
    [Parameter(Mandatory = $true)][string]$Profile,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [ValidateSet("validate", "player")][string]$Mode = "validate",
    [string]$Flavor,
    [Nullable[long]]$MinimumClientBuild,
    [Nullable[long]]$MaximumClientBuild,
    [string]$CiProvider,
    [string]$CiJobName,
    [string]$CiBuildId,
    [string]$CiBuildUrl,
    [string]$CiRevision,
    [int]$TimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "UnityProcessEnvironment.ps1")

function Get-CanonicalPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-ChildPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $parentPath = (Get-CanonicalPath -Path $Parent).TrimEnd('\', '/') +
        [System.IO.Path]::DirectorySeparatorChar
    $childPath = Get-CanonicalPath -Path $Child
    if (-not $childPath.StartsWith($parentPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Description escapes its expected root."
    }

    return $childPath
}

function Assert-SafeSegment
{
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9._-]+$')
    {
        throw "$Name must be a non-empty safe segment."
    }
}

function Add-ArgumentPair
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.Generic.List[string]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $Arguments.Add($Name)
    $Arguments.Add($Value)
}

$repositoryRoot = Get-CanonicalPath -Path (Join-Path $PSScriptRoot "..\..\..")
$unityPath = Get-CanonicalPath -Path $UnityEditorPath
if (-not [System.IO.File]::Exists($unityPath))
{
    throw "Unity executable does not exist: $unityPath"
}

$projectRoot = Assert-ChildPath `
    -Parent $FixtureRoot `
    -Child $ProjectPath `
    -Description "Fixture project"
$repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($projectRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Fixture project cannot be nested inside the package checkout."
}
if (-not [System.IO.File]::Exists((Join-Path $projectRoot "Packages\manifest.json")))
{
    throw "Fixture project manifest does not exist."
}

$outputPath = Assert-ChildPath `
    -Parent (Join-Path $repositoryRoot "Build") `
    -Child $OutputRoot `
    -Description "Output root"
$reportFile = Assert-ChildPath -Parent $outputPath -Child $ReportPath -Description "Report path"
$logFile = Assert-ChildPath -Parent $outputPath -Child $LogPath -Description "Log path"

Assert-SafeSegment -Value $Channel -Name "Channel"
Assert-SafeSegment -Value $BuildTarget -Name "BuildTarget"
Assert-SafeSegment -Value $Version -Name "Version"
Assert-SafeSegment -Value $Profile -Name "Profile"
if (-not [string]::IsNullOrEmpty($Flavor))
{
    Assert-SafeSegment -Value $Flavor -Name "Flavor"
}
if ($PlayerBuildNumber -le 0)
{
    throw "PlayerBuildNumber must be positive."
}
if ($TimeoutSeconds -le 0)
{
    throw "TimeoutSeconds must be positive."
}
$hasMinimumClientBuild = $null -ne $MinimumClientBuild
$hasMaximumClientBuild = $null -ne $MaximumClientBuild
if ($hasMinimumClientBuild -ne $hasMaximumClientBuild)
{
    throw "MinimumClientBuild and MaximumClientBuild must be provided together."
}
if ($hasMinimumClientBuild -and
    ($MinimumClientBuild -le 0 -or $MaximumClientBuild -lt $MinimumClientBuild))
{
    throw "Client build range is invalid."
}

$ciValues = @($CiProvider, $CiJobName, $CiBuildId, $CiRevision)
$hasAnyCi = @($ciValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or
    -not [string]::IsNullOrWhiteSpace($CiBuildUrl)
$hasCompleteCi = @($ciValues | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -eq 0
if ($hasAnyCi -and -not $hasCompleteCi)
{
    throw "CI provider, job name, build id and revision must be provided together."
}

[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null
foreach ($stalePath in @($reportFile, $logFile))
{
    if ([System.IO.File]::Exists($stalePath))
    {
        [System.IO.File]::Delete($stalePath)
    }
}

$arguments = [System.Collections.Generic.List[string]]::new()
foreach ($argument in @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $projectRoot,
    "-executeMethod", "GameDeveloperKit.ChannelBuildCommand.Build",
    "-logFile", $logFile))
{
    $arguments.Add($argument)
}
Add-ArgumentPair -Arguments $arguments -Name "-gdkChannel" -Value $Channel
Add-ArgumentPair -Arguments $arguments -Name "-gdkEnvironment" -Value $Environment
Add-ArgumentPair -Arguments $arguments -Name "-gdkBuildTarget" -Value $BuildTarget
Add-ArgumentPair -Arguments $arguments -Name "-gdkVersion" -Value $Version
Add-ArgumentPair -Arguments $arguments -Name "-gdkPlayerBuildNumber" -Value $PlayerBuildNumber.ToString([Globalization.CultureInfo]::InvariantCulture)
Add-ArgumentPair -Arguments $arguments -Name "-gdkProfile" -Value $Profile
Add-ArgumentPair -Arguments $arguments -Name "-gdkOutputRoot" -Value $outputPath
Add-ArgumentPair -Arguments $arguments -Name "-gdkReportPath" -Value $reportFile
Add-ArgumentPair -Arguments $arguments -Name "-gdkMode" -Value $Mode
if (-not [string]::IsNullOrEmpty($Flavor))
{
    Add-ArgumentPair -Arguments $arguments -Name "-gdkFlavor" -Value $Flavor
}
if ($hasMinimumClientBuild)
{
    Add-ArgumentPair -Arguments $arguments -Name "-gdkMinimumClientBuild" -Value ([long]$MinimumClientBuild).ToString([Globalization.CultureInfo]::InvariantCulture)
    Add-ArgumentPair -Arguments $arguments -Name "-gdkMaximumClientBuild" -Value ([long]$MaximumClientBuild).ToString([Globalization.CultureInfo]::InvariantCulture)
}
if ($hasCompleteCi)
{
    Add-ArgumentPair -Arguments $arguments -Name "-gdkCiProvider" -Value $CiProvider
    Add-ArgumentPair -Arguments $arguments -Name "-gdkCiJobName" -Value $CiJobName
    Add-ArgumentPair -Arguments $arguments -Name "-gdkCiBuildId" -Value $CiBuildId
    Add-ArgumentPair -Arguments $arguments -Name "-gdkCiRevision" -Value $CiRevision
    if (-not [string]::IsNullOrWhiteSpace($CiBuildUrl))
    {
        Add-ArgumentPair -Arguments $arguments -Name "-gdkCiBuildUrl" -Value $CiBuildUrl
    }
}

$escapedArguments = foreach ($argument in $arguments)
{
    if ($argument -match '[\s"]')
    {
        '"' + $argument.Replace('"', '\"') + '"'
    }
    else
    {
        $argument
    }
}
Write-Host "[Channel Build] $unityPath $($escapedArguments -join ' ')"
Initialize-UnityProcessEnvironment
$process = Start-Process `
    -FilePath $unityPath `
    -ArgumentList $escapedArguments `
    -WorkingDirectory $repositoryRoot `
    -NoNewWindow `
    -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000))
{
    Stop-Process -Id $process.Id -Force
    throw "Unity channel build timed out after $TimeoutSeconds seconds."
}

exit $process.ExitCode
