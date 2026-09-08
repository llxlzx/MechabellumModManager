<#
.SYNOPSIS
  Checks a domestic mirror against everything the manager expects of it.

.DESCRIPTION
  Covers the failure modes that are silent from the app's side:

  - the two hardcoded manifest paths resolve over https
  - every file the mirror is supposed to hold is actually reachable on it
  - the bytes served match the sha256 the catalog declares, since a mirror download
    with a mismatched hash is discarded and one with no hash is refused outright
  - the mirror hostname does not contain "github", which RemoteFetch.ClassifySource
    uses to decide a response came from the trusted GitHub origin

  The mirror is a cache of a hot subset, not a full archive, so a mod that is absent
  from it is expected rather than broken: the client falls back to the origin. Pass
  -HotList to say which mods must be present; without it every catalog entry is
  required, which is only right while the whole catalog is small enough to mirror.

  Read-only: safe to run against production at any time.

.PARAMETER BaseUrl
  Mirror root, no trailing slash, e.g. https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com

.PARAMETER ExpectVersion
  Optional. Fails if latest.json does not advertise this version. Use after a release.

.PARAMETER HotList
  The same mirror-hot.txt handed to sync-mirror.ps1. Entries in it must be served
  correctly; entries outside it may 404. Defaults to mirror-hot.txt beside this script
  when that file exists.

.PARAMETER RequireAll
  Treat every catalog entry as required even when a hot list exists. Use to confirm a
  full mirror really is complete.

.EXAMPLE
  .\tools\verify-mirror.ps1 -BaseUrl https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com -ExpectVersion 1.1.8
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [string]$ExpectVersion,
    [string]$HotList,
    [switch]$RequireAll
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
$failures = New-Object System.Collections.Generic.List[string]

function Report {
    param([bool]$Ok, [string]$Label, [string]$Detail = "")

    if ($Ok) {
        Write-Output "  ok    $Label"
    }
    else {
        Write-Output "  FAIL  $Label $Detail"
        $failures.Add("$Label $Detail".Trim())
    }
}

function Test-IsNotFound {
    param($ErrorRecord)
    $response = $ErrorRecord.Exception.Response
    return $response -and [int]$response.StatusCode -eq 404
}

$hotIds = $null
if (-not $HotList) {
    $defaultHotList = Join-Path $PSScriptRoot "mirror-hot.txt"
    if (Test-Path -LiteralPath $defaultHotList -PathType Leaf) { $HotList = $defaultHotList }
}
if ($HotList -and -not $RequireAll) {
    if (-not (Test-Path -LiteralPath $HotList -PathType Leaf)) {
        throw "hot list not found: $HotList"
    }
    $hotIds = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $HotList).Path, [System.Text.Encoding]::UTF8)) {
        $trimmed = "$line".Split('#')[0].Trim()
        if ($trimmed) { [void]$hotIds.Add($trimmed) }
    }
    Write-Output "hot list: $HotList ($($hotIds.Count) mods required present)"
}

Write-Output "== base url =="
$uri = [Uri]$BaseUrl
Report ($uri.Scheme -eq "https") "scheme is https" "(got $($uri.Scheme); the app rejects a non-https mirror and logs it)"
Report (-not $uri.Host.Contains("github")) "host does not contain 'github'" "(host $($uri.Host) would be misread as the GitHub origin)"

# Invoke-WebRequest hands back .Content as a string for text types and bytes for binary
# ones, so read the raw stream instead — the hash check needs the exact bytes either way.
function Get-Bytes {
    param([string]$Url)

    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 30
    $stream = $response.RawContentStream
    $stream.Position = 0
    $buffer = New-Object byte[] $stream.Length
    [void]$stream.Read($buffer, 0, $buffer.Length)

    # sync-mirror.ps1 uploads catalog.json gzip-encoded once it passes 1 MB. Whether those bytes
    # arrive still compressed depends on the host: Windows PowerShell decompresses them and
    # leaves Content-Encoding set, so trusting that header would decompress twice. The magic
    # number says what the bytes actually are, which is true on every host.
    if ($buffer.Length -ge 2 -and $buffer[0] -eq 0x1F -and $buffer[1] -eq 0x8B) {
        $compressed = New-Object System.IO.MemoryStream(, $buffer)
        $gzip = New-Object System.IO.Compression.GZipStream($compressed, [System.IO.Compression.CompressionMode]::Decompress)
        $plain = New-Object System.IO.MemoryStream
        try {
            $gzip.CopyTo($plain)
            return $plain.ToArray()
        }
        finally {
            $gzip.Dispose(); $compressed.Dispose(); $plain.Dispose()
        }
    }

    return $buffer
}

# A UTF-8 BOM survives Encoding.UTF8.GetString as a leading U+FEFF, which ConvertFrom-Json
# rejects as an invalid primitive. Plenty of editors and PowerShell's own Set-Content -Encoding
# UTF8 emit one, and a manifest with a BOM is perfectly valid for the app, so a BOM here must
# not masquerade as a broken mirror.
function ConvertFrom-JsonBytes {
    param([byte[]]$Bytes)

    $text = [System.Text.Encoding]::UTF8.GetString($Bytes)
    return ($text.TrimStart([char]0xFEFF) | ConvertFrom-Json)
}

