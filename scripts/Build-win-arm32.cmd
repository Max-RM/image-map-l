@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-win-arm32.ps1" %*
