# Manager in-app Setup update — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace browser-open manager updates with a modal dialog that downloads Setup in-app, launches it, then exits the manager.

**Architecture:** `UpdateChecker` discovers versions and splits downloadable `SetupUrl` vs `BrowseUrl`. New `ManagerSetupDownloader` + `ManagerUpdateDialog` handle download UX. `MainViewModel` shares one orchestration path for startup (after CriticalOp recovery) and the manual Check Updates button. Soft/Hard critical-op gates stay.

**Tech Stack:** C# / WPF (.NET), xUnit + FluentAssertions, existing `ExternalUrlPolicy` / `HttpClient` patterns

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Spec: `docs/dev/superpowers/specs/2026-09-17-manager-in-app-setup-update-design.md`
- Download only https URLs whose `AbsolutePath` ends with `.exe` (ignore query)
- Never download Release HTML pages as Setup
- Startup check failure: log + status only; manual failure: keep domestic Confirm flow
- Cancel download ≠ session skip; Skip / close dialog = session skip for startup path
- Start Setup (local path) success → then `Application.Current.Shutdown()`
- No `/VERYSILENT`; no sha256 this release
- Sync README + `docs/user/国内镜像搭建.md` in the docs task

---

### Task 1: Downloadable URL helper + UpdateChecker BrowseUrl

**Files:**
- Modify: `src/MechabellumModManager/Services/ExternalUrlPolicy.cs`
- Modify: `src/MechabellumModManager/Services/UpdateChecker.cs`
- Modify: `tests/MechabellumModManager.Tests/ExternalUrlPolicyTests.cs`
- Modify: `tests/MechabellumModManager.Tests/UpdateCheckerTests.cs`

**Interfaces:**
- Produces: `ExternalUrlPolicy.IsDownloadableSetupUrl(string? url)`
- Produces: `UpdateCheckResult(..., string? SetupUrl, string? BrowseUrl = null, ...)` — when update available: `SetupUrl` only if downloadable; else null + `BrowseUrl` set to releases/latest

- [ ] **Step 1: Failing tests for IsDownloadableSetupUrl + BrowseUrl**

```csharp
[Theory]
[InlineData("https://cdn.example/MechabellumModManager_Setup_v1.2.8.exe", true)]
[InlineData("https://cdn.example/Setup.exe?x=1", true)]
[InlineData("https://github.com/x/y/releases/latest", false)]
[InlineData("http://cdn.example/Setup.exe", false)]
public void IsDownloadableSetupUrl_requires_https_exe(string? url, bool expected)
{
    ExternalUrlPolicy.IsDownloadableSetupUrl(url).Should().Be(expected);
}

[Fact]
public async Task CheckAsync_invalid_setupUrl_sets_BrowseUrl_not_SetupUrl()
{
    // mirror returns setupUrl that is not .exe
    // assert SetupUrl is null, BrowseUrl ends with /releases/latest, Kind UpdateAvailable
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/MechabellumModManager.Tests --filter "FullyQualifiedName~ExternalUrlPolicyTests|FullyQualifiedName~UpdateCheckerTests" --no-restore`
(or with restore if needed)

- [ ] **Step 3: Implement**

Add to `ExternalUrlPolicy`:

```csharp
public static bool IsDownloadableSetupUrl(string? url)
{
    if (!IsHttpsUrl(url)) return false;
    if (!TryParse(url, out var uri) || uri is null) return false;
    return uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
```

Change `UpdateCheckResult` to add `string? BrowseUrl = null` (after SetupUrl or as last optional before/after Source — keep Source compatibility: add BrowseUrl after Source with default null, OR insert before Source). Prefer:

```csharp
public sealed record UpdateCheckResult(
    UpdateCheckKind Kind,
    string LocalVersion,
    string? RemoteVersion,
    string? Notes,
    string? SetupUrl,
    string Message,
    string? Source = null,
    string? BrowseUrl = null);
```

In `CheckAsync` when building setup:

```csharp
string? setup = null;
string? browse = $"https://github.com/{Owner}/{Repo}/releases/latest";
if (ExternalUrlPolicy.IsDownloadableSetupUrl(manifest.SetupUrl))
    setup = manifest.SetupUrl!.Trim();
// UpdateAvailable: SetupUrl = setup, BrowseUrl = browse (always provide browse as fallback)
```

Update tests that expected releases/latest in `SetupUrl` to expect `BrowseUrl` instead.

- [ ] **Step 4: Run tests — expect PASS**

---

### Task 2: ManagerSetupDownloader

**Files:**
- Create: `src/MechabellumModManager/Services/ManagerSetupDownloader.cs`
- Create: `tests/MechabellumModManager.Tests/ManagerSetupDownloaderTests.cs`

**Interfaces:**
- Consumes: `ExternalUrlPolicy.IsDownloadableSetupUrl`
- Produces:

```csharp
public sealed record ManagerSetupDownloadProgress(string Message, double? Percent, long BytesReceived, long? TotalBytes);

public sealed class ManagerSetupDownloader
{
    public ManagerSetupDownloader(HttpClient? http = null, string? tempRoot = null);
    public static string SanitizeVersion(string? version);
    public async Task<string> DownloadAsync(
        string setupUrl,
        string version,
        IProgress<ManagerSetupDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
    // returns local exe path; throws on invalid URL / non-https redirect / HTTP error
}
```

