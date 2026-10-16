Set-StrictMode -Version Latest
$Root    = $PSScriptRoot
$LogDir  = Join-Path $Root "logs"
$JobFile = Join-Path $LogDir "running_jobs.json"

Write-Host ""
Write-Host "  =====================================================" -ForegroundColor Red
Write-Host "   CyberShield SOC - Stopping" -ForegroundColor Red
Write-Host "  =====================================================" -ForegroundColor Red
Write-Host ""

if (Test-Path $JobFile) {
    $jobs = Get-Content $JobFile | ConvertFrom-Json
    if ($jobs.ApiJobId) {
        Stop-Job -Id $jobs.ApiJobId -ErrorAction SilentlyContinue
        Remove-Job -Id $jobs.ApiJobId -ErrorAction SilentlyContinue
        Write-Host "  [STOPPED] API job $($jobs.ApiJobId)" -ForegroundColor Green
    }
    if ($jobs.FrontJobId) {
        Stop-Job -Id $jobs.FrontJobId -ErrorAction SilentlyContinue
        Remove-Job -Id $jobs.FrontJobId -ErrorAction SilentlyContinue
        Write-Host "  [STOPPED] Frontend job $($jobs.FrontJobId)" -ForegroundColor Green
    }
    Remove-Item $JobFile -Force
} else {
    Write-Host "  No job file found - scanning ports..." -ForegroundColor Yellow
}

# Stop dotnet on 5080
$c5080 = Get-NetTCPConnection -LocalPort 5080 -ErrorAction SilentlyContinue
if ($c5080) {
    $p = Get-Process -Id $c5080.OwningProcess -ErrorAction SilentlyContinue
    if ($p -and $p.ProcessName -eq 'dotnet') {
        Stop-Process -Id $p.Id -Force
        Write-Host "  [STOPPED] dotnet API (PID $($p.Id))" -ForegroundColor Green
    }
}

# Stop python on 3000
$c3000 = Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue
if ($c3000) {
    $p = Get-Process -Id $c3000.OwningProcess -ErrorAction SilentlyContinue
    if ($p -and $p.ProcessName -eq 'python') {
        Stop-Process -Id $p.Id -Force
        Write-Host "  [STOPPED] Python frontend (PID $($p.Id))" -ForegroundColor Green
    }
}

$agent = Get-Service "CyberShieldAgent" -ErrorAction SilentlyContinue
if ($agent) {
    Write-Host "  [INFO]    Agent service left running" -ForegroundColor Cyan
    Write-Host "            To stop: net stop CyberShieldAgent" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Done." -ForegroundColor Green
Write-Host ""
