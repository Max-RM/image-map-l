# ARM32 build for Windows RT / Surface RT (same path as MCCToolChestPE).
# Output: Publish\ImageMap_win-arm32
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $root
$proj = Join-Path $root "src\ImageMap.WinForms\ImageMap.WinForms.csproj"
$publishRoot = Join-Path $repoRoot "Publish"
$dest = Join-Path $publishRoot "ImageMap_win-arm32"

function Get-PeMachine([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    return [BitConverter]::ToUInt16($bytes, $pe + 4)
}

function Get-PeName([uint16]$machine) {
    switch ($machine) {
        0x014c { "I386" }
        0x8664 { "AMD64" }
        0x01c4 { "ARMNT" }
        0x01c0 { "ARM" }
        0xaa64 { "ARM64" }
        default { "0x{0:X4}" -f $machine }
    }
}

$msbuild = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if ($found) { $msbuild = $found }
}
if (-not $msbuild) {
    foreach ($candidate in @(
            (Join-Path ${env:ProgramFiles} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"),
            (Join-Path ${env:ProgramFiles} "Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"),
            (Join-Path ${env:ProgramFiles} "Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe")
        )) {
        if (Test-Path $candidate) { $msbuild = $candidate; break }
    }
}

dotnet restore $proj --force | Out-Host
if (Test-Path $msbuild) {
    Write-Host "Building ARM32 with MSBuild: $msbuild"
    & $msbuild $proj /t:Rebuild /p:Configuration=Release /p:Platform=ARM /restore /v:minimal
} else {
    Write-Host "MSBuild not found, falling back to dotnet build"
    dotnet build $proj -c Release -p:Platform=ARM -p:PlatformTarget=ARM
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$candidates = @(
    (Join-Path $root "src\ImageMap.WinForms\bin\ARM\Release\net472"),
    (Join-Path $root "src\ImageMap.WinForms\bin\Release\net472")
)
$out = $candidates | Where-Object { Test-Path (Join-Path $_ "ImageMap.exe") } | Select-Object -First 1
if (-not $out) {
    Write-Error "ImageMap.exe not found after ARM32 build"
    exit 1
}

$exe = Join-Path $out "ImageMap.exe"
$machine = Get-PeMachine $exe
Write-Host ("ImageMap.exe PE machine: {0} (0x{1:X4})" -f (Get-PeName $machine), $machine)
if ($machine -ne 0x01c4) {
    Write-Error "ARM32 build produced wrong architecture for ImageMap.exe: expected ARMNT (0x01C4), got $(Get-PeName $machine)."
    exit 1
}

$armLeveldb = Join-Path $root "..\level-db-wrapper\leveldb\leveldb_mcpe_win_arm.dll"
if (-not (Test-Path $armLeveldb) -or (Get-Item $armLeveldb).Length -lt 1024) {
    $armLeveldb = Join-Path $root "lib\level-db-wrapper\leveldb\leveldb_mcpe_win_arm.dll"
}
$mccLeveldb = "c:\Users\Max\Documents\GitHub\MCCToolChestPE\Modified\Src\LevelDB-MCPE-ARM32.dll"
if (-not (Test-Path $armLeveldb) -and (Test-Path $mccLeveldb)) { $armLeveldb = $mccLeveldb }
if (-not (Test-Path $armLeveldb)) {
    Write-Error "ARM32 LevelDB DLL not found"
    exit 1
}
$ldbMachine = Get-PeMachine $armLeveldb
if ($ldbMachine -ne 0x01c4) {
    Write-Error "leveldb ARM32 DLL is $(Get-PeName $ldbMachine), expected ARMNT"
    exit 1
}

$zlib = Join-Path $root "native\arm32\zlib.dll"
$mccZlib = "c:\Users\Max\Documents\GitHub\MCCToolChestPE\Modified\Src\zlib-arm32.dll"
if (-not (Test-Path $zlib) -and (Test-Path $mccZlib)) { $zlib = $mccZlib }
if (-not (Test-Path $zlib)) {
    Write-Error "ARM32 zlib.dll not found"
    exit 1
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
if (Test-Path $dest) {
    try { Remove-Item $dest -Recurse -Force -ErrorAction Stop }
    catch { Write-Host "Keeping existing Publish\ImageMap_win-arm32 (in use); overwriting files." }
}
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Get-ChildItem $out -File | Where-Object {
    $_.Name -notmatch '^leveldb_mcpe_' -and $_.Name -ne 'zlib.dll' -and $_.Name -ne 'leveldb.dll'
} | Copy-Item -Destination $dest -Force

Copy-Item $armLeveldb (Join-Path $dest "leveldb_mcpe_win_arm.dll") -Force
Copy-Item $armLeveldb (Join-Path $dest "leveldb.dll") -Force
Copy-Item $zlib (Join-Path $dest "zlib.dll") -Force

$facade = "${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll"
if ((Test-Path $facade) -and -not (Test-Path (Join-Path $dest "netstandard.dll"))) {
    Copy-Item $facade $dest -Force
}

Write-Host ""
Write-Host "ARM32 build succeeded."
Write-Host "Output: $(Join-Path $dest 'ImageMap.exe')"
Get-ChildItem $dest | Select-Object Name, Length | Format-Table -AutoSize
