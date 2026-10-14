# 简介改为功能与用法

**日期：** 2026-10-14

**范围：** 管理器里玩家看到的简介，以及 MechabellumMods 目录里已经写上的简介。投稿模板和创作标准同步改提示。

## 目标

玩家打开一个 Mod，第一眼看到它能做什么、怎么操作。管理器版本要求继续只出现在浏览列表已有的「需要管理器 x 或更高版本」上。

## 不做的事

- 不新增目录字段。仍用现有 `summary` 和 `locales.*.summary`。
- 不改 `minManagerVersion`、`sha256`、`size`、`parts`、`file`。
- 这一轮不重写单位 DPS、飞弹方向、战局助手、队友信标、科技演示等长简介的后文。只删掉开头的版本句。
- 不把游戏版本号（例如 `2.0.0.0.2320`）从后文里删掉。
- 目录校验不新增「简介必须含按键」之类的规则。

## 管理器显示

浏览详情和本地库详情都把这段标成「功能与用法」。本地库今天的标签是「功能」，浏览详情没有标签。两处改成同一句标签，后面接简介正文。正文为空时整行不显示。

显示前只删掉开头这一句，版本号是三段数字，句后的空白一并删掉。匹配前先去掉开头的空白。后文原样保留。这一句不在开头时不删。删完若为空，显示为空。

ru 句尾在线上目录里是半角句点；2026-10-06 的计划写的是全角「。」。两种句尾都算匹配。

线上目录 22 条里 16 条带这句，每一条都有 `minManagerVersion`。「需要管理器 x 或更高版本」显示在浏览列表的「管理器」列和精选卡片上，不在浏览详情里。删掉前缀之后版本要求仍然看得到；这一轮不往浏览详情里加这一行。

| 语言 | 开头句 |
| --- | --- |
| 默认（简体中文） | `从管理器安装或更新需要 x.y.z 或更高版本。` |
| en | `Installing or updating through the manager requires x.y.z or newer.` |
| de | `Installation oder Aktualisierung über den Manager erfordert x.y.z oder neuer.` |
| ja | `マネージャーからのインストールと更新には x.y.z 以降が必要です。` |
| ru | `Установка и обновление через менеджер требуют версию x.y.z или новее.` |

`1.3.13` 和 `1.3.15` 都要能删掉。科技演示用的是 `1.3.15`。

剥离只发生在界面和搜索用的显示文字上。`CatalogLocaleResolver` 仍按语言取出原文。浏览项和本地库项的 `Summary` 在取出之后再剥离。本地库没有 `CatalogLocales` 时，对 `Package.Summary` 做同一次剥离，避免缓存里的旧中文前缀还露出来。不改写磁盘上的 `package.json`，也不改目录缓存原文；安装时写进 `package.json` 的 `summary` 仍是目录原文（`MainViewModel` 调 `UpdatePackageMetadata` 那一处不变）。

本地库详情今天没有「需要管理器 x」那一行，版本要求只在更新被挡住时出现在更新按钮的提示里。删掉前缀后本地库看不到管理器版本号。已装上的 Mod 不需要这个数字，更新时按钮会说；这一轮不给本地库加这一行。

标签文案：

| 键 | zh | en | de | ja | ru |
| --- | --- | --- | --- | --- | --- |
| `LabelSummary` | 功能与用法 | How to use | Bedienung | 機能と使い方 | Как пользоваться |
| `DirectUploadSummary` | 功能与用法 | How to use | Bedienung | 機能と使い方 | Как пользоваться |

创作标准页 `AuthorCatalogBody` 改为下面这几句：

- zh：简介第一句写玩家能做什么、用哪个键或怎么操作，可以补正式服还是测试服。不要写管理器版本号、更新日志或实现细节。版本要求写在 minManagerVersion。新 Mod 用 1.3.14，除非确实依赖更新的管理器行为。安装位置默认 MelonMod。按 MelonLoader 0.7.3 来写。
- en：The first sentence says what the player can do and which key or action to use. The live server, the test server, or both may follow. Do not put a manager version, a changelog, or implementation notes in the summary. The version requirement belongs in minManagerVersion. A new mod uses 1.3.14 unless it truly needs a newer manager behavior. The install location defaults to MelonMod. Write against MelonLoader 0.7.3.
- de：Der erste Satz sagt, was der Spieler tun kann und welche Taste oder Aktion das ist. Live-Server, Testserver oder beide dürfen danach stehen. Keine Manager-Version, kein Änderungsprotokoll und keine Implementierungsnotizen in die Kurzbeschreibung. Die Versionsanforderung steht in minManagerVersion. Ein neuer Mod nutzt 1.3.14, außer er braucht wirklich ein neueres Manager-Verhalten. Der Installationsort ist standardmäßig MelonMod. Schreibe gegen MelonLoader 0.7.3.
- ja：概要の最初の文には、プレイヤーができることと、使うキーまたは操作を書きます。正式サーバーかテストサーバーか両方かは続けて書いてかまいません。マネージャーの版番号、更新履歴、実装の詳細は書かないでください。版の要件は minManagerVersion に書きます。新しい Mod は 1.3.14 です。より新しいマネージャーの動作に本当に依存するときだけ上げます。インストール先は既定で MelonMod です。MelonLoader 0.7.3 向けに書いてください。
- ru：Первое предложение — что может игрок и какая клавиша или действие. Дальше можно указать официальный сервер, тестовый или оба. Номер версии менеджера, список изменений и заметки о реализации в описание не пишите. Требование к версии — в minManagerVersion. Новый мод ставит 1.3.14 и повышает её, только если ему правда нужно более новое поведение менеджера. Место установки по умолчанию — MelonMod. Пишите под MelonLoader 0.7.3.

