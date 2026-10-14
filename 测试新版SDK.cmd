@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-CadSdkCandidate.ps1"
if errorlevel 1 pause
