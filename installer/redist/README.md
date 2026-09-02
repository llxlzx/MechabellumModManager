# Offline redistributables (optional)

Place official installers here to enable offline / faster installs.
Filenames are matched by prefix; keep Microsoft/GitHub originals.

```
redist/
  dotnet8/windowsdesktop-runtime-8.*-win-x64.exe
  dotnet6/windowsdesktop-runtime-6.*-win-x64.exe
  melonloader/MelonLoader.x64.zip
```

If a file is missing, the installer downloads from official URLs.

Do **not** commit large binaries to git (see `.gitignore`).
