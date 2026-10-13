<#
.SYNOPSIS
  Splits a mod binary into git-sized slices and prints the catalog "parts" array.

.DESCRIPTION
  GitHub rejects a push of a single file at or over 100 MB. Slices are 80 MB so the
  assembled mod can live in the git repo. The manager downloads the slices (GitHub
  first, mirror second) and concatenates them back into the original file.

  The whole file's sha256 and size stay on the catalog entry. This script does not
  rewrite catalog.json; it prints the parts array to paste in, and writes the slices
  under -OutputDir.

.PARAMETER InputPath
  The assembled mod file, usually the dll named by catalog "file".

.PARAMETER OutputDir
  Directory that receives 0000, 0001, ... with no extension added.

.PARAMETER CatalogPrefix
  Catalog-relative directory for the slices, e.g. mods/cut-guide/parts.
  Each printed "file" is {CatalogPrefix}/{name}.

.PARAMETER ChunkBytes
  Slice size. Default 80 MB, safely under GitHub's 100 MB limit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [Parameter(Mandatory = $true)][string]$CatalogPrefix,
    [int]$ChunkBytes = 80MB
)

$ErrorActionPreference = "Stop"

if ($ChunkBytes -le 0 -or $ChunkBytes -ge 100MB) {
    throw "-ChunkBytes must be between 1 and 100MB exclusive (got $ChunkBytes). GitHub rejects a 100MB blob."
}

if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "missing input: $InputPath"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$sha = [System.Security.Cryptography.SHA256]::Create()
$input = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $InputPath).Path)
$parts = New-Object System.Collections.Generic.List[object]
try {
    $index = 0
    $buffer = New-Object byte[] $ChunkBytes
    while ($true) {
        $read = 0
        while ($read -lt $ChunkBytes) {
            $n = $input.Read($buffer, $read, $ChunkBytes - $read)
            if ($n -le 0) { break }
            $read += $n
        }
        if ($read -le 0) { break }

        $name = $index.ToString("D4")
        $outPath = Join-Path $OutputDir $name
        $slice = New-Object byte[] $read
        [Array]::Copy($buffer, $slice, $read)
        [System.IO.File]::WriteAllBytes($outPath, $slice)
        $hash = ($sha.ComputeHash($slice) | ForEach-Object { $_.ToString("x2") }) -join ""
        $rel = ($CatalogPrefix.Trim().Trim('/') + "/" + $name) -replace '\\', '/'
        $parts.Add([pscustomobject]@{
            file   = $rel
            sha256 = $hash
            size   = $read
        }) | Out-Null
        $index++
    }
}
finally {
    $input.Dispose()
    $sha.Dispose()
}

if ($parts.Count -eq 0) {
    throw "input was empty: $InputPath"
}

$whole = (Get-FileHash -LiteralPath $InputPath -Algorithm SHA256).Hash.ToLowerInvariant()
$wholeSize = (Get-Item -LiteralPath $InputPath).Length
Write-Output "whole sha256: $whole"
Write-Output "whole size:   $wholeSize"
Write-Output "slices:       $($parts.Count) under $OutputDir"
$parts | ConvertTo-Json -Depth 4
