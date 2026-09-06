# Installer ACF sanitize + Melon version gate — Design

Date: 2026-09-05  
Status: approved (A + B; dirty snapshots **deleted**)

## Goals

1. **A:** On Setup post-install, delete invalid `%AppData%\MechabellumModManager\steam-acf-snapshots\official.acf` / `beta.acf` that would poison branch restore.
2. **B:** Melon component copy + `LooksLikeMelon` align with bundled minimum **0.7.3** (upgrade when older).

## Rules (same as `IsAlignedWith`)

| File | Keep when |
|------|-----------|
| `official.acf` | UserConfig `BetaKey` blank; if MountedConfig exists, its `BetaKey` blank |
| `beta.acf` | UserConfig `BetaKey` == `betaBranchName` (default `public_test`); Mounted same if present |

Dirty / unreadable → **delete file** (not quarantine). Never touch live Steam `appmanifest_*.acf`.

## Delivery

- C# `SteamAcfSnapshotSanitizer` + CLI `--sanitize-acf-snapshots` (primary)
- PS `Sanitize-AcfSnapshots.ps1` fallback
- ISS: call after config write; Melon strings + version-aware `LooksLikeMelon`

## Non-goals

Steam quit, BetaKey rewrite, junction swap, orphan disk repair.
