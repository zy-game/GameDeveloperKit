[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$guidePath = Join-Path $RepositoryRoot "Documentation~\jenkins-channel-build-operations.md"
$guide = Get-Content -LiteralPath $guidePath -Raw
$main = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile") -Raw
$rollback = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile.rollback") -Raw

$requiredGuideText = @(
    "status: current",
    "unity-windows",
    "UNITY_EDITOR_PATH",
    "Declarative Pipeline",
    "Timestamper",
    "GDK_COS_REGION",
    "GDK_COS_BUCKET",
    "GDK_COS_CREDENTIAL_ID",
    "GDK_RESOURCE_SIGNING_CREDENTIAL_ID",
    "GDK_RESOURCE_SIGNING_KEY_ID",
    "RUN_PLAYER_BUILD",
    "PUBLISH_RESOURCES",
    "PROMOTE_RESOURCES",
    "MINIMUM_CLIENT_BUILD",
    "MAXIMUM_CLIENT_BUILD",
    "Jenkinsfile.rollback",
    "If-Match",
    "If-None-Match: *",
    "旧 Publisher 废弃 Gate",
    "本地fixture通过不等于真实Jenkins/COS验收通过"
)
foreach ($text in $requiredGuideText)
{
    if (-not $guide.Contains($text)) { throw "Jenkins operations guide is missing required content." }
}

foreach ($parameter in @(
    "RUN_PLAYER_BUILD", "PUBLISH_RESOURCES", "PROMOTE_RESOURCES",
    "MINIMUM_CLIENT_BUILD", "MAXIMUM_CLIENT_BUILD"))
{
    $documented = [char]96 + $parameter + [char]96
    if (-not $main.Contains("name: '$parameter'") -or -not $guide.Contains($documented))
    {
        throw "Jenkins parameter is not synchronized with the operations guide."
    }
}
foreach ($parameter in @("CHANNEL", "PLATFORM", "TARGET_VERSION"))
{
    $documented = [char]96 + $parameter + [char]96
    if (-not $rollback.Contains("name: '$parameter'") -or -not $guide.Contains($documented))
    {
        throw "Rollback parameter is not synchronized with the operations guide."
    }
}

if ($guide -match 'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY' -or
    $guide -match 'https?://[^\s]+jenkins' -or
    $guide.Contains("GDK_COS_SECRET_KEY="))
{
    throw "Jenkins operations guide contains credential or environment-specific material."
}

Write-Host "Jenkins operations guide contract validated."
