[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$FixtureRoot,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ResultsPath,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [string]$UpstreamQualityCommand,
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
    $root = (Get-CanonicalPath -Path $Parent).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $path = Get-CanonicalPath -Path $Child
    if (-not $path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Description escapes its expected root."
    }
    return $path
}

if ($TimeoutSeconds -le 0)
{
    throw "TimeoutSeconds must be positive."
}
$unityPath = Get-CanonicalPath -Path $UnityEditorPath
if (-not [System.IO.File]::Exists($unityPath))
{
    throw "Unity executable does not exist."
}
$packageRoot = Get-CanonicalPath -Path $PackagePath
$fixtureProject = Assert-ChildPath -Parent $FixtureRoot -Child $ProjectPath -Description "Quality fixture"
$resultsFile = Assert-ChildPath -Parent $packageRoot -Child $ResultsPath -Description "Quality results"
$logFile = Assert-ChildPath -Parent $packageRoot -Child $LogPath -Description "Quality log"
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resultsFile)) | Out-Null
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($logFile)) | Out-Null
foreach ($path in @($resultsFile, $logFile))
{
    if ([System.IO.File]::Exists($path))
    {
        [System.IO.File]::Delete($path)
    }
}

if (-not [string]::IsNullOrWhiteSpace($UpstreamQualityCommand))
{
    & pwsh -NoProfile -Command $UpstreamQualityCommand
    if ($LASTEXITCODE -ne 0)
    {
        throw "Upstream quality command failed with exit code $LASTEXITCODE."
    }
}

& pwsh -NoProfile -File (Join-Path $PSScriptRoot "New-ChannelBuildSmokeProject.ps1") `
    -ProjectPath $fixtureProject `
    -FixtureRoot $FixtureRoot `
    -PackagePath $packageRoot `
    -UnityEditorPath $unityPath `
    -Channel "quality" `
    -Profile "quality" `
    -IncludeTests
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

$arguments = @(
    "-batchmode", "-nographics", "-warningsAsErrors",
    "-projectPath", $fixtureProject,
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", $resultsFile,
    "-logFile", $logFile)
Initialize-UnityProcessEnvironment
$process = Start-Process -FilePath $unityPath -ArgumentList $arguments -NoNewWindow -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000))
{
    Stop-Process -Id $process.Id -Force
    throw "Local quality gate timed out after $TimeoutSeconds seconds."
}
if ($process.ExitCode -ne 0)
{
    throw "Unity EditMode quality gate failed with exit code $($process.ExitCode)."
}
if (-not [System.IO.File]::Exists($resultsFile))
{
    throw "Unity quality gate did not produce test XML."
}

[xml]$result = Get-Content -LiteralPath $resultsFile -Raw
$run = $result.'test-run'
if ($null -eq $run)
{
    throw "Unity quality XML has no test-run root."
}
$total = [int]$run.total
$failed = [int]$run.failed
if ($total -le 0 -or $failed -ne 0 -or $run.result -ne "Passed")
{
    throw "Unity quality gate failed: result=$($run.result) total=$total failed=$failed."
}

Write-Host "Local quality gate passed: EditMode tests=$total"