- [ ] **Step 1: Failing tests** — reject non-exe URL; download bytes to sanitized path; cancel deletes partial; refuse if handler redirects to `http://`

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** with `ResponseHeadersRead`, throttle progress like MelonLoaderInstaller, write `.part` then rename, delete on cancel/fail. After response, if `resp.RequestMessage?.RequestUri` is non-https, fail.

- [ ] **Step 4: Run — PASS**

---

### Task 3: IProcessStarter + orchestration helpers (testable without WPF dialog)

**Files:**
- Create: `src/MechabellumModManager/Services/IProcessStarter.cs` (+ simple `ShellProcessStarter`)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (inject starter + downloader + dialog callback)
- Create: `tests/MechabellumModManager.Tests/ManagerUpdateFlowTests.cs`

**Interfaces:**
- Produces:

```csharp
public interface IProcessStarter
{
    void StartLocalFile(string path);
}

public enum ManagerUpdateDialogOutcome { Skipped, CompletedAndExit, DismissedBrowseOnly }

// Injected: Func<UpdateCheckResult, Task<ManagerUpdateDialogOutcome>>? showUpdateDialog
// Or: Action that tests replace
```

Prefer inject:

```csharp
Func<UpdateCheckResult, CancellationToken, IProgress<ManagerSetupDownloadProgress>?, Task<ManagerUpdateUiResult>>?
```

Keep it simple for tests:

```csharp
// MainViewModel fields
readonly ManagerSetupDownloader _setupDownloader;
readonly IProcessStarter _processStarter;
readonly Func<ManagerUpdatePrompt, Task<ManagerUpdatePromptResult>> _promptManagerUpdate;
readonly Action _shutdown;

public sealed record ManagerUpdatePrompt(
    string LocalVersion, string RemoteVersion, string Notes,
    string? SetupUrl, string? BrowseUrl);

public enum ManagerUpdatePromptAction { Skip, DownloadAndInstall, OpenBrowseUrl }

public sealed record ManagerUpdatePromptResult(ManagerUpdatePromptAction Action);
```

UI layer later maps dialog buttons → these actions. Download loop stays in VM after `DownloadAndInstall`.

Flow in shared method `RunManagerUpdateCheckAsync(bool fromStartup)`:
1. Soft/Hard gates (startup Hard = silent return; Soft = confirm)
2. If fromStartup && `_managerUpdateSkippedThisSession` → return
3. If `_checkingUpdates` → return
4. CheckAsync
5. Failed: startup → log only; manual → existing Confirm flow
6. UpToDate: startup silent; manual status
7. UpdateAvailable → `_promptManagerUpdate(...)`; Skip → set skip flag; OpenBrowseUrl → TryOpenUrl; DownloadAndInstall → download → StartLocalFile → `_shutdown()`

- [ ] **Step 1: Unit tests** with fake prompt/downloader/process/shutdown recording order
- [ ] **Step 2–4: Implement + PASS**

Also: `TryHandleWindowClosing` / download-in-progress Soft confirm — set `_managerSetupDownloadCts` while downloading.

---

### Task 4: ManagerUpdateDialog + App wiring + i18n

**Files:**
- Create: `Dialogs/ManagerUpdateDialog.xaml` + `.xaml.cs`
- Modify: `App.xaml.cs` — after recovery, `_ = vm.RunStartupManagerUpdateCheckAsync();`; wire prompt to dialog; wire shutdown
- Modify: `docs/dev/i18n/source-zh-CN.tsv` + resx (or project’s i18n generate step)
- Modify: `CheckForUpdatesAsync` to call shared orchestration

Dialog: ShowDialog, Owner=MainWindow; Update/Skip or OpenBrowse/Skip; progress; Cancel mid-download returns to pre-download state (caller handles CTS).

- [ ] Implement dialog + wire App ComposeMainViewModel
- [ ] Manual button uses shared path
- [ ] Smoke: build solution

---

### Task 5: Docs + critical-op tests refresh

**Files:**
- Modify: `README.md` (检查更新 section)
- Modify: `docs/user/国内镜像搭建.md` (browser → client download)
- Modify: `tests/MechabellumModManager.Tests/MainViewModelCriticalOpTests.cs` as needed
- Modify: spec Status → Approved

- [ ] Update docs copy
- [ ] `dotnet test` full suite green

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Downloadable vs BrowseUrl | 1 |
| Downloader + sanitize + https redirect | 2 |
| Shared startup+manual flow, skip, shutdown order | 3 |
| Modal dialog, recovery-after timing, i18n | 4 |
| README + mirror docs | 5 |
| Soft/Hard gates | 3–4 |
| Cancel ≠ skip | 3–4 |
| Close during download | 3 |

## Self-review

- No TBD placeholders
- `UpdateCheckResult.BrowseUrl` named consistently
- Plan has **0 Commit steps** — working tree only
