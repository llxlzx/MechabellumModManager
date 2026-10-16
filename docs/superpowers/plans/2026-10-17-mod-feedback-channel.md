# 模组反馈渠道 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 目录和本地库里每个模组可以有一条作者自己的反馈渠道；关于页「建议」、投稿、举报、诊断包仍发给 `llxmod@foxmail.com`。

**Architecture:** 纯函数 `FeedbackChannel.Classify` 决定隐藏、发信、打开 https 或复制。发信由新的 `ModFeedbackMail` 生成，不调用收件人写死为维护者的那些函数。本地库的渠道只放在视图模型上，不写入 `package.json`。直传表单和 `mergeCatalogMod` / `inspectGrant` 用同一套拒绝规则。

**Tech Stack:** C# / WPF / xUnit / FluentAssertions；Cloudflare Worker 用 TypeScript / vitest。

**Working branch:** `feat/mod-feedback-channel`（从当前 `master` 拉出。不要在 `master` 上提交。）

## Global Constraints

- 关于页「建议」（`SendFeedback` / `BuildFeedbackCompose`）、投稿、举报、诊断包的收件人保持 `llxmod@foxmail.com`。
- 只有目录详情和本地库详情上的「反馈」使用该模组的 `feedback`。
- 判定顺序先匹配先生效。协议比较不分大小写。不从一段话里抽第一个地址。
- 绝对地址且协议不是 `https` 也不是 `mailto`：不显示、不复制、不打开。`http://example.com` 属于这里。
- `https` 但 `ExternalUrlPolicy.TryParse` 拒绝（含用户信息）：不显示、不复制、不打开。
- 可打开的 `https` 只交给现有 `TryOpenUrl`。不另写 `Process.Start`。
- `mailto:` 的 `?` 之前恰好一个邮箱才发信。写坏的 `mailto:` 不显示、不复制、不发信。
- 一个邮箱：整段无空白、恰好一个 `@`、`@` 后有一个 `.`、无回车换行逗号。
- 其余非空文字才复制原文。
- 模组发信不调用 `BuildFeedbackCompose`、`BuildMailto`、`BuildGmailComposeUrl`、`BuildClipboardPackage`、`OpenCompose`。
- Gmail 和本机 `mailto:` 把收件人、主题、正文放进链接，不写剪贴板。只有 QQ 打开 `https://wx.mail.qq.com/` 并复制收件人、主题、正文。取消选择：不打开、不写剪贴板。
- 主题用界面上看到的名称：目录用 `CatalogLocaleResolver.ResolveName`，本地库用 `ModItemViewModel.DisplayName`。版本空着时主题只有名称。正文仍留空的版本行。
- 模组反馈的日志和失败提示不写作者地址，也不写 `llxmod@foxmail.com`。关于页「建议」失败时仍可带维护者邮箱。
- `feedback` 不写入 `package.json`，不改 `Package` 上已有的作者、版本、简介。这次没匹配或目录为空就清掉内存里的渠道。
- 不提高 `minManagerVersion`。不把 GODOG 邀请链接 `https://discord.gg/CQDkCfDTA` 写进任何模组。
- 直传新 id：缺键、空白、`null`、或判定为隐藏，都拒绝。已有 id 的更新缺键则保留旧值；键存在但是空白或 `null` 则拒绝。

---

### Task 1: 渠道判定

**Files:**
- Create: `src/MechabellumModManager/Services/FeedbackChannel.cs`
- Test: `tests/MechabellumModManager.Tests/FeedbackChannelTests.cs`

**Interfaces:**
- Consumes: `ExternalUrlPolicy.TryParse`
- Produces: `FeedbackKind`（`Hidden`、`Mail`、`OpenHttps`、`Copy`）、`FeedbackDecision`（`Kind`、`Target`、`Original`）、`FeedbackChannel.Classify(string? raw)`

- [ ] **Step 1: 建分支并写失败测试**

```bash
git checkout -b feat/mod-feedback-channel
```

