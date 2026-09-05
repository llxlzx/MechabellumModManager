param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath,
    [Parameter(Mandatory = $true)]
    [string] $OutFile
)

$ErrorActionPreference = "Stop"

function Get-AcfQuotedValue([string] $Text, [string] $Key) {
    $m = [regex]::Match($Text, ('"{0}"\s+"([^"]*)"' -f [regex]::Escape($Key)))
    if ($m.Success) { return $m.Groups[1].Value }
    return ""
}

try {
    $full = [IO.Path]::GetFullPath($GamePath)
    $common = Split-Path $full -Parent
    $steamapps = Split-Path $common -Parent
    $acf = Join-Path $steamapps "appmanifest_669330.acf"

    if (-not (Test-Path -LiteralPath $acf)) {
        Set-Content -LiteralPath $OutFile -Value "idle" -Encoding ASCII
        exit 0
    }

    $text = Get-Content -LiteralPath $acf -Raw -ErrorAction Stop
    $btd = Get-AcfQuotedValue $text "BytesToDownload"
    $bdd = Get-AcfQuotedValue $text "BytesDownloaded"

    # Mid-download: both present, download size non-zero, and not yet equal to downloaded.
    if (($btd -ne "") -and ($bdd -ne "") -and ($btd -ne "0") -and ($btd -ne $bdd)) {
        Set-Content -LiteralPath $OutFile -Value "downloading" -Encoding ASCII
    }
    else {
        Set-Content -LiteralPath $OutFile -Value "idle" -Encoding ASCII
    }
}
catch {
    # Fail open: do not skip Melon just because detection failed.
    Set-Content -LiteralPath $OutFile -Value "idle" -Encoding ASCII
}

exit 0
