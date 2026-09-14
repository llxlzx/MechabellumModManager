param(
    [string] $GamePath,
    [string] $RedistDir = ""
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

function Test-SteamBusy {
    return @(Get-Process -Name "steam","steamwebhelper" -ErrorAction SilentlyContinue).Count -gt 0
}

function Test-MelonReady([string] $Root) {
    $melon = Test-Path -LiteralPath (Join-Path $Root "MelonLoader")
    $proxy = (Test-Path -LiteralPath (Join-Path $Root "version.dll")) -or
             (Test-Path -LiteralPath (Join-Path $Root "winhttp.dll"))
    return $melon -and $proxy
}

function Test-IsBranchStore([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $n = [IO.Path]::GetFileName([IO.Path]::GetFullPath($Path))
        return ($n -eq "Mechabellum_official") -or ($n -eq "Mechabellum_beta")
    } catch { return $false }
}

function Get-SiblingSteamLink([string] $StoreOrLink) {
    $full = [IO.Path]::GetFullPath($StoreOrLink)
    $common = [IO.Path]::GetDirectoryName($full)
    return Join-Path $common "Mechabellum"
}

<#
.SYNOPSIS
When GamePath points at Mechabellum_official/beta and Mechabellum is missing,
rename the store to Mechabellum (only if Steam is idle and dual-folder is off).
When Mechabellum already looks like a game, prefer that link path.
#>
function Normalize-GamePathAwayFromStore([string] $Path, [bool] $DualEnabled) {
    if (-not (Test-IsBranchStore $Path)) { return [IO.Path]::GetFullPath($Path) }

    $store = [IO.Path]::GetFullPath($Path)
    $link = Get-SiblingSteamLink $store

    if (Test-LooksLikeGame $link) {
        Write-Host "Preferring Steam link over store: $link (was $store)"
        return [IO.Path]::GetFullPath($link)
    }

    if ($DualEnabled) {
        Write-Host "Dual-folder enabled — leaving store path as-is: $store"
        return $store
    }

    if (Test-SteamBusy) {
        Write-Host "WARN: Path is branch store ($store) but Mechabellum is missing; Steam busy — cannot rename. Re-run Setup after Steam exits."
        return $store
    }

    if (Test-Path -LiteralPath $link) {
        Write-Host "WARN: Mechabellum exists at $link but is not a valid game root; leaving store $store"
        return $store
    }

    Write-Host "Normalizing leftover store to Steam link: $store -> $link"
    Move-Item -LiteralPath $store -Destination $link
    if (-not (Test-LooksLikeGame $link)) {
        Write-Host "WARN: Move finished but $link does not look like a game root."
    }
    return [IO.Path]::GetFullPath($link)
}

$appData = Join-Path $env:APPDATA "MechabellumModManager"
$branchPath = Join-Path $appData "branch-switch.json"
$configPath = Join-Path $appData "config.json"

# Resolve canonical GamePath: prefer branch-switch steamLinkPath when dual-folder was used.
$resolved = $GamePath
$activeBranch = $null
$official = $null
$beta = $null
$enabled = $false

if (Test-Path -LiteralPath $branchPath) {
    try {
        $bs = Get-Content -LiteralPath $branchPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $enabled = [bool]$bs.enabled
        if ($bs.steamLinkPath -and (Test-LooksLikeGame ([string]$bs.steamLinkPath))) {
            $resolved = [IO.Path]::GetFullPath([string]$bs.steamLinkPath)
        }
        if ($bs.officialStorePath) { $official = [string]$bs.officialStorePath }
        if ($bs.betaStorePath) { $beta = [string]$bs.betaStorePath }
        if ($null -ne $bs.activeBranch) { $activeBranch = [int]$bs.activeBranch }
        Write-Host "Found previous dual-folder record. enabled=$enabled activeBranch=$activeBranch"
        Write-Host "Using Steam link path: $resolved"
        Write-Host "NOTE: Will NOT exit Steam, rewrite BetaKey, or swap folders (safe while downloading)."
    } catch {
        Write-Host "branch-switch.json present but unreadable: $($_.Exception.Message)"
    }
}

$resolved = Normalize-GamePathAwayFromStore $resolved $enabled

if (-not (Test-LooksLikeGame $resolved)) {
    Write-Error "Resolved game path is invalid: $resolved"
    exit 1
}

# When dual-folder is off, clear stale store paths in branch-switch.json and warn about leftovers.
if ((Test-Path -LiteralPath $branchPath) -and -not $enabled) {
    try {
        $bs = Get-Content -LiteralPath $branchPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $common = [IO.Path]::GetDirectoryName($resolved)
        $leftovers = @()
        foreach ($name in @("Mechabellum_official", "Mechabellum_beta")) {
            $c = Join-Path $common $name
            if ((Test-Path -LiteralPath $c) -and (Test-LooksLikeGame $c)) { $leftovers += $c }
        }
        if ($leftovers.Count -gt 0) {
            Write-Host "WARN: Leftover dual-folder stores still on disk (block wizard until deleted):"
            $leftovers | ForEach-Object { Write-Host "  $_" }
        }

        $bs.steamLinkPath = $resolved
        $bs.officialStorePath = ""
        $bs.betaStorePath = ""
        $bs.enabled = $false
        [IO.File]::WriteAllText($branchPath, ($bs | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
        Write-Host "Cleared stale dual-folder store paths in branch-switch.json; steamLinkPath=$resolved"
        $official = $null
        $beta = $null
    } catch {
        Write-Host "Could not rewrite branch-switch.json: $($_.Exception.Message)"
    }
}

# Write / merge config.json (preserve unknown keys + uiLanguage)
New-Item -ItemType Directory -Force -Path $appData | Out-Null
$obj = [ordered]@{
    gamePath        = $resolved
    launchMode      = 0
    activeProfileId = "default"
    dataRoot        = $null
}
if (Test-Path -LiteralPath $configPath) {
    $existing = $null
    try {
        $existing = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        try {
            $existing = Get-Content -LiteralPath $configPath -Raw -Encoding Default | ConvertFrom-Json
        } catch {
            Write-Host "Existing config.json could not be parsed; rewriting known fields only."
        }
    }
    if ($null -ne $existing) {
        # Start from all existing properties so unknown keys survive.
        $merged = [ordered]@{}
        foreach ($p in $existing.PSObject.Properties) {
            $merged[$p.Name] = $p.Value
        }
        $merged["gamePath"] = $resolved
        if ($null -eq $merged["launchMode"]) { $merged["launchMode"] = 0 }
        if (-not $merged["activeProfileId"]) { $merged["activeProfileId"] = "default" }
        if (-not ($merged.Keys -contains "dataRoot")) { $merged["dataRoot"] = $null }
        $obj = $merged
    }
}
[IO.File]::WriteAllText($configPath, ($obj | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Host "Wrote config gamePath=$resolved"

# Plan A: machine-wide seed for daily-user first launch (survives wrong elevated AppData).
$seedRoot = Join-Path $env:ProgramData "MechabellumModManager"
New-Item -ItemType Directory -Force -Path $seedRoot | Out-Null
$seed = [ordered]@{
    gamePath   = $resolved
}
if ($obj.uiLanguage) { $seed.uiLanguage = [string]$obj.uiLanguage }
[IO.File]::WriteAllText(
    (Join-Path $seedRoot "install-defaults.json"),
    ($seed | ConvertTo-Json -Depth 5),
    [Text.UTF8Encoding]::new($false))
Write-Host "Wrote ProgramData install-defaults.json"

# Optional: ensure MelonLoader without interrupting Steam downloads.
# If Steam is busy, only fill the Official store (or non-active path). Never write into an active download target.
$steamBusy = Test-SteamBusy
$installScript = Join-Path $PSScriptRoot "Install-MelonLoader.ps1"

function Ensure-Melon([string] $Target, [string] $Why) {
    if (-not (Test-LooksLikeGame $Target)) { return }
    if (Test-MelonReady $Target) {
        Write-Host "MelonLoader already ready: $Target"
        return
    }
    if ([string]::IsNullOrWhiteSpace($RedistDir) -or -not (Test-Path -LiteralPath $installScript)) {
        Write-Host "Skip Melon ensure ($Why): installer script/redist unavailable for $Target"
        return
    }
    Write-Host "Ensuring MelonLoader ($Why): $Target"
    & $installScript -GamePath $Target -RedistDir $RedistDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "MelonLoader ensure failed for $Target (exit $LASTEXITCODE) — continuing."
    }
}

if ($enabled -and $official -and $beta) {
    if ($steamBusy) {
        Write-Host "Steam is running/downloading — only ensuring MelonLoader on Official store (will not touch Beta download folder)."
        Ensure-Melon $official "official-while-steam-busy"
    } else {
        Ensure-Melon $official "official"
        Ensure-Melon $beta "beta"
    }
} elseif (-not $steamBusy) {
    Ensure-Melon $resolved "selected-game-path"
} else {
    Write-Host "Steam busy and no dual-folder record — skip MelonLoader write to avoid interrupting downloads."
}

if ($enabled -and $null -ne $activeBranch) {
    $label = if ($activeBranch -eq 1) { "Beta" } else { "Official" }
    Write-Host "Previous active branch recorded as: $label (activeBranch=$activeBranch)."
    Write-Host "Folders/junction left unchanged so Steam can finish downloading."
}

Write-Host "Restore/write complete."
exit 0
