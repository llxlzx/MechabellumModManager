# 管理器 1.3.14：按 Mod 限制下载，并增加公告页

**日期：** 2026-10-06

**版本：** 管理器程序从 `1.3.13` 改为 `1.3.14`。

目录里每个 Mod 可以写最低管理器版本。1.3.14 和更高版本在通过管理器下载或更新之前核对该数字：当前版本大于或等于该数字才下载。缺这个字段表示不设下限。已经装进本地库、哈希仍与目录一致的，不重新下载。手动放进游戏目录的 DLL 不经过这次核对，游戏里照常加载。启用、停用也不核对。

公告放在 MechabellumMods 的 `notice.json`。只有玩家点开公告页才请求。启动不请求，也不轮询。改公告是推送该文件并同步镜像，不必再发管理器。

本规格不删除整包 DLL。删除仍是以后的手工步骤：1.3.14 成为已发布的最新版满 48 小时之后再删。时钟从 GitHub 与镜像上的 `latest.json` 都写成 `1.3.14` 且安装包可以下载时起算，不从本次目录提交起算。这取代同日《目录只按分块发布》里「新目录公开后再等 24 小时」的删除时钟。那份规格的分块、简介前缀和整包保留仍然有效。

## 三档下载

| 管理器 | 删整包之后，目录已刷新时 |
| --- | --- |
| 1.3.12 及更早 | 不看 `parts`，整包 404，安装和更新失败 |
| 1.3.13 | 不读 `minManagerVersion`。目录里有 `parts` 时仍下载分块并可以使用 |
| 1.3.14 及更高 | 读取 `minManagerVersion`。当前版本低于该数字时不下载，并提示先更新管理器 |

已发出的 1.3.13 无法用这个字段拦住。这是接受的残留，不是本版要补的洞。

## 版本字段

条目增加可选字符串 `minManagerVersion`。本次发布给现有 18 个 Mod 都写成 `1.3.14`。不改它们的 `sha256`、`size`、`parts`、`file`、简介前缀、`originUrl`（保持没有）。只前进目录根上的 `updatedAt`。

以后只改这个字段并发布 `catalog.json`（Git 与镜像），就能收紧或放宽该 Mod，不必发管理器。写成 `1.3.18` 时，1.3.14 到 1.3.17 被拦住，1.3.18 及更高通过。每个 Mod 各写各的。

合法值是恰好三段整数，每段 0 到 65535，形如 `1.3.14`。每段要么是 `0`，要么不以 `0` 开头。不允许 `v` 前缀、加号后缀、预发布后缀、空段、第四段，也不允许 `1.03.14` 这种前导零。

核对规则：

- 字段缺失、JSON `null`、或去掉空白后为空：不设下限，允许下载。
- 非空但不合法：拒绝通过管理器下载。提示「目录里的最低管理器版本无法识别」，并带上原文。不把它当成 0，也不当成当前版本。
- 合法时，把当前管理器版本与该字段都收成主、次、修订三段再比较。当前大于或等于要求则通过，相等通过。
- 当前版本来自 `UpdateChecker.ReadLocalVersion()`，去掉首部 `v` 和 `+` 之后的构建号。剩下若是三段，或三段后再跟一段，只取前三段。除此之外无法收成三段时：有下限的条目拒绝下载；没有下限的条目仍允许。
- 比较用整数三段，不用会把字母吃掉的 `NormalizeVersion`，也不用 `System.Version` 的第四段。因此 `1.3.9` 低于 `1.3.14`，`1.4.0` 高于 `1.3.14`。

界面句子（`{0}` 是要求版本，`{1}` 是当前版本）：

