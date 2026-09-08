<#
.SYNOPSIS
    Diagnose Cursor workspace-root drift that breaks the Grep/Glob tools.

.DESCRIPTION
    Grep and Glob scan every workspace root. If a root is listed but missing on
    disk they fail with "Path does not exist" for the whole call, even when the
    pattern targets a root that does exist. Shell keeps working, which makes the
    failure look random.

    Roots are read from the Cursor workspace.json rather than hardcoded, so this
    stays correct as folders are added or removed.
#>
$ErrorActionPreference = 'Continue'

Write-Host "PowerShell version: $($PSVersionTable.PSVersion)"
Write-Host "Current location:   $(Get-Location)"
Write-Host "Console code page:  $([Console]::OutputEncoding.WebName) (65001/utf-8 avoids mojibake in CJK output)"
Write-Host ""

$workspaceRoot = Join-Path $env:APPDATA 'Cursor\Workspaces'
if (-not (Test-Path -LiteralPath $workspaceRoot)) {
    Write-Host "No Cursor workspace store at $workspaceRoot - nothing to check."
    exit 0
}

# Newest workspace.json is the one the running window most likely uses.
$wsFile = Get-ChildItem -LiteralPath $workspaceRoot -Filter 'workspace.json' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $wsFile) {
    Write-Host "No workspace.json found under $workspaceRoot - nothing to check."
    exit 0
}

Write-Host "workspace.json: $($wsFile.FullName)"
Write-Host ""

# Get-Content -Raw decodes as ANSI on Windows PowerShell 5.1, which corrupts the
# CJK paths badly enough to break JSON parsing. Read the bytes as UTF-8 instead.
$json = [System.IO.File]::ReadAllText($wsFile.FullName, [System.Text.Encoding]::UTF8)
try {
    $folders = @(($json | ConvertFrom-Json).folders)
} catch {
    Write-Host "Could not parse workspace.json: $($_.Exception.Message)"
    exit 2
}

if ($folders.Count -eq 0) {
    Write-Host 'workspace.json lists no folders - cannot verify roots.'
    exit 2
}

$missing = @()

foreach ($folder in $folders) {
    $path = $folder.path -replace '/', '\'
    if (Test-Path -LiteralPath $path) {
        Write-Host "OK      $path"
    } else {
        Write-Host "MISSING $path"
        $missing += $path
    }
}

Write-Host ""
if ($missing.Count -eq 0) {
    Write-Host 'All workspace roots exist. Grep/Glob should work normally.'
    exit 0
}

Write-Host 'Grep/Glob will fail with "Path does not exist" until every root above exists.'
Write-Host 'Fix: recreate the folder, or remove it from the workspace, then run "Developer: Reload Window".'
Write-Host 'Note: an agent chat caches its root list at session start, so a reload only helps a NEW chat.'
exit 1
