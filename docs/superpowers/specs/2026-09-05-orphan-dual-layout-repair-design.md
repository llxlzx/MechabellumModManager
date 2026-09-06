# Orphan dual-layout repair — Design

Date: 2026-09-05  
Status: approved (choices A / A / C) + logic self-review before implementation

## Problem

Dual-folder may be **disabled in config** (`enabled=false`, store paths cleared) while the filesystem still has:

1. `Mechabellum` as a **junction** → `Mechabellum_official` or `Mechabellum_beta`, and/or  
2. Leftover `Mechabellum_official` / `Mechabellum_beta` folders  

Existing **解除双服** is gated on `BranchSwitchEnabled || wizard in progress`, so orphans cannot be repaired. Starting the wizard then fails with「另一服的独立目录已存在」.

## Goals

- UI: settings dual-branch row — button **「恢复为单目录」**, enabled only when orphan detected.
- Confirm → optional second confirm to delete the other store (default **No**).
- Safety: refuse if Steam/game running; validate game root after repair.
- Cover both: junction materialize, and real-`Mechabellum` + leftover cleanup.

## Non-goals

- Installer auto-rewrite of disks (Setup already prefers / normalizes paths).
- Changing ACF / BetaKey.
- Showing this button while dual-folder is **enabled** (use teardown).

## Detection (`HasOrphanDualLayout`)

Preconditions: `!BranchSwitchEnabled`, wizard not mid-flight (`None` or treat in-progress as teardown territory).

Resolve candidate link path: `GamePath` if under `…\common\Mechabellum*` / sibling layout, else `branch-switch.steamLinkPath`, else sibling `Mechabellum` under common of any leftover store.

Orphan if **any**:

| Case | Predicate |
|------|-----------|
| J | Link path exists and is a junction |
| L | Under the same `common`, any of `_official` / `_beta` **exists and LooksLikeGameRoot** |

Empty placeholder folders do **not** count as L (avoid false positives).

## Approaches (chosen)

**Reuse teardown mechanics with disk-aware path seeding** (not a fully separate mover).  
New entry: `BranchSwitchService.TryRepairOrphanDualLayout(bool deleteOtherStore)` that:

1. Resolves link + stores from **disk** (junction target + `SteamBranchLayout` siblings), not only JSON.  
2. Seeds/repairs config paths temporarily as needed.  
3. Reuses the same move/junction/delete/rollback patterns as `TryTeardown` (shared private helper preferred if duplication is large).

## UX flow

1. Button visible/enabled when `CanRepairOrphanDualLayout`.  
2. Confirm: explain will materialize `Mechabellum` if junction; list leftover store paths; warn that **keeping** the other store means wizard may still be blocked.  
3. If another game-like store exists besides the one being kept as link: ask delete (default No).  
4. Busy gate + Steam/game check.  
5. Success → refresh status, log, notify; `GamePath` = materialized `Mechabellum`.

## Safety checks

- `_probe.IsGameOrSteamRunning()` → fail (localized).  
- After materialize: `LooksLikeGameRoot(link)` or rollback.  
- Never delete the store that was just moved into `Mechabellum`.  
- Never `Directory.Delete` the link path.  
- If link is already a real directory and user declines delete-other: success no-op (or success with “nothing to do” if no leftovers) — button should not enable if neither J nor L.

## Self-review — bug risks & mitigations

| # | Risk | Mitigation |
|---|------|------------|
| 1 | Reuse `TryTeardown` with **empty** store paths → `OtherStorePath` returns null → confirmed delete does nothing | Disk-enumerate other stores; do not rely solely on JSON `officialStorePath`/`betaStorePath` |
| 2 | `SteamLinkPath` empty → teardown fails early | Resolve link from `GamePath` / sibling before repair |
| 3 | Junction → official, but `ActiveBranch` says Beta → wrong “current” | **Junction target is source of truth** for which store to materialize |
| 4 | Both stores exist; user keeps other → wizard still blocked | Confirm text must warn; optional second delete remains |
| 5 | Real `Mechabellum` + leftovers; calling teardown tries move and hits “link already exists” | Branch: if `!IsJunction(link)` skip unlink/move; only optional delete leftovers + clear config |
| 6 | After `DeleteJunction`, move fails mid-way → hollow Steam path | Keep teardown-style rollback (recreate junction to store) |
| 7 | Deleting “other” while it is still junction target | Materialize first; only then delete other; never delete currentStore |
| 8 | False orphan: empty `_official` dir | L requires `LooksLikeGameRoot` |
| 9 | Button shown while `enabled=true` | Gate on `!BranchSwitchEnabled` |
| 10 | Wizard in progress residue | Prefer existing teardown; repair button only when wizard step is None (or Ready-cleared / disabled) |
| 11 | Probe `officialLooksLikeGameRoot=false` when paths cleared | Diagnostics: when enumerating leftovers, also set finding `orphan_dual_layout` if J\|\|L under disabled — optional but cheap |
| 12 | Concurrent Steam rewrite during move | Refuse if Steam running (already) |

## Tests (minimum)

- Detect: junction orphan → true; clean single Mechabellum → false; real link + leftover beta → true.  
- Repair junction→official, keep beta: link is real game, beta remains, junction gone.  
- Repair with deleteOther: beta gone.  
- Repair real link + leftover only: leftover removed when deleteOther; link unchanged.  
- Steam busy → fail, no FS change.  
- VM: button gate; confirms; maps failures to localized strings.

## Files

- `SteamBranchLayout` / small detector helper (optional)  
- `BranchSwitchService.TryRepairOrphanDualLayout`  
- `MainViewModel` command + `CanRepairOrphanDualLayout`  
- `MainWindow.xaml` button  
- Strings zh/en/ja/de/ru  
- Tests  
- Optional: diagnostics finding `orphan_dual_layout`