```csharp
public class FeedbackChannelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_is_hidden(string? raw)
    {
        FeedbackChannel.Classify(raw).Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Theory]
    [InlineData("http://example.com/form")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("vbscript:msgbox")]
    public void Non_https_schemes_are_hidden(string raw)
    {
        FeedbackChannel.Classify(raw).Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Https_with_userinfo_is_hidden()
    {
        FeedbackChannel.Classify("https://user@evil.example/form").Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Plain_email_and_mailto_open_mail_to_that_address_only()
    {
        FeedbackChannel.Classify("Player@Example.com").Should().BeEquivalentTo(new FeedbackDecision(
            FeedbackKind.Mail, "Player@Example.com", "Player@Example.com"));
        FeedbackChannel.Classify("mailto:player@example.com?subject=old").Should().BeEquivalentTo(
            new FeedbackDecision(FeedbackKind.Mail, "player@example.com", "mailto:player@example.com?subject=old"));
    }

    [Fact]
    public void Broken_mailto_is_hidden()
    {
        FeedbackChannel.Classify("mailto:not-an-email").Kind.Should().Be(FeedbackKind.Hidden);
        FeedbackChannel.Classify("mailto:a@b.com,c@d.com").Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Acceptable_https_opens_that_url()
    {
        const string url = "https://example.com/form";
        FeedbackChannel.Classify(url).Should().BeEquivalentTo(new FeedbackDecision(FeedbackKind.OpenHttps, url, url));
    }

    [Theory]
    [InlineData("QQ 12345")]
    [InlineData("example.com/form")]
    [InlineData("邮箱是 a@b.com")]
    public void Other_text_is_copied_whole(string raw)
    {
        var decision = FeedbackChannel.Classify(raw);
        decision.Kind.Should().Be(FeedbackKind.Copy);
        decision.Target.Should().Be(raw);
        decision.Original.Should().Be(raw);
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FeedbackChannelTests`

Expected: FAIL，找不到 `FeedbackChannel`。

- [ ] **Step 3: 实现判定**

```csharp
public enum FeedbackKind { Hidden, Mail, OpenHttps, Copy }

public readonly record struct FeedbackDecision(FeedbackKind Kind, string? Target, string Original);

public static class FeedbackChannel
{
    public static FeedbackDecision Classify(string? raw)
    {
        var value = (raw ?? "").Trim();
        var original = value;
        if (value.Length == 0)
            return new(FeedbackKind.Hidden, null, "");

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme;
            if (!scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase))
                return new(FeedbackKind.Hidden, null, original);

            if (scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                if (!ExternalUrlPolicy.TryParse(value, out _))
                    return new(FeedbackKind.Hidden, null, original);
                return new(FeedbackKind.OpenHttps, original, original);
            }

            var address = value["mailto:".Length..];
            var query = address.IndexOf('?');
            if (query >= 0)
                address = address[..query];
            if (IsOneEmail(address))
                return new(FeedbackKind.Mail, address, original);
            return new(FeedbackKind.Hidden, null, original);
        }

        if (IsOneEmail(value))
            return new(FeedbackKind.Mail, value, original);
        return new(FeedbackKind.Copy, original, original);
    }

    static bool IsOneEmail(string value)
    {
        if (value.Length == 0 || value.IndexOfAny([' ', '\r', '\n', ',']) >= 0)
            return false;
        var at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && value.IndexOf('.', at + 1) > at + 1;
    }
}
```

`mailto:` 前缀比较要忽略大小写。上面的切片假设前缀就是小写 `mailto:`；实现时先判断 `StartsWith("mailto:", OrdinalIgnoreCase)`，再按实际前缀长度截取。

- [ ] **Step 4: 再跑测试**

Run: 同上。Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/FeedbackChannel.cs tests/MechabellumModManager.Tests/FeedbackChannelTests.cs
git commit -m "feat: classify mod feedback channels without opening unsafe URLs"
```

### Task 2: 作者发信内容

**Files:**
- Create: `src/MechabellumModManager/Services/ModFeedbackMail.cs`
- Test: `tests/MechabellumModManager.Tests/ModFeedbackMailTests.cs`

**Interfaces:**
- Consumes: 无。不调用 `GitHubCommunityLinks` 的维护者发信函数。
- Produces: `ModFeedbackMail.Build(string author, string visibleName, string? version)` 返回 `To`、`Subject`、`Body`、`GmailUrl`、`MailtoUrl`、`QqClipboard`。`QqWebMail` 常量等于 `https://wx.mail.qq.com/`。

- [ ] **Step 1: 写失败测试**

