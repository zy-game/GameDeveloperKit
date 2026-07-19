[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Channel,
    [Parameter(Mandatory = $true)][string]$Platform,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Region,
    [Parameter(Mandatory = $true)][string]$Bucket,
    [Parameter(Mandatory = $true)][string]$KeyId,
    [Parameter(Mandatory = $true)][string]$SigningKeyFile,
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
if (-not [System.IO.File]::Exists($SigningKeyFile))
{
    throw "Signing key credential file does not exist."
}

$project = Join-Path $PSScriptRoot "..\..\ResourceRelease\src\GameDeveloperKit.ResourceRelease.Cli\GameDeveloperKit.ResourceRelease.Cli.csproj"
& dotnet run --project $project --configuration Release -- `
    promote `
    --channel $Channel `
    --platform $Platform `
    --version $Version `
    --region $Region `
    --bucket $Bucket `
    --key-id $KeyId `
    --signing-key-file $SigningKeyFile `
    --result $ResultPath
exit $LASTEXITCODE
