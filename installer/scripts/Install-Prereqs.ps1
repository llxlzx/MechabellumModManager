param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(6, 8)]
    [int] $Major,
    [Parameter(Mandatory = $true)]
    [string] $RedistDir,
    [string] $WorkDir = $env:TEMP
)

$ErrorActionPreference = "Stop"

# Prefer a recent known-good offline installer; update when bumping redist cache.
$Urls = @{
    8 = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.16/windowsdesktop-runtime-8.0.16-win-x64.exe"
    6 = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/6.0.36/windowsdesktop-runtime-6.0.36-win-x64.exe"
}

$detect = Join-Path $PSScriptRoot "Detect-DotNetDesktop.ps1"
& $detect -MajorVersion $Major
if ($LASTEXITCODE -eq 0) {
    Write-Host "Skip .NET $Major Desktop — already installed."
    exit 0
}

$localDir = Join-Path $RedistDir ("dotnet" + $Major)
$local = $null
if (Test-Path $localDir) {
    $local = Get-ChildItem -Path $localDir -Filter "windowsdesktop-runtime-$Major.*-win-x64.exe" -File -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
}

$installerPath = $null
if ($local) {
    $installerPath = $local.FullName
    Write-Host "Using local redistributable: $installerPath"
} else {
    $url = $Urls[$Major]
    $destDir = Join-Path $WorkDir "mmm-dotnet-redist"
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $installerPath = Join-Path $destDir ("windowsdesktop-runtime-{0}-win-x64.exe" -f $Major)
    Write-Host "Downloading $url ..."
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $installerPath -UseBasicParsing
    } catch {
        Write-Error "Failed to download .NET $Major Desktop Runtime. Install manually from https://dotnet.microsoft.com/download/dotnet/$Major.0 — $($_.Exception.Message)"
        exit 2
    }
}

Write-Host "Installing .NET $Major Desktop Runtime (quiet)..."
$p = Start-Process -FilePath $installerPath -ArgumentList "/install","/quiet","/norestart" -Wait -PassThru
$code = $p.ExitCode
# 0 = success, 3010 = success reboot required
if ($code -eq 0 -or $code -eq 3010) {
    Write-Host "Installed .NET $Major Desktop Runtime (exit $code)."
    exit 0
}

Write-Error "Runtime installer exited with code $code"
exit $code
