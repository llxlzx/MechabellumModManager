# AppendLog i18n batch — Implementation Plan

> **For agentic workers:** Steps use checkbox syntax. Working branch `fix/p0-manager-chrome-i18n`. **No Commit steps.**

**Goal:** Replace all remaining hardcoded Chinese (CJK) `AppendLog` strings in `MainViewModel.cs` with `LocalizationService.T` / `string.Format(LocalizationService.T(...), ...)` across zh-CN / en / ru / ja / de.

**Architecture:** Prefer reusing existing `Log*` / `Notify*` keys when Chinese text already matches; add missing keys via `tools/add-resource-key.ps1` (insert after closing `</data>` of anchor). Interpolated strings use `{0}`, `{1}` placeholders.

**Tech Stack:** .NET resx, `LocalizationService`, xUnit ResourceKeyParity / UiLanguageSwitch / LocalizationService tests.

---

## Global Constraints

- Cultures: `zh-CN` (`Strings.resx`), `en`, `ru`, `ja`, `de`
- Do not insert into the middle of multi-line `<data>` blocks
- Exception `.Message` may remain concatenated after a localized template
- Do not commit unless the user asks

---

### Task 1: Inventory

**Files:** `src/MechabellumModManager/ViewModels/MainViewModel.cs`, optional `tools/_tmp_list_appendlog.py`

- [x] **Step 1:** List every `AppendLog` line containing CJK (~79 literal + unicode-escaped import logs)
- [x] **Step 2:** Map each to a key name (`LogXxx` / reuse `NotifyDeployFailed`, `NotifyBranchWizardFailed`, `DialogYes`/`DialogNo`, `ColumnEnabled`)

---

### Task 2: Add missing keys

**Files:** all five `Strings*.resx`

- [x] **Step 1:** Add new keys via `tools/add-resource-key.ps1` (chained after `LogMirrorMustBeHttps`)
- [x] **Step 2:** Include import DLL/folder/Zip logs that used `\uXXXX` escapes in source

---

### Task 3: Replace call sites

**Files:** `MainViewModel.cs`

- [x] **Step 1:** Replace each hardcoded `AppendLog` with `LocalizationService.T` / `string.Format(...)`
- [x] **Step 2:** Confirm zero `AppendLog` lines still contain CJK (templates localized; `ex.Message` OK)

---

### Task 4: Verify

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ResourceKeyParityTests|FullyQualifiedName~UiLanguageSwitchTests|FullyQualifiedName~LocalizationServiceTests"
```

- [x] **Step 1:** Run the filter above — PASS (45 tests)
- [x] **Step 2:** Delete temporary scan/batch scripts
