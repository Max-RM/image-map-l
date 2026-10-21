param(
    [ValidateSet("win-x64", "win-x86", "win-arm64", "linux-x64", "source")]
    [string[]]$Items = @("win-x64", "win-x86", "win-arm64", "linux-x64"),
    [switch]$SkipSelfContained,
    [switch]$NoPause
)

. (Join-Path $PSScriptRoot "_Common.ps1")

$PublishRoot = Join-Path $ProjectRoot "Publish"
$ArchiveRoot = Join-Path $ProjectRoot "Archives"

New-Item -ItemType Directory -Path $ArchiveRoot -Force | Out-Null

# Prefer 7-Zip (fast) when available on PATH, otherwise fall back to Compress-Archive (slow).
$SevenZip = $null
foreach ($candidate in @("7z", "7za")) {
    $cmd = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { $SevenZip = $cmd.Source; break }
}
if ($SevenZip) {
    Write-Ok "Using 7-Zip: $SevenZip"
}
else {
    Write-Warn "7-Zip not found on PATH, falling back to Compress-Archive (slower)."
}

function New-Zip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [string[]]$Exclude
    )

    if (Test-Path -LiteralPath $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }

    if ($SevenZip) {
        $sevenZipArgs = @("a", "-tzip", "-mx=5", "-y", $ArchivePath, ".\*")
        foreach ($pattern in @($Exclude)) {
            if ($pattern) { $sevenZipArgs += "-xr!$pattern" }
        }
        Push-Location -LiteralPath $SourceDir
        try {
            & $SevenZip @sevenZipArgs | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "7-Zip failed (exit code $LASTEXITCODE) for $SourceDir" }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Compress-Archive -Path "$SourceDir\*" -DestinationPath $ArchivePath -CompressionLevel Optimal
    }
}

function New-Archive {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ArchiveName
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) {
        Write-Warn "  SKIPPED (not found): $SourceDir"
        return
    }

    $archivePath = Join-Path $ArchiveRoot "$ArchiveName.zip"
    Write-Host "  Packing: $SourceDir -> $archivePath"
    New-Zip -SourceDir $SourceDir -ArchivePath $archivePath
}

function New-SourceArchive {
    $archivePath = Join-Path $ArchiveRoot "image-map-l-source.zip"
    $exclude = @(
        ".git", ".vs", ".idea",
        "Publish", "Archives",
        "bin", "obj",
        "*.user", "*.suo"
    )
    Write-Host "  Packing source: $ProjectRoot -> $archivePath"
    New-Zip -SourceDir $ProjectRoot -ArchivePath $archivePath -Exclude $exclude
}

$failed = $false
try {
    Write-Step "=== Packaging ==="

    if ($Items -contains "source") {
        Write-Host "  Source archive..."
        New-SourceArchive
    }

    foreach ($rid in @("win-x64", "win-x86", "win-arm64", "linux-x64")) {
        if ($Items -notcontains $rid) { continue }

        $srcDir = Join-Path $PublishRoot "ImageMap_$rid"
        New-Archive -SourceDir $srcDir -ArchiveName "ImageMap-$rid"

        if (-not $SkipSelfContained) {
            $selfContainedDir = Join-Path $PublishRoot "ImageMap_SelfContained_$rid"
            New-Archive -SourceDir $selfContainedDir -ArchiveName "ImageMap-SelfContained-$rid"
        }
    }

    Write-Ok ""
    Write-Ok "Done. Archives in: $ArchiveRoot"
}
catch {
    Write-Fail ""
    Write-Fail "PACKAGING FAILED: $($_.Exception.Message)"
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}
