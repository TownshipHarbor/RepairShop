param([string]$RepairShop = '')
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$pack = Join-Path $here 'res\repairshop.pack'
$out  = Join-Path $here 'build'

if (-not $RepairShop -or -not (Test-Path $RepairShop)) { throw "download caleb's Repair Shop 0.2.30 release and pass its Client\RepairShop.dll with -RepairShop" }
New-Item -ItemType Directory -Force (Join-Path $here 'res') | Out-Null

& dotnet build (Join-Path $here 'tools\bake\Bake.csproj') -c Release -o (Join-Path $out 'bake') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'bake tool build failed' }
& dotnet (Join-Path $out 'bake\Bake.dll') pack $RepairShop $pack 1024
if ($LASTEXITCODE -ne 0) { throw 'bake failed' }

& dotnet build (Join-Path $here 'quest\RepairShopQuest.csproj') -c Release -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$dll = Join-Path $out 'RepairShopQuest.dll'
Write-Host ("{0} {1}" -f $dll, (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 8).ToLower())
