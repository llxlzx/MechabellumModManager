# Exclusive Pages + System Language Fix — Implementation Plan

> **For agentic workers:** Execute task-by-task with TDD. Do **not** commit unless the user asks.

**Goal:** Mutually exclusive full-height content pages (Library / Catalog / Settings) under a persistent top bar, plus a reliable「跟随系统」language switch with non-modal feedback.

**Architecture:** Replace `CatalogExpanded`/`SettingsExpanded` with `MainContentPage ActiveContentPage` and derived `Is*Page` bools. Restructure `MainWindow.xaml` so one content host (`*`) shows exactly one page. Fix language apply so ComboBox keeps `system` and logs feedback when culture is unchanged.

**Tech Stack:** WPF (.NET 8), CommunityToolkit.Mvvm, xUnit, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-09-05-exclusive-pages-and-system-language-design.md`

## Global Constraints

- Top chrome always visible; content pages mutually exclusive
- No modal for follow-system feedback (AppendLog only)
- Remove catalog `MaxHeight="420"`
- Settings page uses ScrollViewer
- Three nav buttons: 本地库 / Mod 浏览 / 设置 + 启动游戏
- Do not commit unless user requests

## File map

| File | Role |
|------|------|
| `src/.../Models/MainContentPage.cs` | Enum |
| `src/.../ViewModels/LanguageOption.cs` | INPC for Label |
| `src/.../ViewModels/MainViewModel.cs` | Page nav + language apply |
| `src/.../ViewModels/UiStrings.cs` | New string props if needed |
| `src/.../Resources/Strings*.resx` | `NotifyFollowedSystemLanguage` |
| `src/.../MainWindow.xaml` | Layout + nav styles |
| `tests/.../MainContentPageTests.cs` | Page exclusivity |
| `tests/.../UiLanguageSwitchTests.cs` | system language |

---

### Task 1: Page enum + VM navigation (TDD)

**Files:**
- Create: `src/MechabellumModManager/Models/MainContentPage.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Test: `tests/MechabellumModManager.Tests/MainContentPageTests.cs`

- [ ] **Step 1:** Failing tests — `ShowCatalogPage` sets Catalog and clears library/settings flags; `ShowSettingsPage` / `ShowLibraryPage` likewise; opening Catalog with empty mods still triggers refresh path (call `ShowCatalogPage` and assert page).

- [ ] **Step 2:** Add enum + `ActiveContentPage`, `IsLibraryPage`/`IsCatalogPage`/`IsSettingsPage`, commands `ShowLibraryPage`/`ShowCatalogPage`/`ShowSettingsPage`. Map old `ToggleCatalog`/`ToggleSettings` to show pages (or replace commands). Remove expand toggle behavior.

- [ ] **Step 3:** Tests pass.

---

### Task 2: Follow-system language (TDD)

**Files:**
- Modify: `LanguageOption.cs`, `MainViewModel.ApplyUiLanguage`, `Strings*.resx`, `UiStrings.cs`
- Test: `tests/MechabellumModManager.Tests/UiLanguageSwitchTests.cs`

- [ ] **Step 1:** Failing tests — after `SelectedUiLanguageCode = "en"` then `"system"`, assert code stays `"system"` and config `UiLanguage == "system"`. When switching `zh-CN`→`system` with system resolving to zh-CN, assert log contains follow-system message.

- [ ] **Step 2:** Implement INPC on `LanguageOption.Label`; in `ApplyUiLanguage` capture previous configured + previous resolved; after apply if configured is system and previous configured was not system and resolved unchanged, `AppendLog(T("NotifyFollowedSystemLanguage", displayName))`. Defer label update via BeginInvoke when Dispatcher available.

- [ ] **Step 3:** Tests pass.

---

### Task 3: MainWindow exclusive layout

**Files:**
- Modify: `src/MechabellumModManager/MainWindow.xaml`

- [ ] **Step 1:** Collapse rows to Header | Content(`*`) | Risk/Log. Put Library, Catalog, Settings as siblings in content host with `Visibility` bound to `Is*Page`.

- [ ] **Step 2:** Add **本地库** nav button; restyle Browse/Settings/Library with Accent when active. Remove chevron collapse and catalog secondary collapse strip. Remove `MaxHeight="420"`. Wrap Settings in ScrollViewer.

- [ ] **Step 3:** Build + smoke: `dotnet build` and `dotnet test` full suite.

---

### Task 4: Cleanup + verify

- [ ] Remove dead `CatalogToggleLabel` usages; keep unused resx keys if harmless.
- [ ] Publish/copy hotfix to install-test folder if present.
- [ ] Full test suite green.
