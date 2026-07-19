[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][int]$ExpectedExitCode
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
        [Parameter(Mandatory = $true)][string]$Child,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $parentPath = (Get-CanonicalPath -Path $Parent).TrimEnd('\', '/') +
        [System.IO.Path]::DirectorySeparatorChar
    $childPath = Get-CanonicalPath -Path $Child
    if (-not $childPath.StartsWith($parentPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Description escapes output root."
    }

    return $childPath
}

function Assert-ExactKeys
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Value,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Description
    )

    foreach ($key in $Expected)
    {
        if (-not (@($Value.Keys) -ccontains $key))
        {
            throw "$Description is missing required member '$key'."
        }
    }
    foreach ($key in $Value.Keys)
    {
        if (-not ($Expected -ccontains $key))
        {
            throw "$Description contains unknown member '$key'."
        }
    }
}

function Assert-NonEmptyText
{
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value) -or $Value -match '[\r\n]')
    {
        throw "$Description must be non-empty single-line text."
    }
}

function Get-JsonInteger
{
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $isInteger = $Value -is [byte] -or
        $Value -is [sbyte] -or
        $Value -is [int16] -or
        $Value -is [uint16] -or
        $Value -is [int32] -or
        $Value -is [uint32] -or
        $Value -is [int64] -or
        $Value -is [uint64]
    if (-not $isInteger)
    {
        throw "$Description must be a JSON integer."
    }

    return [long]$Value
}