```csharp
public class ModFeedbackMailTests
{
    [Fact]
    public void Subject_uses_the_visible_name_and_omits_an_empty_version()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "网格标尺", "2.0.0");
        mail.To.Should().Be("author@example.com");
        mail.Subject.Should().Be("[Mod 反馈 / Feedback] 网格标尺 2.0.0");
        mail.Body.Should().Be("【Mod】网格标尺\n【版本 / Version】2.0.0\n【问题 / Issue】\n");

        ModFeedbackMail.Build("author@example.com", "网格标尺", "  ").Subject
            .Should().Be("[Mod 反馈 / Feedback] 网格标尺");
    }

    [Fact]
    public void Gmail_and_mailto_address_the_author_and_do_not_mention_the_maintainer()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "Cam", "1");
        mail.GmailUrl.Should().Contain("to=" + Uri.EscapeDataString("author@example.com"));
        mail.GmailUrl.Should().NotContain("llxmod@foxmail.com");
        mail.MailtoUrl.Should().StartWith("mailto:author@example.com?");
        mail.MailtoUrl.Should().NotContain("llxmod");
    }

    [Fact]
    public void Qq_clipboard_contains_the_author_and_the_page_stays_the_qq_home()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "Cam", "1");
        ModFeedbackMail.QqWebMail.Should().Be("https://wx.mail.qq.com/");
        mail.QqClipboard.Should().Contain("To: author@example.com").And.Contain(mail.Subject).And.Contain(mail.Body);
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter ModFeedbackMailTests`

Expected: FAIL，找不到 `ModFeedbackMail`。

- [ ] **Step 3: 实现**

正文用 `\n` 连接四行，最后一行后也有换行。Gmail 链接格式与 `BuildGmailComposeUrl` 相同，但 `to` 是作者。`mailto` 的 subject 和 body 用 `Uri.EscapeDataString`。`QqClipboard` 是 `To: {author}\r\nSubject: {subject}\r\n\r\n{body}`。

- [ ] **Step 4: 再跑测试**

Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/ModFeedbackMail.cs tests/MechabellumModManager.Tests/ModFeedbackMailTests.cs
git commit -m "feat: address per-mod feedback mail to the author"
```

### Task 3: 目录字段和本地库只保留这一次匹配

**Files:**
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs` 的 `CatalogMod`
- Modify: `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` 的 `EnrichModsFromCatalog`
- Test: `tests/MechabellumModManager.Tests/ModFeedbackBindingTests.cs`

**Interfaces:**
- Consumes: `FeedbackChannel.Classify`、`ModCatalogService.Matches`、`MainViewModelFixture`
- Produces: `CatalogMod.Feedback`；`CatalogModItemViewModel.ShowModFeedback`；`ModItemViewModel.SetCatalogFeedback(string?)`、`ClearCatalogFeedback()`、`ShowModFeedback`、`ModFeedbackDecision`

- [ ] **Step 1: 写失败测试**

用 `MainViewModelFixture.CreateReady()`。目录 id 用 `cam-aaaaaaaa`，这样对得上夹具里的包。

```csharp
[Fact]
public void Library_feedback_tracks_only_the_current_match()
{
    using var fx = MainViewModelFixture.CreateReady();
    var vm = fx.CreateVm();
    vm.ApplyCatalogRoot(new CatalogRoot
    {
        Mods =
        [
            new CatalogMod
            {
                Id = "cam-aaaaaaaa",
                Name = "Cam",
                File = "mods/cam/Cam.dll",
                Feedback = "author@example.com"
            }
        ]
    });
    vm.Mods.Single().ShowModFeedback.Should().BeTrue();

    vm.ApplyCatalogRoot(new CatalogRoot { Mods = [] });
    vm.Mods.Single().ShowModFeedback.Should().BeFalse();
    File.ReadAllText(fx.Library.List().Single().PackageDirectory + "\\package.json")
        .Should().NotContain("feedback");
}
```

