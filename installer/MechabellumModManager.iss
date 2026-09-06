; Mechabellum Mod Manager — Inno Setup 6
; Compile with: ISCC.exe MechabellumModManager.iss
; Or run: build-installer.bat

#define MyAppName "Mechabellum Mod Manager"
#define MyAppVersion "1.1.3"
#define MyAppPublisher "Mechabellum Mod Manager"
#define MyAppExeName "MechabellumModManager.exe"
#define MyAppId "MechabellumModManager"

[Setup]
AppId={{A8F3C2E1-7B4D-4F91-9C2A-1E5B8D0F3A27}
AppName={cm:AppDisplayName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MechabellumModManager
DefaultGroupName={cm:AppDisplayName}
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
ShowLanguageDialog=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
InfoBeforeFile=
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}

[Messages]
english.SetupAppRunningError=Setup has detected that %1 is currently running.%n%nPlease close all instances of it now, then click OK to continue, or Cancel to exit.%n%nIf Mechabellum Mod Manager is switching between live and test branches or installing MelonLoader, finish or cancel that task in the manager and exit normally before continuing setup. Force-ending the process is not recommended.
english.ApplicationsFound=The following applications are using files that need to be updated by Setup.%n%nIf Mechabellum Mod Manager is switching between live and test branches or installing MelonLoader, finish or cancel that task in the manager and exit normally before continuing setup. Force-ending the process is not recommended.%n%nIf the manager is not busy, you may allow Setup to automatically close these applications.
english.ApplicationsFound2=The following applications are using files that need to be updated by Setup.%n%nIf Mechabellum Mod Manager is switching between live and test branches or installing MelonLoader, finish or cancel that task in the manager and exit normally before continuing setup. Force-ending the process is not recommended.%n%nIf the manager is not busy, you may allow Setup to automatically close these applications; after installation, Setup will attempt to restart them. Alternatively, close the applications yourself and click Retry.
english.ErrorCloseApplications=Setup was unable to automatically close all applications. It is recommended that you close all applications using files that need to be updated by Setup before continuing.%n%nIf Mechabellum Mod Manager is switching between live and test branches or installing MelonLoader, finish or cancel that task in the manager and exit normally before continuing setup. Force-ending the process is not recommended.

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"; LicenseFile: "EULA.zh-CN.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "EULA.en.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"; LicenseFile: "EULA.ru.txt"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; LicenseFile: "EULA.ja.txt"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"; LicenseFile: "EULA.de.txt"

[CustomMessages]
chinesesimplified.AppDisplayName=钢铁指挥官 Mod 管理器
chinesesimplified.TaskDesktop=创建桌面快捷方式
chinesesimplified.TaskDesktopGroup=附加选项:
chinesesimplified.CompMain=管理器本体（必选）
chinesesimplified.CompDotNet8=.NET 8 Desktop Runtime x64（管理器需要；已安装则跳过；下载约 55-60 MB，安装后约 150-200 MB）
chinesesimplified.CompDotNet6=.NET 6 Desktop Runtime x64（MelonLoader 需要；已安装则跳过；下载约 50-55 MB，安装后约 140-180 MB）
chinesesimplified.CompMelon=MelonLoader（安装包已内嵌离线包；低于 0.7.3 会升级；一般无需访问 GitHub；默认建议安装到游戏目录）
chinesesimplified.RunNow=立即启动 %1
chinesesimplified.RiskWelcome=请仔细阅读下一页用户协议；同意后方可继续安装。
chinesesimplified.GamePathTitle=选择游戏目录
chinesesimplified.GamePathSub=请指定 Mechabellum（钢铁指挥官）的安装目录。
chinesesimplified.GamePathDesc=目录中需包含 Mechabellum.exe 与 GameAssembly.dll。%n安装程序会自动检索 Steam 库（含 D:\steam 等），优先填入游戏根目录 Mechabellum；若曾启用双服，会优先使用上次记录的路径。%n若勾选 MelonLoader：Steam 正在下载时不会写入测试服目录，以免打断下载。
chinesesimplified.GamePathLabel=游戏路径
chinesesimplified.ErrBadGamePath=游戏路径无效：未找到 Mechabellum.exe 与 GameAssembly.dll。
chinesesimplified.StatusWriteConfig=正在写入管理器配置（保留双服记录，不打断 Steam）…
chinesesimplified.StatusSeedUserConfig=正在写入当前用户配置（无界面，请稍候）…
chinesesimplified.ErrWriteConfig=写入管理器配置失败。可稍后在管理器「设置」中手动指定游戏路径。
chinesesimplified.StatusRestoreOptional=可选恢复脚本未运行（不影响安装；配置已用内置方式写入）。
chinesesimplified.StatusSanitizeAcf=正在清理无效的 Steam 分支快照（不修改 Steam 本体）…
chinesesimplified.StatusDotNet8=正在静默安装 .NET 8 Desktop Runtime（下载约 55-60 MB；进度条可能短暂不动，请稍候；已安装则跳过）…
chinesesimplified.ErrDotNet8=.NET 8 Desktop Runtime 安装未成功（exit %1）。请稍后从 https://dotnet.microsoft.com/download/dotnet/8.0 手动安装。
chinesesimplified.StatusNoPsDotNet8=无法启动 PowerShell，已跳过 .NET 8 自动安装（可稍后手动安装）。
chinesesimplified.StatusDotNet8Done=.NET 8 Desktop Runtime 已完成（或已跳过）。
chinesesimplified.StatusDotNet6=正在静默安装 .NET 6 Desktop Runtime（下载约 50-55 MB；进度条可能短暂不动，请稍候；已安装则跳过）…
chinesesimplified.ErrDotNet6=.NET 6 Desktop Runtime 安装未成功（exit %1）。请稍后从 https://dotnet.microsoft.com/download/dotnet/6.0 手动安装。
chinesesimplified.StatusNoPsDotNet6=无法启动 PowerShell，已跳过 .NET 6 自动安装（可稍后手动安装）。
chinesesimplified.StatusDotNet6Done=.NET 6 Desktop Runtime 已完成（或已跳过）。
chinesesimplified.StatusSteamBusyMelon=检测到 Steam 正在下载本游戏，已跳过写入 MelonLoader（避免打断下载）。请等下载完成后再运行安装包并勾选 MelonLoader，或自行安装。
chinesesimplified.WarnSteamBusyMelon=检测到 Steam 正在下载本游戏，安装程序已跳过 MelonLoader。%n%n请等下载完成后再运行本安装包（勾选 MelonLoader），否则管理器会提示「缺少 Loader」，且「应用并启动」不可用。
chinesesimplified.StatusMelon=正在检测/安装 MelonLoader（≥0.7.3 则跳过；优先内嵌离线包）…
chinesesimplified.StatusNoPsMelon=无法启动 PowerShell；将改用管理器内嵌方式安装 MelonLoader。
chinesesimplified.ErrMelon=MelonLoader 安装未成功（exit %1）。%n%nexit 1：路径无效或文件被占用；exit 2：多为 GitHub 下载失败；exit 3：安装不完整；exit 5：游戏正在运行。%n%n也可打开管理器点击「安装 MelonLoader」，或手动安装：%nhttps://github.com/LavaGang/MelonLoader/releases%n（下载 MelonLoader.x64.zip 后按官方说明解压到游戏目录）
chinesesimplified.StatusMelonDone=MelonLoader 已完成（或已跳过）。
chinesesimplified.StatusMelonAlready=检测到 MelonLoader ≥0.7.3 已就绪，跳过安装。
chinesesimplified.ErrMelonVerify=MelonLoader 未能正确写入游戏目录。%n%n请关闭游戏后：打开管理器点「安装 MelonLoader」，或重新运行本安装包并勾选 MelonLoader。%n若杀软拦截 version.dll，请允许后再试。
chinesesimplified.WarnNoPsMelon=无法启动 PowerShell，已改用管理器内嵌方式安装 MelonLoader；若仍失败请打开管理器点「安装 MelonLoader」。
chinesesimplified.StatusPostDone=后置步骤已处理，即将进入完成页…

english.AppDisplayName=Mechabellum Mod Manager
english.TaskDesktop=Create a desktop shortcut
english.TaskDesktopGroup=Additional tasks:
english.CompMain=Manager app (required)
english.CompDotNet8=.NET 8 Desktop Runtime x64 (required by the manager; skipped if already installed; ~55-60 MB download, ~150-200 MB installed)
english.CompDotNet6=.NET 6 Desktop Runtime x64 (required by MelonLoader; skipped if already installed; ~50-55 MB download, ~140-180 MB installed)
english.CompMelon=MelonLoader (offline package embedded; upgrades if below 0.7.3; usually no GitHub access needed; recommended for the game folder)
english.RunNow=Launch %1 now
english.RiskWelcome=Please read the license agreement on the next page carefully. You must agree before continuing.
english.GamePathTitle=Select game folder
english.GamePathSub=Specify the Mechabellum install directory.
english.GamePathDesc=The folder must contain Mechabellum.exe and GameAssembly.dll.%nSetup scans Steam libraries and prefers the Mechabellum root; if dual-folder was used before, the previous path is preferred.%nIf MelonLoader is selected: files are not written to a store that Steam is currently downloading.
english.GamePathLabel=Game path
english.ErrBadGamePath=Invalid game path: Mechabellum.exe and GameAssembly.dll were not found.
english.StatusWriteConfig=Writing manager config (keep dual-folder records; do not interrupt Steam)…
english.StatusSeedUserConfig=Writing per-user config (no UI; please wait)…
english.ErrWriteConfig=Failed to write manager config. You can set the game path later in Settings.
english.StatusRestoreOptional=Optional restore script did not run (install continues; config was written natively).
english.StatusSanitizeAcf=Removing invalid Steam branch snapshots (Steam itself is not modified)…
english.StatusDotNet8=Quietly installing .NET 8 Desktop Runtime (~55-60 MB download; progress may pause briefly; skipped if present)…
english.ErrDotNet8=.NET 8 Desktop Runtime install failed (exit %1). Install manually from https://dotnet.microsoft.com/download/dotnet/8.0
english.StatusNoPsDotNet8=Could not start PowerShell; skipped automatic .NET 8 install (you can install it later).
english.StatusDotNet8Done=.NET 8 Desktop Runtime finished (or skipped).
english.StatusDotNet6=Quietly installing .NET 6 Desktop Runtime (~50-55 MB download; progress may pause briefly; skipped if present)…
english.ErrDotNet6=.NET 6 Desktop Runtime install failed (exit %1). Install manually from https://dotnet.microsoft.com/download/dotnet/6.0
english.StatusNoPsDotNet6=Could not start PowerShell; skipped automatic .NET 6 install (you can install it later).
english.StatusDotNet6Done=.NET 6 Desktop Runtime finished (or skipped).
english.StatusSteamBusyMelon=Steam is downloading this game; MelonLoader write was skipped. After the download finishes, re-run Setup with MelonLoader checked, or install MelonLoader manually.
english.WarnSteamBusyMelon=Steam is downloading this game, so Setup skipped MelonLoader.%n%nAfter the download finishes, re-run this Setup (keep MelonLoader checked). Otherwise the manager will show “Missing Loader” and Apply and Launch will stay disabled.
english.StatusMelon=Checking/installing MelonLoader (skip if ≥0.7.3; prefer embedded offline package)…
english.StatusNoPsMelon=Could not start PowerShell; falling back to the manager’s built-in MelonLoader installer.
english.WarnNoPsMelon=Could not start PowerShell; already using the manager's built-in MelonLoader installer. If it still fails, open the manager and click Install MelonLoader.
english.ErrMelon=MelonLoader install failed (exit %1).%n%nexit 1: invalid path or files locked; exit 2: often GitHub download failure; exit 3: incomplete install; exit 5: game running.%n%nOr open the manager and click Install MelonLoader, or install manually:%nhttps://github.com/LavaGang/MelonLoader/releases%n(Download MelonLoader.x64.zip and extract into the game folder per upstream docs.)
english.StatusMelonDone=MelonLoader finished (or skipped).
english.StatusMelonAlready=MelonLoader ≥0.7.3 already present; skipping install.
english.ErrMelonVerify=MelonLoader was not written correctly into the game folder.%n%nClose the game, then open the manager and click Install MelonLoader — or re-run this Setup with MelonLoader checked.%nIf antivirus quarantined version.dll, allow it and retry.
english.StatusPostDone=Post-install steps done; opening the Completed page…

russian.AppDisplayName=Mechabellum Mod Manager
russian.TaskDesktop=Создать ярлык на рабочем столе
russian.TaskDesktopGroup=Дополнительно:
russian.CompMain=Приложение менеджера (обязательно)
russian.CompDotNet8=.NET 8 Desktop Runtime x64 (нужен менеджеру; пропуск, если установлен; ~55–60 МБ загрузка, ~150–200 МБ после установки)
russian.CompDotNet6=.NET 6 Desktop Runtime x64 (нужен MelonLoader; пропуск, если установлен; ~50–55 МБ загрузка, ~140–180 МБ после установки)
russian.CompMelon=MelonLoader (офлайн-пакет в комплекте; обновление, если ниже 0.7.3; обычно GitHub не нужен; рекомендуется для папки игры)
russian.RunNow=Запустить %1 сейчас
russian.RiskWelcome=Внимательно прочитайте лицензию на следующей странице. Без согласия установка невозможна.
russian.GamePathTitle=Выбор папки игры
russian.GamePathSub=Укажите каталог установки Mechabellum.
russian.GamePathDesc=В папке должны быть Mechabellum.exe и GameAssembly.dll.%nУстановщик ищет библиотеки Steam и предпочитает корень Mechabellum; при двух папках используется прежний путь.%nПри выборе MelonLoader: запись не выполняется в каталог, который Steam сейчас загружает.
russian.GamePathLabel=Путь к игре
russian.ErrBadGamePath=Неверный путь: не найдены Mechabellum.exe и GameAssembly.dll.
russian.StatusWriteConfig=Запись конфигурации менеджера (сохранение двух папок; без прерывания Steam)…
russian.StatusSeedUserConfig=Запись пользовательской конфигурации (без окна; подождите)…
russian.ErrWriteConfig=Не удалось записать конфигурацию. Путь к игре можно указать позже в настройках.
russian.StatusRestoreOptional=Дополнительный скрипт восстановления не выполнен (установка продолжается; конфиг записан встроенным способом).
russian.StatusSanitizeAcf=Удаление недействительных снимков ветки Steam (сам Steam не изменяется)…
russian.StatusDotNet8=Тихая установка .NET 8 Desktop Runtime (~55–60 МБ; прогресс может замирать; пропуск, если есть)…
russian.ErrDotNet8=Установка .NET 8 Desktop Runtime не удалась (код %1). Установите вручную: https://dotnet.microsoft.com/download/dotnet/8.0
russian.StatusNoPsDotNet8=Не удалось запустить PowerShell; автоматическая установка .NET 8 пропущена (можно установить позже).
russian.StatusDotNet8Done=.NET 8 Desktop Runtime завершён (или пропущен).
russian.StatusDotNet6=Тихая установка .NET 6 Desktop Runtime (~50–55 МБ; прогресс может замирать; пропуск, если есть)…
russian.ErrDotNet6=Установка .NET 6 Desktop Runtime не удалась (код %1). Установите вручную: https://dotnet.microsoft.com/download/dotnet/6.0
russian.StatusNoPsDotNet6=Не удалось запустить PowerShell; автоматическая установка .NET 6 пропущена (можно установить позже).
russian.StatusDotNet6Done=.NET 6 Desktop Runtime завершён (или пропущен).
russian.StatusSteamBusyMelon=Steam загружает эту игру; запись MelonLoader пропущена. После загрузки снова запустите Setup с MelonLoader или установите вручную.
russian.WarnSteamBusyMelon=Steam загружает эту игру, поэтому MelonLoader пропущен.%n%nПосле загрузки снова запустите Setup (оставьте MelonLoader) — иначе менеджер покажет «нет Loader», а «Применить и запустить» будет недоступно.
russian.StatusMelon=Проверка/установка MelonLoader (пропуск при ≥0.7.3; предпочтение офлайн-пакету)…
russian.StatusNoPsMelon=Не удалось запустить PowerShell; автоматическая установка MelonLoader пропущена (можно установить позже).
russian.WarnNoPsMelon=Не удалось запустить PowerShell; используется встроенный установщик MelonLoader. Если ошибка повторится, откройте менеджер и нажмите «Установить MelonLoader».
russian.ErrMelon=Установка MelonLoader не удалась (код %1).%n%nкод 1: неверный путь или файлы заняты; код 2: часто сбой загрузки с GitHub; код 3: неполная установка.%n%nМожно снять MelonLoader и переустановить менеджер, или установить вручную:%nhttps://github.com/LavaGang/MelonLoader/releases%n(Скачайте MelonLoader.x64.zip и распакуйте в папку игры по инструкции.)
russian.StatusMelonDone=MelonLoader завершён (или пропущен).
russian.StatusMelonAlready=MelonLoader ≥0.7.3 уже установлен; установка пропущена.
russian.ErrMelonVerify=MelonLoader was not written correctly. Open the manager and click Install MelonLoader, or re-run Setup.
russian.StatusPostDone=Постустановочные шаги выполнены; переход к странице завершения…

japanese.AppDisplayName=Mechabellum Mod Manager
japanese.TaskDesktop=デスクトップにショートカットを作成
japanese.TaskDesktopGroup=追加タスク:
japanese.CompMain=マネージャー本体（必須）
japanese.CompDotNet8=.NET 8 Desktop Runtime x64（マネージャーに必要；インストール済みならスキップ；ダウンロード約55–60 MB、インストール後約150–200 MB）
japanese.CompDotNet6=.NET 6 Desktop Runtime x64（MelonLoader に必要；インストール済みならスキップ；ダウンロード約50–55 MB、インストール後約140–180 MB）
japanese.CompMelon=MelonLoader（オフラインパッケージ同梱；0.7.3 未満はアップグレード；通常 GitHub 不要；ゲームフォルダへのインストールを推奨）
japanese.RunNow=今すぐ %1 を起動
japanese.RiskWelcome=次のページの利用規約をよくお読みください。同意しないとインストールを続行できません。
japanese.GamePathTitle=ゲームフォルダの選択
japanese.GamePathSub=Mechabellum のインストール先を指定してください。
japanese.GamePathDesc=フォルダに Mechabellum.exe と GameAssembly.dll が必要です。%nセットアップは Steam ライブラリを検索し Mechabellum ルートを優先します；以前に双フォルダを使用した場合は前回のパスを優先します。%nMelonLoader を選択した場合：Steam がダウンロード中のストアには書き込みません。
japanese.GamePathLabel=ゲームパス
japanese.ErrBadGamePath=ゲームパスが無効です：Mechabellum.exe と GameAssembly.dll が見つかりません。
japanese.StatusWriteConfig=マネージャー設定を書き込み中（双フォルダ記録を保持；Steam を中断しません）…
japanese.StatusSeedUserConfig=ユーザー設定を書き込み中（画面なし・お待ちください）…
japanese.ErrWriteConfig=マネージャー設定の書き込みに失敗しました。後で設定からゲームパスを指定できます。
japanese.StatusRestoreOptional=オプションの復元スクリプトは実行されませんでした（インストールは続行；設定は組み込み方式で書き込み済み）。
japanese.StatusSanitizeAcf=無効な Steam ブランチスナップショットを削除中（Steam 本体は変更しません）…
japanese.StatusDotNet8=.NET 8 Desktop Runtime をサイレントインストール中（約55–60 MB；進捗が一時停止することがあります；存在すればスキップ）…
japanese.ErrDotNet8=.NET 8 Desktop Runtime のインストールに失敗しました（終了コード %1）。https://dotnet.microsoft.com/download/dotnet/8.0 から手動インストールしてください。
japanese.StatusNoPsDotNet8=PowerShell を起動できませんでした；.NET 8 の自動インストールをスキップしました（後でインストール可能）。
japanese.StatusDotNet8Done=.NET 8 Desktop Runtime が完了しました（またはスキップ）。
japanese.StatusDotNet6=.NET 6 Desktop Runtime をサイレントインストール中（約50–55 MB；進捗が一時停止することがあります；存在すればスキップ）…
japanese.ErrDotNet6=.NET 6 Desktop Runtime のインストールに失敗しました（終了コード %1）。https://dotnet.microsoft.com/download/dotnet/6.0 から手動インストールしてください。
japanese.StatusNoPsDotNet6=PowerShell を起動できませんでした；.NET 6 の自動インストールをスキップしました（後でインストール可能）。
japanese.StatusDotNet6Done=.NET 6 Desktop Runtime が完了しました（またはスキップ）。
japanese.StatusSteamBusyMelon=Steam が本ゲームをダウンロード中のため MelonLoader 書き込みをスキップしました。完了後に Setup で MelonLoader を選ぶか手動インストールしてください。
japanese.WarnSteamBusyMelon=Steam が本ゲームをダウンロード中のため MelonLoader をスキップしました。%n%n完了後に本 Setup を再実行（MelonLoader にチェック）してください。そうしないと「Loader なし」になり「適用して起動」が使えません。
japanese.StatusMelon=MelonLoader を確認/インストール中（≥0.7.3 ならスキップ；同梱オフラインパッケージを優先）…
japanese.StatusNoPsMelon=PowerShell を起動できませんでした；MelonLoader の自動インストールをスキップしました（後でインストール可能）。
japanese.WarnNoPsMelon=PowerShell を起動できませんでした。マネージャー内蔵の MelonLoader インストーラに切り替えました。失敗する場合はマネージャーで「MelonLoader をインストール」を押してください。
japanese.ErrMelon=MelonLoader のインストールに失敗しました（終了コード %1）。%n%n終了コード 1：無効なパスまたはファイルロック；2：多くは GitHub ダウンロード失敗；3：不完全なインストール。%n%nMelonLoader のチェックを外してマネージャーを再インストールするか、手動でインストールしてください：%nhttps://github.com/LavaGang/MelonLoader/releases%n（MelonLoader.x64.zip をダウンロードし、公式手順に従いゲームフォルダに展開）
japanese.StatusMelonDone=MelonLoader が完了しました（またはスキップ）。
japanese.StatusMelonAlready=MelonLoader ≥0.7.3 が既にあります。インストールをスキップします。
japanese.ErrMelonVerify=MelonLoader was not written correctly. Open the manager and click Install MelonLoader, or re-run Setup.
japanese.StatusPostDone=インストール後処理が終わりました。完了ページへ進みます…

german.AppDisplayName=Mechabellum Mod Manager
german.TaskDesktop=Desktop-Verknüpfung erstellen
german.TaskDesktopGroup=Zusätzliche Aufgaben:
german.CompMain=Manager-App (erforderlich)
german.CompDotNet8=.NET 8 Desktop Runtime x64 (vom Manager benötigt; übersprungen wenn installiert; ~55–60 MB Download, ~150–200 MB installiert)
german.CompDotNet6=.NET 6 Desktop Runtime x64 (von MelonLoader benötigt; übersprungen wenn installiert; ~50–55 MB Download, ~140–180 MB installiert)
german.CompMelon=MelonLoader (Offline-Paket enthalten; Upgrade wenn unter 0.7.3; meist kein GitHub nötig; für Spielordner empfohlen)
german.RunNow=%1 jetzt starten
german.RiskWelcome=Bitte lesen Sie die Lizenz auf der nächsten Seite sorgfältig. Ohne Zustimmung kann die Installation nicht fortgesetzt werden.
german.GamePathTitle=Spielordner auswählen
german.GamePathSub=Geben Sie das Mechabellum-Installationsverzeichnis an.
german.GamePathDesc=Der Ordner muss Mechabellum.exe und GameAssembly.dll enthalten.%nSetup durchsucht Steam-Bibliotheken und bevorzugt den Mechabellum-Stamm; bei Dual-Ordner wird der frühere Pfad bevorzugt.%nBei MelonLoader: Es wird nicht in einen Store geschrieben, den Steam gerade herunterlädt.
german.GamePathLabel=Spielpfad
german.ErrBadGamePath=Ungültiger Spielpfad: Mechabellum.exe und GameAssembly.dll nicht gefunden.
german.StatusWriteConfig=Manager-Konfiguration wird geschrieben (Dual-Ordner-Einträge bleiben; Steam wird nicht unterbrochen)…
german.StatusSeedUserConfig=Benutzerkonfiguration wird geschrieben (ohne UI; bitte warten)…
german.ErrWriteConfig=Manager-Konfiguration konnte nicht geschrieben werden. Spielpfad später in den Einstellungen setzen.
german.StatusRestoreOptional=Optionales Wiederherstellungsskript nicht ausgeführt (Installation läuft weiter; Konfiguration nativ geschrieben).
german.StatusSanitizeAcf=Ungültige Steam-Branch-Snapshots werden entfernt (Steam selbst wird nicht geändert)…
german.StatusDotNet8=.NET 8 Desktop Runtime wird still installiert (~55–60 MB; Fortschritt kann kurz stehen; übersprungen wenn vorhanden)…
german.ErrDotNet8=.NET 8 Desktop Runtime Installation fehlgeschlagen (Exit %1). Manuell installieren: https://dotnet.microsoft.com/download/dotnet/8.0
german.StatusNoPsDotNet8=PowerShell konnte nicht gestartet werden; automatische .NET 8-Installation übersprungen (später möglich).
german.StatusDotNet8Done=.NET 8 Desktop Runtime abgeschlossen (oder übersprungen).
german.StatusDotNet6=.NET 6 Desktop Runtime wird still installiert (~50–55 MB; Fortschritt kann kurz stehen; übersprungen wenn vorhanden)…
german.ErrDotNet6=.NET 6 Desktop Runtime Installation fehlgeschlagen (Exit %1). Manuell installieren: https://dotnet.microsoft.com/download/dotnet/6.0
german.StatusNoPsDotNet6=PowerShell konnte nicht gestartet werden; automatische .NET 6-Installation übersprungen (später möglich).
german.StatusDotNet6Done=.NET 6 Desktop Runtime abgeschlossen (oder übersprungen).
german.StatusSteamBusyMelon=Steam lädt dieses Spiel; MelonLoader-Schreiben übersprungen. Nach dem Download Setup erneut mit MelonLoader ausführen oder manuell installieren.
german.WarnSteamBusyMelon=Steam lädt dieses Spiel, daher wurde MelonLoader übersprungen.%n%nNach dem Download Setup erneut ausführen (MelonLoader angehakt). Sonst zeigt der Manager „Loader fehlt“ und Anwenden und Starten bleibt deaktiviert.
german.StatusMelon=MelonLoader prüfen/installieren (überspringen ab ≥0.7.3; Offline-Paket bevorzugt)…
german.StatusNoPsMelon=PowerShell konnte nicht gestartet werden; automatische MelonLoader-Installation übersprungen (später möglich).
german.WarnNoPsMelon=PowerShell konnte nicht gestartet werden; es wird der eingebaute MelonLoader-Installer des Managers verwendet. Bei weiterem Fehler öffnen Sie den Manager und klicken Sie auf MelonLoader installieren.
german.ErrMelon=MelonLoader-Installation fehlgeschlagen (Exit %1).%n%nExit 1: ungültiger Pfad oder Dateien gesperrt; Exit 2: oft GitHub-Download-Fehler; Exit 3: unvollständige Installation.%n%nMelonLoader abwählen und Manager neu installieren, oder manuell installieren:%nhttps://github.com/LavaGang/MelonLoader/releases%n(MelonLoader.x64.zip herunterladen und gemäß Anleitung in Spielordner entpacken)
german.StatusMelonDone=MelonLoader abgeschlossen (oder übersprungen).
german.StatusMelonAlready=MelonLoader ≥0.7.3 bereits vorhanden; Installation übersprungen.
german.ErrMelonVerify=MelonLoader was not written correctly. Open the manager and click Install MelonLoader, or re-run Setup.
german.StatusPostDone=Nachinstallation erledigt; Abschlussseite folgt…

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:TaskDesktopGroup}"

