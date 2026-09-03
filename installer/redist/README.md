# Redistributables for the Setup

## MelonLoader — **required for release builds**

Place the official zip here before running `installer\build-installer`:

```
redist/melonloader/MelonLoader.x64.zip
```

Download: https://github.com/LavaGang/MelonLoader/releases  
(file name must be `MelonLoader.x64.zip`)

`build-installer` **hard-fails** if this file is missing or empty.  
Local debug only: PowerShell `-SkipMelonRedistCheck` or `set SKIP_MELON_REDIST_CHECK=1` — **do not** use for release.

Do **not** commit the zip to git (see `.gitignore`).

## .NET Desktop Runtime — optional

```
redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe
redist/dotnet6/windowsdesktop-runtime-6.*-win-x64.exe
```

If missing, the installer downloads from Microsoft CDN when those components are selected.
