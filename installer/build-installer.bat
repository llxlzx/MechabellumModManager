@echo off
setlocal
cd /d "%~dp0\.."

echo [1/3] Publishing manager...
dotnet publish "src\MechabellumModManager\MechabellumModManager.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "publish"
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

if not exist "publish\MechabellumModManager.exe" (
  echo Missing published exe.
  exit /b 1
)

if not exist "publish\Assets" mkdir "publish\Assets"
copy /Y "src\MechabellumModManager\Assets\*" "publish\Assets\" >nul

echo [2/3] Ensuring redist folders + offline redist check...
if not exist "installer\redist\dotnet8" mkdir "installer\redist\dotnet8"
if not exist "installer\redist\dotnet6" mkdir "installer\redist\dotnet6"
if not exist "installer\redist\melonloader" mkdir "installer\redist\melonloader"
if not exist "installer\redist\unity-deps" mkdir "installer\redist\unity-deps"

if /I "%SKIP_MELON_REDIST_CHECK%"=="1" (
  echo WARNING: SKIP_MELON_REDIST_CHECK=1 — skips all release redist checks. Do NOT use for release.
  goto :after_redist_check
)
if not exist "installer\redist\melonloader\MelonLoader.x64.zip" (
  echo ERROR: Missing installer\redist\melonloader\MelonLoader.x64.zip
  echo Place the official MelonLoader.x64.zip there before building a release Setup.
  echo Download: https://github.com/LavaGang/MelonLoader/releases
  echo Local debug only: set SKIP_MELON_REDIST_CHECK=1
  exit /b 3
)
for %%A in ("installer\redist\melonloader\MelonLoader.x64.zip") do if %%~zA==0 (
  echo ERROR: MelonLoader.x64.zip is empty.
  exit /b 3
)
echo Found MelonLoader offline zip.

set "UNITY_DEPS_OK=0"
for %%F in ("installer\redist\unity-deps\UnityDependencies_*.zip") do (
  if exist "%%~fF" if not "%%~zF"=="0" set "UNITY_DEPS_OK=1"
)
if "%UNITY_DEPS_OK%"=="0" (
  echo ERROR: Missing non-empty installer\redist\unity-deps\UnityDependencies_*.zip
  echo Download: https://github.com/LavaGang/Unity-Runtime-Libraries
  echo Rename upstream files e.g. 2022.3.62.zip to UnityDependencies_2022.3.62.zip
  echo Local debug only: set SKIP_MELON_REDIST_CHECK=1
  exit /b 3
)
echo Found UnityDependencies offline zip.

set "DOTNET8_OK=0"
for %%F in ("installer\redist\dotnet8\windowsdesktop-runtime-8.*-win-x64.exe") do (
  if exist "%%~fF" if not "%%~zF"=="0" set "DOTNET8_OK=1"
)
if "%DOTNET8_OK%"=="0" (
  echo ERROR: Missing non-empty installer\redist\dotnet8\windowsdesktop-runtime-8.*-win-x64.exe
  echo Download: https://dotnet.microsoft.com/download/dotnet/8.0
  echo Local debug only: set SKIP_MELON_REDIST_CHECK=1
  exit /b 3
)
echo Found .NET 8 offline installer.
:after_redist_check

echo [3/3] Compiling Inno Setup...
set "ISCC="
where ISCC >nul 2>&1 && for /f "delims=" %%I in ('where ISCC') do set "ISCC=%%I" & goto :have_iscc
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
:have_iscc

if not defined ISCC (
  echo ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php
  echo Published app is ready under publish\
  exit /b 2
)

echo Using ISCC: %ISCC%
"%ISCC%" "installer\MechabellumModManager.iss"
if errorlevel 1 (
  echo ISCC failed.
  exit /b 1
)

echo Done. Output under dist\
dir /b "dist\*Setup*.exe" 2>nul
exit /b 0
