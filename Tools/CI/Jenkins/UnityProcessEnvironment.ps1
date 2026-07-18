function Initialize-UnityProcessEnvironment
{
    if (-not [string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE))
    {
        return
    }

    if ([string]::IsNullOrWhiteSpace($env:ProgramData))
    {
        throw "ProgramData must be available when ALLUSERSPROFILE is missing."
    }

    $env:ALLUSERSPROFILE = $env:ProgramData
}
