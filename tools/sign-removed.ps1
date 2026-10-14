<#
.SYNOPSIS
  Creates the maintainer takedown signing key, or signs the takedown list into catalog.json.

.DESCRIPTION
  The manager only removes mods listed in a takedown list signed by the key whose public half
  is compiled into RemovalListVerifier.PublicKey. A mirror can serve catalog.json, but it
  cannot add a single entry to the list.

  -NewKey   Writes a new ECDSA P-256 private key to -KeyFile and prints the public key to paste
            into src/MechabellumModManager/Services/RemovalListVerifier.cs. Keep the file
            offline and backed up. Losing it means shipping a manager with a new key before
            any further takedown works. Anyone who obtains it can remove mods on every
            player's machine.

  (default) Reads removed.json (the hand-edited list, a JSON array) from -ModsRepo, signs
            {"issuedAt": now, "removed": <that array>} with -KeyFile, and writes the result as
            the top-level "removedSigned" object of catalog.json. Other catalog text is left
            byte-for-byte as it was.

.PARAMETER IssuedAt
  Override the signing time (ISO 8601, UTC). The manager ignores a list older than the one it
  has already accepted, so never sign with a time earlier than the last published list.

.EXAMPLE
  .\tools\sign-removed.ps1 -NewKey -KeyFile D:\keys\mmm-removal-key.json

.EXAMPLE
  .\tools\sign-removed.ps1 -ModsRepo D:\path\to\MechabellumMods -KeyFile D:\keys\mmm-removal-key.json
#>
param(
    [switch] $NewKey,
    [Parameter(Mandatory = $true)][string] $KeyFile,
    [string] $ModsRepo,
    [string] $IssuedAt
)

$ErrorActionPreference = "Stop"
$curve = [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Get-FullPath([string] $Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
}

function Assert-KeyOutside([string] $KeyPath, [string[]] $Roots) {
    $key = Get-FullPath $KeyPath
    foreach ($root in $Roots) {
        if (-not $root) { continue }
        $prefix = Get-FullPath $root
        if ($key.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to use a key file inside $root. Keep the private key outside every repository."
        }
    }
    # Any git working tree counts, not just the two repos named here: one 'git add -A' publishes it.
    $dir = Split-Path ([IO.Path]::GetFullPath($KeyPath)) -Parent
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir ".git")) {
            throw "Refusing to use a key file inside the git working tree $dir. Keep the private key outside every repository."
        }
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
}

# Windows PowerShell's ConvertFrom-Json caps input near 2 MB, which a full catalog can pass.
function ConvertFrom-LargeJson([string] $Text) {
    if ($PSVersionTable.PSVersion.Major -ge 6) { return ($Text | ConvertFrom-Json) }
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = [int]::MaxValue
    return $serializer.DeserializeObject($Text)
}

function Get-PublicKey($Ecdsa) {
    $p = $Ecdsa.ExportParameters($false)
    return [Convert]::ToBase64String([byte[]]($p.Q.X + $p.Q.Y))
}

$managerRepo = Split-Path $PSScriptRoot -Parent
Assert-KeyOutside $KeyFile @($managerRepo, $ModsRepo)

