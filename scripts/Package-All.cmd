@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Package-All.ps1" %*