再加一个目录项测试：`Feedback` 为 `https://example.com/form` 时 `ShowModFeedback` 为真；为 `http://example.com` 时为假。

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter ModFeedbackBindingTests`

Expected: FAIL，没有 `Feedback` 或 `ShowModFeedback`。

- [ ] **Step 3: 实现**

`CatalogMod` 增加 `[JsonPropertyName("feedback")] public string? Feedback { get; set; }`。

`CatalogModItemViewModel.ShowModFeedback` 为 `Classify(Mod.Feedback).Kind != Hidden`。

`ModItemViewModel` 用私有字段保存这一次的原文。`SetCatalogFeedback` 保存并通知。`ClearCatalogFeedback` 把它设为 null。`ApplyCatalogEnrichment` 只调用 `SetCatalogFeedback(catalog.Feedback)`，不改 `Package` 的其他字段来存放它，也不调用 `SavePackageMeta`。

`EnrichModsFromCatalog`：目录为空时，每个未缺失的模组 `ClearCatalogFeedback()`。目录非空时，没有 `FindCatalogMatch` 的也 `ClearCatalogFeedback()`；匹配到的仍走 `ApplyCatalogEnrichment`。

- [ ] **Step 4: 再跑测试**

Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/ModCatalogService.cs src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs src/MechabellumModManager/ViewModels/ModItemViewModel.cs src/MechabellumModManager/ViewModels/MainViewModel.cs tests/MechabellumModManager.Tests/ModFeedbackBindingTests.cs
git commit -m "feat: keep mod feedback on the current catalog match only"
```

### Task 4: 两个详情上的按钮

**Files:**
- Modify: `src/MechabellumModManager/Resources/Strings.resx`、`Strings.en.resx`、`Strings.ru.resx`、`Strings.ja.resx`、`Strings.de.resx`
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`
- Modify: `src/MechabellumModManager/MainWindow.xaml` 目录详情约 1511 行、本地库详情约 2010 行，放在「举报」按钮上方
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Test: `tests/MechabellumModManager.Tests/ModFeedbackCommandTests.cs`

**Interfaces:**
- Consumes: `FeedbackChannel`、`ModFeedbackMail`、`TryOpenUrl`、`TryCopyText`、`_promptMailProvider`
- Produces: `SendCatalogModFeedbackCommand`、`SendLibraryModFeedbackCommand`。文案键 `Feedback`、`FeedbackCopied`、`FeedbackPasteMail`。

- [ ] **Step 1: 写失败测试**

用可以注入 `promptMailProvider`、`tryOpenExternal`、`copyText` 的 `CreateVm`。目录反馈为 `author@example.com`。选择该目录项后执行 `SendCatalogModFeedbackCommand`。

- `promptMailProvider` 返回 `MailProvider.Gmail`：打开的 URL 含 `to=` 加作者，不含 `llxmod`；`copyText` 没被调用。
- 返回 `MailProvider.Qq`：打开的 URL 是 `https://wx.mail.qq.com/`，剪贴板含 `To: author@example.com`。
- 返回 null：打开函数和复制函数都不调用。
- 反馈为 `https://example.com/form`：不弹邮件选择，`tryOpenExternal` 收到这个地址。
- 反馈为 `QQ 12345`：只复制这五个字符，通知用 `FeedbackCopied`。

`MailProvider` 只有 `Qq` 和 `Gmail`。命令不打开 `MailtoUrl`，也不加第三颗按钮。

本地库用 Task 3 的匹配夹具，再执行 `SendLibraryModFeedbackCommand`，Gmail 的主题含界面上的 `DisplayName`。

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter ModFeedbackCommandTests`

Expected: FAIL，没有这两个命令。

- [ ] **Step 3: 实现**

五种语言都加键。zh-CN：`反馈`、`已复制作者留下的反馈方式`、`已复制收件人、主题和正文，请粘贴到邮件里`。en 以及 ru、ja、de：`Feedback`、`Copied the author's feedback channel`、`Copied the recipient, subject, and body. Paste them into the mail.`

按钮 `Style="{StaticResource GhostButtonStyle}"`，`Visibility` 绑 `ShowModFeedback`。命令在 `Kind == Hidden` 时直接返回。`OpenHttps` 调 `TryOpenUrl(decision.Target)`，失败通知用现有打开失败文案，不拼接邮箱。`Copy` 调 `TryCopyText(decision.Original)`。`Mail` 先 `_promptMailProvider`。`Gmail` 打开 `GmailUrl`。`Qq` 先复制 `QqClipboard`，再打开 `QqWebMail`。没有第三种提供方。日志只写「已打开模组反馈」或「已复制模组反馈」，不写地址。

名称：目录用 `SelectedCatalogMod.Name`（已是 `ResolveName`）。本地库用 `SelectedLibraryMod.DisplayName`。版本按规格。

- [ ] **Step 4: 再跑测试**

