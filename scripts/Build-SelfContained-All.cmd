@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-SelfContained.ps1" %*
