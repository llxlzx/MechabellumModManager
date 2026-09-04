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

if (-not (Test-LooksLikeGame $resolved)) {
    Write-Error "Resolved game path is invalid: $resolved"
    exit 1
}

# Write / merge config.json
New-Item -ItemType Directory -Force -Path $appData | Out-Null
$obj = [ordered]@{
    gamePath        = $resolved
    launchMode      = 0
    activeProfileId = "default"
    dataRoot        = $null
}
if (Test-Path -LiteralPath $configPath) {
    try {
        $existing = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $existing.launchMode) { $obj.launchMode = [int]$existing.launchMode }
        if ($existing.activeProfileId) { $obj.activeProfileId = [string]$existing.activeProfileId }
        if ($existing.PSObject.Properties.Name -contains "dataRoot") { $obj.dataRoot = $existing.dataRoot }
    } catch { }
}
[IO.File]::WriteAllText($configPath, ($obj | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Host "Wrote config gamePath=$resolved"

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
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript -GamePath $Target -RedistDir $RedistDir
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
    $label = if ($activeBranch -eq 1) { "测试服" } else { "正式服" }
    Write-Host "Previous active branch recorded as: $label (activeBranch=$activeBranch)."
    Write-Host "Folders/junction left unchanged so Steam can finish downloading."
}

Write-Host "Restore/write complete."
exit 0
