# v1.2.8 deep-scan report

**Date:** 2026-09-16  
**Branch:** `feature/v1.2.8-hide-console-mirror-deep-fix`  
**HEAD at scan:** `4eca8f54bbc40c4f2a5ed25970986dcb13409142`  
**Scope:** Static logic review of must-fix paths from  
`2026-09-16-v128-hide-console-mirror-expand-and-deep-fix-design.md`  
**Tests:** Full suite already green — **781 passed, 9 skipped, 0 failed**.  
This scan did **not** re-run the full suite.  
**Note:** Environmental Melon/CLI install checks may be skipped while the game process is running (installer/CLI exit when `Mechabellum` is active).

## Severity gate applied

| Severity | Action |
|----------|--------|
| Crash, wrong result, main-path hang/no-op, data loss, security | Must fix in 1.2.8 |
| Small + tested | May fix |
| Large / unclear / edge UX | Report only |

## Verdict

**No must-fix defects found.** Prior blocking fixes (Steam stale-running, stale duplicate Update) remain coherent with call sites and tests. Hide-console + Advanced mirror restore matches the design.

---

## 1. Hide Melon console path

**Reviewed:** `MainViewModel.ApplyHideMelonConsolePreference` / `EnumerateHideConsoleTargets`, launch hint branching, `PreferHideConsole` on install/dual-sync/CLI, `MelonLoaderConfigOptimizer.SetHideConsole` / `ApplyRecommendedSettings`.

| Finding | Severity | Notes |
|---------|----------|-------|
| Toggle persists config, sets `_melonDualSync.PreferHideConsole`, writes all enumerated game roots (active path + dual stores when enabled) | OK | `HashSet` + `LooksLikeGameRoot` dedupes junction/store overlap |
| Launch uses `LogMelonConsoleHiddenHint` / `LogMelonLaunchVerifyHiddenHint` when hide on; visible-console strings when off | OK | Spec self-check satisfied for Apply & launch |
| Install UI, dual Ensure, CLI thread `PreferHideConsole` / `LoadHideConsolePreference` | OK | CLI returns `null` when no config (Setup does not invent hide) |
| Load sets backing field only (no SetHideConsole on startup) | Report-only | Preference still applied on install/optimize/Apply; edge if Setup rewrote cfg without hide until next Apply |
| `LogFirstAssemblyLaunchHint` / `UpdateFirstAssemblyWarning` still mention console scroll when hide is on | Report-only | UX copy conflict only; injection still uses Latest.log |

**Must-fix:** none.

## 2. Mirror expander / MirrorSummaryText / ApplyMirrorBaseUrl HTTPS

**Reviewed:** Settings `Expander` (`IsExpanded="False"`), `RefreshMirrorSummary`, `ApplyMirrorBaseUrl` + `ExternalUrlPolicy.IsHttpsUrl`.

| Finding | Severity | Notes |
|---------|----------|-------|
| Default collapsed Advanced; summary Off / OnDefault / Custom | OK | |
| `null` → factory COS fill; `""` → opt-out persisted; non-HTTPS refused → persist `""`, clear active mirror | OK | Security posture intact |
| LostFocus binding + suppress loop on refuse | OK | |

**Must-fix:** none.

## 3. Apply & launch / Steam stale-running

**Reviewed:** `IsSteamLaunchBlockedByStaleFlag`, `SteamAppRunningState`, `VerifyLaunchProcessThenInjectionAsync`, tests in `MainViewModelTests` / `SteamAppRunningStateTests`.

| Finding | Severity | Notes |
|---------|----------|-------|
| SteamOnly / SteamThenExe blocked when registry Running / RunningAppID set and process absent | OK | Pre-launch return + post-launch not-seen reclassify |
| ExeOnly unaffected | OK | |
| Assembly-gen one-shot ExeOnly `AppConfig` clone omits unrelated fields | OK | `Launch` only needs path + mode; `SaveConfig` uses full loaded config |

**Must-fix:** none.

## 4. Catalog update / stale duplicate update

**Reviewed:** `ModCatalogService.IsStaleDuplicate`, `ModItemViewModel.ApplyCatalogEnrichment`, `UpdateModAsync` / `RetireStaleDuplicate`, detection tests.

| Finding | Severity | Notes |
|---------|----------|-------|
| Stale duplicate: `HasUpdate=false`, `IsStaleDuplicate=true`, Update retires old row instead of catalog no-op | OK | |
| True update still downloads via `AddOneCatalogModAsync` + game-running gate | OK | |
| Retire aborts safely if no UpToDate twin found | OK | Log only; no wrong delete |

**Must-fix:** none.  
**May-fix (optional):** no dedicated VM flow test for `UpdateModAsync` → `RetireStaleDuplicate` (detection covered; flow coverage thinner).

## 5. Dual-branch wizard gates

**Reviewed:** `IsEnableDualWaitingDownload`, `CanConfirmWizardExitSteam` / disk vs process ready, `CanConfirmManualBeta`, `CanSwitchGameBranch`, `IsSessionLocked`, `BranchStoreHealth`.

| Finding | Severity | Notes |
|---------|----------|-------|
| WaitingDownloadB: disk-ready shows exit-Steam confirm; auto-continue requires Steam/game not running | OK | Split disk vs full ready is intentional |
| Switch disabled while settle + Ready (confirm path owns promote) | OK | |
| Session lock gates Apply/Launch without blanking whole UI | OK | |

**Must-fix:** none.

## 6. Melon install / redist half-install

**Reviewed:** `RedistEnsureService` `.partial-*` + sha gate, `InstallMelonLoaderCli.ExitSeedIncomplete`, installer/dual-sync Success vs seed warning.

| Finding | Severity | Notes |
|---------|----------|-------|
| Redist downloads to temp; hash mismatch deletes temp; atomic move | OK | Partial files not promoted |
| CLI returns exit **6** when Melon written but seed incomplete | OK | Setup can treat as incomplete |
| UI zip/GitHub install can report Success with seed **warning** | Report-only | Player still warned in log; not silent success without message |
| Detect-fail after zip write does not rollback payload (pre-existing) | Report-only | Leaves Loader on disk for repair; not a new 1.2.8 regression |

**Must-fix:** none.

## 7. Loader.cfg / Setup PS1 hide_console coexistence

**Reviewed:** `packaging/installer/scripts/Install-MelonLoader.ps1` `Apply-LoaderCfgOptimizations` + comment (103e699).

| Finding | Severity | Notes |
|---------|----------|-------|
| PS1 only rewrites `force_quit` / `force_offline_generation`; comment documents never invent/strip `hide_console` | OK | Matches design ownership split |
| Manager `hideConsole: null` leaves existing hide keys | OK | Covered by optimizer tests |

**Must-fix:** none.

---

## Fixed-in-1.2.8 (pre-scan, still valid)

| Item | Commit | Status |
|------|--------|--------|
| Steam stale Running blocks steam:// launch | `35a465c` | Still correct |
| Stale library duplicate Update retires instead of no-op | `31eb308` | Still correct |
| Hide console + Advanced mirror restore | `aa7ce69`…`1d2b9f4` | Matches design |

## Release readiness (deep-scan slice)

- Must-fix backlog for this scan: **empty**
- Full unit evidence: **781 passed / 9 skipped / 0 failed** (prior run; not re-executed here)
- Remaining release work is packaging / channel (outside this report’s code gate)
