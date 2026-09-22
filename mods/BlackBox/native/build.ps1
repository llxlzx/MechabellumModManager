$ErrorActionPreference = "Stop"
$cl = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio","$env:ProgramFiles\Microsoft Visual Studio" -Filter cl.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\cl.exe$' } |
    Select-Object -First 1
if (-not $cl) {
    Write-Error "cl.exe not found. Install the Windows SDK and MSVC x64 tools. BlackBoxDump.exe is not committed."
    exit 1
}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "BlackBoxDump.exe"
Push-Location $here
& $cl.FullName /nologo /O2 /W3 /DUNICODE /D_UNICODE BlackBoxDump.c /Fe:$out /link dbghelp.lib
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) { exit $code }
Write-Output $out
