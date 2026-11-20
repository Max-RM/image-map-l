# Local x64/AnyCPU test build (this machine). Output: artifacts\x64-test
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "src\ImageMap.WinForms\ImageMap.WinForms.csproj"
$out = Join-Path $root "src\ImageMap.WinForms\bin\Release\net472"
$dest = Join-Path $root "artifacts\x64-test"

dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $out "*") $dest -Recurse -Force
Write-Host "Built: $(Join-Path $dest 'ImageMap.exe')"
Write-Host "Crash log will be written next to the exe as crash.log"
