@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-SelfContained.ps1" %*
