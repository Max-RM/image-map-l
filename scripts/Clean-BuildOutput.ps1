param(
    [switch]$IncludeArchives,
    [switch]$NoPause
)

# Removes compile caches (obj/bin, including ARM32 platform folders) and
# the contents of Publish\ (the Publish folder itself is kept).
# Archives are kept unless -IncludeArchives is passed.

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

function Clear-FolderContents {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Write-Host "$Label did not exist; created empty folder."
        return
    }

    $items = @(Get-ChildItem -LiteralPath $Path -Force)
    if ($items.Count -eq 0) {
        Write-Host "$Label is already empty."
        return
    }

    foreach ($item in $items) {
        try {
            Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "  removed: $($item.FullName)"
        }
        catch {
            Write-Warn "  could not remove: $($item.FullName)"
        }
    }
}

$failed = $false
try {
    Write-Step "=== Cleaning build output ==="

    Write-Host "Removing compile caches (obj)..."
    Remove-Directories -Root $ProjectRoot -Filter "obj" -Label "obj"

    Write-Host "Removing build outputs (bin)..."
    Remove-Directories -Root $ProjectRoot -Filter "bin" -Label "bin"

    Write-Host "Removing ARM32 artifacts..."
    foreach ($armArtifacts in @(
            (Join-Path $ProjectRoot "ImageMap4.WinForms\artifacts"),
            (Join-Path $ProjectRoot "TEST\artifacts")
        )) {
        Remove-Folder -Path $armArtifacts -Label "ARM32 artifacts"
    }

    Write-Host "Clearing Publish contents (keeping the folder)..."
    Clear-FolderContents -Path (Join-Path $ProjectRoot "Publish") -Label "Publish"

    if ($IncludeArchives) {
        Write-Host "Clearing Archives contents (keeping the folder)..."
        Clear-FolderContents -Path (Join-Path $ProjectRoot "Archives") -Label "Archives"
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