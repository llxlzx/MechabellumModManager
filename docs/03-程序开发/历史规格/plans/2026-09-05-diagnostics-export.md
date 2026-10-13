# Diagnostics Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist manager logs daily and let testers export a diagnostic zip (optional strong redaction) then save / open folder / mailto.

**Architecture:** `ManagerLogWriter` appends to `Paths.LogsDir`; `DiagnosticsExportService` builds the zip read-only; a small dialog chooses redaction; `MainViewModel` orchestrates SaveFileDialog + folder + mailto.

**Tech Stack:** WPF (.NET 8), CommunityToolkit.Mvvm, xUnit + FluentAssertions, System.IO.Compression

**Spec:** `docs/superpowers/specs/2026-09-05-diagnostics-export-design.md`

## Global Constraints

- Logs follow `Paths.DataRoot` / `LogsDir` (portable-safe); never hardcode AppData for manager logs
- Export must not mutate branch-switch / deploy / Melon install state
- Do not pack `library/`, Mod DLLs, previews, or installer redist
- `mailto:` cannot attach files — README + body must say attach manually
- Crash log: search both `DataRoot/crash.log` and `%AppData%\MechabellumModManager\crash.log`
- Strong redaction applies only to zip contents, never rewrite live config files
- Retention: keep at most 14 days AND at most 7 `manager-*.log` files

---

### Task 1: ManagerLogWriter

**Files:**
- Create: `src/MechabellumModManager/Services/ManagerLogWriter.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`AppendLog`, ctor inject optional writer)
- Test: `tests/MechabellumModManager.Tests/ManagerLogWriterTests.cs`

**Interfaces:**
- Produces: `ManagerLogWriter(string logsDir, int retainDays = 14, int maxFiles = 7)` with `void Append(string line)`, `void PurgeOldFiles()`, `IReadOnlyList<string> ListRecentLogFiles()`

- [ ] **Step 1:** Write failing tests for Append creates daily file, Purge respects retention, concurrent read while append
- [ ] **Step 2:** Implement `ManagerLogWriter` with lock + `FileShare.ReadWrite`
- [ ] **Step 3:** Wire into `AppendLog`; call `PurgeOldFiles` once at startup after `EnsureCreated`
- [ ] **Step 4:** Run tests; self-check (portable LogsDir, append failure does not throw)
- [ ] **Step 5:** Commit

---

### Task 2: DiagnosticsExportService + redaction

**Files:**
- Create: `src/MechabellumModManager/Services/DiagnosticsExportService.cs`
- Create: `src/MechabellumModManager/Models/DiagnosticsRedactionMode.cs` (enum None / Strong)
- Test: `tests/MechabellumModManager.Tests/DiagnosticsExportServiceTests.cs`

**Interfaces:**
- Consumes: `PathsService`, game path, session log text, app version, optional status/branch summary
- Produces: `DiagnosticsExportResult { bool Success; string? ZipPath; string Message; IReadOnlyList<string> Missing }`
- `DiagnosticsExportService.ExportToFile(string zipPath, DiagnosticsExportRequest request)`

- [ ] **Step 1:** Failing tests — missing Melon log still succeeds; strong redacts `Users\Name`; environment has redaction field
- [ ] **Step 2:** Implement collect → redact → zip
- [ ] **Step 3:** Run tests; self-check (no write to config paths; dual crash lookup)
- [ ] **Step 4:** Commit

---

### Task 3: Dialog + VM command + UI + i18n

**Files:**
- Create: `Dialogs/ExportDiagnosticsDialog.xaml` (+ `.cs`)
- Modify: `App.xaml.cs` (prompt + save zip + open folder + mailto helpers)
- Modify: `MainViewModel.cs` (`ExportDiagnosticsCommand`)
- Modify: `MainWindow.xaml` (settings + log panel buttons)
- Modify: `UiStrings.cs` + `Strings*.resx` (zh/en/de/ja/ru)

- [ ] **Step 1:** Dialog returns redaction mode or cancel
- [ ] **Step 2:** Wire command: dialog → export temp/save → open folder → mailto → AppendLog
- [ ] **Step 3:** Localize strings
- [ ] **Step 4:** Build + targeted tests; self-check (cancel paths, mailto failure still opens folder)
- [ ] **Step 5:** Commit

---

### Task 4: Full suite + spot self-review

- [ ] **Step 1:** `dotnet test -c Release`
- [ ] **Step 2:** Spec checklist walkthrough (DataRoot, crash dual path, no library pack)
- [ ] **Step 3:** Commit any fixes
