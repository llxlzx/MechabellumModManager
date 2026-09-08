# Redistributables staging (mirror upload — not embedded in thin Setup)

From **v1.2.0** the Inno Setup is **thin**: MelonLoader / UnityDependencies / Cpp2IL / .NET are **not** packed into the installer. They live under `{mirror}/MechabellumRedist/` and are downloaded at Setup post-install (`--ensure-redist`) or when the user clicks Install MelonLoader in the app.

Keep this folder as **staging for `tools/sync-mirror.ps1`** (default includes redist; use `-SkipRedist` only for debugging).

## Required staging layout

```
redist/melonloader/MelonLoader.x64.zip
redist/unity-deps/UnityDependencies_2022.3.62.zip
redist/cpp2il/Cpp2IL.exe
redist/cpp2il/Cpp2IL.Plugin.StrippedCodeRegSupport.dll
redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe
```

Artifact ids / origin URLs / expected hashes are defined in:

`src/MechabellumModManager/Assets/redist-manifest.json`

`sync-mirror` re-hashes local files and uploads `MechabellumRedist/manifest.json` last.

## Sources

| Artifact | Upstream |
|----------|----------|
| MelonLoader.x64.zip | https://github.com/LavaGang/MelonLoader/releases (pin v0.7.3) |
| UnityDependencies_*.zip | https://github.com/LavaGang/Unity-Runtime-Libraries (rename `2022.3.62.zip`) |
| Cpp2IL | https://github.com/SamboyCoding/Cpp2IL/releases/tag/2022.1.0-pre-release.21 |
| .NET 8 Desktop | https://dotnet.microsoft.com/download/dotnet/8.0 |

## Build note

`build-installer.ps1` only **warns** if staging is incomplete; thin Setup still builds. A release that ships without a successful `sync-mirror` (redist) will fail China installs at the download step.
