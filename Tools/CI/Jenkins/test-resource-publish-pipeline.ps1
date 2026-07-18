[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$jenkinsfile = Get-Content -LiteralPath (Join-Path $RepositoryRoot "Jenkinsfile") -Raw
if ($jenkinsfile.Contains('$env:WORKSPACE\'))
{
    throw "Groovy PowerShell strings must use forward-slash workspace paths."
}
$required = @(
    "booleanParam(name: 'PUBLISH_RESOURCES'",
    "stage('Prepare Workspace')",
    "[char[]]@(",
    "Channel output root escapes the Jenkins workspace.",
    "[System.IO.Directory]::Delete",
    "stage('Publish Immutable Resources')",
    "expression { return params.PUBLISH_RESOURCES }",
    "withCredentials([usernamePassword(",
    "credentialsId: env.GDK_COS_CREDENTIAL_ID",
    "usernameVariable: 'GDK_COS_SECRET_ID'",
    "passwordVariable: 'GDK_COS_SECRET_KEY'",
    "invoke-resource-release.ps1",
    "Build/Channel/staged-release.json",
    "Build/Channel/resources/**/*"
)
foreach ($text in $required)
{
    if (-not $jenkinsfile.Contains($text))
    {
        throw "Jenkins resource publish contract is missing required text."
    }
}
$publishIndex = $jenkinsfile.IndexOf("stage('Publish Immutable Resources')", [StringComparison]::Ordinal)
$prepareIndex = $jenkinsfile.IndexOf("stage('Prepare Workspace')", [StringComparison]::Ordinal)
$qualityIndex = $jenkinsfile.IndexOf("stage('Local Quality Gate')", [StringComparison]::Ordinal)
$promoteIndex = $jenkinsfile.IndexOf("stage('Promote Resource Pointer')", [StringComparison]::Ordinal)
$credentialIndex = $jenkinsfile.IndexOf("withCredentials([usernamePassword(", [StringComparison]::Ordinal)
if ($credentialIndex -lt $publishIndex -or $promoteIndex -le $publishIndex)
{
    throw "COS credentials are outside the optional publish stage."
}
if ($prepareIndex -lt 0 -or $qualityIndex -le $prepareIndex)
{
    throw "Channel output cleanup must run before the quality gate."
}
$publishStage = $jenkinsfile.Substring($publishIndex, $promoteIndex - $publishIndex)
if ($publishStage.Contains("GDK_RESOURCE_SIGNING_KEY") -or $publishStage.Contains("PutTextConditional"))
{
    throw "Publish stage must not receive signing material or write a pointer."
}

Write-Host "Jenkins resource publish contract validated."