| 键 | zh-CN（`Strings.resx`） | en | de | ja | ru |
| --- | --- | --- | --- | --- | --- |
| `ManagerFloorLine` | 需要管理器 {0} 或更高版本 | Requires manager {0} or newer | Erfordert Manager {0} oder neuer | マネージャー {0} 以降が必要です | Нужен менеджер {0} или новее |
| `ManagerFloorBlocked` | 此 Mod 需要管理器 {0} 或更高版本。当前是 {1}。请先更新管理器。 | This mod requires manager {0} or newer. This copy is {1}. Update the manager first. | Dieses Mod erfordert Manager {0} oder neuer. Diese Fassung ist {1}. Bitte zuerst den Manager aktualisieren. | この Mod にはマネージャー {0} 以降が必要です。今の版は {1} です。先にマネージャーを更新してください。 | Этому моду нужен менеджер {0} или новее. Сейчас {1}. Сначала обновите менеджер. |
| `ManagerFloorUnreadable` | 目录里的最低管理器版本无法识别（{0}），已拒绝通过管理器下载。 | The catalog minimum manager version ({0}) is unreadable. Download through the manager was refused. | Die Mindestversion des Managers im Katalog ({0}) ist unlesbar. Der Download über den Manager wurde abgelehnt. | カタログの最低マネージャー版（{0}）を読めないため、マネージャーからのダウンロードを拒否しました。 | Минимальная версия менеджера в каталоге ({0}) не читается. Загрузка через менеджер отклонена. |
| `UpdateManager` | 更新管理器 | Update manager | Manager aktualisieren | マネージャーを更新 | Обновить менеджер |
| `NoticeNav` | 公告 | Notices | Hinweise | お知らせ | Объявления |
| `NavNoteNotice` | 打开时更新 | Refreshes when opened | Beim Öffnen aktualisiert | 開いたときに更新 | Обновляется при открытии |
| `NoticeEmpty` | 暂无公告 | No notices | Keine Hinweise | お知らせはありません | Объявлений нет |
| `NoticeStale` | 公告暂时无法刷新，以下是上次成功获取的内容。 | Notices could not be refreshed. Showing the last copy that loaded. | Hinweise konnten nicht aktualisiert werden. Es folgt die letzte erfolgreiche Fassung. | お知らせを更新できません。前回読み込めた内容を表示しています。 | Объявления не обновились. Ниже последняя удачно загруженная копия. |
| `NoticeUnavailable` | 公告暂时无法获取。 | Notices could not be loaded. | Hinweise konnten nicht geladen werden. | お知らせを取得できません。 | Объявления загрузить не удалось. |

「更新管理器」调用现有的手动检查更新（`CheckForUpdatesCommand`），不另写下载器。

## 何时拦截

拦截只发生在即将通过管理器下载字节之前。

- 目录多选加入、以及本地库里对单条的更新，都进 `AddOneCatalogModAsync`。已在本地库且哈希已是最新（函数开头那个直接返回的分支）不下载，因此不拦截。
- 在该返回之后、冲突判断、游戏是否在运行、高风险确认和 `DownloadModAsync` 之前做版本核对。不通过则写上面的句子到日志和 `CatalogStatus`，本条结束。不替换已安装文件。
- 一次多选里，被拦住的跳过，其余继续。全部被拦住时不进入下载进度。
- 从本地库点更新时，对应目录条目不通过则同样不下载。本地库行在「有更新且被拦住」时显示同一句和「更新管理器」。启用按钮不因此禁用。
- 手动导入 DLL、把文件放进游戏目录、启用、停用，都不调用这次核对。

目录行在字段非空时显示 `ManagerFloorLine`。不通过时该行显示拦住句子和「更新管理器」。通过时安装按钮保持可用，要求版本仍显示出来。

玩家最多 12 小时才看到新的下限：磁盘目录静默仍是 `DiskQuietPeriod`（12 小时）。手动刷新目录（`forceCold: true`）立刻看到。一直开着、没有刷新的进程，内存里的目录保持打开时的数字。这是已有静默，本版不改时长。

## 升级时丢掉目录缓存

数据目录增加 `catalog-manager-version.txt`，内容是当前 `ReadLocalVersion()` 的原文。

进程启动、第一次 `FetchCatalogSmartAsync` 之前：

1. 读该文件。与当前版本完全一致（序号比较）则不动缓存。
2. 不一致或文件不存在：删除 `catalog-cache.json`、`catalog-cache.etag`、`catalog-cache.source`。不删除本地库、不删除 `update-check.stamp`、不删除公告缓存。
3. 删除之后再写版本文件。先写版本、后删除会导致崩溃后永远留着旧目录。写失败则下次启动再删一次。

从 1.3.13 升上来时没有这个文件，因此会删一次。同一版本再次启动不删。文件不存在时 `IsYoungerThan` 为假，紧接着的那次获取会走网络。

## 整包 404 时刷新一次

只修「手里还是没有 `parts` 的旧目录，整包已经被删」这一种情况。

`DownloadModAsync` 在条目没有非空 `parts`、走整包地址、并且每一个候选都以 HTTP 404 结束时，抛出新的 `CatalogFileMissingException`。任何一个候选是超时、哈希不符、其它 HTTP 状态或连接失败，仍抛原来的异常，不刷新。分块下载的 404 也抛原来的异常，不刷新。候选列表为空不刷新。

一次 `RunCatalogDownloadAsync` 最多刷新一次目录（`forceCold: true`）：

