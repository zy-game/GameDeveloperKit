[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $RepositoryRoot "Tools\CI\Jenkins\UnityProcessEnvironment.ps1")

$originalAllUsersProfile = $env:ALLUSERSPROFILE
$originalProgramData = $env:ProgramData
try
{
    $env:ALLUSERSPROFILE = $null
    $env:ProgramData = "C:\ProgramData"
    Initialize-UnityProcessEnvironment
    if ($env:ALLUSERSPROFILE -cne $env:ProgramData)
    {
        throw "Unity process environment did not restore ALLUSERSPROFILE."
    }

    $env:ALLUSERSPROFILE = $null
    $env:ProgramData = $null
    try
    {
        Initialize-UnityProcessEnvironment
        throw "Missing ProgramData was accepted."
    }
    catch
    {
        if ($_.Exception.Message -eq "Missing ProgramData was accepted.")
        {
            throw
        }
    }
}
finally
{
    $env:ALLUSERSPROFILE = $originalAllUsersProfile
    $env:ProgramData = $originalProgramData
}

Write-Host "Unity process environment contract validated."
