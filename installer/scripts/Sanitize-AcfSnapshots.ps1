param(
    [string] $SnapshotsDir = "",
    [string] $BetaBranchName = "public_test"
)

$ErrorActionPreference = "Stop"

function Get-AcfBetaKeyFromBlock([string] $Text, [string] $BlockName) {
    $needle = '"' + $BlockName + '"'
    $idx = $Text.IndexOf($needle)
    if ($idx -lt 0) { return $null }
    $rest = $Text.Substring($idx + $needle.Length)
    $open = $rest.IndexOf('{')
    if ($open -lt 0) { return $null }
    $depth = 0
    $close = -1
    for ($j = $open; $j -lt $rest.Length; $j++) {
        $ch = $rest[$j]
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) { $close = $j; break }
        }
    }
    if ($close -lt 0) { return $null }
    $inner = $rest.Substring($open + 1, $close - $open - 1)
    $m = [regex]::Match($inner, '"BetaKey"\s+"([^"]*)"')
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Test-HasMountedConfig([string] $Text) {
    return $Text.Contains('"MountedConfig"')
}

function Test-ValidSnapshot([string] $Text, [string] $Branch, [string] $ExpectedBeta) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    $userKey = Get-AcfBetaKeyFromBlock $Text "UserConfig"
    $hasMounted = Test-HasMountedConfig $Text
    $mountedKey = if ($hasMounted) { Get-AcfBetaKeyFromBlock $Text "MountedConfig" } else { $null }

    if ($Branch -eq "official") {
        $userOk = [string]::IsNullOrWhiteSpace($userKey)
        $mountedOk = (-not $hasMounted) -or [string]::IsNullOrWhiteSpace($mountedKey)
        return $userOk -and $mountedOk
    }

    if ([string]::IsNullOrWhiteSpace($ExpectedBeta)) { return $false }
    if (-not [string]::Equals(($userKey ?? ""), $ExpectedBeta, [StringComparison]::Ordinal)) { return $false }
    if (-not $hasMounted) { return $true }
    return [string]::Equals(($mountedKey ?? ""), $ExpectedBeta, [StringComparison]::Ordinal)
}

function Remove-IfInvalid([string] $Path, [string] $Branch, [string] $ExpectedBeta) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    try {
        $text = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
    } catch {
        Write-Host "Deleting unreadable snapshot: $Path"
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        return
    }
    if (Test-ValidSnapshot $text $Branch $ExpectedBeta) {
        Write-Host "Keep valid $Branch snapshot: $Path"
        return
    }
    Write-Host "Deleting dirty $Branch snapshot: $Path"
    Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
}

if ([string]::IsNullOrWhiteSpace($SnapshotsDir)) {
    $SnapshotsDir = Join-Path $env:APPDATA "MechabellumModManager\steam-acf-snapshots"
}

$branchSwitch = Join-Path $env:APPDATA "MechabellumModManager\branch-switch.json"
if (Test-Path -LiteralPath $branchSwitch) {
    try {
        $json = Get-Content -LiteralPath $branchSwitch -Raw | ConvertFrom-Json
        if ($json.betaBranchName -and -not [string]::IsNullOrWhiteSpace([string]$json.betaBranchName)) {
            $BetaBranchName = [string]$json.betaBranchName
        }
    } catch { }
}

Write-Host "Sanitize ACF snapshots under: $SnapshotsDir (betaBranchName=$BetaBranchName)"
if (-not (Test-Path -LiteralPath $SnapshotsDir)) {
    Write-Host "No snapshots directory; nothing to do."
    exit 0
}

Remove-IfInvalid (Join-Path $SnapshotsDir "official.acf") "official" $BetaBranchName
Remove-IfInvalid (Join-Path $SnapshotsDir "beta.acf") "beta" $BetaBranchName
exit 0
