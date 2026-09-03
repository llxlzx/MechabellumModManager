@echo off
REM ASCII-only paths to avoid GBK/UTF-8 mojibake under cmd.exe
cd /d "%~dp0"
echo Publishing MechabellumModManager.exe ...
dotnet publish "src\MechabellumModManager\MechabellumModManager.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "publish"
if errorlevel 1 (
  echo Publish failed.
  pause
  exit /b 1
)
if not exist "publish\MechabellumModManager.exe" (
  echo Missing publish\MechabellumModManager.exe
  pause
  exit /b 1
)
copy /Y "publish\MechabellumModManager.exe" "MechabellumModManager.exe" >nul
echo.
echo Done. Output:
echo   publish\MechabellumModManager.exe
echo   MechabellumModManager.exe  (copy in repo root)
echo.
echo Requires .NET 8 Desktop Runtime:
echo   https://dotnet.microsoft.com/download/dotnet/8.0
pause
