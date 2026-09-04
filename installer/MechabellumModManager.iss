; 钢铁指挥官 Mod 管理器 — Inno Setup 6
; Compile with: ISCC.exe MechabellumModManager.iss
; Or run: build-installer.bat

#define MyAppName "钢铁指挥官 Mod 管理器"
#define MyAppVersion "1.0.8"
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
; Default is "auto": skips manager install-dir page on upgrade/reinstall. Always show it.
DisableDirPage=no
AlwaysShowDirOnReadyPage=yes
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
LicenseFile=EULA.zh-CN.txt
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
; Used during wizard before files are copied to {app}
Source: "scripts\Detect-GamePath.ps1"; Flags: dontcopy

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

function PowerShellExe: string;
begin
  { Prefer System32 PowerShell; avoid bare "powershell.exe" PATH issues under Setup. }
  Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  if not FileExists(Result) then
    Result := 'powershell.exe';
end;

function PsFromSrc(const ScriptName, ExtraArgs: string): Integer;
var
  ResultCode: Integer;
  Cmd: string;
  ScriptPath: string;
begin
  ScriptPath := ExpandConstant('{app}') + '\installer-scripts\' + ScriptName;
  if not FileExists(ScriptPath) then
  begin
    Result := -2;
    exit;
  end;
  { Hidden + NonInteractive reduces 0xc0000142 / desktop-heap failures under elevated Setup. }
  Cmd := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "' +
         ScriptPath + '" ' + ExtraArgs;
  if not Exec(PowerShellExe(), Cmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
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

function JsonUnescapePath(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '\\', '\', True);
end;

{ Best-effort extract of "steamLinkPath":"..." from branch-switch.json without PowerShell. }
function TryReadSteamLinkFromBranchSwitch: string;
var
  BranchFile, Content, Key, Marker: string;
  ContentA: AnsiString;
  P, Q: Integer;
begin
  Result := '';
  BranchFile := ExpandConstant('{userappdata}\MechabellumModManager\branch-switch.json');
  if not FileExists(BranchFile) then
    exit;
  if not LoadStringFromFile(BranchFile, ContentA) then
    exit;
  Content := string(ContentA);
  Key := '"steamLinkPath"';
  P := Pos(Key, Content);
  if P = 0 then
  begin
    Key := '"SteamLinkPath"';
    P := Pos(Key, Content);
  end;
  if P = 0 then
    exit;
  Marker := Copy(Content, P + Length(Key), Length(Content));
  P := Pos('"', Marker);
  if P = 0 then
    exit;
  Marker := Copy(Marker, P + 1, Length(Marker));
  Q := Pos('"', Marker);
  if Q <= 1 then
    exit;
  Result := JsonUnescapePath(Copy(Marker, 1, Q - 1));
  if not LooksLikeGame(Result) then
    Result := '';
end;

function EscapeJsonPath(const Path: string): string;
begin
  Result := Path;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

function WriteManagerConfigNative(const GamePath: string): Boolean;
var
  Root, ConfigPath, ProfilePath, Json, Resolved, Link: string;
  ProfileJson: AnsiString;
begin
  Result := False;
  Resolved := GamePath;
  Link := TryReadSteamLinkFromBranchSwitch();
  if Link <> '' then
    Resolved := Link;

  if not LooksLikeGame(Resolved) then
    exit;

  Root := ExpandConstant('{userappdata}\MechabellumModManager');
  if not ForceDirectories(Root) then
    exit;
  ForceDirectories(Root + '\library\mods');
  ForceDirectories(Root + '\library\plugins');
  ForceDirectories(Root + '\library\userlibs');
  ForceDirectories(Root + '\library\userdata');
  ForceDirectories(Root + '\profiles');
  ForceDirectories(Root + '\logs');

  ConfigPath := Root + '\config.json';
  Json :=
    '{' + #13#10 +
    '  "gamePath": "' + EscapeJsonPath(Resolved) + '",' + #13#10 +
    '  "launchMode": 0,' + #13#10 +
    '  "activeProfileId": "default",' + #13#10 +
    '  "dataRoot": null' + #13#10 +
    '}' + #13#10;
  if not SaveStringToFile(ConfigPath, Json, False) then
    exit;

  ProfilePath := Root + '\profiles\default.json';
  if not FileExists(ProfilePath) then
  begin
    ProfileJson :=
      '{' + #13#10 +
      '  "id": "default",' + #13#10 +
      '  "name": "default",' + #13#10 +
      '  "enabledPackageIds": []' + #13#10 +
      '}' + #13#10;
    SaveStringToFile(ProfilePath, ProfileJson, False);
  end;

  Result := True;
end;

function DetectGamePathViaScript: string;
var
  ResultCode: Integer;
  Cmd, OutFile, ScriptFile, S: string;
  Line: AnsiString;
begin
  Result := '';
  ExtractTemporaryFile('Detect-GamePath.ps1');
  ScriptFile := ExpandConstant('{tmp}\Detect-GamePath.ps1');
  OutFile := ExpandConstant('{tmp}\mmm-detected-game-path.txt');
  DeleteFile(OutFile);
  Cmd := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "' +
         ScriptFile + '" -OutFile "' + OutFile + '"';
  if not Exec(PowerShellExe(), Cmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    exit;
  if ResultCode <> 0 then
    exit;
  if not FileExists(OutFile) then
    exit;
  if LoadStringFromFile(OutFile, Line) then
  begin
    S := string(Line);
    StringChangeEx(S, #13, '', True);
    StringChangeEx(S, #10, '', True);
    Result := Trim(S);
  end;
end;

function TrySteamGuess(): string;
var
  SteamPath: string;
  Candidate: string;
  Link: string;
begin
  { 1) Previous dual-folder link (no PowerShell) }
  Link := TryReadSteamLinkFromBranchSwitch();
  if (Link <> '') and LooksLikeGame(Link) then
  begin
    Result := Link;
    exit;
  end;

  { 2) PowerShell scan (optional; may fail with 0xc0000142 on some PCs) }
  Result := DetectGamePathViaScript();
  if (Result <> '') and LooksLikeGame(Result) then
    exit;

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
    Candidate := AddBackslash(SteamPath) + 'steamapps\common\Mechabellum_official';
    if LooksLikeGame(Candidate) then
    begin
      Result := Candidate;
      exit;
    end;
  end;
  Candidate := 'C:\Program Files (x86)\Steam\steamapps\common\Mechabellum';
  if LooksLikeGame(Candidate) then
  begin
    Result := Candidate;
    exit;
  end;
  Candidate := 'D:\steam\steamapps\common\Mechabellum';
  if LooksLikeGame(Candidate) then
  begin
    Result := Candidate;
    exit;
  end;
  Candidate := 'D:\steam\steamapps\common\Mechabellum_official';
  if LooksLikeGame(Candidate) then
    Result := Candidate;
end;

function QuerySteamBusyViaPs: Boolean;
var
  ResultCode: Integer;
  OutFile, Cmd, S: string;
  Line: AnsiString;
begin
  { Default: assume busy so we never write Melon into an active download folder if PS fails. }
  Result := True;
  OutFile := ExpandConstant('{tmp}\mmm-steam-busy.txt');
  DeleteFile(OutFile);
  Cmd := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -Command "' +
         'if (Get-Process steam,steamwebhelper -ErrorAction SilentlyContinue) { Set-Content -LiteralPath ''' +
         OutFile + ''' -Value busy } else { Set-Content -LiteralPath ''' + OutFile + ''' -Value idle }"';
  if not Exec(PowerShellExe(), Cmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    exit;
  if FileExists(OutFile) and LoadStringFromFile(OutFile, Line) then
  begin
    S := string(Line);
    StringChangeEx(S, #13, '', True);
    StringChangeEx(S, #10, '', True);
    Result := Pos('busy', LowerCase(Trim(S))) > 0;
  end;
end;

procedure InitializeWizard;
var
  Guess: string;
begin
  GamePathPage := CreateInputDirPage(wpSelectDir,
    '选择游戏目录',
    '请指定 Mechabellum（钢铁指挥官）的安装目录。',
    '目录中需包含 Mechabellum.exe 与 GameAssembly.dll。' + #13#10 +
    '安装程序会自动检索 Steam 库（含 D:\steam 等），优先填入游戏根目录 Mechabellum；' +
    '若曾启用双服，会优先使用上次记录的路径。' + #13#10 +
    '若勾选 MelonLoader：Steam 正在下载时不会写入测试服目录，以免打断下载。',
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
  RiskLabel.Caption := '请仔细阅读下一页用户协议；同意后方可继续安装。';
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
  SteamBusy: Boolean;
begin
  if CurStep <> ssPostInstall then exit;

  GamePath := GamePathPage.Values[0];
  Redist := ExpandConstant('{app}\installer-redist');

  { Native config write first — does not require PowerShell (avoids 0xc0000142).
    Does NOT exit Steam / rewrite BetaKey / swap junction. }
  SetStatus('正在写入管理器配置（保留双服记录，不打断 Steam）…');
  if not WriteManagerConfigNative(GamePath) then
    MsgBox('写入管理器配置失败。可稍后在管理器「设置」中手动指定游戏路径。', mbError, MB_OK);

  { Optional extras via PowerShell; failures are non-fatal. }
  Args := '-GamePath "' + GamePath + '" -RedistDir "' + Redist + '"';
  Code := PsFromSrc('Restore-DualFolderConfig.ps1', Args);
  if Code <> 0 then
    SetStatus('可选恢复脚本未运行（不影响安装；配置已用内置方式写入）。');

  SteamBusy := QuerySteamBusyViaPs();

  if WizardIsComponentSelected('dotnet8') then
  begin
    SetStatus('正在准备 .NET 8 Desktop Runtime（下载约 55-60 MB；安装后约 150-200 MB；已安装则跳过）…');
    Args := '-Major 8 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) and (Code <> -1) then
      MsgBox('.NET 8 Desktop Runtime 安装未成功（exit ' + IntToStr(Code) + '）。请稍后从 https://dotnet.microsoft.com/download/dotnet/8.0 手动安装。', mbError, MB_OK)
    else if Code = -1 then
      SetStatus('无法启动 PowerShell，已跳过 .NET 8 自动安装（可稍后手动安装）。')
    else
      SetStatus('.NET 8 Desktop Runtime 已完成（或已跳过）。');
  end;

  if WizardIsComponentSelected('dotnet6') then
  begin
    SetStatus('正在准备 .NET 6 Desktop Runtime（下载约 50-55 MB；安装后约 140-180 MB；已安装则跳过）…');
    Args := '-Major 6 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) and (Code <> -1) then
      MsgBox('.NET 6 Desktop Runtime 安装未成功（exit ' + IntToStr(Code) + '）。请稍后从 https://dotnet.microsoft.com/download/dotnet/6.0 手动安装。', mbError, MB_OK)
    else if Code = -1 then
      SetStatus('无法启动 PowerShell，已跳过 .NET 6 自动安装（可稍后手动安装）。')
    else
      SetStatus('.NET 6 Desktop Runtime 已完成（或已跳过）。');
  end;

  if WizardIsComponentSelected('melon') then
  begin
    if SteamBusy then
    begin
      SetStatus('检测到 Steam 正在运行/下载，或无法确认：跳过向游戏目录写入 MelonLoader，以免打断下载。');
    end
    else
    begin
      SetStatus('正在检测/安装 MelonLoader（已安装则跳过；优先内嵌离线包）…');
      Args := '-GamePath "' + GamePath + '" -RedistDir "' + Redist + '"';
      Code := PsFromSrc('Install-MelonLoader.ps1', Args);
      if Code = -1 then
        SetStatus('无法启动 PowerShell，已跳过 MelonLoader 自动安装（可稍后手动安装）。')
      else if Code <> 0 then
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
  end;

  SetStatus('安装后置步骤完成。');
end;
