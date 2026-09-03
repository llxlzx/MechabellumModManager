; 钢铁指挥官 Mod 管理器 — Inno Setup 6
; Compile with: ISCC.exe MechabellumModManager.iss
; Or run: build-installer.bat

#define MyAppName "钢铁指挥官 Mod 管理器"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mechabellum Mod Manager"
#define MyAppExeName "MechabellumModManager.exe"
#define MyAppId "MechabellumModManager"

[Setup]
AppId={{A8F3C2E1-7B4D-4F91-9C2A-1E5B8D0F3A27}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\钢铁指挥官Mod管理器
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MechabellumModManager_Setup_v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
SetupLogging=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=
InfoBeforeFile=

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: unchecked

[Components]
Name: "main"; Description: "管理器本体（必选）"; Types: full compact custom; Flags: fixed
Name: "dotnet8"; Description: ".NET 8 Desktop Runtime x64（管理器需要；已安装则跳过；下载约 55-60 MB，安装后约 150-200 MB）"; Types: full compact custom
Name: "dotnet6"; Description: ".NET 6 Desktop Runtime x64（MelonLoader 需要；已安装则跳过；下载约 50-55 MB，安装后约 140-180 MB）"; Types: full compact custom
Name: "melon"; Description: "MelonLoader（安装包已内嵌离线包；已安装则跳过；一般无需访问 GitHub；默认建议安装到游戏目录）"; Types: full custom

[Files]
; Published app + assets (build-installer.bat publishes first)
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "..\publish\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main
; Helper scripts shipped with the app for repair / documentation
Source: "scripts\*"; DestDir: "{app}\installer-scripts"; Flags: ignoreversion; Components: main
Source: "redist\*"; DestDir: "{app}\installer-redist"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  GamePathPage: TInputDirWizardPage;
  RiskLabel: TNewStaticText;

procedure SetStatus(const Msg: string);
begin
  WizardForm.StatusLabel.Caption := Msg;
  WizardForm.StatusLabel.Update;
  WizardForm.Update;
end;

function PsFromSrc(const ScriptName, ExtraArgs: string): Integer;
var
  ResultCode: Integer;
  Cmd: string;
  ScriptPath: string;
begin
  ScriptPath := ExpandConstant('{app}') + '\installer-scripts\' + ScriptName;
  Cmd := '-NoProfile -ExecutionPolicy Bypass -File "' + ScriptPath + '" ' + ExtraArgs;
  if not Exec('powershell.exe', Cmd, '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := -1;
    exit;
  end;
  Result := ResultCode;
end;

function LooksLikeGame(const Path: string): Boolean;
begin
  Result := FileExists(AddBackslash(Path) + 'Mechabellum.exe') and
            FileExists(AddBackslash(Path) + 'GameAssembly.dll');
end;

function TrySteamGuess(): string;
var
  SteamPath: string;
  Candidate: string;
begin
  Result := '';
  if RegQueryStringValue(HKLM64, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', SteamPath) or
     RegQueryStringValue(HKLM32, 'SOFTWARE\Valve\Steam', 'InstallPath', SteamPath) or
     RegQueryStringValue(HKCU, 'SOFTWARE\Valve\Steam', 'SteamPath', SteamPath) then
  begin
    StringChangeEx(SteamPath, '/', '\', True);
    Candidate := AddBackslash(SteamPath) + 'steamapps\common\Mechabellum';
    if LooksLikeGame(Candidate) then
    begin
      Result := Candidate;
      exit;
    end;
  end;
  Candidate := 'C:\Program Files (x86)\Steam\steamapps\common\Mechabellum';
  if LooksLikeGame(Candidate) then Result := Candidate;
end;

procedure InitializeWizard;
var
  Guess: string;
begin
  GamePathPage := CreateInputDirPage(wpSelectDir,
    '选择游戏目录',
    '请指定 Mechabellum（钢铁指挥官）的安装目录。',
    '目录中需包含 Mechabellum.exe 与 GameAssembly.dll。安装程序会把该路径写入管理器配置。' + #13#10 +
    '若勾选 MelonLoader，将安装到此目录。',
    False, '');
  GamePathPage.Add('游戏路径');
  Guess := TrySteamGuess();
  if Guess <> '' then
    GamePathPage.Values[0] := Guess;

  RiskLabel := TNewStaticText.Create(WizardForm);
  RiskLabel.Parent := WizardForm.WelcomePage;
  RiskLabel.Left := ScaleX(20);
  RiskLabel.Top := ScaleY(160);
  RiskLabel.Width := WizardForm.WelcomePage.Width - ScaleX(40);
  RiskLabel.Height := ScaleY(80);
  RiskLabel.WordWrap := True;
  RiskLabel.Caption :=
    '风险提示：本工具用于客户端 QoL Mod。修改战斗逻辑可能导致 Data Error 与处罚；官方未支持 Mod，风险自负。';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = GamePathPage.ID then
  begin
    if not LooksLikeGame(GamePathPage.Values[0]) then
    begin
      MsgBox('游戏路径无效：未找到 Mechabellum.exe 与 GameAssembly.dll。', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  GamePath, Redist, Args: string;
  Code: Integer;
begin
  if CurStep <> ssPostInstall then exit;

  GamePath := GamePathPage.Values[0];
  Redist := ExpandConstant('{app}\installer-redist');

  { Always write manager config }
  SetStatus('正在写入管理器配置…');
  Args := '-GamePath "' + GamePath + '"';
  Code := PsFromSrc('Write-ManagerConfig.ps1', Args);
  if Code <> 0 then
    MsgBox('写入管理器配置失败（exit ' + IntToStr(Code) + '）。可稍后在管理器「设置」中手动指定游戏路径。', mbError, MB_OK);

  if WizardIsComponentSelected('dotnet8') then
  begin
    SetStatus('正在准备 .NET 8 Desktop Runtime（下载约 55-60 MB；安装后约 150-200 MB；已安装则跳过）…');
    Args := '-Major 8 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) then
      MsgBox('.NET 8 Desktop Runtime 安装未成功（exit ' + IntToStr(Code) + '）。请稍后从 https://dotnet.microsoft.com/download/dotnet/8.0 手动安装。', mbError, MB_OK)
    else
      SetStatus('.NET 8 Desktop Runtime 已完成（或已跳过）。');
  end;

  if WizardIsComponentSelected('dotnet6') then
  begin
    SetStatus('正在准备 .NET 6 Desktop Runtime（下载约 50-55 MB；安装后约 140-180 MB；已安装则跳过）…');
    Args := '-Major 6 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) then
      MsgBox('.NET 6 Desktop Runtime 安装未成功（exit ' + IntToStr(Code) + '）。请稍后从 https://dotnet.microsoft.com/download/dotnet/6.0 手动安装。', mbError, MB_OK)
    else
      SetStatus('.NET 6 Desktop Runtime 已完成（或已跳过）。');
  end;

  if WizardIsComponentSelected('melon') then
  begin
    SetStatus('正在检测/安装 MelonLoader（已安装则跳过；优先内嵌离线包）…');
    Args := '-GamePath "' + GamePath + '" -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-MelonLoader.ps1', Args);
    if Code <> 0 then
      MsgBox(
        'MelonLoader 安装未成功（exit ' + IntToStr(Code) + '）。' + #13#10 + #13#10 +
        'exit 1：路径无效或文件被占用；exit 2：多为 GitHub 下载失败；exit 3：安装不完整。' + #13#10 + #13#10 +
        '也可取消 MelonLoader 组件后重装管理器，或手动安装：' + #13#10 +
        'https://github.com/LavaGang/MelonLoader/releases' + #13#10 +
        '（下载 MelonLoader.x64.zip 后按官方说明解压到游戏目录）',
        mbError, MB_OK)
    else
      SetStatus('MelonLoader 已完成（或已跳过）。');
  end;

  SetStatus('安装后置步骤完成。');
end;
