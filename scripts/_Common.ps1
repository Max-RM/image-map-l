# Common helpers shared by the build scripts in this folder.
# Dot-source this file:  . (Join-Path $PSScriptRoot "_Common.ps1")

$ErrorActionPreference = "Stop"

$Script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$Script:SolutionFile = Join-Path $Script:ProjectRoot "ImageMap4.sln"
$Script:GuiProject = Join-Path $Script:ProjectRoot "ImageMap4\ImageMap4.csproj"
$Script:CmdProject = Join-Path $Script:ProjectRoot "ImageMap4.CMD\ImageMap4.CMD.csproj"

function Write-Step {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Red
}

# Keeps the console window open so the user can read the output.
# Skipped when -NoPause is passed (useful for CI).
function Wait-ForExit {
    param([switch]$NoPause)
    if ($NoPause) { return }
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor DarkGray
    try {
        $null = [System.Console]::ReadKey($true)
    }
    catch {
        try { $null = Read-Host }
        catch { Start-Sleep -Seconds 3 }
    }
}

# Returns a list of human readable problems. Empty list means everything is ready.
function Test-DotNetPrerequisites {
    $problems = New-Object System.Collections.Generic.List[string]

    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        $problems.Add("The 'dotnet' CLI was not found on PATH.")
        return $problems
    }

    $sdks = @()
    try { $sdks = @(& dotnet --list-sdks 2>$null) } catch { }
    if (-not $sdks -or $sdks.Count -eq 0) {
        $problems.Add("No .NET SDK is installed.")
    }
    else {
        $hasSupported = $false
        foreach ($line in $sdks) {
            if ($line -match '^(?<major>\d+)\.') {
                if ([int]$Matches['major'] -ge 6) { $hasSupported = $true }
            }
        }
        if (-not $hasSupported) {
            $problems.Add(".NET SDK 6.0 or newer is required (found: $($sdks -join '; ')).")
        }
    }

    # The WPF GUI (ImageMap4) needs the Windows Desktop / WPF components.
    if ($env:OS -eq "Windows_NT") {
        $runtimes = @()
        try { $runtimes = @(& dotnet --list-runtimes 2>$null) } catch { }
        $hasDesktop = $false
        foreach ($line in $runtimes) {
            if ($line -match 'Microsoft\.WindowsDesktop\.App') { $hasDesktop = $true }
        }
        if (-not $hasDesktop) {
            $problems.Add("The 'Microsoft.WindowsDesktop.App' components are missing (required by the WPF GUI).")
        }
    }

    return $problems
}

function Show-PrerequisiteHelp {
    param($Problems)

    Write-Fail ""
    Write-Fail "Cannot start the build because some required components are missing:"
    foreach ($p in $Problems) { Write-Fail "  - $p" }
    Write-Host ""
    Write-Warn "How to fix:"
    Write-Host "  1. Install the .NET SDK 6.0 or newer: https://dotnet.microsoft.com/download"
    Write-Host "  2. If you use Visual Studio, open the Visual Studio Installer, click Modify and"
    Write-Host "     enable the workload: .NET desktop development"
    Write-Host "     (this provides the Windows Desktop / WPF components)."
    Write-Host "  3. Make sure the git submodules are present:"
    Write-Host "       git submodule update --init --recursive"
}
