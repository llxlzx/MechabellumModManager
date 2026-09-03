param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath,
    [Parameter(Mandatory = $true)]
    [string] $RedistDir,
    [string] $WorkDir = $env:TEMP
)

$ErrorActionPreference = "Stop"

$exe = Join-Path $GamePath "Mechabellum.exe"
$ga = Join-Path $GamePath "GameAssembly.dll"
if (-not (Test-Path $exe) -or -not (Test-Path $ga)) {
    Write-Error "Invalid game path (need Mechabellum.exe and GameAssembly.dll): $GamePath"
    exit 1
}

$zipUrl = "https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip"
$localZip = Join-Path $RedistDir "melonloader\MelonLoader.x64.zip"
$zipPath = $null

if (Test-Path $localZip) {
    $zipPath = $localZip
    Write-Host "Using local MelonLoader zip: $zipPath"
} else {
    $destDir = Join-Path $WorkDir "mmm-melon-redist"
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $zipPath = Join-Path $destDir "MelonLoader.x64.zip"
    Write-Host "Local MelonLoader zip not found; downloading from GitHub..."
    Write-Host "Note: GitHub may be unreachable without a proxy in some regions. If this hangs or fails, use a proxy or install MelonLoader manually."
    Write-Host "URL: $zipUrl"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    } catch {
        Write-Error @"
Failed to download MelonLoader from GitHub (often blocked without a proxy).
Place MelonLoader.x64.zip under installer-redist\melonloader\ (or rebuild Setup with redist embedded), or install manually from:
  https://github.com/LavaGang/MelonLoader/releases
$($_.Exception.Message)
"@
        exit 2
    }
}

$extract = Join-Path $WorkDir ("mmm-melon-extract-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extract | Out-Null
try {
    Expand-Archive -Path $zipPath -DestinationPath $extract -Force
    Get-ChildItem $extract -Force | ForEach-Object {
        $dest = Join-Path $GamePath $_.Name
        if ($_.PSIsContainer) {
            Copy-Item $_.FullName $dest -Recurse -Force
        } else {
            Copy-Item $_.FullName $dest -Force
        }
    }
} finally {
    Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
}

# Loader.cfg optimizations (same intent as MelonLoaderConfigOptimizer)
$userData = Join-Path $GamePath "UserData"
New-Item -ItemType Directory -Force -Path $userData | Out-Null
$cfg = Join-Path $userData "Loader.cfg"
if (-not (Test-Path $cfg)) {
    @"
[loader]
force_quit = true

[unityengine]
force_offline_generation = true
"@ | Set-Content -Path $cfg -Encoding UTF8
} else {
    $text = Get-Content $cfg -Raw -Encoding UTF8
    $text = [regex]::Replace($text, '(?m)^(\s*)force_quit\s*=\s*false\s*$', '${1}force_quit = true')
    $text = [regex]::Replace($text, '(?m)^(\s*)force_offline_generation\s*=\s*false\s*$', '${1}force_offline_generation = true')
    if ($text -notmatch '(?m)^\s*force_quit\s*=') {
        if ($text -match '(?m)^\[loader\]\s*$') {
            $text = [regex]::Replace($text, '(?m)^\[loader\]\s*$', "[loader]`r`nforce_quit = true")
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[loader]`r`nforce_quit = true`r`n"
        }
    }
    if ($text -notmatch '(?m)^\s*force_offline_generation\s*=') {
        if ($text -match '(?m)^\[unityengine\]\s*$') {
            $text = [regex]::Replace($text, '(?m)^\[unityengine\]\s*$', "[unityengine]`r`nforce_offline_generation = true")
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[unityengine]`r`nforce_offline_generation = true`r`n"
        }
    }
    Set-Content -Path $cfg -Value $text -Encoding UTF8
}

$melonOk = (Test-Path (Join-Path $GamePath "MelonLoader")) -and (
    (Test-Path (Join-Path $GamePath "version.dll")) -or (Test-Path (Join-Path $GamePath "winhttp.dll")))
if (-not $melonOk) {
    Write-Error "MelonLoader files were written but detection still incomplete."
    exit 3
}

Write-Host "MelonLoader installed to $GamePath"
exit 0
