Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root         = $PSScriptRoot
$ApiDir       = Join-Path $Root "backend\CyberShield.Api"
$AgentDir     = Join-Path $Root "backend\CyberShield.Agent"
$FrontendDir  = Join-Path $Root "public"
$LogDir       = Join-Path $Root "logs"
$ApiPort      = 5080
$FrontendPort = 3000
$Ts           = Get-Date -Format "yyyyMMdd_HHmmss"
$ApiLog       = Join-Path $LogDir "api_$Ts.log"
$AgentLog     = Join-Path $LogDir "agent_$Ts.log"
$FrontLog     = Join-Path $LogDir "frontend_$Ts.log"
$LaunchLog    = Join-Path $LogDir "launcher_$Ts.log"
$SqlSvc       = "MSSQL`$SQLEXPRESS02"

if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }

function Log {
    param([string]$Msg, [string]$Col = 'Cyan')
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $Msg"
    Write-Host $line -ForegroundColor $Col
    Add-Content -Path $LaunchLog -Value $line -ErrorAction SilentlyContinue
}
function OK   { param([string]$m) Log "  [OK] $m" 'Green' }
function ERR  { param([string]$m) Log " [ERR] $m" 'Red' }
function WARN { param([string]$m) Log "[WARN] $m" 'Yellow' }

Write-Host ""
Write-Host "  =====================================================" -ForegroundColor Cyan
Write-Host "    CyberShield SOC - Starting..." -ForegroundColor Cyan
Write-Host "  =====================================================" -ForegroundColor Cyan
Write-Host ""
Log "Root: $Root"

# Step 1 - Check tools
Log "Step 1/6: Checking tools..."
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { ERR "dotnet not found!"; exit 1 }
OK "dotnet: $($dotnet.Source)"

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { ERR "python not found!"; exit 1 }
$pyOk = & python -c "print('ok')" 2>&1
if ($pyOk -ne 'ok') { ERR "python not working: $pyOk"; exit 1 }
OK "python OK"

if (-not (Test-Path "$ApiDir\CyberShield.Api.csproj")) { ERR "API not found: $ApiDir"; exit 1 }
OK "API project found"

# Step 2 - SQL Server
Log "Step 2/6: Checking SQL Server..."
$svc = Get-Service -Name $SqlSvc -ErrorAction SilentlyContinue
if (-not $svc) { ERR "SQL Server $SqlSvc not found!"; exit 1 }
if ($svc.Status -ne 'Running') {
    WARN "SQL Server stopped - starting..."
    Start-Service -Name $SqlSvc
    Start-Sleep -Seconds 4
    $svc.Refresh()
    if ($svc.Status -ne 'Running') { ERR "SQL Server failed to start"; exit 1 }
}
OK "SQL Server: $($svc.Status)"

# Step 3 - Free port 5080 (only stop dotnet processes)
Log "Step 3/6: Checking port $ApiPort..."
$conns = Get-NetTCPConnection -LocalPort $ApiPort -ErrorAction SilentlyContinue
foreach ($conn in $conns) {
    $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -eq 'dotnet') {
        WARN "Stopping old dotnet on port $ApiPort (PID $($proc.Id))"
        Stop-Process -Id $proc.Id -Force
        Start-Sleep -Seconds 2
    }
}
OK "Port $ApiPort ready"

# Step 4 - Build API
Log "Step 4/6: Building API..."
$build = & dotnet build "$ApiDir\CyberShield.Api.csproj" -c Debug --nologo -v quiet 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { ERR "Build failed: $build"; exit 1 }
OK "Build succeeded"

# Start API
Log "  Starting API..."
$apiJob = Start-Job -ScriptBlock {
    param($d, $l)
    Set-Location $d
    & dotnet run --no-build 2>&1 | Tee-Object -FilePath $l
} -ArgumentList $ApiDir, $ApiLog

