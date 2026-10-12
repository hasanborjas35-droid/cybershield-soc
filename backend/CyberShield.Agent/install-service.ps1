# CyberShield Monitoring Agent - Windows Service Installer
# Run as Administrator

$ServiceName = "CyberShieldAgent"
$DisplayName = "CyberShield Monitoring Agent"
$Description = "Collects real-time system health, network, and security telemetry."
$PublishDir  = Join-Path $PSScriptRoot "publish"
$ExePath     = Join-Path $PublishDir "CyberShield.Agent.exe"

Write-Host "=== CyberShield Agent - Service Installer ===" -ForegroundColor Cyan

# Step 1: Publish
Write-Host "[1/3] Publishing agent..." -ForegroundColor Yellow
dotnet publish (Join-Path $PSScriptRoot "CyberShield.Agent.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $PublishDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed."
    exit 1
}

# Step 2: Remove old service
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "[2/3] Removing old service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Step 3: Install
Write-Host "[3/3] Installing service..." -ForegroundColor Yellow
New-Service -Name $ServiceName `
            -BinaryPathName $ExePath `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic

Start-Service -Name $ServiceName

$svc = Get-Service -Name $ServiceName
Write-Host "Service installed. Status: $($svc.Status)" -ForegroundColor Green
Write-Host "Name: $ServiceName"
Write-Host "Exe:  $ExePath"
