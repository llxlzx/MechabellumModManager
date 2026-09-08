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
  Also upload the ~87 MB installer, and repoint setupUrl in the mirror's copy of
  latest.json at it. Off by default: egress for the installer dwarfs everything else.

  Requires -MirrorBaseUrl. The client hands setupUrl straight to the browser rather
  than downloading it itself, so an installer uploaded without rewriting setupUrl is
  paid for and never served.

.PARAMETER MirrorBaseUrl
  Public mirror root, e.g. https://mmm-mirror-1300000000.cos.ap-shanghai.myqcloud.com
  Only used to build setupUrl. The GitHub copy of latest.json keeps its own setupUrl.

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
    [switch]$IncludeSetup,
    [string]$MirrorBaseUrl
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

# Rewrites just the setupUrl value in place. A ConvertFrom-Json/ConvertTo-Json round
# trip would reformat the file and escape every non-ASCII character in "notes"; the
# value is a plain URL, so it can never contain an escaped quote.
function Set-SetupUrl {
    param([string]$Text, [string]$Url)

    $pattern = '("setupUrl"\s*:\s*")([^"]*)(")'
    $matched = [regex]::Matches($Text, $pattern)
    if ($matched.Count -ne 1) {
        throw "expected exactly one setupUrl in latest.json, found $($matched.Count)"
    }

    $rewritten = [regex]::Replace($Text, $pattern, { param($m) $m.Groups[1].Value + $Url + $m.Groups[3].Value })

    # Cheap guard against a botched substitution reaching players as invalid JSON.
    $parsed = $rewritten | ConvertFrom-Json
    if ($parsed.setupUrl -ne $Url) {
        throw "setupUrl rewrite did not take: got '$($parsed.setupUrl)'"
    }
    return $rewritten
}

Assert-Coscli

if ($IncludeSetup) {
    if (-not $ReleaseDir) {
        throw "-IncludeSetup needs -ReleaseDir, that is where the installer and latest.json live."
    }
    if (-not $MirrorBaseUrl) {
        throw "-IncludeSetup needs -MirrorBaseUrl. Without it setupUrl keeps pointing at GitHub and nobody ever downloads the installer you just paid to upload."
    }

    $MirrorBaseUrl = $MirrorBaseUrl.TrimEnd('/')
    $mirrorUri = [Uri]$MirrorBaseUrl
    if ($mirrorUri.Scheme -ne "https") {
        throw "-MirrorBaseUrl must be https, the app rejects anything else (got $($mirrorUri.Scheme))."
    }
    if ($mirrorUri.Host.Contains("github")) {
        throw "-MirrorBaseUrl host contains 'github', which RemoteFetch.ClassifySource reads as the trusted GitHub origin and skips the mirror hash check. Rename the bucket."
    }
}

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

$setupName = $null
if ($ReleaseDir) {
    $ReleaseDir = (Resolve-Path -LiteralPath $ReleaseDir).Path
    if ($IncludeSetup) {
        Write-Output "== installer =="
        $setup = Get-ChildItem -LiteralPath $ReleaseDir -Filter "*Setup*.exe" | Select-Object -First 1
        if (-not $setup) { throw "no *Setup*.exe under $ReleaseDir" }
        $setupName = $setup.Name
        Push-File -LocalPath $setup.FullName -RemoteKey "MechabellumModManager/$setupName"
    }
}

Write-Output "== manifests (last) =="
Push-File -LocalPath $catalogPath -RemoteKey "MechabellumMods/catalog.json"

if ($ReleaseDir) {
    $latest = Join-Path $ReleaseDir "latest.json"
    if (-not (Test-Path -LiteralPath $latest -PathType Leaf)) {
        throw "missing local file: $latest"
    }

    if ($setupName) {
        # The mirror's latest.json points at the mirror's installer; the GitHub copy on
        # disk is left untouched so the two origins each serve their own download.
        $setupUrl = "$MirrorBaseUrl/MechabellumModManager/$([Uri]::EscapeDataString($setupName))"
        $original = [System.IO.File]::ReadAllText($latest, [System.Text.Encoding]::UTF8)
        $patched = Set-SetupUrl -Text $original -Url $setupUrl

        $temp = Join-Path ([System.IO.Path]::GetTempPath()) "latest-mirror-$([Guid]::NewGuid()).json"
        try {
            [System.IO.File]::WriteAllText($temp, $patched, (New-Object System.Text.UTF8Encoding($false)))
            Write-Output "  setupUrl -> $setupUrl"
            Push-File -LocalPath $temp -RemoteKey "MechabellumModManager/latest.json"
        }
        finally {
            # -WhatIf would otherwise propagate here and leak the temp file.
            Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue -WhatIf:$false
        }
    }
    else {
        Push-File -LocalPath $latest -RemoteKey "MechabellumModManager/latest.json"
    }
}

Write-Output ""
Write-Output "Done. Verify with tools\verify-mirror.ps1 -BaseUrl https://<your-bucket-domain>"
