# Design: UI icons, catalog multi-select, apply feedback, installer 5 languages (v1.0.9)

**Status:** Draft for user review (self-checked 2026-09-05)  
**Version target:** 1.0.9 (fuse with existing installer language-dialog work)  
**Date:** 2026-09-05

## Problem

1. Catalog (Mod 浏览) detail strip lacks **收起介绍** / **举报** (library detail already has both).
2. **加入本地库** is single-select only; multi-select was expected.
3. Browse collapse control uses an opaque `▾`/`-` glyph; first-time users do not understand it.
4. Primary actions are text-only; users expect familiar icons (gear, magnifier, etc.).
5. **应用方案** stays orange and appears clickable when only library rows are multi-selected (`LibrarySelectionCount > 0`) even if `IsDirty` is false; click redeploys an already-synced profile with little visible feedback.
6. Installer language dialog currently offers only 简体中文 / English; the app supports five UI languages and should align; chosen installer language should seed `UiLanguage`.

## Goals (v1.0.9)

- Fix apply enablement + visual state + click feedback.
- Catalog multi-select: Extended selection **and** checkbox column; batch add to library.
- Catalog detail: collapse intro + report parity with library.
- Replace opaque collapse glyph with labeled toggle (icon + text).
- Add lightweight Path-geometry icons next to key actions/search (no third-party icon pack).
- Installer: five languages matching the app; persist selection into manager `config.json` `UiLanguage`.

## Non-goals

- Multi-select does **not** drive Apply (no “apply only selected mods” temporary profile).
- No full redesign of layout density / theme.
- No new languages beyond zh-CN / en / ru / ja / de.
- No change to MelonLoader / dual-folder / Steam settle logic except as already in 1.0.9 packaging.

## Decisions (approved)

| Topic | Choice |
|-------|--------|
| Overall approach | Scheme 1: minimal fix + clear feedback + Path icons |
| Catalog multi-select | A+B: Ctrl/Shift Extended **and** row checkboxes |
| Apply enablement | `CanDeployOrLaunch && IsDirty` only |
| Apply look | Ghost when not dirty; Accent when dirty |
| Installer langs | zh-CN, en, ru, ja, de |
| Installer → app | Write selected language to `UiLanguage` |

---

## 1. Apply scheme behavior and appearance

### Enablement

```text
CanApplyProfile := CanDeployOrLaunch && IsDirty
```

Remove `LibrarySelectionCount > 0` from this predicate. `LibrarySelectionCount` remains for other UX (selection hint) but must not orange-enable Apply.

### Visual

- **Must** switch style by dirty state, not rely on `IsEnabled` alone.
  - Current `AccentButtonStyle` when disabled only sets `Opacity=0.4` and **still looks amber** — that matches the report “颜色始终保持橙色”.
  - **Not dirty OR not ready:** `GhostButtonStyle` (and disabled when `!CanApplyProfile`).
  - **Dirty and ready:** `AccentButtonStyle`.
- Prefer a single enablement source: either Command `CanExecute` **or** `IsEnabled` binding, not both fighting different predicates. Today both use `CanApplyProfile` — keep them identical after the predicate fix.

### Feedback

On every Apply click path:

- Always append a clear sync-log line (success, cancelled overwrite, Steam settle block, not ready). Successful deploy already logs `DeployService`’s success message; also call `_notify` on success so feedback is not log-only.
- After enablement fix, “click while not dirty” should be unreachable (button disabled).

### Files

- `ViewModels/MainViewModel.cs` — `CanApplyProfile`, apply feedback
- `MainWindow.xaml` — Apply button style binding
- Optional small strings in `Strings*.resx` / `UiStrings` for success toast

---

## 2. Catalog multi-select and detail actions

### Multi-select + checkboxes

- Catalog `DataGrid`: `SelectionMode="Extended"`.
- Add checkbox column (selection only — distinct from library package `IsEnabled`).
- **Sync rule (mandatory):** one authority path — `SelectionChanged` updates VM selected-id set / `CatalogSelectionCount`; checkbox toggles must add/remove from `DataGrid.SelectedItems` (or set selection) so they cannot drift from row highlight.
- `SelectedItem` / `SelectedCatalogMod` remains the “primary” row for the detail strip and Report (WPF Extended mode still has one current item). Batch add uses **all** `SelectedItems`, not only `SelectedCatalogMod`.
- On catalog refresh/rebuild of `CatalogMods`, clear selection state (new VM instances would otherwise orphan checkbox flags).