Write-Output "== manifests =="
$catalog = $null
try {
    $catalog = ConvertFrom-JsonBytes (Get-Bytes "$BaseUrl/MechabellumMods/catalog.json")
    Report $true "MechabellumMods/catalog.json ($($catalog.mods.Count) entries)"
}
catch {
    Report $false "MechabellumMods/catalog.json" "($($_.Exception.Message))"
}

Write-Output "== redist =="
$redistManifest = $null
try {
    $redistManifest = ConvertFrom-JsonBytes (Get-Bytes "$BaseUrl/MechabellumRedist/manifest.json")
    Report ($redistManifest.artifacts.Count -gt 0) "MechabellumRedist/manifest.json ($($redistManifest.artifacts.Count) artifacts)"
}
catch {
    Report $false "MechabellumRedist/manifest.json" "($($_.Exception.Message))"
}

if ($redistManifest) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($art in $redistManifest.artifacts) {
            $rel = ($art.path -replace '\\', '/')
            $declared = "$($art.sha256)".ToLowerInvariant()
            if ($declared -notmatch '^[0-9a-f]{64}$') {
                Report $false "redist $($art.id) declares sha256" "(thin Setup refuses bad hashes)"
                continue
            }
            try {
                $bytes = Get-Bytes "$BaseUrl/MechabellumRedist/$rel"
                $actual = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
                Report ($actual -eq $declared) "redist $($art.id) -> $rel" "(manifest $declared, served $actual)"
            }
            catch {
                Report $false "redist $($art.id) -> $rel" "($($_.Exception.Message))"
            }
        }
    }
    finally {
        $sha.Dispose()
    }
}

try {
    $latest = ConvertFrom-JsonBytes (Get-Bytes "$BaseUrl/MechabellumModManager/latest.json")
    Report $true "MechabellumModManager/latest.json (version $($latest.version))"
    if ($ExpectVersion) {
        Report ($latest.version -eq $ExpectVersion) "latest.json advertises $ExpectVersion" "(got $($latest.version))"
    }

    # setupUrl is opened in the player's browser as-is, so a mirrored installer only
    # gets used if this points at the mirror, and only works if the object is there.
    $setupUrl = "$($latest.setupUrl)".Trim()
    if ($setupUrl) {
        $setupHost = ([Uri]$setupUrl).Host
        if ($setupHost -eq $uri.Host) {
            try {
                Invoke-WebRequest -Uri $setupUrl -Method Head -UseBasicParsing -TimeoutSec 30 | Out-Null
                Report $true "setupUrl points at this mirror and resolves"
            }
            catch {
                Report $false "setupUrl points at this mirror but 404s" "($setupUrl)"
            }
        }
        else {
            Write-Output "  note  setupUrl points at $setupHost, not this mirror (fine unless you meant to mirror the installer)"
        }
    }
}
catch {
    Report $false "MechabellumModManager/latest.json" "($($_.Exception.Message))"
}

if ($catalog) {
    Write-Output "== mod files =="
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($mod in $catalog.mods) {
            $relative = ($mod.file -replace '\\', '/')
            $declared = "$($mod.sha256)".ToLowerInvariant()
            $required = (-not $hotIds) -or $hotIds.Contains("$($mod.id)")

            if ($declared -notmatch '^[0-9a-f]{64}$') {
                Report $false "$($mod.id) declares a sha256" "(the app refuses downloads without one)"
                continue
            }

            try {
                $bytes = Get-Bytes "$BaseUrl/MechabellumMods/$relative"
                $actual = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""

                # Wrong bytes are a failure whether or not the mod was meant to be here: an
                # unlisted object still gets served to whoever asks, stale hash and all.
                Report ($actual -eq $declared) "$($mod.id) -> $relative" "(catalog $declared, served $actual)"

                if (-not $required) {
                    Write-Output "  note  $($mod.id) is mirrored though the hot list omits it (harmless, but it is costing storage)"
                }
            }
            catch {
                if ((-not $required) -and (Test-IsNotFound $_)) {
                    Write-Output "  cold  $($mod.id) absent from the mirror, served from the origin (expected)"
                }
                else {
                    Report $false "$($mod.id) -> $relative" "($($_.Exception.Message))"
                }
            }

            if ($mod.preview) {
                $previewRelative = ($mod.preview -replace '\\', '/')
                try {
                    Get-Bytes "$BaseUrl/MechabellumMods/$previewRelative" | Out-Null
                    Report $true "$($mod.id) preview"
                }
                catch {
                    Report $false "$($mod.id) preview" "($($_.Exception.Message))"
                }
            }
        }
    }
    finally {
        $sha.Dispose()
    }
}

Write-Output ""
if ($failures.Count -eq 0) {
    Write-Output "Mirror looks good."
    exit 0
}

Write-Output "$($failures.Count) check(s) failed:"
foreach ($failure in $failures) { Write-Output "  - $failure" }
exit 1
