[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$main = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile") -Raw
$rollback = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile.rollback") -Raw
$mainRequired = @(
    "booleanParam(name: 'PROMOTE_RESOURCES'",
    "stage('Promote Resource Pointer')",
    "PROMOTE_RESOURCES requires PUBLISH_RESOURCES",
    "input(",
    "credentialsId: env.GDK_RESOURCE_SIGNING_CREDENTIAL_ID",
    "variable: 'GDK_RESOURCE_SIGNING_KEY_FILE'",
    "invoke-resource-promotion.ps1",
    "Build/Channel/promotion-result.json"
)
$rollbackRequired = @(
    "string(name: 'CHANNEL'",
    "string(name: 'PLATFORM'",
    "string(name: 'TARGET_VERSION'",
    "stage('Approve Rollback')",
    "stage('Rollback Resource Pointer')",
    "input(",
    "invoke-resource-promotion.ps1",
    "Build/Channel/rollback-result.json"
)
foreach ($requirement in $mainRequired)
{
    if (-not $main.Contains($requirement)) { throw "Main promotion contract is incomplete." }
}
foreach ($requirement in $rollbackRequired)
{
    if (-not $rollback.Contains($requirement)) { throw "Rollback contract is incomplete." }
}
foreach ($content in @($main, $rollback))
{
    if ($content.Contains('$env:WORKSPACE\'))
    {
        throw "Groovy PowerShell strings must use forward-slash workspace paths."
    }
    if ($content.Contains("signature =") -or $content.Contains("publish.json"))
    {
        throw "Jenkins pipelines must delegate pointer JSON and signing to the release tool."
    }
}

Write-Host "Jenkins promotion and rollback contracts validated."
