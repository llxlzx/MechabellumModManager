<#
.SYNOPSIS
  Inserts a resource key into every Strings*.resx after an existing anchor key.

.DESCRIPTION
  Preserves each file's existing BOM state; the locale files disagree about it and
  flipping one rewrites line 1, burying the real change in the diff.
#>
param(
    [Parameter(Mandatory = $true)][string]$AnchorKey,
    [Parameter(Mandatory = $true)][string]$NewKey,
    [Parameter(Mandatory = $true)][hashtable]$Values
)

$resourceDir = Join-Path $PSScriptRoot "..\src\MechabellumModManager\Resources"

foreach ($locale in $Values.Keys) {
    $file = if ($locale -eq "zh-CN") { "Strings.resx" } else { "Strings.$locale.resx" }
    $path = Join-Path $resourceDir $file
    $raw = [System.IO.File]::ReadAllBytes($path)
    $hadBom = $raw.Length -ge 3 -and $raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding($hadBom)
    $lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

    if ($lines -match [regex]::Escape("name=`"$NewKey`"")) {
        Write-Output "skip $file (already present)"
        continue
    }

    $out = New-Object System.Collections.Generic.List[string]
    $inserted = $false
    foreach ($line in $lines) {
        $out.Add($line)
        if (-not $inserted -and $line -match [regex]::Escape("name=`"$AnchorKey`"")) {
            $escaped = [System.Security.SecurityElement]::Escape($Values[$locale])
            $out.Add("  <data name=`"$NewKey`" xml:space=`"preserve`"><value>$escaped</value></data>")
            $inserted = $true
        }
    }

    if (-not $inserted) { throw "anchor '$AnchorKey' not found in $file" }
    [System.IO.File]::WriteAllLines($path, $out, $encoding)
    Write-Output "updated $file"
}