### 加入本地库

- `AddCatalogModToLibraryAsync` loops all selected catalog mods (not only `SelectedCatalogMod`).
- Skip already-in-library; log each skip/add.
- `CanAddCatalogMod`: at least one selected item that is not already in library (or allow click with all-skip and log — prefer disable when nothing actionable).

### Detail strip

Mirror library detail chrome:

- **举报** → existing `ReportCatalogModCommand` / `ReportCatalogModAsync` (ensure CanExecute when selection exists).
- **收起介绍** → clear catalog selection (`SelectedCatalogMod = null` **and** `DataGrid.UnselectAll()` / clear checkbox selection set) so detail collapses; does **not** set `CatalogExpanded = false`.
- Same UnselectAll gap already exists on library `ClearLibraryModSelection` (only nulls `SelectedLibraryMod`); fix library collapse the same way while touching this area, otherwise `LibrarySelectionCount` stays stale after「收起介绍」.

### Files

- `MainWindow.xaml` / `MainWindow.xaml.cs`
- `ViewModels/MainViewModel.cs`
- Strings if selection count hint is added

---

## 3. Browse collapse affordance

- Remove lone `▾` / `-` / `?` glyph-only button.
- Replace with a control showing **icon + text**:
  - Expanded: 「收起浏览」 (+ collapse chevron)
  - Collapsed: 「展开浏览」 (+ expand chevron)
- **Layout constraint (bug if ignored):** today the entire catalog `Border` uses `Visibility="{Binding CatalogExpanded}"`, including the header that hosts the collapse button. Putting the only labeled toggle inside that Border makes「展开浏览」**disappear when collapsed**. Required structure:
  1. Always-visible thin header strip (title + labeled toggle) **outside** the `CatalogExpanded` visibility gate; **or**
  2. Split: outer header always visible; inner body (filters/grid/detail) gated by `CatalogExpanded`.
  Top toolbar「Mod 浏览」remains a second toggle, not the sole expand path.
- Tooltips match the action.
- Keep `ToggleCatalogCommand` / `CatalogExpanded` semantics (refresh catalog on first expand if empty).

### Files

- `MainWindow.xaml`
- `Strings*.resx` / `UiStrings` for CollapseBrowse / ExpandBrowse

---

## 4. Iconography (Path geometries)

No MahApps/Material packs. Add a small `ResourceDictionary` (e.g. `Assets/Icons.xaml` or section in `App.xaml`) with `Geometry` / `Path` resources.

Suggested placements:

| UI | Icon |
|----|------|
| 设置 | gear |
| Search TextBoxes | magnifier (left adorner or docked Path) |
| 刷新 | refresh arrows |
| 加入本地库 | plus / download-to-library |
| 举报 | flag |
| 收起/展开 | chevron pair |
| 应用方案 | check / apply |
| 应用并启动 | play |
| 投稿 Mod | upload |
| 导入 DLL / Zip / 文件夹 | import |
| 新建/重命名/复制/删除方案 | plus / pencil / copy / trash (optional if space allows; at least Delete + New) |

Implementation pattern: `StackPanel` Orientation=Horizontal with `Path` + `TextBlock`, or `Button.Content` template. Keep hit targets ≥32px height where already used.

---

## 5. Installer five languages + seed UiLanguage

### Languages

`[Languages]` entries (Inno official / local isl):

| Inno name | App `UiLanguage` | License |
|-----------|------------------|---------|
| chinesesimplified | zh-CN | `EULA.zh-CN.txt` |
| english | en | `EULA.en.txt` |
| russian | ru | `EULA.ru.txt` |
| japanese | ja | `EULA.ja.txt` |
| german | de | `EULA.de.txt` |

`ShowLanguageDialog=yes`. Extend `[CustomMessages]` for all five (tasks, components, status strings, game-path page). `[Code]` continues to use `CustomMessage` / `FmtMessage`.

EULA for ru/ja/de: full translation preferred; English body acceptable only as temporary fallback if translation slips, but **target** is five EULA files for 1.0.9.

### Seed config

Map Inno `ActiveLanguage` name → app code:

| `ActiveLanguage` | `UiLanguage` |
|------------------|--------------|
| chinesesimplified | zh-CN |
| english | en |
| russian | ru |
| japanese | ja |
| german | de |

