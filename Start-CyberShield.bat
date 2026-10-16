@echo off
title CyberShield SOC — Launcher
color 0A
echo.
echo  Starting CyberShield SOC...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-CyberShield.ps1"
pause
