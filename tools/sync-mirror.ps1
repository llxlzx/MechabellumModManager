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

  The mirror is a cache, not the archive. GitHub Releases hold every mod at no cost
  (see tools/publish-mods-release.ps1), so mirroring the whole catalog would tie the
  COS bill to the catalog's total size for no benefit. -HotList narrows the upload to
  the mods worth accelerating; the client falls back to the origin for the rest, which
  is why a cold mod missing from the bucket is correct rather than broken.

  Uploads are incremental. Each object carries its sha256 as user metadata, and an
  object already holding the right hash is skipped, so a rerun costs one HEAD per mod
  instead of re-uploading gigabytes.

  Requires coscli (https://cloud.tencent.com/document/product/436/63143) configured with
  a CAM sub-user that can only write this one bucket.

.PARAMETER Bucket
  coscli bucket alias, or the full name-appid form, e.g. mmm-mirror-1312774738.

.PARAMETER ModsRepo
  Local clone of MechabellumMods. Its root becomes {mirror}/MechabellumMods/.

.PARAMETER HotList
  Text file of mod ids to mirror, one per line; '#' starts a comment. Omit to mirror
  every entry in the catalog. Defaults to mirror-hot.txt beside this script when that
  file exists.

.PARAMETER Force
  Upload every selected file even when the mirror already holds the right bytes.

.PARAMETER ReleaseDir
  Release folder holding latest.json, e.g. release\v1.1.7. Omit to skip the manager tree.

.PARAMETER IncludeSetup
  Also upload the ~87 MB installer, and repoint setupUrl in the mirror's copy of
  latest.json at it. Off by default: egress for the installer dwarfs everything else.

  Requires -MirrorBaseUrl. The client hands setupUrl straight to the browser rather
  than downloading it itself, so an installer uploaded without rewriting setupUrl is
  paid for and never served.

.PARAMETER MirrorBaseUrl
  Public mirror root, e.g. https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com
  Only used to build setupUrl. The GitHub copy of latest.json keeps its own setupUrl.

.PARAMETER WhatIf
  Print the coscli calls without running them.

.EXAMPLE
  .\tools\sync-mirror.ps1 -Bucket mmm-mirror-1312774738 `
      -ModsRepo D:\gongzuo\独立工作区\MechabellumMods `
      -ReleaseDir .\release\v1.1.7
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Bucket,
    [Parameter(Mandatory = $true)][string]$ModsRepo,
    [string]$ReleaseDir,
    [switch]$IncludeSetup,
    [string]$MirrorBaseUrl,
    [string]$HotList,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Key under which each object records the sha256 of its own bytes, so a later run can tell
# "already mirrored" from "changed since" without downloading anything.
$HashMetaKey = "mmm-sha256"

# Above this, catalog.json is uploaded gzip-encoded. JSON this size compresses about tenfold,
# and it is the single most-requested object in the bucket.
$GzipCatalogThresholdBytes = 1MB

$script:Uploaded = 0
$script:Skipped = 0

function Assert-Coscli {
    if (-not (Get-Command coscli -ErrorAction SilentlyContinue)) {
        throw "coscli not found on PATH. Install it and run 'coscli config init' with the mirror sub-user's keys."
    }
}

# The public https base for -Bucket, used to read back each object's stamped hash. -Bucket is
# usually a coscli alias, which says nothing about the host, so fall back to the coscli config
# that already maps alias -> real name + region. Returning $null just disables the skip
# optimisation, so a shape this does not recognise costs uploads, never correctness.
function Resolve-PublicBase {
    if ($MirrorBaseUrl) { return $MirrorBaseUrl.TrimEnd('/') }

    $configPath = Join-Path $env:USERPROFILE ".cos.yaml"
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { return $null }

    $name = $null
    $region = $null
    $matched = $false
    foreach ($line in (Get-Content -LiteralPath $configPath -Encoding UTF8)) {
        if ($line -match '^\s*-\s*name:\s*(\S+)') {
            if ($matched -and $name -and $region) { break }
            $name = $Matches[1]; $region = $null
            $matched = ($name -eq $Bucket)
            continue
        }
        if ($line -match '^\s*alias:\s*(\S+)' -and $Matches[1] -eq $Bucket) { $matched = $true; continue }
        if ($line -match '^\s*region:\s*(\S+)') { $region = $Matches[1]; continue }
    }

    if ($matched -and $name -and $region) { return "https://$name.cos.$region.myqcloud.com" }
    return $null
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

function Get-FileHashHex {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# Reads back the sha256 this script stamped on the object last time. Any failure (absent
# object, no metadata, no public read) reports "unknown", which re-uploads: wasting an
# upload is recoverable, skipping a needed one silently serves stale bytes.
#
# This asks the public URL rather than coscli, for two reasons. coscli's own stat HEADs the
# bucket before the object, which a correctly least-privileged sub-user policy -- scoped to
# the objects, not the bucket -- answers with 403. And the public view is the one that
# actually matters, since it is what the manager sees.
function Get-RemoteHash {
    param([string]$RemoteKey)

    if (-not $script:PublicBase) { return $null }

    try {
        $response = Invoke-WebRequest -Uri "$($script:PublicBase)/$RemoteKey" `
            -Method Head -UseBasicParsing -TimeoutSec 20
    }
    catch {
        return $null
    }

    $value = "$($response.Headers["x-cos-meta-$HashMetaKey"])"
    if ($value -match '^[0-9a-fA-F]{64}$') { return $value.ToLowerInvariant() }
    return $null
}

function Push-File {
    param(
        [string]$LocalPath,
        [string]$RemoteKey,
        [switch]$NoSkip,
        [string]$ExtraMeta
    )

    if (-not (Test-Path -LiteralPath $LocalPath -PathType Leaf)) {
        throw "missing local file: $LocalPath"
    }

    $localHash = Get-FileHashHex -Path $LocalPath

    if (-not $Force -and -not $NoSkip) {
        if ((Get-RemoteHash -RemoteKey $RemoteKey) -eq $localHash) {
            Write-Output "  skip    $RemoteKey"
            $script:Skipped++
            return
        }
    }

    $meta = "x-cos-meta-$HashMetaKey`:$localHash"
    if ($ExtraMeta) { $meta = "$meta;$ExtraMeta" }

    Invoke-Coscli -CoscliArgs @("cp", $LocalPath, "cos://$Bucket/$RemoteKey", "--meta", $meta) -What $RemoteKey
    Write-Output "  upload  $RemoteKey"
    $script:Uploaded++
}

function Read-HotList {
    param([string]$Path)

    $ids = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        $trimmed = "$line".Split('#')[0].Trim()
        if ($trimmed) { [void]$ids.Add($trimmed) }
    }
    return $ids
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

$script:PublicBase = Resolve-PublicBase
if ($script:PublicBase) {
    Write-Output "incremental skip reads back from $($script:PublicBase)"
}
else {
    Write-Output "could not resolve a public base for '$Bucket', so every object is re-uploaded. Pass -MirrorBaseUrl to enable the skip."
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

if (-not $HotList) {
    $defaultHotList = Join-Path $PSScriptRoot "mirror-hot.txt"
    if (Test-Path -LiteralPath $defaultHotList -PathType Leaf) { $HotList = $defaultHotList }
}

$hotIds = $null
if ($HotList) {
    if (-not (Test-Path -LiteralPath $HotList -PathType Leaf)) {
        throw "hot list not found: $HotList"
    }
    $hotIds = Read-HotList -Path (Resolve-Path -LiteralPath $HotList).Path

    $unknown = @($hotIds | Where-Object { $id = $_; -not ($catalog.mods | Where-Object { $_.id -eq $id }) })
    if ($unknown.Count -gt 0) {
        throw "hot list names ids that are not in catalog.json: $($unknown -join ', '). Fix the typo, or the mod you meant to accelerate is silently not mirrored."
    }

    Write-Output "hot list: $HotList ($($hotIds.Count) of $($catalog.mods.Count) mods)"
}

# Previews are small and drive the browse UI for every entry, so they are mirrored for the
# whole catalog even when the binaries are not.
Write-Output "== mod files =="
foreach ($mod in $catalog.mods) {
    if ($hotIds -and -not $hotIds.Contains("$($mod.id)")) {
        Write-Output "  cold    $($mod.id) (served from the origin)"
    }
    else {
        $relative = ($mod.file -replace '\\', '/')
        Push-File -LocalPath (Join-Path $ModsRepo $relative) -RemoteKey "MechabellumMods/$relative"
    }

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

# catalog.json is the one object that grows with the whole catalog, and it is JSON, so it
# compresses roughly tenfold. Below the threshold the saving is not worth the extra moving
# part; above it, egress on the most-requested object in the bucket starts to matter.
# The client enables automatic decompression, so a gzip body is transparent to it.
$catalogSize = (Get-Item -LiteralPath $catalogPath).Length
if ($catalogSize -gt $GzipCatalogThresholdBytes) {
    Write-Output "  catalog is $([math]::Round($catalogSize / 1MB, 2)) MB, uploading gzip-encoded"
    $gz = Join-Path ([System.IO.Path]::GetTempPath()) "catalog-$([Guid]::NewGuid()).json.gz"
    try {
        $input = [System.IO.File]::OpenRead($catalogPath)
        try {
            $output = [System.IO.File]::Create($gz)
            try {
                $gzip = New-Object System.IO.Compression.GZipStream($output, [System.IO.Compression.CompressionLevel]::Optimal)
                try { $input.CopyTo($gzip) } finally { $gzip.Dispose() }
            }
            finally { $output.Dispose() }
        }
        finally { $input.Dispose() }

        # The object keeps its .json key: the client requests catalog.json and the header tells
        # it how the body is encoded. Renaming it to .json.gz would just 404.
        Push-File -LocalPath $gz -RemoteKey "MechabellumMods/catalog.json" -ExtraMeta "Content-Encoding:gzip"
    }
    finally {
        Remove-Item -LiteralPath $gz -Force -ErrorAction SilentlyContinue -WhatIf:$false
    }
}
else {
    Push-File -LocalPath $catalogPath -RemoteKey "MechabellumMods/catalog.json"
}

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
Write-Output "$script:Uploaded uploaded, $script:Skipped already current."
$verifyHint = "Verify with tools\verify-mirror.ps1 -BaseUrl https://<your-bucket-domain>"
if ($HotList) { $verifyHint += " -HotList '$HotList'" }
Write-Output $verifyHint
