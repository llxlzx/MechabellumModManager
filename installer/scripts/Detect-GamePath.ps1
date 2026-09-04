param(
    [switch] $Json,
    [string] $OutFile = ""
)

$ErrorActionPreference = "Stop"

function Test-LooksLikeGame([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $p = [IO.Path]::GetFullPath($Path)
        return (Test-Path -LiteralPath (Join-Path $p "Mechabellum.exe")) -and
               (Test-Path -LiteralPath (Join-Path $p "GameAssembly.dll"))
    } catch { return $false }
}

function Get-SteamRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

    $regPaths = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )
    foreach ($rp in $regPaths) {
        try {
            $key = Get-ItemProperty -Path $rp -ErrorAction SilentlyContinue
            foreach ($name in @("SteamPath", "InstallPath")) {
                $v = $key.$name
                if ([string]::IsNullOrWhiteSpace($v)) { continue }
                $norm = ($v -replace '/', '\').TrimEnd('\')
                if ((Test-Path -LiteralPath $norm) -and $seen.Add($norm)) { $roots.Add($norm) }
            }
        } catch { }
    }

    foreach ($drive in Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Name) {
        foreach ($guess in @(
            "$drive`:\Program Files (x86)\Steam",
            "$drive`:\Program Files\Steam",
            "$drive`:\Steam",
            "$drive`:\steam"
        )) {
            if ((Test-Path -LiteralPath $guess) -and $seen.Add($guess)) { $roots.Add($guess) }
        }
    }

    return $roots
}

function Get-SteamLibraries([string] $SteamRoot) {
    $libs = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    [void]$seen.Add($SteamRoot)
    $libs.Add($SteamRoot)

    $vdf = Join-Path $SteamRoot "steamapps\libraryfolders.vdf"
    if (-not (Test-Path -LiteralPath $vdf)) { return $libs }

    $text = Get-Content -LiteralPath $vdf -Raw -Encoding UTF8
    $matches = [regex]::Matches($text, '"path"\s+"([^"]+)"', 'IgnoreCase')
    foreach ($m in $matches) {
        $raw = $m.Groups[1].Value
        if ($raw -match 'depotcache') { continue }
        $lib = ($raw -replace '\\\\', '\' -replace '/', '\').TrimEnd('\')
        if ([string]::IsNullOrWhiteSpace($lib)) { continue }
        if (-not (Test-Path -LiteralPath $lib)) { continue }
        if ($seen.Add($lib)) { $libs.Add($lib) }
    }
    return $libs
}

function Find-Candidates {
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($root in Get-SteamRoots) {
        foreach ($lib in Get-SteamLibraries $root) {
            $common = Join-Path $lib "steamapps\common"
            if (-not (Test-Path -LiteralPath $common)) { continue }
            foreach ($name in @("Mechabellum", "Mechabellum_official", "Mechabellum_beta")) {
                $c = Join-Path $common $name
                if (Test-LooksLikeGame $c) { $list.Add([IO.Path]::GetFullPath($c)) }
            }
        }
    }

    # Drive fallbacks
    foreach ($drive in Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Name) {
        foreach ($base in @(
            "$drive`:\steam\steamapps\common",
            "$drive`:\Steam\steamapps\common",
            "$drive`:\SteamLibrary\steamapps\common",
            "$drive`:\Program Files (x86)\Steam\steamapps\common"
        )) {
            if (-not (Test-Path -LiteralPath $base)) { continue }
            foreach ($name in @("Mechabellum", "Mechabellum_official", "Mechabellum_beta")) {
                $c = Join-Path $base $name
                if (Test-LooksLikeGame $c) {
                    $full = [IO.Path]::GetFullPath($c)
                    if (-not ($list -contains $full)) { $list.Add($full) }
                }
            }
        }
    }
    return $list
}

function Prefer-Path([System.Collections.Generic.List[string]] $Candidates) {
    # Prefer official store as the "stable root" for installer default,
    # then Steam link folder, then beta.
    $official = $Candidates | Where-Object { ([IO.Path]::GetFileName($_)) -eq "Mechabellum_official" } | Select-Object -First 1
    if ($official) { return $official }
    $link = $Candidates | Where-Object { ([IO.Path]::GetFileName($_)) -eq "Mechabellum" } | Select-Object -First 1
    if ($link) { return $link }
    $beta = $Candidates | Where-Object { ([IO.Path]::GetFileName($_)) -eq "Mechabellum_beta" } | Select-Object -First 1
    if ($beta) { return $beta }
    if ($Candidates.Count -gt 0) { return $Candidates[0] }
    return $null
}

$appData = Join-Path $env:APPDATA "MechabellumModManager"
$branchCfg = Join-Path $appData "branch-switch.json"
$configPath = Join-Path $appData "config.json"

function Emit([string] $Path, [string] $Source) {
    if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
        [IO.File]::WriteAllText($OutFile, $Path, [Text.UTF8Encoding]::new($false))
    }
    if ($Json) {
        @{ path = $Path; source = $Source } | ConvertTo-Json -Compress
        exit 0
    }
    Write-Output $Path
    exit 0
}

# 1) Previous dual-folder steam link (best for reinstall)
if (Test-Path -LiteralPath $branchCfg) {
    try {
        $bs = Get-Content -LiteralPath $branchCfg -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($bs.steamLinkPath -and (Test-LooksLikeGame ([string]$bs.steamLinkPath))) {
            Emit ([IO.Path]::GetFullPath([string]$bs.steamLinkPath)) "branch-switch"
        }
    } catch { }
}

# 2) Previous manager config
if (Test-Path -LiteralPath $configPath) {
    try {
        $cfg = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($cfg.gamePath -and (Test-LooksLikeGame ([string]$cfg.gamePath))) {
            Emit ([IO.Path]::GetFullPath([string]$cfg.gamePath)) "config"
        }
    } catch { }
}

$candidates = Find-Candidates
$path = Prefer-Path $candidates
if ([string]::IsNullOrWhiteSpace($path)) {
    if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
        [IO.File]::WriteAllText($OutFile, "", [Text.UTF8Encoding]::new($false))
    }
    if ($Json) { @{ path = ""; source = "none" } | ConvertTo-Json -Compress; exit 2 }
    exit 2
}

Emit $path "scan"