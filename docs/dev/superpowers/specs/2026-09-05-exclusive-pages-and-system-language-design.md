# Exclusive Content Pages + Follow-System Language Fix

**Date:** 2026-09-05  
**Status:** Approved in chat (user chose approach A); self-checked 2026-09-05 before implementation  
**Product:** Mechabellum Mod Manager

## Problem

1. **Stacked panels:** Settings strip and Mod Catalog can expand while the Mod Library stays visible underneath. Catalog is capped (`MaxHeight="420"`), so browse feels information-poor on a maximized window.
2. **Follow system language:** After switching from「跟随系统」to an explicit language (e.g. 简体中文 / English), selecting「跟随系统」again often appears to do nothing—dropdown may snap back, and when the OS language already matches the previous explicit choice the UI text does not change, so users get no feedback.

## Goals

- Top chrome (brand, status, profile row, **启动游戏 / Mod 浏览 / 设置**) stays visible.
- Below that chrome, **exactly one** content page fills the remaining space (above risk banner + log): **本地库** | **Mod 浏览** | **设置**.
- Pages are mutually exclusive; no stacking.
- Catalog/settings use the full content height (remove catalog max-height clamp).
- Selecting「跟随系统」always persists `UiLanguage = "system"`, keeps the ComboBox on the system option, and shows a short notice when the resolved culture equals the previous explicit culture.

## Non-goals

- Redesigning visual theme, icons, or log/risk banner placement.
- Hiding the top navigation bar (rejected option B).
- Adding extra pages beyond Library / Catalog / Settings.
- Changing installer language seeding.

## Design

### 1. Navigation model

Introduce a single page enum on `MainViewModel`:

```csharp
public enum MainContentPage
{
    Library = 0,
    Catalog = 1,
    Settings = 2,
}
```

Property: `ActiveContentPage` (default `Library`).

| Top control | Behavior |
|-------------|----------|
| **本地库** (new nav button) | `ActiveContentPage = Library`. |
| **Mod 浏览** | `ActiveContentPage = Catalog`. If catalog empty, refresh (same as today’s first-open). Not a toggle. |
| **设置** | `ActiveContentPage = Settings`. Not a toggle. |
| **启动游戏** | Unchanged primary action; not a page. |

**Locked decision (self-check):** Exactly **three** radio-style nav buttons—本地库 / Mod 浏览 / 设置—plus 启动游戏. No “optional or” paths.

Active page button uses Accent style; inactive Ghost (`DataTrigger` on `IsLibraryPage` / `IsCatalogPage` / `IsSettingsPage`, same idea as `UseApplyAccent`).

Deprecate stacked toggles:

- Remove `CatalogExpanded` / `SettingsExpanded` (or keep as obsolete wrappers that map to page enum only if tests need a short migration).
- Derived bools for XAML: `IsLibraryPage` / `IsCatalogPage` / `IsSettingsPage`.
- Remove expand/collapse chevrons and the **duplicate** catalog strip header that only existed to collapse the stacked panel.
- Drop `CatalogToggleLabel` / ExpandBrowse / CollapseBrowse from top nav (strings may remain unused).

### 2. Layout

Current main grid rows (simplified):

0. Header (always)  
1. Settings strip (optional stack)  
2. Catalog strip (optional stack, body max-height 420)  
3. Library (`*`)  
4–6. Risk / extras / log  

Target:

0. Header (always)  
1. **Content host (`*`)** — single child visible: Library **or** Catalog **or** Settings  
2–n. Risk banner + log (unchanged)

- Settings panel moves into the content host as a full-page form wrapped in **`ScrollViewer`** (path, branch, language, credits, dual-store controls—same fields as today). Language ComboBox stays on Settings only (already true today).
- Catalog panel moves into the content host; **remove `MaxHeight="420"`**; list + detail share remaining height (`*` row). Remove the secondary “收起浏览” chrome row.
- Library keeps profiles | library split, now with full content height when active.
- Header profile row + 启动游戏 remain on all pages so deploy/launch does not require returning to Library first.

### 3. Follow-system language

Root causes to address:

1. ComboBox selection can fail to stick on `system` when option `Label` is mutated during apply without `INotifyPropertyChanged`.
2. When resolved culture equals the previous explicit culture, UI strings do not change → looks like a no-op.

Fixes:

1. Make `LanguageOption` raise change for `Label`, **or** avoid mutating label during the same selection commit (defer label refresh via `Dispatcher.BeginInvoke`), **and** bind robustly (`SelectedValue` + `SelectedValuePath="Code"` kept; ensure after apply `SelectedUiLanguageCode` remains `"system"`).
2. Persist `config.UiLanguage = "system"` whenever system is chosen.
3. If previous configured code was not `"system"` and the **resolved** culture equals the culture that was active before apply, show non-modal feedback: **`AppendLog` + optional status line** (do **not** use modal `_notify`—existing `_notify` opens a blocking dialog and is too heavy for “already Chinese”). Example:「已跟随系统语言（简体中文）」.

4. Defer `LanguageOption.Label` update with `Dispatcher.BeginInvoke` **or** implement `INotifyPropertyChanged` on `Label` so ComboBox does not lose `SelectedValue=system` mid-commit.

Tests (unit):

- `en` → `system` ⇒ `SelectedUiLanguageCode == "system"`, config saved as `system`.
- `zh-CN` → `system` when system resolves to `zh-CN` ⇒ still `"system"` in VM/config; log/status feedback recorded (spy `AppendLog` via captured log property or a small test hook).
- `ShowLibraryPage` / `ShowCatalogPage` / `ShowSettingsPage` set `ActiveContentPage` exclusively.

### 4. Localization / strings

- Nav: ensure labels for Library / Browse / Settings exist (reuse `ModLibrary` / `BrowseMods` / `Settings` where possible).
- Add notify string keys for follow-system feedback.
- Drop or stop using Expand/Collapse browse labels on the top nav.

## Success criteria

- [ ] Only one of Library / Catalog / Settings content visible at a time.
- [ ] Catalog list uses full content area height (no 420px cap).
- [ ] Top bar remains; 启动游戏 still works from any page.
- [ ] Switching to「跟随系统」leaves dropdown on 跟随系统 and writes `system` to config.
- [ ] When system culture matches prior explicit language, user sees an explicit notice.
- [ ] Existing unit tests updated; new page + language tests pass.

## Out of scope follow-ups

- Persisting last `ActiveContentPage` across restarts (default Library is enough for v1).
- Animated page transitions.

## Self-check notes (2026-09-05)

| Finding | Resolution |
|---------|------------|
| Spec had “or” ambiguity for returning to Library | Locked: third nav button **本地库** |
| Modal `_notify` for follow-system would interrupt UX | Use `AppendLog` (+ status), not modal |
| Catalog has a second collapse header strip | Remove with exclusive page; avoid double chrome |
| Tall Settings form | Require `ScrollViewer` |
| Apply/Launch live in header | Keep header on all pages |
| No existing tests bind `CatalogExpanded` | Safe to replace with page enum |
| `MaxHeight="420"` confirmed on catalog `DockPanel` | Must delete |
| Language control lives only in Settings | Acceptable; no move to header |
