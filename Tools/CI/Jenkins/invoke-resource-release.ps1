[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][long]$MinimumClientBuild,
    [Parameter(Mandatory = $true)][long]$MaximumClientBuild,
    [Parameter(Mandatory = $true)][string]$Region,
    [Parameter(Mandatory = $true)][string]$Bucket,
    [Parameter(Mandatory = $true)][string]$ResultPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

foreach ($name in @("GDK_COS_SECRET_ID", "GDK_COS_SECRET_KEY"))
{
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name)))
    {
        throw "Required COS credential environment variable is missing."
    }
}

$project = Join-Path $PSScriptRoot "..\..\ResourceRelease\src\GameDeveloperKit.ResourceRelease.Cli\GameDeveloperKit.ResourceRelease.Cli.csproj"
& dotnet run --project $project --configuration Release -- `
    stage `
    --report $ReportPath `
    --output-root $OutputRoot `
    --minimum-client-build $MinimumClientBuild `
    --maximum-client-build $MaximumClientBuild `
    --region $Region `
    --bucket $Bucket `
    --result $ResultPath
exit $LASTEXITCODE
