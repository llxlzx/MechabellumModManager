# Design: Steam settle Ready gate + launch Melon hint

Date: 2026-09-05  
Scope: **Approach 1** — fix Track B state machine; Track A observability only.

## Problem

- **Track B:** `ConfirmManualBeta` → `DeployBoundProfileAndClearSettle` always calls `ClearSteamSettle()` even when `ApplyProfile` fails (e.g. `GameMissing`) or ACF snapshot fails (`Manifest is not settled`). Result: `WizardStep=Ready` with no deploy-manifest and Melon `0 Mods`.
- **Track A:** `ApplyAndLaunch` reports “已请求启动” after `steam://` without verifying Melon. Confirmed: Melon did not start for member 161520. Root cause of non-start is unverified; do not change Steam/exe fallback yet.

## Goals

1. Settle confirmation must not mark Ready unless deploy succeeded and game path Detect is Ready.
2. If ACF is not settled, do not clear settle (stay awaiting) and tell the user why.
3. `IsAlignedWith` early-out must not claim “already on branch” when the Steam link path is not a valid Ready game for Detect.
4. After launch request, reinforce that Melon console / Latest.log is the proof of injection (string/hint only).

## Non-goals

- SteamThenExe timeout → exe fallback
- Polling Latest.log mtime / process wait after launch
- Auto-redeploy on later RefreshStatus when Ready recovers
- Changing Melon install / assembly generation

## Behavior

### B1 — `DeployBoundProfileAndClearSettle`

Order:

1. Refresh / Detect on `GamePath` (via existing `ApplyProfile` path).
2. If not Ready → log message, **do not** clear settle, notify user to wait for Steam / valid install.
3. `ApplyProfile(ignoreBranchGate: true)` — if false → **do not** clear settle.
4. `TrySnapshotSettledAcf(ActiveGameBranch)` — if fail because unsettled (or equivalent) → log, **do not** clear settle; keep `IsAwaitingSteamSettle`.
5. Only on Apply success **and** snapshot success (or snapshot skipped only if we explicitly allow “no ACF file” — **not** for unsettled): call `ClearSteamSettle()`.

**Decision (explicit):** unsettled ACF **blocks** Ready. Missing ACF file also blocks (same as today’s snapshot fail paths that are not “already saved”). If snapshot fails for “Game or Steam is running”, also block Ready (user can retry after exit).

### B2 — `IsAlignedWith` / switch early-out

Before treating “already on branch” as done, require `GameDetector.Detect(SteamLinkPath).Kind == Ready` (or at least not `GameMissing` / not missing exe+dll). Prefer full **Ready** so assemblies/proxy are present.

Implementation: either extend `IsAlignedWith` to accept/use detector, or gate in `SwitchToBranchAsync` after `IsAlignedWith`:

```
if (IsAlignedWith(target) && Detect(SteamLinkPath|GamePath).Kind == Ready) → already message
if (IsAlignedWith(target) && Detect != Ready) → do not early-out; allow swap/repair path OR show distinct “aligned keys but game incomplete” message and return without claiming success
```

**Decision (explicit):** If junction+ACF aligned but Detect ≠ Ready → **do not** show `NotifyAlreadyOnGameBranch`; show a new message that Steam/files are incomplete and stay put (no fake success). Do not force a full swap blindly.

### A1 — Launch hint

Keep existing Melon console hint. Add one log/notify line after successful launch request: remind user that if no Melon black console appears within ~30s, Melon did not load; check `version.dll` / `MelonLoader` and export diagnostics. No behavior change to `GameLauncher`.

## Localization

- New strings (zh + en at minimum; mirror other locales if project requires all): settle blocked by not Ready; settle blocked by unsettled ACF; aligned-but-incomplete game; launch Melon-not-started reminder.

## Tests

- `ConfirmManualBeta` when Detect is GameMissing → stays `IsAwaitingSteamSettle`, no deploy-manifest, WizardStep not Ready.
- `ConfirmManualBeta` when Ready but ACF unsettled → stays awaiting, no Ready.
- `ConfirmManualBeta` when Ready + settled ACF → clears settle + writes branch deploy-manifest (existing test still passes; fixture may need settled ACF).
- `SwitchToBranch` when IsAlignedWith but GameMissing → does not emit “already on branch” success text.
- Launch: after ApplyAndLaunch success, session log contains Melon reminder (optional light assert).

## Diagnostics enrichment (added)

Export `environment.json` with structured `probe` + `findings[]` so packs answer Track A/B without guessing:

**Track A (Melon / launch)**
- `launchMode`, `gameRunning`, `steamRunning`
- Game-path flags: exe, GameAssembly, version.dll, winhttp.dll, MelonLoader dir, Il2Cpp assemblies
- `melonLatestLog`: exists, lastWriteTime, ageSeconds vs export, lineCount, hasPreferencesLoaded, `staleOrIncomplete` heuristic

**Track B (branch / settle)**
- `isAwaitingSteamSettle`, steamLink junction?, junctionTarget
- link / official / beta `looksLikeGameRoot`
- ACF exists, settled?, summary fields (Bytes*, buildid, TargetBuildID, StateFlags, BetaKey, MountedBetaKey)
- deploy-manifest presence for legacy + official + beta

**findings** (machine-readable codes), e.g. `melon_log_stale_or_incomplete`, `game_missing`, `acf_not_settled`, `ready_without_branch_deploy_manifest`, `junction_missing_while_branch_enabled`.

Redaction: apply existing path redaction to probe string fields.

## Risks

- Stricter settle may leave users stuck in awaiting longer — correct vs silent Ready.
- Snapshot requiring Steam not running may conflict with “confirm while Steam running” risk dialog — if user confirms risk, Apply may succeed while snapshot fails → stay awaiting (acceptable; they retry after Steam exit).
