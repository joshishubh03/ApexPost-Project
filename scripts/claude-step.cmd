@echo off
cd /d "%~dp0.."
del /q claude-run.done 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0claude-task.ps1" > claude-run.log 2>&1
echo done> claude-run.done
