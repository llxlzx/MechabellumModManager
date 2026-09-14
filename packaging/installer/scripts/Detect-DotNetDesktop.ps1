param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(6, 8)]
    [int] $MajorVersion
)

$ErrorActionPreference = "Stop"

function Test-DesktopRuntime([int] $major) {
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes) {
            foreach ($line in $runtimes) {
                if ($line -match "Microsoft\.WindowsDesktop\.App\s+$major\.") {
                    return $true
                }
            }
        }
    } catch { }

    $key = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (Test-Path $key) {
        $names = (Get-Item $key).GetValueNames()
        foreach ($n in $names) {
            if ($n -like "$major.*") { return $true }
        }
    }

    $key32 = "HKLM:\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (Test-Path $key32) {
        $names = (Get-Item $key32).GetValueNames()
        foreach ($n in $names) {
            if ($n -like "$major.*") { return $true }
        }
    }

    return $false
}

if (Test-DesktopRuntime $MajorVersion) {
    Write-Host "FOUND Desktop Runtime $MajorVersion"
    exit 0
}

Write-Host "MISSING Desktop Runtime $MajorVersion"
exit 1
