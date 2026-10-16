# 模组反馈渠道

**Status:** 作者已通过（2026-10-17）。实现计划见 `docs/superpowers/plans/2026-10-17-mod-feedback-channel.md`、`docs/superpowers/plans/2026-10-17-feedback-catalog-standard.md`、`docs/superpowers/plans/2026-10-17-author-reply-packs.md`。  
**ID:** `mod-feedback-channel`  
**范围：** 管理器为每个目录模组显示作者自己提供的反馈渠道，并按邮箱、链接或其他文字打开它。创作标准和投稿模板改为必填。另给 Oddz、Minz 各准备一封回信，内含改过的源码和用本机游戏引用编出的 DLL。这三份模组先不写入公开目录。

## 目的

玩家在某个模组上出了问题，需要有一条作者指定的途径把问题送回去。渠道由作者提供，可以是邮箱、问卷链接，或一段玩家能照做的说明。管理器负责把这段文字变成可点的「反馈」，并按内容选择发信、打开网页或复制原文。

顶部现有的社区按钮保持原样：国际版打开 Discord `https://discord.gg/CQDkCfDTA`，国内版打开 QQ 群。那是社区入口，不是某个模组作者的反馈渠道。

管理器自己的反馈仍发给维护者 `llxmod@foxmail.com`。关于页的「建议」按钮（`SendFeedback` / `BuildFeedbackCompose`）、投稿、举报、诊断包都走这条，收件人和现有文案不改。只有目录详情和本地库详情里、某一个模组上的「反馈」按钮，才使用该模组作者留下的 `feedback`。

## 不做的事

- 不改关于页「建议」、投稿、举报、诊断包的收件人。它们仍然是 `llxmod@foxmail.com`。
- 不把加入 Discord 或 QQ 群写成作者义务。
- 不在模组列表里直接显示邮箱或链接原文。
- 不代替作者发信，也不代收问卷。
- 不因为缺 `feedback` 拒绝安装、更新或启用。
- 不把 `http`、带用户信息的地址，或 `https` 以外的协议交给系统外壳。链接只走现有的 `ExternalUrlPolicy` 和 `TryOpenUrl`。
- 不因为旧目录条目没有 `feedback` 而让整份 `catalog.json` 校验失败。
- 不为此提高 `minManagerVersion`。旧管理器忽略不认识的字段，下载照旧。
- 不把 GODOG 的 Discord 邀请链接填进任何模组的 `feedback`。
- 不把这次三份投稿的邮箱写进公开目录。回信里提议，等作者确认、你收录时再写入。
- 不改网格标尺挑选单位的方式，也不改它的 C 键。
- 不改升级助手「超时 5 秒后中止后续队列」。
- 不提交 `_eml_extract` 和回信文件夹。

## 渠道怎么判定

目录字段名是 `feedback`，字符串。先去掉首尾空白。判定只用整段文字，不从一段话里抽第一个地址。协议比较不分大小写。按下面的顺序，先匹配的一行生效。

一个「邮箱」是整段不含空白、恰好一个 `@`、`@` 后面含一个 `.`、且不含回车、换行、逗号的文字。

| 顺序 | 整段文字 | 动作 |
|---|---|---|
| 1 | 空 | 不显示按钮 |
| 2 | 能被当成绝对地址，且协议不是 `https` 也不是 `mailto`。包括 `http`、`file`、`javascript`、`data`、`vbscript`，以及任何自定义协议 | 不显示按钮，不复制，不打开 |
| 3 | `https` 开头，但 `ExternalUrlPolicy.TryParse` 不接受。包括带用户信息的地址、没有主机的地址 | 不显示按钮，不复制，不打开 |
| 4 | `mailto:` 开头，`?` 之前恰好是一个邮箱 | 打开发信窗口，收件人只取这个邮箱 |
| 5 | 恰好一个邮箱 | 同上 |
| 6 | `mailto:` 开头，但 `?` 之前不是恰好一个邮箱 | 不显示按钮，不复制，不发信 |
| 7 | `https` 开头，且 `ExternalUrlPolicy.TryParse` 接受 | 只用 `TryOpenUrl` 打开。不另写 `Process.Start` |
| 8 | 其余任何非空文字。包括 QQ 号、不带协议的网址、带说明的句子 | 显示按钮。点击后复制原文，并提示已复制 |

第 2 行使用 `Uri.TryCreate` 的绝对地址判断。`http://example.com` 因此不打开。带说明的句子，例如「邮箱是 a@b.com」或「打开 https://example.com」，整段落到第 8 行，只复制。`mailto:` 忽略作者原来附在 `?` 后面的主题和正文。`?` 之前不是恰好一个邮箱时走第 6 行，不显示按钮。