1. 捕获 `CatalogFileMissingException`。
2. 若这次点击已经刷新过，把原错误显示出来，结束本条。
3. 否则刷新。刷新失败、或新目录里没有同一 `id`：显示原错误，结束本条。不再下载。
4. 用新目录里同一 `id` 的条目替换内存中的那一条，后面的批量条目也按新目录重新取，避免继续用刷新前的对象。
5. 对新条目再做一次版本核对。不通过则显示拦住句子，不下载。
6. 新条目有非空 `parts` 且核对通过：下载分块一次。这次失败不再刷新。
7. 新条目仍没有 `parts`：显示原错误，不下载。

## 直传

直传发布的管理器版本就是当时正在运行的 `ReadLocalVersion()`，收成三段。收不成则拒绝本次直传。写进目录的是这三段拼成的字符串，例如本地 `1.3.14.0` 写成 `1.3.14`，以便通过目录校验。

合并条目时：

- 调用方传入发布者版本，且条目还没有下限：写成发布者版本。新 Mod 因此带上下限。
- 已有合法下限：保持原值。用更新的管理器直传一个小修复，不会把 `1.3.14` 自动抬成 `1.3.18`，也不会把 `1.3.18` 降回 `1.3.14`。抬高或放宽只通过手改 `catalog.json`。
- 已有值不合法：整次合并失败，错误字段名 `minManagerVersion`。
- 调用方没有传入发布者版本：不动已有的 `minManagerVersion`。现有测试走这条。
- 上传的 `entry.json` 里自带的 `minManagerVersion` 不采用。

`parts` 与去掉 `originUrl` 的现有规则不变。

## 公告

文件在 MechabellumMods 仓库根目录 `notice.json`。镜像键 `MechabellumMods/notice.json`。地址顺序与目录相同：先 GitHub raw `https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/notice.json`，失败再镜像。能连上 GitHub 的玩家不碰镜像。

导航在「目录」和「指南」之间增加「公告」。启动页仍是本地库或指南，不会停在公告页。请求只写在 `ShowNoticePage` 里。`OnActiveContentPageChanged` 不请求，避免启动时切到指南也把公告拉下来。离开页面时取消尚未完成的请求。再次点开再请求一次。停在页面上不轮询。

成功则把正文写入数据目录 `notice-cache.json` 并显示。失败则显示该缓存，并加上 `NoticeStale`。从未成功过则只显示 `NoticeUnavailable`。响应大于 1MB，或 JSON 不是带 `items` 数组的对象：视为失败，不覆盖缓存。`items` 按文件顺序显示，最多 30 条，其余丢掉。一条在解析出标题和正文都为空时不显示。空列表显示 `NoticeEmpty`。

```json
{
  "updatedAt": "2026-10-06T00:00:00Z",
  "items": [
    {
      "time": "2026-10-06T00:00:00Z",
      "title": "管理器 1.3.14",
      "body": "从这一版起，目录里的每个 Mod 可以写明最低管理器版本。版本不够时，管理器会提示先更新，并且不会开始下载。已经装进本地库的，以及手动放进游戏目录的文件，不受这条规定。公告在打开本页时更新，不随管理器发版。",
      "locales": {
        "en": {
          "title": "Manager 1.3.14",
          "body": "From this version, each catalog mod can name a minimum manager version. If this manager is older, it asks you to update and does not start the download. Mods already in the library, and files placed in the game folder by hand, are not affected. This page refreshes when opened, without a manager release."
        },
        "de": {
          "title": "Manager 1.3.14",
          "body": "Ab dieser Version kann jedes Katalog-Mod eine Mindestversion des Managers angeben. Ist dieser Manager älter, fordert er zum Update auf und startet keinen Download. Bereits in der Bibliothek vorhandene Mods und von Hand in den Spielordner gelegte Dateien sind nicht betroffen. Diese Seite aktualisiert sich beim Öffnen, ohne eine neue Manager-Version."
        },
        "ja": {
          "title": "マネージャー 1.3.14",
          "body": "この版から、カタログの各 Mod は必要なマネージャーの最低バージョンを指定できます。今のマネージャーがそれより古い場合は更新を案内し、ダウンロードは開始しません。すでにライブラリにあるもの、およびゲームフォルダに自分で置いたファイルは、この制限を受けません。このページは開いたときに更新され、マネージャー自体の更新は不要です。"
        },
        "ru": {
          "title": "Менеджер 1.3.14",
          "body": "Начиная с этой версии каждая запись каталога может указать минимальную версию менеджера. Если эта копия старше, менеджер просит обновиться и не начинает загрузку. Уже добавленные в библиотеку моды и файлы, положенные в папку игры вручную, не затрагиваются. Страница обновляется при открытии, без выпуска новой версии менеджера."
        }
      }
    }
  ]
}
```

