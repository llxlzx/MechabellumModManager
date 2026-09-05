# Redistributables for the Setup

## MelonLoader — **required for release builds**

Place the official zip here before running `installer\build-installer`:

```
redist/melonloader/MelonLoader.x64.zip
```

Download: https://github.com/LavaGang/MelonLoader/releases  
(file name must be `MelonLoader.x64.zip`)

## Unity Il2Cpp dependencies — **required for release builds**

Place at least one matching zip here:

```
redist/unity-deps/UnityDependencies_{major.minor.patch}.zip
```

Example for Mechabellum on Unity `2022.3.62f3`:

```
redist/unity-deps/UnityDependencies_2022.3.62.zip
```

Download: https://github.com/LavaGang/Unity-Runtime-Libraries  
Upstream files are named by patch only (e.g. `2022.3.62.zip`). **Rename** them to `UnityDependencies_2022.3.62.zip` before placing in `unity-deps/`. You may keep multiple versions for different game builds.

## .NET Desktop Runtime

**.NET 8 — required for release builds:**

```
redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe
```

Download: https://dotnet.microsoft.com/download/dotnet/8.0  
(Windows x64 Desktop Runtime offline installer)

**.NET 6 — optional:**

```
redist/dotnet6/windowsdesktop-runtime-6.*-win-x64.exe
```

If .NET 6 is missing, the installer downloads from Microsoft CDN when that component is selected.

## Build gate

`build-installer` **hard-fails** if any required file above is missing or empty.

Local debug only: PowerShell `-SkipMelonRedistCheck` or `set SKIP_MELON_REDIST_CHECK=1` skips **all** release redist checks (MelonLoader, UnityDependencies, and .NET 8) — **do not** use for release.

Do **not** commit large binaries to git (see `.gitignore`).
