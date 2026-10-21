param(
    [ValidateSet("win-x64", "win-x86", "win-arm64", "linux-x64")]
    [string[]]$Rids = @("win-x64", "win-x86", "win-arm64", "linux-x64"),
    [string]$Configuration = "Release",
    [switch]$SelfContained,
    [switch]$SkipBuild,
    [switch]$NoPause
)

. (Join-Path $PSScriptRoot "_Common.ps1")

$OutputRoot = Join-Path $ProjectRoot "Publish"

function Get-PublishArgs {
    param([string]$Rid)
    $selfContainedValue = if ($SelfContained) { "true" } else { "false" }
    $singleFile = if ($SelfContained) { "true" } else { "false" }
    return @(
        "--configuration", $Configuration,
        "--runtime", $Rid,
        "--self-contained", $selfContainedValue,
        "-p:PublishSingleFile=$singleFile",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:SkipPostPublish=true",
        "--nologo"
    )
}

function Get-PublishDirName {
    param([string]$Rid)
    if ($SelfContained) { return "ImageMap_SelfContained_$Rid" }
    return "ImageMap_$Rid"
}

# Each release only needs the leveldb native library that matches its architecture.
function Get-NativeLibraryName {
    param([string]$Rid)
    switch ($Rid) {
        "win-x64" { return "leveldb_mcpe_win_amd64.dll" }
        "win-x86" { return "leveldb_mcpe_win32.dll" }
        "win-arm64" { return "leveldb_mcpe_win_arm64.dll" }
        "linux-x64" { return "leveldb_mcpe_linux_x86_64.so" }
        default { return $null }
    }
}

function Remove-ForeignNatives {
    param([string]$Dir, [string]$Keep)
    if (-not $Keep) { return }
    $removed = 0
    Get-ChildItem -LiteralPath $Dir -File -Filter "leveldb_*" -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -ne $Keep) {
            Remove-Item -LiteralPath $_.FullName -Force
            $removed++
        }
    }
    if ($removed -gt 0) {
        Write-Host "  Removed $removed native libraries for other architectures."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Dir $Keep))) {
        Write-Warn "  WARNING: expected native library '$Keep' was not found!"
    }
}

function Publish-Rid {
    param([string]$Rid)

    $isWindows = $Rid -like "win-*"
    $outDir = Join-Path $OutputRoot (Get-PublishDirName $Rid)

    if (Test-Path -LiteralPath $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    if ($isWindows) {
        Write-Host "  Building GUI  (ImageMap.exe)..."
        & dotnet publish $GuiProject @(Get-PublishArgs $Rid) -o $outDir
        if ($LASTEXITCODE -ne 0) { throw "Publishing the GUI for $Rid failed (exit code $LASTEXITCODE)." }
    }

    $cmdName = if ($isWindows) { "ImageMap-cmd.exe" } else { "ImageMap-cmd" }
    Write-Host "  Building CLI  ($cmdName)..."
    & dotnet publish $CmdProject @(Get-PublishArgs $Rid) -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publishing the CLI for $Rid failed (exit code $LASTEXITCODE)." }

    Remove-ForeignNatives -Dir $outDir -Keep (Get-NativeLibraryName $Rid)
}

$failed = $false
try {
    Write-Step "Checking prerequisites..."
    $problems = Test-DotNetPrerequisites
    if ($problems.Count -gt 0) {
        Show-PrerequisiteHelp -Problems $problems
        throw "Prerequisites are missing."
    }
    Write-Ok "Prerequisites OK."

    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

    foreach ($rid in $Rids) {
        Write-Step ""
        Write-Step "=== Publishing $rid ==="
        if ($SkipBuild) {
            Write-Warn "  -SkipBuild set, skipping compilation for $rid"
        }
        else {
            Publish-Rid -Rid $rid
        }
        Write-Ok "OK: $rid  ->  $(Join-Path $OutputRoot (Get-PublishDirName $rid))"
    }

    Write-Step ""
    if (-not $SelfContained) {
        Write-Host "This build requires the .NET 6 runtime (and the Desktop Runtime for the GUI) on the target machine."
    }
    Write-Ok "Done. Published to: $OutputRoot"
}
catch {
    if ($_.Exception.Message -ne "Prerequisites are missing.") {
        Write-Fail ""
        Write-Fail "BUILD FAILED: $($_.Exception.Message)"
        Write-Host "If the error mentions NuGet / downloading packages, check your internet connection:"
        Write-Host "the runtime packs for self-contained builds are downloaded on the first build."
        Write-Host "If the problem is about Windows Desktop / WPF components or the SDK, install the"
        Write-Host "'.NET desktop development' workload in the Visual Studio Installer and try again."
    }
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}
