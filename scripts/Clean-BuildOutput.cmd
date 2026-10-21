@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Clean-BuildOutput.ps1" %*
