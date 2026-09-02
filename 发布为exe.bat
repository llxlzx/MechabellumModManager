@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 正在发布「钢铁指挥官Mod管理器.exe」...
dotnet publish "src\MechabellumModManager\MechabellumModManager.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "发布"
if errorlevel 1 (
  echo 发布失败。
  pause
  exit /b 1
)
copy /Y "发布\钢铁指挥官Mod管理器.exe" "钢铁指挥官Mod管理器.exe" >nul
echo.
echo 完成。请双击根目录或「发布」文件夹中的：
echo   钢铁指挥官Mod管理器.exe
echo.
echo 说明：需要本机已安装 .NET 8 桌面运行时（Desktop Runtime）。
echo 下载：https://dotnet.microsoft.com/download/dotnet/8.0
pause
