param(
    [Parameter(Mandatory=$true)]
    [string]$Command,
    
    [Parameter(Mandatory=$false)]
    [string]$Arguments = "{}",
    
    [Parameter(Mandatory=$false)]
    [string]$WorkingDirectory = "",
    
    [Parameter(Mandatory=$false)]
    [int]$Timeout = 30
)

$ErrorActionPreference = "Stop"

function CleanupOldTasks {
    param(
        [string]$CommandsDir,
        [int]$KeepCount = 20
    )
    
    try {
        $files = Get-ChildItem -Path $CommandsDir -Filter "*.json" -ErrorAction SilentlyContinue
        if ($null -eq $files -or $files.Count -le $KeepCount) {
            return
        }
        
        $completedTasks = @()
        foreach ($file in $files) {
            try {
                $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($content.status -eq "completed" -or $content.status -eq "failed") {
                    $completedTasks += @{
                        File = $file
                        CreatedAt = $content.created_at
                    }
                }
            } catch { }
        }
        
        if ($completedTasks.Count -gt $KeepCount) {
            $sortedTasks = $completedTasks | Sort-Object { $_.CreatedAt }
            $toDelete = $completedTasks.Count - $KeepCount
            
            for ($i = 0; $i -lt $toDelete; $i++) {
                Remove-Item -Path $sortedTasks[$i].File.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    } catch { }
}

if ($WorkingDirectory -eq "") {
    $WorkingDirectory = Get-Location
}

$commandsDir = Join-Path $WorkingDirectory "Library\CLI\commands"

if (-not (Test-Path $commandsDir)) {
    New-Item -ItemType Directory -Path $commandsDir -Force | Out-Null
}

$id = [guid]::NewGuid().ToString()
$cmdFile = Join-Path $commandsDir "$id.json"

try {
    $argsObj = $Arguments | ConvertFrom-Json
} catch {
    $argsObj = @{}
}

$commandData = @{
    id = $id
    command = $Command
    arguments = $argsObj
    status = "pending"
    created_at = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    result = $null
    error = $null
}

$commandData | ConvertTo-Json -Depth 10 | Set-Content -Path $cmdFile -Encoding UTF8

for ($i = 0; $i -lt $Timeout; $i++) {
    Start-Sleep -Milliseconds 500
    
    try {
        $data = Get-Content -Path $cmdFile -Raw -Encoding UTF8 | ConvertFrom-Json
        
        if ($data.status -eq "completed") {
            if ($null -ne $data.result) {
                Write-Output $data.result
            } else {
                Write-Output '{"success":true}'
            }
            CleanupOldTasks -CommandsDir $commandsDir -KeepCount 20
            exit 0
        }
        
        if ($data.status -eq "failed") {
            $errorMsg = if ($null -ne $data.error) { $data.error } else { "Unknown error" }
            $errorResult = @{
                success = $false
                error = $errorMsg
            } | ConvertTo-Json -Compress
            Write-Output $errorResult
            CleanupOldTasks -CommandsDir $commandsDir -KeepCount 20
            exit 0
        }
    } catch {
    }
}

$timeoutResult = @{
    success = $false
    error = "Command timeout after $Timeout seconds"
} | ConvertTo-Json -Compress
Write-Output $timeoutResult
exit 0
