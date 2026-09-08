<#
.SYNOPSIS
  Publishes the manager's update manifest and the mod catalog to the domestic COS mirror.

.DESCRIPTION
  The client hardcodes two paths under the mirror root:

    {mirror}/MechabellumModManager/latest.json
    {mirror}/MechabellumMods/catalog.json   (plus every file catalog.json points at)

  so the bucket must mirror those two trees verbatim.

  Upload order matters. Both manifests are the index into everything else, so they go
  last: a player refreshing mid-sync then reads an old manifest pointing at files that
  are already there, instead of a new manifest pointing at files that are not.

  Requires coscli (https://cloud.tencent.com/document/product/436/63143) configured with
  a CAM sub-user that can only write this one bucket.

.PARAMETER Bucket
  coscli bucket alias, or the full name-appid form, e.g. mmm-mirror-1300000000.

.PARAMETER ModsRepo
  Local clone of MechabellumMods. Its root becomes {mirror}/MechabellumMods/.

.PARAMETER ReleaseDir
  Release folder holding latest.json, e.g. release\v1.1.7. Omit to skip the manager tree.

.PARAMETER IncludeSetup
  Also upload the ~87 MB installer. Off by default: egress for the installer dwarfs
  everything else, and latest.json can keep pointing setupUrl at GitHub.

.PARAMETER WhatIf
  Print the coscli calls without running them.

.EXAMPLE
  .\tools\sync-mirror.ps1 -Bucket mmm-mirror-1300000000 `
      -ModsRepo D:\gongzuo\独立工作区\MechabellumMods `
      -ReleaseDir .\release\v1.1.7
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Bucket,
    [Parameter(Mandatory = $true)][string]$ModsRepo,
    [string]$ReleaseDir,
    [switch]$IncludeSetup
)

$ErrorActionPreference = "Stop"

function Assert-Coscli {
    if (-not (Get-Command coscli -ErrorAction SilentlyContinue)) {
        throw "coscli not found on PATH. Install it and run 'coscli config init' with the mirror sub-user's keys."
    }
}

function Invoke-Coscli {
    param([string[]]$CoscliArgs, [string]$What)

    if ($PSCmdlet.ShouldProcess($What, "coscli $($CoscliArgs -join ' ')")) {
        & coscli @CoscliArgs
        if ($LASTEXITCODE -ne 0) { throw "coscli failed ($LASTEXITCODE): $What" }
    }
    else {
        Write-Output "would run: coscli $($CoscliArgs -join ' ')"
    }
}

function Push-File {
    param([string]$LocalPath, [string]$RemoteKey)

    if (-not (Test-Path -LiteralPath $LocalPath -PathType Leaf)) {
        throw "missing local file: $LocalPath"
    }
    Invoke-Coscli -CoscliArgs @("cp", $LocalPath, "cos://$Bucket/$RemoteKey") -What $RemoteKey
}

Assert-Coscli

$ModsRepo = (Resolve-Path -LiteralPath $ModsRepo).Path
$catalogPath = Join-Path $ModsRepo "catalog.json"
if (-not (Test-Path -LiteralPath $catalogPath)) {
    throw "catalog.json not found under $ModsRepo"
}

# A mirror-served mod with no declared hash is refused by the client outright, so a
# half-stamped catalog would take the whole mirror down rather than degrade.
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$unstamped = @($catalog.mods | Where-Object { -not ($_.sha256 -match '^[0-9a-f]{64}$') })
if ($unstamped.Count -gt 0) {
    throw "these catalog entries have no sha256, the client would refuse them from a mirror: $($unstamped.id -join ', '). Run scripts/stamp_hashes.py in MechabellumMods first."
}

Write-Output "== mod files =="
foreach ($mod in $catalog.mods) {
    $relative = ($mod.file -replace '\\', '/')
    Push-File -LocalPath (Join-Path $ModsRepo $relative) -RemoteKey "MechabellumMods/$relative"

    if ($mod.preview) {
        $previewRelative = ($mod.preview -replace '\\', '/')
        Push-File -LocalPath (Join-Path $ModsRepo $previewRelative) -RemoteKey "MechabellumMods/$previewRelative"
    }
}

if ($ReleaseDir) {
    $ReleaseDir = (Resolve-Path -LiteralPath $ReleaseDir).Path
    if ($IncludeSetup) {
        Write-Output "== installer =="
        $setup = Get-ChildItem -LiteralPath $ReleaseDir -Filter "*Setup*.exe" | Select-Object -First 1
        if (-not $setup) { throw "no *Setup*.exe under $ReleaseDir" }
        Push-File -LocalPath $setup.FullName -RemoteKey "MechabellumModManager/$($setup.Name)"
    }
}

Write-Output "== manifests (last) =="
Push-File -LocalPath $catalogPath -RemoteKey "MechabellumMods/catalog.json"

if ($ReleaseDir) {
    $latest = Join-Path $ReleaseDir "latest.json"
    Push-File -LocalPath $latest -RemoteKey "MechabellumModManager/latest.json"
}

Write-Output ""
Write-Output "Done. Verify with tools\verify-mirror.ps1 -BaseUrl https://<your-bucket-domain>"
