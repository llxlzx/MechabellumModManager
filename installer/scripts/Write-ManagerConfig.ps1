param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath
)

$ErrorActionPreference = "Stop"

$root = Join-Path $env:APPDATA "MechabellumModManager"
New-Item -ItemType Directory -Force -Path $root | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "library\mods") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "library\plugins") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "library\userlibs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "library\userdata") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "profiles") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "logs") | Out-Null

$configPath = Join-Path $root "config.json"
$obj = [ordered]@{
    gamePath         = $GamePath
    launchMode       = 0
    activeProfileId  = "default"
    dataRoot         = $null
}

if (Test-Path $configPath) {
    try {
        $existing = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $existing.launchMode) { $obj.launchMode = [int]$existing.launchMode }
        if ($existing.activeProfileId) { $obj.activeProfileId = [string]$existing.activeProfileId }
        if ($existing.PSObject.Properties.Name -contains "dataRoot") { $obj.dataRoot = $existing.dataRoot }
    } catch { }
}

$json = ($obj | ConvertTo-Json -Depth 5)
[IO.File]::WriteAllText($configPath, $json, [Text.UTF8Encoding]::new($false))

$profile = Join-Path $root "profiles\default.json"
if (-not (Test-Path $profile)) {
    $defaultName = [Text.Encoding]::UTF8.GetString([byte[]](0xE9,0xBB,0x98,0xE8,0xAE,0xA4))
    $profileObj = [ordered]@{
        id                 = "default"
        name               = $defaultName
        enabledPackageIds  = @()
    }
    $profileJson = ($profileObj | ConvertTo-Json -Depth 5)
    [IO.File]::WriteAllText($profile, $profileJson, [Text.UTF8Encoding]::new($false))
}

Write-Host "Wrote config: $configPath"
exit 0