# Wait for health
Log "  Waiting for API health check (max 60s)..."
$ready = $false
for ($i = 1; $i -le 60; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest "http://localhost:$ApiPort/health" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; OK "API READY after ${i}s"; break }
    } catch { }
    if ($i % 15 -eq 0) { Log "  Still waiting... (${i}s)" }
}
if (-not $ready) {
    ERR "API not ready after 60s. Check log: $ApiLog"
    Receive-Job $apiJob | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    exit 1
}

# Step 5 - Agent
Log "Step 5/6: Checking Agent..."
$agentSvc = Get-Service -Name "CyberShieldAgent" -ErrorAction SilentlyContinue
if ($agentSvc) {
    if ($agentSvc.Status -ne 'Running') {
        Start-Service "CyberShieldAgent" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    OK "Agent service: $($agentSvc.Status)"
} else {
    WARN "Agent not installed as service - starting as process..."
    if (Test-Path "$AgentDir\CyberShield.Agent.csproj") {
        $agentJob = Start-Job -ScriptBlock {
            param($d, $l)
            Set-Location $d
            & dotnet run 2>&1 | Tee-Object -FilePath $l
        } -ArgumentList $AgentDir, $AgentLog
        OK "Agent started as job (log: $AgentLog)"
    } else {
        WARN "Agent project not found - skipping"
    }
}

# Step 6 - Frontend
Log "Step 6/6: Starting Frontend on port $FrontendPort..."
$fp = Get-NetTCPConnection -LocalPort $FrontendPort -ErrorAction SilentlyContinue
if ($fp) {
    $fpProc = Get-Process -Id $fp.OwningProcess -ErrorAction SilentlyContinue
    if ($fpProc -and $fpProc.ProcessName -eq 'python') {
        Stop-Process -Id $fpProc.Id -Force
        Start-Sleep -Seconds 1
    }
}
$frontJob = Start-Job -ScriptBlock {
    param($d, $p, $l)
    Set-Location $d
    & python -m http.server $p 2>&1 | Tee-Object -FilePath $l
} -ArgumentList $FrontendDir, $FrontendPort, $FrontLog

Start-Sleep -Seconds 2
try {
    $fr = Invoke-WebRequest "http://localhost:$FrontendPort" -UseBasicParsing -TimeoutSec 5
    OK "Frontend READY (HTTP $($fr.StatusCode))"
} catch {
    WARN "Frontend may not be ready yet: $_"
}

# Save job IDs
@{ ApiJobId = $apiJob.Id; FrontJobId = $frontJob.Id; Ts = $Ts } |
    ConvertTo-Json | Set-Content (Join-Path $LogDir "running_jobs.json")

# Summary
Write-Host ""
Write-Host "  =====================================================" -ForegroundColor Green
Write-Host "   CyberShield SOC is RUNNING!" -ForegroundColor Green
Write-Host "  =====================================================" -ForegroundColor Green
Write-Host ""
Write-Host "   Frontend : http://localhost:$FrontendPort" -ForegroundColor White
Write-Host "   API      : http://localhost:$ApiPort" -ForegroundColor White
Write-Host "   Swagger  : http://localhost:$ApiPort/swagger" -ForegroundColor White
Write-Host ""
Write-Host "   Login    : m.vance@cybershield.internal" -ForegroundColor Yellow
Write-Host "   Password : ChangeMe!123" -ForegroundColor Yellow
Write-Host ""
Write-Host "   Logs     : $LogDir" -ForegroundColor Gray
Write-Host "   Stop     : run Stop-CyberShield.bat" -ForegroundColor Cyan
Write-Host ""

Start-Process "http://localhost:$FrontendPort"

Log "System running. Press Ctrl+C to stop."
while ($true) {
    Start-Sleep -Seconds 10
    Receive-Job $apiJob -ErrorAction SilentlyContinue | ForEach-Object { Add-Content $ApiLog $_ }
    Receive-Job $frontJob -ErrorAction SilentlyContinue | ForEach-Object { Add-Content $FrontLog $_ }
}