默认 `title` 与 `body` 是简体中文。`locales` 的键与目录相同：`en`、`de`、`ja`、`ru`。界面语言是 `zh-CN` 时用默认文字。其它语言有非空标题或正文就用该语言，缺的那一侧退回默认。`time` 能解析成时间则按本地时间显示到分钟，不能解析则原样显示，不因此丢掉整条。

`tools/sync-mirror.ps1` 在 `notice.json` 存在时，于 `catalog.json` 之前上传到 `MechabellumMods/notice.json`。文件不存在不让整个同步失败。本次发版清单要求该文件存在。脚本不增加删除整包的步骤。

`scripts/validate_catalog.py`：`minManagerVersion` 可缺。若存在，必须是上面的三段整数。不要求简介前缀改成 1.3.14。现有「需要 1.3.13」前缀保持不动，因为 1.3.13 仍能下载；真正的下限由字段和 1.3.14 的界面显示。

## 测试

纯函数与服务，不靠点界面：

- 三段比较：相等通过，`1.3.15` 对 `1.3.14` 通过，`1.3.13` 对 `1.3.14` 拒绝，`1.3.9` 低于 `1.3.14`，`1.4.0` 高于 `1.3.14`。
- 空字段通过。`1.3`、`1.3.14.1`、`1.3.14-rc`、`v1.3.14`、`1.03.14` 作为目录值拒绝。
- 当前版本 `1.3.14+abc` 与 `1.3.14.0` 都按 `1.3.14` 比较。
- 版本文件不一致时删三份目录缓存且不删 `update-check.stamp`；一致时不删。
- 整包候选全部 HTTP 404 时抛 `CatalogFileMissingException`。混有超时或哈希不符时不抛它。有 `parts` 时不抛它。
- 一次下载动作里第二次 404 不再调用刷新。刷新后的条目下限更高时不下载。刷新后有 `parts` 且下限通过时下载一次。
- 直传合并：空下限写成发布者版本；已有 `1.3.14`、发布者 `1.3.18` 时仍是 `1.3.14`；已有 `1.3.18`、发布者 `1.3.14` 时仍是 `1.3.18`；已有值不合法时失败；不传发布者版本时原字段保留。
- 公告：成功写入缓存；失败返回缓存且不覆盖；大于 1MB 为失败；超过 30 条只留前 30 条；语言退回中文。
- 生产代码里 `NoticeService.LoadAsync` 只有 `ShowNoticePage` 这一处调用。启动、目录刷新和 `OnActiveContentPageChanged` 都不调用它。

## 发版

功能测试与下面的验收都通过之后才打包。打包与上传使用现有渠道，不把安装包塞进每个 Mod 目录。

1. `MechabellumModManager.csproj` 的 `Version` 与 `packaging/installer/MechabellumModManager.iss` 的 `MyAppVersion` 都是 `1.3.14`。
2. `packaging/installer/build-installer.ps1` 产出 `MechabellumModManager_Setup_v1.3.14.exe`。
3. MechabellumMods：18 条都有 `minManagerVersion` `1.3.14`，校验脚本通过，并带上上面的 `notice.json`。先推送该仓库。
4. 管理器仓库提交代码。`release/latest.json` 仍指向已经能下载的上一版，直到本版安装包在 GitHub Release 上可以下载。
5. GitHub Release 上传安装包、便携 zip 和该版 `latest.json`。字段与 1.3.13 的 `release/latest.json` 相同：`version`、`notes`、`setupUrl`、`setupSha256`、`portableUrl`、`portableSha256`、`publishedAt`、`build`。`version` 为 `1.3.14`。两个 URL 指向本次附件，两个 SHA256 是附件字节。便携 zip 是 `publish` 目录打出来的 `MechabellumModManager_portable_v1.3.14.zip`。`notes` 用简体中文说明下限与公告，后面接一段英文。
6. `tools/sync-mirror.ps1` 带安装包与镜像根地址，上传 Mod 文件、`notice.json`、`catalog.json`、管理器 `latest.json` 和安装包。不删除整包键。
7. 安装包确认可下载之后，再单独推送管理器仓库的 `release/latest.json`，使 raw 指针也是 `1.3.14`。
8. `tools/verify-mirror.ps1 -ExpectVersion 1.3.14` 通过。用一次不带缓存的 GET 确认 GitHub 与镜像的 `catalog.json` 含 `minManagerVersion`，`notice.json` 为 200，整包 DLL 仍为 200。

48 小时未满之前不删除整包。