模组上的「反馈」是一条新路径，函数接收作者地址。它不调用 `BuildFeedbackCompose`、`BuildMailto`、`BuildGmailComposeUrl`、`BuildClipboardPackage`、`OpenCompose`，免得把信寄到 `llxmod@foxmail.com`。上述函数留给关于页「建议」、投稿、举报和诊断包，继续把收件人写成这个维护者邮箱。模组反馈的日志和失败提示不写作者地址，也不写维护者地址。关于页「建议」失败时仍可按现在这样带上维护者邮箱。

玩家仍看见现在的选择窗口。这个窗口只有 QQ 邮箱和 Gmail 两颗按钮，不新增「本机邮件程序」。

- Gmail 撰写链接的收件人、主题、正文都放进链接。这条不写剪贴板。
- QQ 打开现有的 `https://wx.mail.qq.com/`。该地址带不上收件人，所以只在这一条把收件人、主题、正文复制到剪贴板，并提示粘贴。
- 玩家取消选择窗口：不打开页面，不写剪贴板。

`ModFeedbackMail.MailtoUrl` 只用来锁定「收件人是作者」的地址格式，命令不打开它。

主题用玩家当前界面上看到的名称。目录详情用 `CatalogLocaleResolver.ResolveName`。本地库用 `ModItemViewModel.DisplayName`。版本空着时主题只有名称。正文固定为：

```
【Mod】{名称}
【版本 / Version】{版本}
【问题 / Issue】

```

名称和上面的主题是同一个字符串。版本空着时仍留「版本」行，值为空。目录详情用目录版本。本地库用已安装包的版本；该版本空着时用目录版本。

复制失败时，用现有通知把原文显示出来，不写日志里的地址。打开 `https` 失败时用现有的打开失败提示，不附任何邮箱。发信窗口打不开时同样不附邮箱。

## 数据从哪来

`feedback` 与 `author` 一样，是目录条目上的可选字符串。键不存在的旧条目保持有效。

键存在时，去掉空白后必须非空，并且不能落到判定表的第 2、3、6 行。校验脚本是 `D:\gongzuo\MechabellumMods\scripts\validate_catalog.py`。它只拒绝这种写错的键。整份目录不因为旧条目缺少该键而失败。

新投稿和更新要带上这段，维护者才收录这一条。白名单直传的表单增加一栏。客户端在新条目上不发送缺键或空白。`services/mod-publish-worker` 的单条发布：新 id 缺键、空白、`null`，或落到第 2、3、6 行，都拒绝。已有 id 的更新若请求里没有这个键，保留目录里已有的值，不把旧渠道抹掉；键存在但是空白或 `null` 则拒绝这次发布。

本地库沿用 `FindCatalogMatch`。这一次匹配成功时，详情读该目录条目的 `feedback`，不写入 `package.json`，也不改 `Package` 上已有的作者、版本或简介。这一次没有匹配，或目录为空：按钮不显示，内存里不留上一次的渠道。`ApplyCatalogEnrichment` 现有「匹配失败不清字段」的行为不扩展到反馈。

## 界面

两处详情里，现有「举报」按钮上方放「反馈」按钮，样式与「举报」相同。只在判定结果为邮箱、可打开的 `https` 或复制时显示。

文案加进 `Strings.resx`、`Strings.en.resx`、`Strings.ru.resx`、`Strings.ja.resx`、`Strings.de.resx`：

| 键 | zh-CN | en |
|---|---|---|
| `Feedback` | 反馈 | Feedback |
| `FeedbackCopied` | 已复制作者留下的反馈方式 | Copied the author's feedback channel |
| `FeedbackPasteMail` | 已复制收件人、主题和正文，请粘贴到邮件里 | Copied the recipient, subject, and body. Paste them into the mail. |

俄文、日文、德文用英文这句，直到有人改译文。

## 创作标准与投稿模板

下列位置把「【联系方式 / Contact】（可选）」改成必填的反馈渠道，中英各一行，说明作者自己提供、玩家要能据此联系上作者：

- 本仓库 `GitHubCommunityLinks.BuildSubmitBody` 与 `BuildUpdateBody`
- `D:\gongzuo\MechabellumMods\README.md` 里三处投稿模板
- `D:\gongzuo\MechabellumMods\docs\submit.html` 里对应模板
- `D:\gongzuo\MechabellumMods\docs\ai-mod-standard.md` 的「先问完再写」

标准里的「身份」改为：作者名写入 `author`。反馈渠道写入 `feedback`，不写进简介。缺了 `feedback` 就停下来问作者，不得留空，不得用 GODOG 的 Discord 邀请链接充数。作者自己会看的链接（含他指定的 Discord 帖子）、邮箱，或一段玩家能照做的说明，都可以。会打开的链接只有不带用户信息的 `https://`。`http://` 和其它协议不会打开，也不要这样写。同一条写明必要性：这是玩家把使用问题送回作者的渠道。中文玩家和英文玩家都要能看懂、都能提交。管理器不检查语言；收录时由维护者核对。

