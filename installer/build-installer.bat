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

echo [2/3] Ensuring redist folders + MelonLoader offline check...
if not exist "installer\redist\dotnet8" mkdir "installer\redist\dotnet8"
if not exist "installer\redist\dotnet6" mkdir "installer\redist\dotnet6"
if not exist "installer\redist\melonloader" mkdir "installer\redist\melonloader"

if /I "%SKIP_MELON_REDIST_CHECK%"=="1" (
  echo WARNING: SKIP_MELON_REDIST_CHECK=1 — do NOT use for release.
  goto :after_melon_check
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
:after_melon_check

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
