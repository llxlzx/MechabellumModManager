# v1.2.8 — Hide Melon console + mirror Advanced expander + deep fix — Design

**Date:** 2026-09-16  
**Status:** Approved for planning (brainstorming complete)  
**Delivery:** Full release (code + Setup/portable + update channel)

## Goal

Ship **1.2.8** that restores two settings UX features lost from WIP (`backup/wip-before-release-cleanup-20260914` / `91532b2`), then deep-scan the manager’s main paths, fix blocking/high-severity defects into the same release, and publish Setup + portable + `latest.json` like 1.2.7.

## Non-goals

- Unrelated large refactors
- Defaulting **Hide Melon console** to on
- Changing the factory mirror base URL unless deep-scan proves it must change
- Blind-merge of the entire WIP backup branch

## Approach (locked)

**Surgical port from WIP onto current `master` (1.2.7 tip)** — not merge the backup branch, not rewrite from memory.

## Feature restore

### A. Hide Melon console

- Settings checkbox **Hide Melon console** (`AppConfig.HideMelonConsole`, default `false`).
- On change: persist config; set `PreferHideConsole` on Melon install/dual-sync paths; call `MelonLoaderConfigOptimizer.SetHideConsole` / `ApplyRecommendedSettings(..., hideConsole:)` for **every active game root** (single path, or both dual-folder stores).
- Writes Melon `UserData\Loader.cfg`: `[console] hide_console = true|false`. Applies on **next game launch**.
- Strings (zh/en/ru/ja/de): `HideMelonConsole`, `HideMelonConsoleTip`, `LogHideMelonConsoleRestartHint`, `LogMelonConsoleHiddenHint`.
- **Launch hint conflict (self-check fix):** when hide is on, do **not** tell the player a black Melon window must appear; use `LogMelonConsoleHiddenHint` and rely on `Latest.log` / injection checks.
- **Installer Setup PS1 (self-check fix):** `packaging/installer/scripts/Install-MelonLoader.ps1` continues to write only `force_quit` / `force_offline_generation`. It must **not** invent a hide preference (no manager config at first Setup). After first manager run with the checkbox on, manager owns `hide_console`. Document this split so Setup does not stomp a later player preference (PS1 must leave existing `hide_console` lines alone if present).

### B. Mirror under Advanced expander

- Replace always-visible mirror `DockPanel` with `Expander` (`IsExpanded="False"` by default).
- Header: `SettingsAdvanced` + `MirrorSummaryText` (on default / custom / off).
- Body: existing mirror TextBox + hint; HTTPS validation and null/empty semantics unchanged (`null` → factory fill, `""` → opt out).

### C. Call-site wiring (self-check)

HEAD overloads are `ApplyRecommendedSettings(gamePath)` and `(gamePath, knownUnityVersion)`. Port must add optional `bool? hideConsole` without breaking callers, and thread preference through:

- `MainViewModel` (load/save + checkbox + launch logs)
- `MelonLoaderInstaller.PreferHideConsole`
- `MelonLoaderDualStoreSync.PreferHideConsole`
- `InstallMelonLoaderCli` (read config when present)
- Tests restored/adapted from WIP `MelonLoaderConfigOptimizerTests` hide cases

## Version & release

| Artifact | Action |
|----------|--------|
| `MechabellumModManager.csproj` Version | `1.2.8` |
| `packaging/installer/MechabellumModManager.iss` `MyAppVersion` | `1.2.8` |
| Setup / portable | Build, SHA256, GitHub Release `v1.2.8` |
| `release/latest.json` (+ mirror channel as for 1.2.7) | Point URLs/sha/notes/build at 1.2.8 |

Release notes: hide console, Advanced mirror, plus any blocking/high fixes landed in deep-scan.

## Deep scan & fix gate

### Coverage

Static review + full unit tests; add targeted tests when a defect is confirmed:

1. Startup / game detect / Melon ready / portable data root  
2. Catalog / library / update detection / stale duplicate update  
3. Apply & launch / Steam stale-running / Il2Cpp then ExeOnly / injection checks vs hide console  
4. Dual-branch wizard / settle / emergency recover  
5. Mirror HTTPS / default COS / fetch fallback  
6. Melon install & redist half-install guards / Loader.cfg coexistence with `hide_console`

### Severity gate (into 1.2.8)

| Severity | Action |
|----------|--------|
| Crash, wrong result, main-path hang/no-op, data loss, security | **Must fix** in 1.2.8 |
| Small, tested, no schedule slip | May fix in 1.2.8 |
| Large refactor / unclear product / edge-only UX | Report only; later version |

### Deliverable

Short defect list (severity + fixed-in-1.2.8?) + full test run evidence before claiming release-ready.

## Risks & mitigations (from plan self-check)

1. **WIP vs HEAD drift** — MainViewModel/XAML/services diverged heavily; port by behavior and file hunks, never whole-branch merge.  
2. **Hide vs “must see Melon console” copy** — gate launch strings on `HideMelonConsole`.  
3. **Setup vs manager cfg ownership** — Setup does not set hide; must not strip existing `hide_console`.  
4. **Dual-folder roots** — preference write must hit all enumerated game roots.  
5. **Dirty tree** — uncommitted `packaging/installer/redist/manifest.json` and untracked local files must not ride into feature commits; isolate release work on a feature branch.  
6. **Deep-scan scope creep** — enforce severity gate; do not block release on report-only items.

## Success criteria

- [ ] Settings: Hide Melon console toggles `Loader.cfg` on next launch; dual-folder both roots updated when applicable  
- [ ] Mirror field collapsed under Advanced by default; summary accurate  
- [ ] With hide on, launch UI/logs do not demand a visible black console  
- [ ] Full test suite green  
- [ ] Deep-scan report produced; must-fix items closed or release blocked  
- [ ] GitHub `v1.2.8` Setup + portable + sha; `latest.json` (and COS channel) live  

## Implementation next

After user confirms this written spec: **writing-plans** → task plan under `docs/dev/superpowers/plans/`, then execute on a non-`main` working branch with TDD for the restore and each must-fix.