function ConvertTo-UtcTimestamp
{
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($Value -is [DateTime])
    {
        if ($Value.Kind -ne [DateTimeKind]::Utc)
        {
            throw "$Description must be UTC."
        }

        return [DateTimeOffset]::new($Value)
    }

    Assert-NonEmptyText -Value $Value -Description $Description
    try
    {
        $timestamp = [DateTimeOffset]::ParseExact(
            $Value,
            "o",
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch
    {
        throw "$Description must use round-trip UTC format."
    }
    if ($timestamp.Offset -ne [TimeSpan]::Zero)
    {
        throw "$Description must be UTC."
    }

    return $timestamp
}

if (@(0, 2, 3, 4, 5, 6) -notcontains $ExpectedExitCode)
{
    throw "ExpectedExitCode is not part of the channel build protocol."
}

$outputPath = Get-CanonicalPath -Path $OutputRoot
$reportFile = Assert-ChildPath -Parent $outputPath -Child $ReportPath -Description "Report path"
if (-not [System.IO.File]::Exists($reportFile))
{
    throw "Channel build report does not exist: $reportFile"
}

try
{
    $json = Get-Content -LiteralPath $reportFile -Raw
    if ((Get-Command ConvertFrom-Json).Parameters.ContainsKey("DateKind"))
    {
        $report = $json | ConvertFrom-Json -AsHashtable -DateKind String
    }
    else
    {
        $report = $json | ConvertFrom-Json -AsHashtable
    }
}
catch
{
    throw "Channel build report is not valid JSON."
}
if ($report -isnot [System.Collections.IDictionary])
{
    throw "Channel build report root must be an object."
}

Assert-ExactKeys `
    -Value $report `
    -Expected @(
        "schemaVersion", "status", "failureKind", "exitCode", "context", "ci",
        "artifacts", "steps", "warnings", "startedAtUtc", "finishedAtUtc") `
    -Description "Report"
$schemaVersion = Get-JsonInteger -Value $report.schemaVersion -Description "schemaVersion"
if ($schemaVersion -ne 1)
{
    throw "Channel build report schemaVersion must be 1."
}
$reportExitCode = Get-JsonInteger -Value $report.exitCode -Description "exitCode"
if ($reportExitCode -ne $ExpectedExitCode)
{
    throw "Report exitCode does not match the Unity process exit code."
}

$failureKinds = @{
    2 = "invalid-input"
    3 = "pipeline"
    4 = "resource-build"
    5 = "player-build"
    6 = "report"
}
if ($ExpectedExitCode -eq 0)
{
    if ($report.status -cne "succeeded" -or $report.failureKind -cne "none" -or $null -eq $report.context)
    {
        throw "Succeeded report outcome is inconsistent."
    }
}
else
{
    if ($report.status -cne "failed" -or $report.failureKind -cne $failureKinds[$ExpectedExitCode])
    {
        throw "Failed report outcome is inconsistent."
    }
}

if ($null -ne $report.context)
{
    if ($report.context -isnot [System.Collections.IDictionary])
    {
        throw "Report context must be an object or null."
    }
    Assert-ExactKeys -Value $report.context -Expected @("channel", "platform", "version") -Description "Context"
    Assert-NonEmptyText -Value $report.context.channel -Description "Context channel"
    Assert-NonEmptyText -Value $report.context.platform -Description "Context platform"
    Assert-NonEmptyText -Value $report.context.version -Description "Context version"
}

if ($null -ne $report.ci)
{
    if ($report.ci -isnot [System.Collections.IDictionary])
    {
        throw "Report ci must be an object or null."
    }
    Assert-ExactKeys `
        -Value $report.ci `
        -Expected @("provider", "jobName", "buildId", "buildUrl", "revision") `
        -Description "CI metadata"
    Assert-NonEmptyText -Value $report.ci.provider -Description "CI provider"
    Assert-NonEmptyText -Value $report.ci.jobName -Description "CI jobName"
    Assert-NonEmptyText -Value $report.ci.buildId -Description "CI buildId"
    Assert-NonEmptyText -Value $report.ci.revision -Description "CI revision"
    if ($null -ne $report.ci.buildUrl)
    {
        Assert-NonEmptyText -Value $report.ci.buildUrl -Description "CI buildUrl"
        $buildUri = $null
        if (-not [Uri]::TryCreate($report.ci.buildUrl, [UriKind]::Absolute, [ref]$buildUri) -or
            @("http", "https") -cnotcontains $buildUri.Scheme)
        {
            throw "CI buildUrl must be an absolute HTTP or HTTPS URL."
        }
    }
}

$startedAt = ConvertTo-UtcTimestamp -Value $report.startedAtUtc -Description "startedAtUtc"
$finishedAt = ConvertTo-UtcTimestamp -Value $report.finishedAtUtc -Description "finishedAtUtc"
if ($finishedAt -lt $startedAt)
{
    throw "Report finishedAtUtc precedes startedAtUtc."
}

foreach ($collectionName in @("artifacts", "steps", "warnings"))
{
    if ($report[$collectionName] -isnot [object[]])
    {
        throw "Report $collectionName must be an array."
    }
}

$artifactPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($artifact in @($report.artifacts))
{
    if ($artifact -isnot [System.Collections.IDictionary])
    {
        throw "Artifact entry must be an object."
    }
    Assert-ExactKeys -Value $artifact -Expected @("kind", "path", "sha256", "sizeBytes") -Description "Artifact"
    Assert-NonEmptyText -Value $artifact.kind -Description "Artifact kind"
    Assert-NonEmptyText -Value $artifact.path -Description "Artifact path"
    if ([System.IO.Path]::IsPathRooted($artifact.path) -or $artifact.path.Contains('\'))
    {
        throw "Artifact path must be a forward-slash relative path."
    }
    $pathSegments = @($artifact.path.Split('/'))
    if (@($pathSegments | Where-Object { [string]::IsNullOrEmpty($_) -or $_ -eq "." -or $_ -eq ".." }).Count -gt 0)
    {
        throw "Artifact path must be normalized."
    }
    if (-not $artifactPaths.Add([string]$artifact.path))
    {
        throw "Artifact path is duplicated."
    }

    $artifactFile = Assert-ChildPath `
        -Parent $outputPath `
        -Child (Join-Path $outputPath $artifact.path.Replace('/', [System.IO.Path]::DirectorySeparatorChar)) `
        -Description "Artifact path"
    if (-not [System.IO.File]::Exists($artifactFile))
    {
        throw "Artifact file does not exist."
    }
    if ($artifact.sha256 -isnot [string] -or $artifact.sha256 -cnotmatch '^[0-9a-f]{64}$')
    {
        throw "Artifact sha256 must be lowercase hexadecimal."
    }
    $actualHash = (Get-FileHash -LiteralPath $artifactFile -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $artifact.sha256)
    {
        throw "Artifact sha256 does not match the file."
    }
    $reportedSize = Get-JsonInteger -Value $artifact.sizeBytes -Description "Artifact sizeBytes"
    $actualSize = (Get-Item -LiteralPath $artifactFile).Length
    if ($reportedSize -ne $actualSize)
    {
        throw "Artifact sizeBytes does not match the file."
    }
}

$stepIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($step in @($report.steps))
{
    if ($step -isnot [System.Collections.IDictionary])
    {
        throw "Step entry must be an object."
    }
    Assert-ExactKeys -Value $step -Expected @("id", "status", "message") -Description "Step"
    Assert-NonEmptyText -Value $step.id -Description "Step id"
    Assert-NonEmptyText -Value $step.status -Description "Step status"
    if (-not $stepIds.Add([string]$step.id))
    {
        throw "Step id is duplicated."
    }
    if ($null -ne $step.message)
    {
        Assert-NonEmptyText -Value $step.message -Description "Step message"
    }
}

foreach ($warning in @($report.warnings))
{
    Assert-NonEmptyText -Value $warning -Description "Warning"
}

Write-Host "Channel build report validated: status=$($report.status) exitCode=$ExpectedExitCode artifacts=$(@($report.artifacts).Count)"
