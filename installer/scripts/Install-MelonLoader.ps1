param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath,
    [Parameter(Mandatory = $true)]
    [string] $RedistDir,
    [string] $WorkDir = $env:TEMP
)

$ErrorActionPreference = "Stop"

function Test-MelonLoaderInstalled {
    param([string] $Root)
    $melonDir = Join-Path $Root "MelonLoader"
    $proxy = (Test-Path (Join-Path $Root "version.dll")) -or (Test-Path (Join-Path $Root "winhttp.dll"))
    return (Test-Path $melonDir) -and $proxy
}

function Test-HasIl2CppAssemblies {
    param([string] $Root)
    $assemblies = Join-Path $Root "MelonLoader\Il2CppAssemblies"
    if (-not (Test-Path $assemblies)) { return $false }
    try {
        return @(Get-ChildItem -Path $assemblies -Filter "*.dll" -File -ErrorAction Stop).Count -gt 0
    } catch {
        return $false
    }
}

function Resolve-UnityMajorMinorPatch {
    param([string] $Root)

    $candidates = @()
    $preferred = Join-Path $Root "Mechabellum_Data\globalgamemanagers"
    if (Test-Path $preferred) {
        $candidates += $preferred
    } else {
        Get-ChildItem -Path $Root -Directory -Filter "*_Data" -ErrorAction SilentlyContinue | ForEach-Object {
            $ggm = Join-Path $_.FullName "globalgamemanagers"
            if (Test-Path $ggm) { $candidates += $ggm }
        }
    }

    foreach ($file in $candidates) {
        try {
            $fs = [System.IO.File]::OpenRead($file)
            try {
                $max = [Math]::Min($fs.Length, 4 * 1024 * 1024)
                $buf = New-Object byte[] $max
                $read = $fs.Read($buf, 0, $buf.Length)
                if ($read -le 0) { continue }
                $text = [System.Text.Encoding]::UTF8.GetString($buf, 0, $read)
                $m = [regex]::Match($text, '(20\d{2}\.\d+\.\d+[a-zA-Z]\d+)')
                if (-not $m.Success) { continue }
                $norm = [regex]::Match($m.Value, '^\s*(\d+)\.(\d+)\.(\d+)')
                if (-not $norm.Success) { continue }
                return "$($norm.Groups[1].Value).$($norm.Groups[2].Value).$($norm.Groups[3].Value)"
            } finally {
                $fs.Dispose()
            }
        } catch {
            continue
        }
    }

    return $null
}

function Test-CanForceOfflineGeneration {
    param([string] $Root)

    if (Test-HasIl2CppAssemblies -Root $Root) {
        return $true
    }

    $version = Resolve-UnityMajorMinorPatch -Root $Root
    if ([string]::IsNullOrWhiteSpace($version)) {
        return $false
    }

    $zip = Join-Path $Root "MelonLoader\Dependencies\Il2CppAssemblyGenerator\UnityDependencies_$version.zip"
    return Test-Path -LiteralPath $zip
}

function Apply-LoaderCfgOptimizations {
    param([string] $Root)
    $userData = Join-Path $Root "UserData"
    New-Item -ItemType Directory -Force -Path $userData | Out-Null
    $cfg = Join-Path $userData "Loader.cfg"
    $forceOffline = Test-CanForceOfflineGeneration -Root $Root
    $offlineLiteral = if ($forceOffline) { "true" } else { "false" }

    if (-not (Test-Path $cfg)) {
        @"
[loader]
force_quit = true

[unityengine]
force_offline_generation = $offlineLiteral
"@ | Set-Content -Path $cfg -Encoding UTF8
        return
    }

    $text = Get-Content $cfg -Raw -Encoding UTF8
    $text = [regex]::Replace($text, '(?m)^(\s*)force_quit\s*=\s*false\s*$', '${1}force_quit = true')
    $text = [regex]::Replace($text, '(?m)^(\s*)force_offline_generation\s*=\s*(true|false)\s*$', "`${1}force_offline_generation = $offlineLiteral")
    if ($text -notmatch '(?m)^\s*force_quit\s*=') {
        if ($text -match '(?m)^\[loader\]\s*$') {
            $text = [regex]::Replace($text, '(?m)^\[loader\]\s*$', "[loader]`r`nforce_quit = true")
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[loader]`r`nforce_quit = true`r`n"
        }
    }
    if ($text -notmatch '(?m)^\s*force_offline_generation\s*=') {
        if ($text -match '(?m)^\[unityengine\]\s*$') {
            $text = [regex]::Replace($text, '(?m)^\[unityengine\]\s*$', "[unityengine]`r`nforce_offline_generation = $offlineLiteral")
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[unityengine]`r`nforce_offline_generation = $offlineLiteral`r`n"
        }
    }
    Set-Content -Path $cfg -Value $text -Encoding UTF8
}

$exe = Join-Path $GamePath "Mechabellum.exe"
$ga = Join-Path $GamePath "GameAssembly.dll"
if (-not (Test-Path $exe) -or -not (Test-Path $ga)) {
    Write-Error "Invalid game path (need Mechabellum.exe and GameAssembly.dll): $GamePath"
    exit 1
}

if (@(Get-Process -Name "Mechabellum" -ErrorAction SilentlyContinue).Count -gt 0) {
    Write-Error "Mechabellum is running. Close the game before installing MelonLoader."
    exit 5
}

# Same readiness rule as GameDetector: MelonLoader folder + version.dll or winhttp.dll
if (Test-MelonLoaderInstalled -Root $GamePath) {
    Write-Host "Skip MelonLoader — already installed at $GamePath"
    try {
        Apply-LoaderCfgOptimizations -Root $GamePath
    } catch {
        Write-Host "Loader.cfg optimize skipped: $($_.Exception.Message)"
    }
    exit 0
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
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $extractFull = [System.IO.Path]::GetFullPath($extract)
        $extractPrefix = $extractFull.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $dest = [System.IO.Path]::GetFullPath((Join-Path $extractFull $name))
            if (-not ($dest.StartsWith($extractPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                      $dest.Equals($extractFull, [StringComparison]::OrdinalIgnoreCase))) {
                throw "Refusing MelonLoader zip entry outside extract root (zip-slip): $name"
            }
            if ($name.EndsWith('/') -or $name.EndsWith('\')) {
                New-Item -ItemType Directory -Force -Path $dest | Out-Null
                continue
            }
            $parent = Split-Path -Parent $dest
            if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
        }
    } finally {
        $zip.Dispose()
    }

    Get-ChildItem $extract -Force | ForEach-Object {
        $dest = Join-Path $GamePath $_.Name
        if ($_.PSIsContainer) {
            Copy-Item $_.FullName $dest -Recurse -Force
        } else {
            Copy-Item $_.FullName $dest -Force
        }
    }
} catch {
    Write-Error @"
Failed to install MelonLoader files (exit will be 1).
If MelonLoader is already present, close the game and retry — or the installer will skip when detection succeeds.
If files are locked (version.dll in use), close Mechabellum / MelonLoader processes and antivirus locks, then retry.
$($_.Exception.Message)
"@
    exit 1
} finally {
    Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
}

try {
    Apply-LoaderCfgOptimizations -Root $GamePath
} catch {
    Write-Host "Loader.cfg optimize skipped: $($_.Exception.Message)"
}

if (-not (Test-MelonLoaderInstalled -Root $GamePath)) {
    Write-Error "MelonLoader files were written but detection still incomplete."
    exit 3
}

Write-Host "MelonLoader installed to $GamePath"
exit 0
