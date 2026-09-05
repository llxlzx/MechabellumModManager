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

# Approximate sizes for user-facing messages (package / installed footprint).
$SizeHint = @{
    8 = @{ Package = "约 55-60 MB"; Installed = "约 150-200 MB" }
    6 = @{ Package = "约 50-55 MB"; Installed = "约 140-180 MB" }
}

$detect = Join-Path $PSScriptRoot "Detect-DotNetDesktop.ps1"
& $detect -MajorVersion $Major
if ($LASTEXITCODE -eq 0) {
    Write-Host "Skip .NET $Major Desktop — already installed."
    exit 0
}

$hint = $SizeHint[$Major]
Write-Host ("Preparing .NET {0} Desktop Runtime (download {1}, installed footprint {2})..." -f $Major, $hint.Package, $hint.Installed)

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
    Write-Host "Downloading .NET $Major Desktop Runtime from Microsoft CDN..."
    Write-Host "URL: $url"
    Write-Host "Expected package size: $($hint.Package). Please wait..."
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'Continue'
        Invoke-WebRequest -Uri $url -OutFile $installerPath -UseBasicParsing
        $bytes = (Get-Item $installerPath).Length
        Write-Host ("Download complete ({0:N1} MB)." -f ($bytes / 1MB))
    } catch {
        Write-Error "Failed to download .NET $Major Desktop Runtime. Install manually from https://dotnet.microsoft.com/download/dotnet/$Major.0 — $($_.Exception.Message)"
        exit 2
    }
}

Write-Host "Launching .NET $Major Desktop Runtime installer (/quiet — no separate finish UI)..."
Write-Host "Installed footprint typically $($hint.Installed)."
$p = Start-Process -FilePath $installerPath -ArgumentList "/install","/quiet","/norestart" -Wait -PassThru
$code = $p.ExitCode
# 0 = success, 3010 = success reboot required
if ($code -eq 0 -or $code -eq 3010) {
    Write-Host "Installed .NET $Major Desktop Runtime (exit $code)."
    exit 0
}

Write-Error "Runtime installer exited with code $code"
exit $code
