param(
    [ValidateSet("ngdev", "ngstop", "ngapi", "ngweb", "help")]
    [string]$Command = "help"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ApiDir = Join-Path $RepoRoot "NeighborGoods.Api"
$WebDir = Join-Path $RepoRoot "NeighborGoods.Frontend"

# Default local dev ports
$ApiPorts = @(5065, 7233)
$WebPorts = @(5173)

function Start-NgApi {
    param([switch]$NewWindow)

    if ($NewWindow) {
        $cmd = "cd `"$ApiDir`"; dotnet watch run"
        Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd | Out-Null
        Write-Host "[ngapi] API started in new window: $ApiDir"
        return
    }

    Set-Location $ApiDir
    Write-Host "[ngapi] Starting API: dotnet watch run"
    dotnet watch run
}

function Start-NgWeb {
    Set-Location $WebDir
    Write-Host "[ngweb] Starting Frontend: npm run dev"
    npm run dev
}

function Stop-Ports {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$Ports,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $killedAny = $false
    foreach ($port in $Ports) {
        $pids = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique

        if (-not $pids) {
            Write-Host "[$Label] No listener on port $port"
            continue
        }

        foreach ($pid in $pids) {
            try {
                $proc = Get-Process -Id $pid -ErrorAction Stop
                Stop-Process -Id $pid -Force -ErrorAction Stop
                Write-Host "[$Label] Stopped PID $pid ($($proc.ProcessName)) on port $port"
                $killedAny = $true
            } catch {
                Write-Warning "[$Label] Failed to stop PID $pid on port ${port}: $($_.Exception.Message)"
            }
        }
    }

    return $killedAny
}

function Stop-NgDev {
    $apiStopped = Stop-Ports -Ports $ApiPorts -Label "ngstop-api"
    $webStopped = Stop-Ports -Ports $WebPorts -Label "ngstop-web"

    if (-not $apiStopped -and -not $webStopped) {
        Write-Host "[ngstop] No API/Frontend process found to stop"
    } else {
        Write-Host "[ngstop] Stop completed"
    }
}

function Show-Help {
    $scriptPath = $PSCommandPath
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\start-all.ps1 ngdev   # API in new window + Frontend in current window"
    Write-Host "  .\start-all.ps1 ngapi   # Start API only (current window)"
    Write-Host "  .\start-all.ps1 ngweb   # Start Frontend only (current window)"
    Write-Host "  .\start-all.ps1 ngstop  # Stop API + Frontend by ports"
    Write-Host ""
    Write-Host "To use direct commands, paste these into `$PROFILE:"
    Write-Host ('function ngdev  { & "' + $scriptPath + '" ngdev }')
    Write-Host ('function ngapi  { & "' + $scriptPath + '" ngapi }')
    Write-Host ('function ngweb  { & "' + $scriptPath + '" ngweb }')
    Write-Host ('function ngstop { & "' + $scriptPath + '" ngstop }')
    Write-Host ""
}

switch ($Command) {
    "ngdev" {
        Start-NgApi -NewWindow
        Start-Sleep -Seconds 1
        Start-NgWeb
    }
    "ngapi" { Start-NgApi }
    "ngweb" { Start-NgWeb }
    "ngstop" { Stop-NgDev }
    default { Show-Help }
}