`author`、`summary` 那种「问齐但不由脚本退回」的名单加上 `feedback`：脚本不因缺少该键退回整份目录，标准仍要求新投稿问齐。

## 测试

分类规则单独测：空、普通邮箱、`mailto:`、带多余说明的句子、可接受的 `https://`、`http://`、`HTTPS://用户@主机/`、`file:`、`javascript:`、`JavaScript:`、QQ 号、不带协议的网址。`http`、带用户信息的 `https` 和任何非 `https`/`mailto` 协议都不显示按钮。不带协议的说明才复制。

界面测目录详情和本地库详情：有效渠道才出现「反馈」。空、被拒绝的协议、对不上目录的本地模组都不出现。先匹配到一个带渠道的条目，再让这次匹配失败，按钮消失。`package.json` 不出现 `feedback`。

发信测试断言：作者渠道是邮箱或写对的 `mailto:` 时都进入现在的选择窗口。Gmail 链接的收件人是作者，剪贴板不变，命令不单独打开 `MailtoUrl`。QQ 路径打开 `https://wx.mail.qq.com/`，剪贴板含作者地址和主题。取消选择时剪贴板不变，也不打开页面。模组反馈的日志和失败提示里没有作者地址，也没有 `llxmod@foxmail.com`。关于页「建议」的 `BuildFeedbackCompose` 收件人仍是 `llxmod@foxmail.com`。链接测试断言交给 `TryOpenUrl` 的字符串与该 `https` 相同，且没有第二条打开函数。复制测试断言剪贴板是整段原文。

直传校验测：新 id 缺键、空白、`null` 都拒绝。已有 id 的更新缺键时保留旧值。空白、`null`、`javascript:`、`http://` 都拒绝。

`D:\gongzuo\MechabellumMods\scripts\validate_catalog.py` 测：旧条目没有该键仍通过。键存在但是空、`file:` 或 `http:` 则失败。

## 给作者的回信

输出目录：`D:\gongzuo\作者回信\2026-10-17\`。每个作者一个文件夹、一封 `回信.md`。

| 文件夹 | 作者 | 模组 | 提议的 `feedback` |
|---|---|---|---|
| `Oddz` | Oddz | HideControlTips 1.0.0 | `mailto:rnkt@pm.me` |
| `Minz` | Minz | 升级助手 2.0.0、网格标尺 2.0.0 | `mailto:c4ever_zm@hotmail.com` |

每个模组文件夹含可编译源码，以及编成功时的 DLL。引用取自 `D:\Steam\steamapps\common\Mechabellum` 的 MelonLoader 和游戏程序集。目标框架跟随该 MelonLoader，不用反编译器标成的 `net40`。某个文件编不过：源码保留，`回信.md` 写明文件和错误。

回信说明提议的邮箱只是待确认的渠道，请作者确认或换成问卷、链接、其他说明，并保证中英文玩家都能用。同时写上下面的代码改动和故意不动的地方。

### HideControlTips

隐藏逻辑不动：仍把 `MainUI/tip_unchoose_text` 和 `MainUI/tip_catch_text` 译成空字符串。

- 程序集版本改为 `1.0.0.0`，与 Melon 信息 `1.0.0` 一致。
- `PatchAll` 抛错时用 Melon 日志记下异常，模组继续加载。

### 升级助手

默认锁定的两个单位各有一对名字，必须整段相等，不分大小写，不做子串匹配：`爬虫` / `Crawler`，`尖牙` / `Fang`。对不上的名字忽略，不会锁到别的单位。卡牌表或当前单位行上，一对里任一名字对上就得到该单位的卡牌 ID。这一次刷新的锁定集合包含已经认出的 ID。两个单位都认出之后，才把偏好从 `default` 写成 ID 列表并停止再查名字。只认出一个时不写偏好，下次刷新再查，避免卡牌表还没载入就写成残缺列表。回信写明这两个英文名尚未对照游戏卡牌表，对不上就仍然只锁得住中文名。

本地玩家不再处于部署（`IsDeploying` 为假，或已经 `IsDeployOver`）时，停掉升级队列。原有的「离开全局部署阶段就停止」保留。

5 秒超时后把当前步记为失败并中止后续队列。回信里说明这一点。

### 网格标尺

距离环在 `IsDeploymentActive()` 为假时隐藏，战斗中不画。彩色标尺的画法和显示条件不动。

`GridView.views` 仍只取前 4 个。切换热键仍是偏好里的键，默认 C，任何界面都会响应。偏好里「只在拖动单位时可见」这句说明也不改。回信写明：距离环现在是整个部署阶段都可能出现，不是只在拖动时出现；前 4 个 `views` 和 C 键也请 Minz 确认是不是他要的行为。

## 实现顺序

管理器、目录校验、创作标准是一条线，回信是另一条线。两条线可以分别验证：前者看测试和界面，后者看源码、编译输出和 `回信.md`。