投稿邮件正文里的「【一句话简介 / Summary】」整行改为：

`【功能与用法 / How to use】第一句写能做什么、怎么操作，不要写管理器版本或更新日志。First sentence: what it does and how to use it. No manager version or changelog.`

## 目录

改动在 MechabellumMods 的 `catalog.json`，不在本仓库。目录改动在那份仓库里做（本机克隆在 `D:\gongzuo\MechabellumMods`）；管理器部分不等它，两边发布不分先后。

这一步部分撤回了 2026-10-06《目录只按分块发布》的决定。那次把版本句写进简介，是因为 1.3.12 及更早的管理器不读 `parts`，下载会报 HTTP 404，又不读 `minManagerVersion`，弹窗也改不了，简介开头是它们唯一能看到的原因。删掉之后，这些旧版本下载失败时只剩 404，看不到「需要 1.3.13」。接受这个取舍：1.3.12 的更新检查仍指向 GitHub `releases/latest` 和 `release/latest.json`，这些用户照样会收到升级管理器的提示。换来的是 1.3.13 到 2.0.0 这些还不会剥离前缀的管理器，立刻先显示功能和用法。

每条 `summary` 和每条非空的 `locales.*.summary`，若以「管理器显示」表里的对应句子开头，删掉这一句和紧跟的空白。下面六条本来就没有这句，保持不动：`friend-overlay`、`replay-reset`、`predict-position`、`leaderboard-fix`、`axis-lock`、`skin-selector`。其余十六条五种语言都带这句。目录根上的 `updatedAt` 前进；条目自己的 `updatedAt` 不动，改简介不是 Mod 更新。

`quick-mark` 在删掉版本句之后，把按键放在剩余正文前面。后文不改。

按键来自「巴巴」2026-10-14 在群里发的说明：CTRL+左键标记地面、ALT+左键擦除地面、ALT+右键清除所有标记。目录里 `quick-mark` 的 `author` 就是「巴巴」，这是作者本人给的用法。本仓库没有 QuickMark 的源码，不再从代码核对。

默认：

`CTRL+左键标记地面，ALT+左键擦除地面，ALT+右键清除所有标记。在对局里给地面格子做标记，对局结束时清除。格子尺寸或网格轴对不上时不会乱点。`

en：

`Ctrl+left click marks the ground, Alt+left click erases a mark, Alt+right click clears all marks. Marks ground cells during a match and clears them when the match ends. It will not pick a cell when the grid size or native axes do not match.`

de：

`Strg+Linksklick markiert den Boden, Alt+Linksklick löscht eine Markierung, Alt+Rechtsklick löscht alle Markierungen. Markiert Bodenzellen während eines Matches und entfernt sie am Matchende. Bei abweichender Gittergröße oder nativen Achsen wird keine Zelle gewählt.`

ja：

`Ctrl+左クリックで地面をマーク、Alt+左クリックで消去、Alt+右クリックで全部消去。対局中に地面マスへマークし、対局終了時に消します。マス寸法やネイティブ軸が合わないときは選択しません。`

ru：

`Ctrl+левая кнопка ставит метку на землю, Alt+левая стирает метку, Alt+правая снимает все метки. Ставит метки на клетки поля во время матча и снимает их в конце. Если размер сетки или оси не совпадают, клетка не выбирается.`

MechabellumMods 里另有三处要跟着改。Issue 模板只有举报，不涉及。

- `docs/submit.html` 和 `README.md` 的投稿模板各有一行 `【一句话简介 / Summary】`，换成与管理器投稿正文完全相同的那一行。
- `README.md` 字段表里 `summary` 的说明 `Short description` 改为 `What it does and how to use it (first sentence). No manager version or changelog`。
- `docs/ai-mod-standard.md`（管理器「下载创作标准」拿到的就是它）的「简介」条目改为：`summary` 第一句写玩家能做什么、用哪个键或怎么操作，后面可以补正式服 / 测试服 / 两者。目录没有单独的游戏端字段。不超过 400 字。不要写管理器版本、更新日志或实现细节；更新内容写进投稿邮件的「更新说明」。

## 测试

- 五种语言的版本句都能从开头剥掉，`1.3.13` 与 `1.3.15` 都算。后文保留，包括后文里的游戏版本号。
- 版本句不在开头时保留。没有这句时原文不变。整段只有版本句时结果为空。ru 半角句点和全角句号都能剥。
- 本地库项没有 `CatalogLocales`、`Package.Summary` 带中文前缀时，显示文字已剥掉前缀。
- 浏览列表搜索按剥掉前缀后的文字匹配：搜「1.3.13」不再命中只在前缀里出现这个数字的条目。
- 投稿正文含「功能与用法」和「How to use」，不再含「一句话简介」。

## 涉及文件

管理器：

- `src/MechabellumModManager/Services/CatalogLocaleResolver.cs` 旁新增显示用剥离，或放在同一文件的独立方法里。语言解析本身不剥。
- `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`
- `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- `src/MechabellumModManager/MainWindow.xaml` 浏览详情补上与本地库相同的标签行。
- `src/MechabellumModManager/Resources/Strings.resx` 及 `.en` `.de` `.ja` `.ru`
- `src/MechabellumModManager/Services/GitHubCommunityLinks.cs`
- 对应测试

目录仓库 MechabellumMods：`catalog.json`、`docs/submit.html`、`README.md`、`docs/ai-mod-standard.md`。
