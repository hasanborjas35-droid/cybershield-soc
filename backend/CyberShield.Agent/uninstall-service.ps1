# CyberShield Monitoring Agent — Windows Service Uninstaller
# Run as Administrator in PowerShell

$ServiceName = "CyberShieldAgent"

Write-Host "=== CyberShield Agent — Service Uninstaller ===" -ForegroundColor Cyan

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName
    Write-Host "✅ Service removed." -ForegroundColor Green
} else {
    Write-Host "Service '$ServiceName' not found." -ForegroundColor Yellow
}