Expected: PASS。再跑 `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~GitHubCommunityLinksTests|FullyQualifiedName~ModFeedback"`，确认关于页建议的收件人测试仍是 `llxmod@foxmail.com`。

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Resources src/MechabellumModManager/ViewModels/UiStrings.cs src/MechabellumModManager/ViewModels/MainViewModel.cs src/MechabellumModManager/MainWindow.xaml tests/MechabellumModManager.Tests/ModFeedbackCommandTests.cs
git commit -m "feat: add a per-mod feedback button beside report"
```

### Task 5: 投稿模板改为必填渠道

**Files:**
- Modify: `src/MechabellumModManager/Services/GitHubCommunityLinks.cs` 的 `BuildSubmitBody`、`BuildUpdateBody`
- Test: `tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: 两段正文都含「【反馈渠道 / Feedback】（必填」且不再含「【联系方式 / Contact】（可选）」

- [ ] **Step 1: 改测试**

把现有对「联系方式」可选的断言改成：投稿和更新正文都包含

`【反馈渠道 / Feedback】（必填。作者自己提供，玩家要能据此联系上作者。会打开的只有不带用户信息的 https。这是把使用问题送回作者的渠道，中文和英文玩家都要能用。）`

并且 `Should().NotContain("【联系方式 / Contact】（可选）")`。`Inbox` 仍等于 `llxmod@foxmail.com`。

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter GitHubCommunityLinksTests`

Expected: FAIL，正文仍是旧的联系方式行。

- [ ] **Step 3: 改两个 Build 方法里的那一行**

- [ ] **Step 4: 再跑，Expected: PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/GitHubCommunityLinks.cs tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs
git commit -m "feat: require an author feedback channel in submit mail"
```

### Task 6: 直传校验

**Files:**
- Modify: `services/mod-publish-worker/src/catalog.ts`
- Modify: `services/mod-publish-worker/src/catalog.test.ts`
- Modify: `services/mod-publish-worker/src/inboxGrant.ts`
- Modify: `services/mod-publish-worker/src/inboxGrant.test.ts`
- Modify: `services/mod-publish-worker/src/inboxHttp.test.ts` 的 `singleGrantBody`
- Modify: `src/MechabellumModManager/Services/InboxSubmission.cs` 与 `DirectUploadDialog.xaml` / `.xaml.cs`
- Test: C# 侧若 `InboxSubmission.Build` 增加参数，改 `tests/MechabellumModManager.Tests/InboxGrantClientTests.cs` 里构造请求的地方，并加一条空白渠道不发出的测试

**Interfaces:**
- Consumes: 与 `FeedbackChannel` 相同的隐藏规则。TypeScript 里写 `classifyFeedback(value: string): "hidden" | "ok"`，不要引用 C#。
- Produces: `CatalogMod.feedback?: string | null`。`mergeCatalogMod` 用 `Object.prototype.hasOwnProperty.call(entry, "feedback")` 区分缺键和空白。`inspectGrant` 成功值含 `feedback: string`。

- [ ] **Step 1: 先改 worker 测试**

现有成功的 `mergeCatalogMod` 新条目加上 `feedback: "author@example.com"`。新增：新 id 不带该键、`""`、`null`、`http://example.com`、`javascript:alert(1)`、`mailto:not-an-email` 都抛 `validation_failed`。已有条目不带该键时，合并结果仍是旧的 `feedback`。已有条目带 `""` 或 `null` 抛错。

`singleGrantBody` 增加 `feedback: "author@example.com"`。缺这个字段、空白、`http://example.com` 时期望 `validation_failed`。

- [ ] **Step 2: 运行，确认失败**

Run: `npm test`，工作目录 `services/mod-publish-worker`。

Expected: FAIL。

- [ ] **Step 3: 实现 worker 与客户端**

`inspectGrant` 要求 `feedback` 是非空字符串且 `classifyFeedback` 为 `ok`。`mergeCatalogMod` 按 Global Constraints 处理缺键和空白。

直传对话框在简介下加 `FeedbackBox`。`InboxSubmission.Build` 增加 `string? feedback`。空白或判定为隐藏时 `Build` 返回失败，对话框不调用 `RequestAsync`。合法时放进请求正文。

- [ ] **Step 4: 再跑 worker 测试和相关 C# 测试**

Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/mod-publish-worker/src src/MechabellumModManager/Services/InboxSubmission.cs src/MechabellumModManager/Dialogs/DirectUploadDialog.xaml src/MechabellumModManager/Dialogs/DirectUploadDialog.xaml.cs tests/MechabellumModManager.Tests/InboxGrantClientTests.cs
git commit -m "feat: reject direct uploads that omit a usable feedback channel"
```
