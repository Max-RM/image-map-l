param(
    [switch]$IncludeArchives,
    [switch]$NoPause
)

# Removes .NET compile caches (obj) and build outputs (bin), plus the local
# Publish folder created by Publish-All.ps1. Archives are kept unless
# -IncludeArchives is passed.

. (Join-Path $PSScriptRoot "_Common.ps1")

function Remove-Directories {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Filter,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $targets = @(Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force -Filter $Filter -ErrorAction SilentlyContinue)
    if ($targets.Count -eq 0) {
        Write-Host "No $Label folders found."
        return
    }

    foreach ($dir in $targets) {
        try {
            Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "  removed: $($dir.FullName)"
        }
        catch {
            Write-Warn "  could not remove: $($dir.FullName)"
        }
    }
}

function Remove-Folder {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host "$Label not found, nothing to clean."
        return
    }

    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        Write-Host "  removed: $Path"
    }
    catch {
        Write-Warn "  could not remove: $Path"
    }
}

$failed = $false
try {
    Write-Step "=== Cleaning build output ==="

    Write-Host "Removing compile caches (obj)..."
    Remove-Directories -Root $ProjectRoot -Filter "obj" -Label "obj"

    Write-Host "Removing build outputs (bin)..."
    Remove-Directories -Root $ProjectRoot -Filter "bin" -Label "bin"

    Write-Host "Removing publish output..."
    Remove-Folder -Path (Join-Path $ProjectRoot "Publish") -Label "Publish"

    if ($IncludeArchives) {
        Write-Host "Removing archives..."
        Remove-Folder -Path (Join-Path $ProjectRoot "Archives") -Label "Archives"
    }
    else {
        Write-Host "Archives kept (pass -IncludeArchives to remove them)."
    }

    Write-Ok ""
    Write-Ok "Clean complete."
}
catch {
    Write-Fail ""
    Write-Fail "CLEAN FAILED: $($_.Exception.Message)"
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}