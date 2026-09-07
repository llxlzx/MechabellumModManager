param(
    [Parameter(Mandatory = $true)]
    [string] $ExePath,
    [int] $TimeoutSec = 30
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExePath)) {
    Write-Host "Sanitize CLI exe missing: $ExePath"
    exit 1
}

$p = Start-Process -FilePath $ExePath -ArgumentList "--sanitize-acf-snapshots" -PassThru -WindowStyle Minimized
if (-not $p.WaitForExit([Math]::Max(1, $TimeoutSec) * 1000)) {
    Write-Host "Sanitize CLI timed out after ${TimeoutSec}s; killing PID $($p.Id)"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 124
}

exit $p.ExitCode