Write `"uiLanguage": "<code>"` into `%AppData%\MechabellumModManager\config.json` (camelCase — matches `JsonStore` / `AppConfig.UiLanguage`).

**Critical existing bug:** `WriteManagerConfigNative` currently **overwrites** `config.json` with only `gamePath` / `launchMode` / `activeProfileId` / `dataRoot`. That wipes `uiLanguage` (and any future fields) on every install/repair. Seeding language **requires** read-merge-write:

1. If file exists, parse JSON object (or best-effort keep unknown keys).
2. Set/overwrite `gamePath` (and resolved link behavior as today) and `uiLanguage` from installer selection.
3. Preserve other known keys when present; create defaults only when missing.
4. Do not invent dual-folder keys here if they live in `branch-switch.json` (current design) — but must not destroy unrelated `config.json` keys.

Reinstall choosing a language **may** overwrite `uiLanguage` (installer choice wins for that run). In-app Settings changes after launch remain authoritative until next install seed.

### Files

- `installer/MechabellumModManager.iss` (`WriteManagerConfigNative` merge + language map)
- `installer/EULA.*.txt`
- Optional: align `installer/scripts/Write-ManagerConfig.ps1` if still used as fallback (must not regress merge).

---

## Testing

1. Apply: with clean sync, button Ghost/disabled; toggle a mod enable → Accent + Apply works → log + toast; multi-select alone does not Accent-enable.
2. Catalog: Ctrl/Shift multi-select + checkboxes; batch 加入本地库; skips logged.
3. Catalog detail: 举报 opens dialog; 收起介绍 hides detail without closing browse.
4. Collapse: labeled toggle works both ways; top「Mod 浏览」still toggles.
5. Icons visible at 100%/125% DPI, not clipped.
6. Installer: language dialog lists 5; English/German/etc. wizard + EULA; after install, `config.json` `uiLanguage` matches; app UI matches on first launch.
7. Existing unit tests still pass; add/adjust tests for `CanApplyProfile` if covered.

## Risks

- Checkbox ↔ DataGrid selection sync can desync if only one path updates — centralize in SelectionChanged.
- Accent vs Ghost style switching needs careful XAML so disabled state remains readable.
- Inno `AppName={cm:...}` already warns on VersionInfo*; set `VersionInfoProductName` explicitly if needed.
- Writing `UiLanguage` must not break UTF-8 JSON; native writer must merge, not clobber.
- Pascal JSON merge is fragile (no real JSON parser in Inno) — keep merge minimal (regex/key replace for `uiLanguage` / `gamePath`) or rewrite whole object only after reading known fields into variables.

## Self-check findings (2026-09-05)

| # | Severity | Issue | Spec / fix |
|---|----------|-------|------------|
| 1 | High | Apply stays amber when disabled (`Opacity` only) | Style must switch Ghost↔Accent by dirty, not disable-only |
| 2 | High | `WriteManagerConfigNative` clobbers `config.json` | Mandatory merge before seeding `uiLanguage` |
| 3 | High | Collapse control lives inside `CatalogExpanded` Visibility | Header/toggle must stay outside collapsed body |
| 4 | Med | `ClearLibraryModSelection` does not `UnselectAll` | Fix with catalog clear; count otherwise stale |
| 5 | Med | Batch add vs detail/Report | Batch uses all SelectedItems; detail/Report use primary `SelectedCatalogMod` — document & implement explicitly |
| 6 | Med | `IsCatalogSelected` on row VM without refresh clear | Clear selection on catalog rebuild |
| 7 | Low | Spec said “prefer” for header layout | Upgraded to required structure |
| 8 | Low | Dual `IsEnabled` + Command CanExecute | Keep predicates identical after change |
| 9 | Info | Root cause of “click no feedback” confirmed | `CanApplyProfile` includes `LibrarySelectionCount > 0` while Apply ignores selection |
| 10 | Info | App languages match Inno set | zh-CN/en/ru/ja/de ↔ chinesesimplified/english/russian/japanese/german |

No contradiction found with non-goal “multi-select does not drive Apply.” No missing language vs `LocalizationService.SupportedCultures`.

## Out of scope follow-ups

- Checkbox multi-select for Apply-as-selection-set (rejected for 1.0.9).
- Additional locales.
- Animated collapse.
