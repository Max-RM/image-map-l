# .NET Framework WinForms ARM32 (Surface RT / Windows 10 ARM).
# Builds ImageMap4.WinForms into Publish\ImageMap_win-arm32.
& (Join-Path (Split-Path -Parent $PSScriptRoot) "ImageMap4.WinForms\build-arm32.ps1") @args
