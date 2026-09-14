param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath,
    [string] $UiLanguage = ""
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

if (Test-Path -LiteralPath $configPath) {
    $existing = $null
    try {
        $existing = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        try {
            $existing = Get-Content -LiteralPath $configPath -Raw -Encoding Default | ConvertFrom-Json
        } catch { }
    }
    if ($null -ne $existing) {
        $merged = [ordered]@{}
        foreach ($p in $existing.PSObject.Properties) {
            $merged[$p.Name] = $p.Value
        }
        $merged["gamePath"] = $GamePath
        if ($null -eq $merged["launchMode"]) { $merged["launchMode"] = 0 }
        if (-not $merged["activeProfileId"]) { $merged["activeProfileId"] = "default" }
        if (-not ($merged.Keys -contains "dataRoot")) { $merged["dataRoot"] = $null }
        $obj = $merged
    }
}

if ($UiLanguage) {
    $obj.uiLanguage = $UiLanguage
}

$json = ($obj | ConvertTo-Json -Depth 5)
[IO.File]::WriteAllText($configPath, $json, [Text.UTF8Encoding]::new($false))

# Plan A: also refresh machine-wide seed when this script runs (elevated or original user).
$seedRoot = Join-Path $env:ProgramData "MechabellumModManager"
New-Item -ItemType Directory -Force -Path $seedRoot | Out-Null
$seed = [ordered]@{ gamePath = $GamePath }
if ($obj.uiLanguage) { $seed.uiLanguage = [string]$obj.uiLanguage }
elseif ($UiLanguage) { $seed.uiLanguage = $UiLanguage }
[IO.File]::WriteAllText(
    (Join-Path $seedRoot "install-defaults.json"),
    ($seed | ConvertTo-Json -Depth 5),
    [Text.UTF8Encoding]::new($false))

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
