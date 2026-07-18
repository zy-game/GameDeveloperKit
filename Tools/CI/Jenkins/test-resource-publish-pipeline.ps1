[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$jenkinsfile = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile") -Raw
$required = @(
    "booleanParam(name: 'PUBLISH_RESOURCES'",
    "stage('Publish Immutable Resources')",
    "expression { return params.PUBLISH_RESOURCES }",
    "withCredentials([usernamePassword(",
    "credentialsId: env.GDK_COS_CREDENTIAL_ID",
    "usernameVariable: 'GDK_COS_SECRET_ID'",
    "passwordVariable: 'GDK_COS_SECRET_KEY'",
    "invoke-resource-release.ps1",
    "Build/Channel/staged-release.json"
)
foreach ($text in $required)
{
    if (-not $jenkinsfile.Contains($text))
    {
        throw "Jenkins resource publish contract is missing required text."
    }
}
if ($jenkinsfile.Contains("GDK_RESOURCE_SIGNING_KEY") -or $jenkinsfile.Contains("PutTextConditional"))
{
    throw "Publish pipeline must not receive signing material or write a pointer."
}

$publishIndex = $jenkinsfile.IndexOf("stage('Publish Immutable Resources')", [StringComparison]::Ordinal)
$credentialIndex = $jenkinsfile.IndexOf("withCredentials([usernamePassword(", [StringComparison]::Ordinal)
if ($credentialIndex -lt $publishIndex)
{
    throw "COS credentials are outside the optional publish stage."
}

Write-Host "Jenkins resource publish contract validated."
