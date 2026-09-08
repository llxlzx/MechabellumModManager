<#
.SYNOPSIS
  Checks a domestic mirror against everything the manager expects of it.

.DESCRIPTION
  Covers the failure modes that are silent from the app's side:

  - the two hardcoded manifest paths resolve over https
  - every file catalog.json references is actually reachable on the mirror
  - the bytes served match the sha256 the catalog declares, since a mirror download
    with a mismatched hash is discarded and one with no hash is refused outright
  - the mirror hostname does not contain "github", which RemoteFetch.ClassifySource
    uses to decide a response came from the trusted GitHub origin

  Read-only: safe to run against production at any time.

.PARAMETER BaseUrl
  Mirror root, no trailing slash, e.g. https://mmm-mirror-1300000000.cos.ap-shanghai.myqcloud.com

.PARAMETER ExpectVersion
  Optional. Fails if latest.json does not advertise this version. Use after a release.

.EXAMPLE
  .\tools\verify-mirror.ps1 -BaseUrl https://mmm-mirror-1300000000.cos.ap-shanghai.myqcloud.com -ExpectVersion 1.1.8
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [string]$ExpectVersion
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
    return $buffer
}

Write-Output "== manifests =="
$catalog = $null
try {
    $catalogBytes = Get-Bytes "$BaseUrl/MechabellumMods/catalog.json"
    $catalogText = [System.Text.Encoding]::UTF8.GetString($catalogBytes)
    $catalog = $catalogText | ConvertFrom-Json
    Report $true "MechabellumMods/catalog.json ($($catalog.mods.Count) entries)"
}
catch {
    Report $false "MechabellumMods/catalog.json" "($($_.Exception.Message))"
}

try {
    $latestBytes = Get-Bytes "$BaseUrl/MechabellumModManager/latest.json"
    $latest = [System.Text.Encoding]::UTF8.GetString($latestBytes) | ConvertFrom-Json
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

            if ($declared -notmatch '^[0-9a-f]{64}$') {
                Report $false "$($mod.id) declares a sha256" "(the app refuses mirror downloads without one)"
                continue
            }

            try {
                $bytes = Get-Bytes "$BaseUrl/MechabellumMods/$relative"
                $actual = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
                Report ($actual -eq $declared) "$($mod.id) -> $relative" "(catalog $declared, served $actual)"
            }
            catch {
                Report $false "$($mod.id) -> $relative" "($($_.Exception.Message))"
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
