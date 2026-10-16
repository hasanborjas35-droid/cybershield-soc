@echo off
title CyberShield SOC — Stop
color 0C
echo.
echo  Stopping CyberShield SOC...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-CyberShield.ps1"
pause
