# Build publish folder + Inno Setup installer
param(
    [switch] $SkipMelonRedistCheck
)

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
  "installer\redist\melonloader",
  "installer\redist\unity-deps",
  "installer\redist\cpp2il"
) | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

Write-Host "[2/3] Checking offline redist (MelonLoader, UnityDependencies, Cpp2IL, .NET 8)..."
$melonZip = Join-Path (Get-Location) "installer\redist\melonloader\MelonLoader.x64.zip"
$unityDepsDir = Join-Path (Get-Location) "installer\redist\unity-deps"
$dotnet8Dir = Join-Path (Get-Location) "installer\redist\dotnet8"

function Test-NonEmptyFile([string] $Path) {
    return (Test-Path -LiteralPath $Path) -and ((Get-Item -LiteralPath $Path).Length -gt 0)
}

if (-not $SkipMelonRedistCheck) {
    if (-not (Test-NonEmptyFile $melonZip)) {
        Write-Error @"
Missing MelonLoader offline package (required for release builds).
Place the official file here:
  installer\redist\melonloader\MelonLoader.x64.zip
Download: https://github.com/LavaGang/MelonLoader/releases
(Use MelonLoader.x64.zip)

Local debug only: re-run with -SkipMelonRedistCheck (do NOT use for release).
"@
        exit 3
    }
    Write-Host "Found MelonLoader redist: $melonZip ($([math]::Round((Get-Item $melonZip).Length / 1MB, 1)) MB)"

    $unityDepsZip = Get-ChildItem -Path $unityDepsDir -Filter "UnityDependencies_*.zip" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt 0 } |
        Select-Object -First 1
    if (-not $unityDepsZip) {
        Write-Error @"
Missing UnityDependencies offline package (required for release builds).
Place at least one non-empty zip here:
  installer\redist\unity-deps\UnityDependencies_{major.minor.patch}.zip
Download: https://github.com/LavaGang/Unity-Runtime-Libraries
Rename upstream files (e.g. 2022.3.62.zip) to UnityDependencies_2022.3.62.zip before placing.

Local debug only: re-run with -SkipMelonRedistCheck (do NOT use for release).
"@
        exit 3
    }
    Write-Host "Found UnityDependencies redist: $($unityDepsZip.FullName) ($([math]::Round($unityDepsZip.Length / 1MB, 1)) MB)"

    $cpp2IlDir = Join-Path (Get-Location) "installer\redist\cpp2il"
    $cpp2IlExe = Join-Path $cpp2IlDir "Cpp2IL.exe"
    $cpp2IlPlugin = Join-Path $cpp2IlDir "Cpp2IL.Plugin.StrippedCodeRegSupport.dll"
    if (-not (Test-NonEmptyFile $cpp2IlExe) -or -not (Test-NonEmptyFile $cpp2IlPlugin)) {
        Write-Error @"
Missing Cpp2IL offline package (required for release builds).
Place Melon 0.7.3 matching files here:
  installer\redist\cpp2il\Cpp2IL.exe
  installer\redist\cpp2il\Cpp2IL.Plugin.StrippedCodeRegSupport.dll
From: https://github.com/SamboyCoding/Cpp2IL/releases/tag/2022.1.0-pre-release.21
(Windows asset: Cpp2IL-*-Windows.exe → rename to Cpp2IL.exe)

Local debug only: re-run with -SkipMelonRedistCheck (do NOT use for release).
"@
        exit 3
    }
    Write-Host ("Found Cpp2IL redist: {0} ({1} MB)" -f $cpp2IlExe, [math]::Round((Get-Item $cpp2IlExe).Length / 1MB, 1))

    $dotnet8Exe = Get-ChildItem -Path $dotnet8Dir -Filter "windowsdesktop-runtime-8.*-win-x64.exe" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt 0 } |
        Select-Object -First 1
    if (-not $dotnet8Exe) {
        Write-Error @"
Missing .NET 8 Desktop Runtime offline installer (required for release builds).
Place at least one non-empty file here:
  installer\redist\dotnet8\windowsdesktop-runtime-8.*-win-x64.exe
Download: https://dotnet.microsoft.com/download/dotnet/8.0

Local debug only: re-run with -SkipMelonRedistCheck (do NOT use for release).
"@
        exit 3
    }
    Write-Host "Found .NET 8 redist: $($dotnet8Exe.FullName) ($([math]::Round($dotnet8Exe.Length / 1MB, 1)) MB)"
} else {
    Write-Warning "SkipMelonRedistCheck set 鈥?Setup may lack MelonLoader, UnityDependencies, Cpp2IL, or .NET 8 redist. Do not use for release."
}


# UTF-8 BOM check for Inno script (prevents Chinese CustomMessages mojibake)
$issPath = Join-Path (Get-Location) "installer\MechabellumModManager.iss"
$issBytes = [IO.File]::ReadAllBytes($issPath)
if ($issBytes.Length -lt 3 -or $issBytes[0] -ne 0xEF -or $issBytes[1] -ne 0xBB -or $issBytes[2] -ne 0xBF) {
  Write-Error "installer\MechabellumModManager.iss must be UTF-8 with BOM (Inno Unicode). Re-save with BOM; do not use PowerShell Set-Content without -Encoding utf8BOM."
  exit 4
}
$issText = [Text.Encoding]::UTF8.GetString($issBytes, 3, $issBytes.Length - 3)
if ($issText -notmatch '钢铁指挥官 Mod 管理器') {
  Write-Error "Chinese AppDisplayName missing/corrupt in MechabellumModManager.iss. Restore from git and bump version with UTF-8-preserving tools only."
  exit 4
}
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
