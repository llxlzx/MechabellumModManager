# Build publish folder + Inno Setup installer
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "[1/3] Publishing..."
dotnet publish "src\MechabellumModManager\MechabellumModManager.csproj" `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o "publish"
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$exe = Join-Path (Get-Location) "publish\MechabellumModManager.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "missing $exe" }

$assetsOut = Join-Path (Get-Location) "publish\Assets"
New-Item -ItemType Directory -Force -Path $assetsOut | Out-Null
Copy-Item "src\MechabellumModManager\Assets\*" $assetsOut -Force

@(
  "installer\redist\dotnet8",
  "installer\redist\dotnet6",
  "installer\redist\melonloader"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

Write-Host "[3/3] Compiling Inno Setup..."
$iscc = $null
foreach ($c in @(
    (Get-Command ISCC -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)) {
    if ($c -and (Test-Path $c)) { $iscc = $c; break }
}

if (-not $iscc) {
    Write-Warning "ISCC.exe not found. Install Inno Setup 6: https://jrsoftware.org/isinfo.php"
    Write-Host "Published app ready under publish\"
    exit 2
}

Write-Host "Using $iscc"
& $iscc "installer\MechabellumModManager.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Get-ChildItem "dist\*Setup*.exe" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "OK $($_.FullName)" }