if ($NewKey) {
    if (Test-Path -LiteralPath $KeyFile) {
        throw "$KeyFile already exists. Refusing to overwrite a signing key."
    }
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create($curve)
    $p = $ecdsa.ExportParameters($true)
    $json = [ordered]@{
        curve = "P-256"
        d     = [Convert]::ToBase64String($p.D)
        x     = [Convert]::ToBase64String($p.Q.X)
        y     = [Convert]::ToBase64String($p.Q.Y)
    } | ConvertTo-Json
    $dir = Split-Path ([IO.Path]::GetFullPath($KeyFile)) -Parent
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    # Lock the file to the current user while it is still empty, then write the key into it.
    [IO.File]::WriteAllText($KeyFile, "", $utf8NoBom)
    $acl = Get-Acl -LiteralPath $KeyFile
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($inherited in @($acl.Access)) { [void]$acl.RemoveAccessRule($inherited) }
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($me, "FullControl", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -LiteralPath $KeyFile -AclObject $acl
    [IO.File]::WriteAllText($KeyFile, $json, $utf8NoBom)

    Write-Output "Private key written to $KeyFile (back it up offline)."
    Write-Output "PublicKey: $(Get-PublicKey $ecdsa)"
    Write-Output "Paste it into RemovalListVerifier.PublicKey and ship a manager release before signing any list."
    exit 0
}

if (-not $ModsRepo) { throw "-ModsRepo is required unless -NewKey is given." }

$key = [IO.File]::ReadAllText($KeyFile, [Text.Encoding]::UTF8) | ConvertFrom-Json
if ($key.curve -ne "P-256") { throw "Unsupported key curve: $($key.curve)" }
$point = New-Object System.Security.Cryptography.ECPoint
$point.X = [Convert]::FromBase64String($key.x)
$point.Y = [Convert]::FromBase64String($key.y)
$params = New-Object System.Security.Cryptography.ECParameters
$params.Curve = $curve
$params.Q = $point
$params.D = [Convert]::FromBase64String($key.d)
$ecdsa = [System.Security.Cryptography.ECDsa]::Create($params)
$publicKey = Get-PublicKey $ecdsa

$verifierPath = Join-Path $managerRepo "src\MechabellumModManager\Services\RemovalListVerifier.cs"
if (Test-Path -LiteralPath $verifierPath) {
    $shipped = [regex]::Match([IO.File]::ReadAllText($verifierPath), 'const string PublicKey = "([^"]*)"').Groups[1].Value
    if ($shipped -ne $publicKey) {
        Write-Warning "RemovalListVerifier.PublicKey in this checkout is not this key's public key. Managers built from it will reject the list."
    }
}

$removedPath = Join-Path $ModsRepo "removed.json"
$catalogPath = Join-Path $ModsRepo "catalog.json"
if (-not (Test-Path -LiteralPath $removedPath)) { throw "missing $removedPath (a JSON array of takedown entries)" }
if (-not (Test-Path -LiteralPath $catalogPath)) { throw "missing $catalogPath" }

$removedText = [IO.File]::ReadAllText($removedPath, [Text.Encoding]::UTF8).Trim()
if (-not $removedText.StartsWith("[")) { throw "removed.json must be a JSON array" }

$raw = [IO.File]::ReadAllBytes($catalogPath)
$hadBom = $raw.Length -ge 3 -and $raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF
$catalogText = [Text.Encoding]::UTF8.GetString($raw)
if ($hadBom) { $catalogText = $catalogText.Substring(1) }
$catalog = ConvertFrom-LargeJson $catalogText

# Bytes and ids the catalog still lists. Taking them down would delete a live mod on every refresh.
$listedIds = @{}
$shippedBy = @{}
foreach ($mod in @($catalog.mods)) {
    if ($mod.id) { $listedIds[([string]$mod.id).ToLowerInvariant()] = $true }
    $hashes = @($mod.sha256) + @(@($mod.bundle) | ForEach-Object { if ($_) { $_.sha256 } })
    foreach ($h in $hashes) { if ($h) { $shippedBy[([string]$h).ToLowerInvariant()] = [string]$mod.id } }
}

$emptyFileHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
# Piping through ForEach-Object unrolls the array; Windows PowerShell emits it as one object.
$entries = @(($removedText | ConvertFrom-Json) | ForEach-Object { $_ })
foreach ($entry in $entries) {
    $hasId = ($entry.id -is [string]) -and $entry.id.Trim().Length -gt 0
    $hasHash = @($entry.sha256 | Where-Object { $_ -match '^[0-9a-fA-F]{64}$' }).Count -gt 0
    if (-not $hasId -and -not $hasHash) { throw "every removed.json entry needs an id or at least one sha256" }
    if ($hasId -and $listedIds.ContainsKey($entry.id.Trim().ToLowerInvariant())) {
        throw "'$($entry.id)' is still listed in catalog.json mods. Move it out of mods before taking it down."
    }
    foreach ($hash in @($entry.sha256)) {
        if (-not $hash) { continue }
        if ($hash -notmatch '^[0-9a-fA-F]{64}$') { throw "not a sha256 in entry '$($entry.id)': $hash" }
        $lower = $hash.ToLowerInvariant()
        if ($lower -eq $emptyFileHash) { throw "entry '$($entry.id)' lists the empty-file hash, which matches any empty file" }
        if ($shippedBy.ContainsKey($lower)) {
            throw "entry '$($entry.id)' lists $lower, which listed mod '$($shippedBy[$lower])' still ships"
        }
    }
}

# Clients ignore a list older than the one they hold, so the time must move forward.
$invariant = [Globalization.CultureInfo]::InvariantCulture
$utcStyles = [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
$format = "yyyy-MM-dd'T'HH:mm:ss'Z'"
if ($IssuedAt) {
    $issued = [DateTime]::MinValue
    if (-not [DateTime]::TryParseExact($IssuedAt, $format, $invariant, $utcStyles, [ref]$issued)) {
        throw "-IssuedAt must look like 2026-10-14T09:30:00Z"
    }
}
else {
    $issued = [DateTime]::UtcNow
}
if ($issued -gt [DateTime]::UtcNow.AddMinutes(10)) {
    throw "issuedAt $($issued.ToString($format, $invariant)) is in the future. A future-dated list blocks every later list until that time."
}

$previous = [regex]::Match($catalogText, '"removedSigned"\s*:\s*\{[^{}]*"payload"\s*:\s*"([^"]*)"')
if ($previous.Success) {
    $previousText = $utf8NoBom.GetString([Convert]::FromBase64String($previous.Groups[1].Value))
    $previousAt = [regex]::Match($previousText, '"issuedAt"\s*:\s*"([^"]*)"').Groups[1].Value
    $previousTime = [DateTime]::MinValue
    if ([DateTime]::TryParseExact($previousAt, $format, $invariant, $utcStyles, [ref]$previousTime) -and $issued -le $previousTime) {
        throw "issuedAt $($issued.ToString($format, $invariant)) is not later than the published list ($previousAt). Clients would ignore it."
    }
}

$IssuedAt = $issued.ToString($format, $invariant)
$payloadText = '{"issuedAt":"' + $IssuedAt + '","removed":' + $removedText + '}'
$payload = $utf8NoBom.GetBytes($payloadText)
if ($payload.Length -gt 1048576) { throw "the signed list would be $($payload.Length) bytes; the manager accepts at most 1048576" }
$signature = $ecdsa.SignData($payload, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
if (-not $ecdsa.VerifyData($payload, $signature, [System.Security.Cryptography.HashAlgorithmName]::SHA256)) {
    throw "signature self-check failed"
}

$payloadB64 = [Convert]::ToBase64String($payload)
$signatureB64 = [Convert]::ToBase64String($signature)
$blob = '"removedSigned": { "payload": "' + $payloadB64 + '", "signature": "' + $signatureB64 + '" }'

$newline = if ($catalogText.Contains("`r`n")) { "`r`n" } else { "`n" }

$existing = [regex]::new('"removedSigned"\s*:\s*\{[^{}]*\}')
if ($existing.IsMatch($catalogText)) {
    $updated = $existing.Replace($catalogText, { param($m) $blob }, 1)
}
else {
    $open = $catalogText.IndexOf('{')
    if ($open -lt 0 -or $catalogText.Substring($open + 1).Trim().StartsWith('}')) {
        throw "catalog.json must be a non-empty JSON object"
    }
    $updated = $catalogText.Substring(0, $open + 1) + $newline + "  " + $blob + "," + $catalogText.Substring($open + 1)
}

$check = ConvertFrom-LargeJson $updated
if ($check.removedSigned.payload -ne $payloadB64 -or $null -eq $check.mods) {
    throw "catalog.json did not round-trip after inserting removedSigned; left unchanged"
}

[IO.File]::WriteAllText($catalogPath, $updated, (New-Object System.Text.UTF8Encoding($hadBom)))
Write-Output "Signed $($entries.Count) takedown entries (issuedAt $IssuedAt) into $catalogPath"
Write-Output "PublicKey: $publicKey"
