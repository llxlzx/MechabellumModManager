<#
.SYNOPSIS
  Publishes every mod binary in the catalog as GitHub Release assets, and points
  catalog.json at them.

.DESCRIPTION
  Git is the wrong home for a catalog heading towards hundreds of gigabytes: GitHub
  rejects a push containing a file over 100 MB, and every clone would carry the full
  history of every binary forever. Release assets have neither problem, cost nothing,
  and are served with no egress bill.

  So the split is: the git repo keeps catalog.json and the preview images, a Release
  holds the binaries, and the COS mirror caches only the popular subset. This script
  owns the middle tier.

  Assets share one flat namespace per release, so 'mods/show-grid/ShowGrid.dll' is
  uploaded as 'mods__show-grid__ShowGrid.dll'. Two mods shipping 'Mod.dll' would
  otherwise collide. scripts/stamp_hashes.py and scripts/validate_catalog.py encode
  the same rule, and CI fails if the three ever disagree.

  Uploads are incremental: an asset whose size already matches is left alone, because
  re-uploading a 500 MB unchanged mod on every run is the difference between a usable
  tool and one nobody runs.

.PARAMETER ModsRepo
  Local clone of MechabellumMods.

.PARAMETER Tag
  Release tag holding the binaries. One rolling tag is intended; the catalog always
  names an exact asset, so history is not needed here.

.PARAMETER Repo
  owner/name of the mods repository.

.PARAMETER SkipCatalogStamp
  Upload only, leaving originUrl in catalog.json as it is. Use when re-uploading an
  asset whose bytes did not change.

.PARAMETER WhatIf
  Print what would happen without uploading or rewriting anything.

.EXAMPLE
  .\tools\publish-mods-release.ps1 -ModsRepo D:\gongzuo\独立工作区\MechabellumMods
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ModsRepo,
    [string]$Tag = "mods-current",
    [string]$Repo = "llxlzx/MechabellumMods",
    [switch]$SkipCatalogStamp
)

$ErrorActionPreference = "Stop"

# GitHub rejects a single release asset over 2 GB.
$MaxAssetBytes = 2GB

function Assert-Tool {
    param([string]$Name, [string]$Hint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name not found on PATH. $Hint"
    }
}

# Must stay identical to stamp_hashes.asset_name and validate_catalog.asset_name.
function Get-AssetName {
    param([string]$Relative)
    return ($Relative -replace '\\', '/') -replace '/', '__'
}

Assert-Tool -Name gh -Hint "Install the GitHub CLI and run 'gh auth login'."
Assert-Tool -Name python -Hint "Needed to re-stamp catalog.json."

$ModsRepo = (Resolve-Path -LiteralPath $ModsRepo).Path
$catalogPath = Join-Path $ModsRepo "catalog.json"
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "catalog.json not found under $ModsRepo"
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json

# A hash-less entry is refused by the client from every source, so publishing one would
# just fill a release with bytes nobody can install.
$unstamped = @($catalog.mods | Where-Object { -not ("$($_.sha256)" -match '^[0-9a-f]{64}$') })
if ($unstamped.Count -gt 0) {
    throw "these entries have no sha256, the client refuses them: $($unstamped.id -join ', '). Run scripts/stamp_hashes.py first."
}

Write-Output "== release $Tag on $Repo =="
$releaseExists = $false
& gh release view $Tag --repo $Repo 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) { $releaseExists = $true }

if (-not $releaseExists) {
    if ($PSCmdlet.ShouldProcess("$Repo $Tag", "gh release create")) {
        & gh release create $Tag --repo $Repo --title "Mod binaries" `
            --notes "Binary assets for catalog.json. Managed by tools/publish-mods-release.ps1; do not edit by hand." | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed ($LASTEXITCODE)" }
        Write-Output "  created"
    }
    else {
        Write-Output "  would create release $Tag"
    }
}
else {
    Write-Output "  exists"
}

# One listing up front; asking gh per mod would be a round trip per entry.
$existing = @{}
if ($releaseExists) {
    $assetsJson = & gh release view $Tag --repo $Repo --json assets 2>$null
    if ($LASTEXITCODE -eq 0 -and $assetsJson) {
        foreach ($asset in ($assetsJson | ConvertFrom-Json).assets) {
            $existing[$asset.name] = [int64]$asset.size
        }
    }
}

Write-Output "== assets =="
$uploaded = 0
$skipped = 0
foreach ($mod in $catalog.mods) {
    $relative = ($mod.file -replace '\\', '/')
    $localPath = Join-Path $ModsRepo $relative
    if (-not (Test-Path -LiteralPath $localPath -PathType Leaf)) {
        throw "missing local file for $($mod.id): $relative"
    }

    $file = Get-Item -LiteralPath $localPath
    if ($file.Length -gt $MaxAssetBytes) {
        throw "$($mod.id) is $([math]::Round($file.Length / 1GB, 2)) GB, over GitHub's 2 GB per-asset limit. Split it into volumes or host it on object storage and set originUrl by hand."
    }

    $name = Get-AssetName $relative
    if ($existing.ContainsKey($name) -and $existing[$name] -eq $file.Length) {
        # Size alone is a weak identity check, but the client verifies sha256 on download,
        # so a stale same-size asset fails loudly there rather than installing silently.
        Write-Output "  skip    $name ($($file.Length) bytes, unchanged)"
        $skipped++
        continue
    }

    if ($PSCmdlet.ShouldProcess($name, "gh release upload")) {
        # gh takes 'localpath#displayname' to name the asset independently of the file.
        & gh release upload $Tag "$($file.FullName)#$name" --repo $Repo --clobber
        if ($LASTEXITCODE -ne 0) { throw "gh release upload failed ($LASTEXITCODE): $name" }
        Write-Output "  upload  $name ($($file.Length) bytes)"
        $uploaded++
    }
    else {
        Write-Output "  would upload $name ($($file.Length) bytes)"
    }
}

Write-Output "  $uploaded uploaded, $skipped unchanged"

if ($SkipCatalogStamp) {
    Write-Output ""
    Write-Output "Skipped the catalog stamp. originUrl still points wherever it did before."
    return
}

Write-Output "== catalog originUrl =="
$originBase = "https://github.com/$Repo/releases/download/$Tag"
if ($PSCmdlet.ShouldProcess($catalogPath, "stamp originUrl -> $originBase")) {
    Push-Location $ModsRepo
    try {
        & python scripts/stamp_hashes.py --origin-base $originBase
        if ($LASTEXITCODE -ne 0) { throw "stamp_hashes.py failed ($LASTEXITCODE)" }
        & python scripts/validate_catalog.py
        if ($LASTEXITCODE -ne 0) { throw "validate_catalog.py failed ($LASTEXITCODE)" }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Output "  would run: python scripts/stamp_hashes.py --origin-base $originBase"
}

Write-Output ""
Write-Output "Done. Commit the catalog.json change, then sync the mirror:"
Write-Output "  tools\sync-mirror.ps1 -Bucket <bucket> -ModsRepo $ModsRepo"