[Components]
Name: "main"; Description: "{cm:CompMain}"; Types: full compact custom; Flags: fixed
Name: "dotnet8"; Description: "{cm:CompDotNet8}"; Types: full compact custom
Name: "dotnet6"; Description: "{cm:CompDotNet6}"; Types: full compact custom
Name: "melon"; Description: "{cm:CompMelon}"; Types: full compact custom

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
Name: "{group}\{cm:AppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{cm:AppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Config is written natively in CurStepChanged (userappdata + ProgramData seed).
; Do NOT launch the manager exe again before the Finished page — that caused a second
; "completion" wait / possible UI flash (duplicate finish experience).
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:RunNow,{cm:AppDisplayName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  GamePathPage: TInputDirWizardPage;
  RiskLabel: TNewStaticText;
  G_PostGamePath: string;
  G_PostUiLang: string;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  ExePath: string;
begin
  { Run BEFORE files are deleted. [UninstallRun] defaults to after delete and would skip.}
  if CurUninstallStep = usUninstall then
  begin
    ExePath := ExpandConstant('{app}\{#MyAppExeName}');
    if FileExists(ExePath) then
    begin
      if Exec(ExePath, '--pure-game-cleanup --confirm-delete-other-store', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        if ResultCode <> 0 then
          MsgBox(
            '游戏内 Melon/Mods 清理未完全成功（退出码 ' + IntToStr(ResultCode) + '）。' + #13#10 +
            '管理器安装目录仍会删除。请查看日志：' + #13#10 +
            '%AppData%\MechabellumModManager\pure-game-cleanup.log' + #13#10 +
            '若 Steam 在运行，双服可能未解除；Mods 一般仍会尽量清理。',
            mbInformation, MB_OK);
      end
      else
        MsgBox('无法启动游戏残留清理程序。管理器安装目录仍会删除。', mbError, MB_OK);
    end;
  end;
end;

function GetPostGamePath(Param: string): string;
begin
  Result := G_PostGamePath;
end;

function GetPostUiLang(Param: string): string;
begin
  Result := G_PostUiLang;
end;

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
  { Minimized (not Hidden): Chinese AV "敏感动作/隐藏执行PowerShell" treats SW_HIDE as malware-like.
    Minimized still avoids stealing focus under elevated Setup. }
  Cmd := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Minimized -File "' +
         ScriptPath + '" ' + ExtraArgs;
  if not Exec(PowerShellExe(), Cmd, '', SW_SHOWMINNOACTIVE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := -1;
    exit;
  end;
  Result := ResultCode;
end;

function MelonDllVersionAtLeast073(const DllPath: string): Boolean;
var
  MS, LS: Cardinal;
  Major, Minor, Release: Word;
begin
  Result := False;
  if not FileExists(DllPath) then
    exit;
  if not GetVersionNumbers(DllPath, MS, LS) then
    exit;
  Major := Word(MS shr 16);
  Minor := Word(MS and $FFFF);
  Release := Word(LS shr 16);
  if Major > 0 then
  begin
    Result := True;
    exit;
  end;
  if Minor > 7 then
  begin
    Result := True;
    exit;
  end;
  if Minor < 7 then
    exit;
  Result := Release >= 3;
end;

function LooksLikeMelon(const Path: string): Boolean;
var
  Root: string;
begin
  { Presence alone is not enough — bundled redist is 0.7.3; older installs must upgrade. }
  Root := AddBackslash(Path);
  if not DirExists(Root + 'MelonLoader') then
  begin
    Result := False;
    exit;
  end;
  if not (FileExists(Root + 'version.dll') or FileExists(Root + 'winhttp.dll')) then
  begin
    Result := False;
    exit;
  end;
  Result :=
    MelonDllVersionAtLeast073(Root + 'MelonLoader\net6\MelonLoader.dll') or
    MelonDllVersionAtLeast073(Root + 'MelonLoader\net472\MelonLoader.dll') or
    MelonDllVersionAtLeast073(Root + 'MelonLoader\net35\MelonLoader.dll') or
    MelonDllVersionAtLeast073(Root + 'MelonLoader\MelonLoader.dll');
end;

function SanitizeAcfViaApp: Integer;
var
  ResultCode: Integer;
begin
  if not Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--sanitize-acf-snapshots', '',
              SW_SHOWMINNOACTIVE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := -1;
    exit;
  end;
  Result := ResultCode;
end;

function InstallMelonViaApp(const GamePath, Redist: string): Integer;
var
  Params: string;
  ResultCode: Integer;
begin
  { Prefer the manager EXE + offline zip — no PowerShell, works when PS is blocked. }
  Params := '--install-melon-loader --game-path "' + GamePath + '" --redist-dir "' + Redist + '"';
  if not Exec(ExpandConstant('{app}\{#MyAppExeName}'), Params, '', SW_SHOWMINNOACTIVE, ewWaitUntilTerminated, ResultCode) then
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

function TryExtractJsonString(const Content, Key: string): string;
var
  Marker, SearchKey: string;
  P, Q: Integer;
begin
  Result := '';
  SearchKey := '"' + Key + '"';
  P := Pos(SearchKey, Content);
  if P = 0 then
    exit;
  Marker := Copy(Content, P + Length(SearchKey), Length(Content));
  P := Pos(':', Marker);
  if P = 0 then
    exit;
  Marker := Trim(Copy(Marker, P + 1, Length(Marker)));
  if Copy(LowerCase(Marker), 1, 4) = 'null' then
    exit;
  P := Pos('"', Marker);
  if P = 0 then
    exit;
  Marker := Copy(Marker, P + 1, Length(Marker));
  Q := Pos('"', Marker);
  if Q <= 0 then
    exit;
  Result := JsonUnescapePath(Copy(Marker, 1, Q - 1));
end;

function TryExtractJsonInt(const Content, Key: string; Default: Integer): Integer;
var
  Marker, SearchKey, NumStr: string;
  P, Q: Integer;
begin
  Result := Default;
  SearchKey := '"' + Key + '"';
  P := Pos(SearchKey, Content);
  if P = 0 then
    exit;
  Marker := Copy(Content, P + Length(SearchKey), Length(Content));
  P := Pos(':', Marker);
  if P = 0 then
    exit;
  Marker := Trim(Copy(Marker, P + 1, Length(Marker)));
  NumStr := '';
  for Q := 1 to Length(Marker) do
  begin
    if ((Marker[Q] >= '0') and (Marker[Q] <= '9')) or (Marker[Q] = '-') then
      NumStr := NumStr + Marker[Q]
    else if NumStr <> '' then
      break;
  end;
  if NumStr <> '' then
    Result := StrToIntDef(NumStr, Default);
end;

function TryExtractJsonNullOrString(const Content, Key: string; var IsNull: Boolean; var Value: string): Boolean;
var
  Marker, SearchKey: string;
  P, Q: Integer;
begin
  Result := False;
  IsNull := False;
  Value := '';
  SearchKey := '"' + Key + '"';
  P := Pos(SearchKey, Content);
  if P = 0 then
    exit;
  Marker := Copy(Content, P + Length(SearchKey), Length(Content));
  P := Pos(':', Marker);
  if P = 0 then
    exit;
  Marker := Trim(Copy(Marker, P + 1, Length(Marker)));
  if Copy(LowerCase(Marker), 1, 4) = 'null' then
  begin
    IsNull := True;
    Result := True;
    exit;
  end;
  P := Pos('"', Marker);
  if P = 0 then
    exit;
  Marker := Copy(Marker, P + 1, Length(Marker));
  Q := Pos('"', Marker);
  if Q <= 0 then
    exit;
  Value := JsonUnescapePath(Copy(Marker, 1, Q - 1));
  Result := True;
end;

function MapInstallerLanguageToUi: string;
begin
  if ActiveLanguage = 'chinesesimplified' then
    Result := 'zh-CN'
  else if ActiveLanguage = 'russian' then
    Result := 'ru'
  else if ActiveLanguage = 'japanese' then
    Result := 'ja'
  else if ActiveLanguage = 'german' then
    Result := 'de'
  else
    Result := 'en';
end;

function SaveUtf8TextFile(const FileName, Content: string): Boolean;
var
  Utf8: AnsiString;
begin
  Utf8 := Utf8Encode(Content);
  Result := SaveStringToFile(FileName, Utf8, False);
end;

function LoadUtf8TextFile(const FileName: string; var Content: string): Boolean;
var
  Raw: AnsiString;
begin
  Result := False;
  Content := '';
  if not LoadStringFromFile(FileName, Raw) then
    exit;
  { Prefer UTF-8; fall back to ANSI string cast if decode yields empty for non-empty raw. }
  Content := Utf8Decode(Raw);
  if (Content = '') and (Length(Raw) > 0) then
    Content := string(Raw);
  Result := True;
end;

function WriteManagerConfigNative(const GamePath: string): Boolean;
var
  Root, ConfigPath, ProfilePath, Json, Resolved, Link, ExistingContent, Lang: string;
  CommonRoot, SeedJson: string;
  LaunchMode: Integer;
  ActiveProfileId, DataRoot: string;
  DataRootIsNull: Boolean;
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
  LaunchMode := 0;
  ActiveProfileId := 'default';
  DataRootIsNull := True;
  DataRoot := '';

  if FileExists(ConfigPath) and LoadUtf8TextFile(ConfigPath, ExistingContent) then
  begin
    LaunchMode := TryExtractJsonInt(ExistingContent, 'launchMode', 0);
    ActiveProfileId := TryExtractJsonString(ExistingContent, 'activeProfileId');
    if ActiveProfileId = '' then
      ActiveProfileId := 'default';
    if not TryExtractJsonNullOrString(ExistingContent, 'dataRoot', DataRootIsNull, DataRoot) then
      DataRootIsNull := True;
  end;

  Lang := MapInstallerLanguageToUi();

  Json :=
    '{' + #13#10 +
    '  "gamePath": "' + EscapeJsonPath(Resolved) + '",' + #13#10 +
    '  "launchMode": ' + IntToStr(LaunchMode) + ',' + #13#10 +
    '  "activeProfileId": "' + EscapeJsonPath(ActiveProfileId) + '",' + #13#10;
  if DataRootIsNull then
    Json := Json + '  "dataRoot": null,' + #13#10
  else
    Json := Json + '  "dataRoot": "' + EscapeJsonPath(DataRoot) + '",' + #13#10;
  Json := Json +
    '  "uiLanguage": "' + Lang + '"' + #13#10 +
    '}' + #13#10;
  if not SaveUtf8TextFile(ConfigPath, Json) then
    exit;

  { Plan A: machine-wide seed under ProgramData (survives wrong elevated AppData). }
  CommonRoot := ExpandConstant('{commonappdata}\MechabellumModManager');
  ForceDirectories(CommonRoot);
  SeedJson :=
    '{' + #13#10 +
    '  "gamePath": "' + EscapeJsonPath(Resolved) + '",' + #13#10 +
    '  "uiLanguage": "' + Lang + '"' + #13#10 +
    '}' + #13#10;
  SaveUtf8TextFile(CommonRoot + '\install-defaults.json', SeedJson);

  ProfilePath := Root + '\profiles\default.json';
  if not FileExists(ProfilePath) then
  begin
    if not SaveUtf8TextFile(ProfilePath,
      '{' + #13#10 +
      '  "id": "default",' + #13#10 +
      '  "name": "default",' + #13#10 +
      '  "enabledPackageIds": []' + #13#10 +
      '}' + #13#10) then
      { non-fatal for profile seed };
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
  Cmd := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Minimized -File "' +
         ScriptFile + '" -OutFile "' + OutFile + '"';
  if not Exec(PowerShellExe(), Cmd, '', SW_SHOWMINNOACTIVE, ewWaitUntilTerminated, ResultCode) then
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

function QueryGameDownloading(const GamePath: string): Boolean;
var
  ResultCode: Integer;
  OutFile, Args, S: string;
  Line: AnsiString;
begin
  { Only busy when THIS game appears mid-download. Steam sitting in tray must not skip Melon. }
  Result := False;
  if GamePath = '' then
    exit;

  OutFile := ExpandConstant('{tmp}\mmm-game-downloading.txt');
  DeleteFile(OutFile);
  Args := '-GamePath "' + GamePath + '" -OutFile "' + OutFile + '"';
  ResultCode := PsFromSrc('Detect-GameDownloading.ps1', Args);
  if ResultCode <> 0 then
    exit;
  if FileExists(OutFile) and LoadStringFromFile(OutFile, Line) then
  begin
    S := string(Line);
    StringChangeEx(S, #13, '', True);
    StringChangeEx(S, #10, '', True);
    Result := Pos('downloading', LowerCase(Trim(S))) > 0;
  end;
end;

procedure InitializeWizard;
var
  Guess: string;
begin
  GamePathPage := CreateInputDirPage(wpSelectDir,
    CustomMessage('GamePathTitle'),
    CustomMessage('GamePathSub'),
    CustomMessage('GamePathDesc'),
    False, '');
  GamePathPage.Add(CustomMessage('GamePathLabel'));
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
  RiskLabel.Caption := CustomMessage('RiskWelcome');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = GamePathPage.ID then
  begin
    if not LooksLikeGame(GamePathPage.Values[0]) then
    begin
      MsgBox(CustomMessage('ErrBadGamePath'), mbError, MB_OK);
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
  G_PostGamePath := GamePath;
  G_PostUiLang := MapInstallerLanguageToUi();

  { Native config write first — does not require PowerShell (avoids 0xc0000142).
    Also writes ProgramData install-defaults.json (Plan A).
    Does NOT exit Steam / rewrite BetaKey / swap junction. }
  SetStatus(CustomMessage('StatusWriteConfig'));
  if not WriteManagerConfigNative(GamePath) then
    MsgBox(CustomMessage('ErrWriteConfig'), mbError, MB_OK);

  { Optional extras via PowerShell; failures are non-fatal. }
  Args := '-GamePath "' + GamePath + '" -RedistDir "' + Redist + '"';
  Code := PsFromSrc('Restore-DualFolderConfig.ps1', Args);
  if Code <> 0 then
    SetStatus(CustomMessage('StatusRestoreOptional'));

  { Delete dirty AppData ACF snapshots that would poison one-click branch switch. }
  SetStatus(CustomMessage('StatusSanitizeAcf'));
  Code := SanitizeAcfViaApp();
  if Code <> 0 then
    PsFromSrc('Sanitize-AcfSnapshots.ps1', '');

  { Skip Melon only when THIS game is mid-download — not merely because Steam is open. }
  SteamBusy := QueryGameDownloading(GamePath);

  if WizardIsComponentSelected('dotnet8') then
  begin
    SetStatus(CustomMessage('StatusDotNet8'));
    Args := '-Major 8 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) and (Code <> -1) then
      MsgBox(FmtMessage(CustomMessage('ErrDotNet8'), [IntToStr(Code)]), mbError, MB_OK)
    else if Code = -1 then
      SetStatus(CustomMessage('StatusNoPsDotNet8'))
    else
      SetStatus(CustomMessage('StatusDotNet8Done'));
  end;

  if WizardIsComponentSelected('dotnet6') then
  begin
    SetStatus(CustomMessage('StatusDotNet6'));
    Args := '-Major 6 -RedistDir "' + Redist + '"';
    Code := PsFromSrc('Install-Prereqs.ps1', Args);
    if (Code <> 0) and (Code <> 3010) and (Code <> -1) then
      MsgBox(FmtMessage(CustomMessage('ErrDotNet6'), [IntToStr(Code)]), mbError, MB_OK)
    else if Code = -1 then
      SetStatus(CustomMessage('StatusNoPsDotNet6'))
    else
      SetStatus(CustomMessage('StatusDotNet6Done'));
  end;

  if WizardIsComponentSelected('melon') then
  begin
    if SteamBusy then
    begin
      SetStatus(CustomMessage('StatusSteamBusyMelon'));
      MsgBox(CustomMessage('WarnSteamBusyMelon'), mbInformation, MB_OK);
    end
    else
    begin
      if LooksLikeMelon(GamePath) then
      begin
        SetStatus(CustomMessage('StatusMelonAlready'));
      end
      else
      begin
        SetStatus(CustomMessage('StatusMelon'));
        { Primary: manager EXE + embedded zip (no PowerShell). }
        Code := InstallMelonViaApp(GamePath, Redist);
        if (Code <> 0) and (not LooksLikeMelon(GamePath)) then
        begin
          { Fallback: PowerShell script (same offline zip). }
          if Code = -1 then
            SetStatus(CustomMessage('StatusNoPsMelon'));
          Args := '-GamePath "' + GamePath + '" -RedistDir "' + Redist + '"';
          Code := PsFromSrc('Install-MelonLoader.ps1', Args);
        end;

        if LooksLikeMelon(GamePath) then
          SetStatus(CustomMessage('StatusMelonDone'))
        else if Code = -1 then
          MsgBox(CustomMessage('ErrMelonVerify'), mbError, MB_OK)
        else if Code <> 0 then
          MsgBox(FmtMessage(CustomMessage('ErrMelon'), [IntToStr(Code)]), mbError, MB_OK)
        else
          MsgBox(CustomMessage('ErrMelonVerify'), mbError, MB_OK);
      end;
    end;
  end;

  SetStatus(CustomMessage('StatusPostDone'));
end;
