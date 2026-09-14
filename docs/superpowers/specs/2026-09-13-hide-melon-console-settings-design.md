# Hide MelonLoader Console (Settings Toggle)

**Date:** 2026-09-13  
**Status:** Approved (revised after auto-gated design review)  
**Approach:** AppConfig preference + immediate `UserData/Loader.cfg` `[console] hide_console` writes

## Goal

Players who dislike the MelonLoader black console can turn on **「隐藏 Melon 控制台」** in Settings. Closing the console with the window X kills the game; hiding via Melon’s official config does not.

## Non-goals (this version)

- Bumping app version / packaging / publishing releases  
- Teaching the Inno/PowerShell `Apply-LoaderCfgOptimizations` path to read AppConfig (Setup may recreate cfg without hide; user re-toggles Settings to restore)  
- Changing Melon injection detection (already uses process + `Latest.log`, not console HWND)  
- Launch-arg-only approach (`--melonloader.hideconsole`) as the sole mechanism  

## Defaults

- `AppConfig.HideMelonConsole` = **false** (console visible; preserves existing “look for black Melon window” mental model for new users).

## Behavior (timing A — approved)

1. Toggle on/off → save `AppConfig` immediately.  
2. Immediately write `hide_console = true|false` under `[console]` in each target game root’s `UserData/Loader.cfg`.  
3. **Takes effect on next game launch.** UI copy must say so; if the game is already running, log a short “restart the game” hint.  
4. Closing the Melon console with the X still exits the game (Melon process model) — tip text only.

### Target paths

- Always: current `GamePath` when it looks like a valid game root.  
- If dual-folder is enabled and `OfficialStorePath` / `BetaStorePath` look like game roots: include them.  
- Deduplicate by normalized full path before writing (junction/`GamePath` may alias a store).

### Failure policy

- Per-path write failures: log only; do not block the Settings UI with a fatal dialog.  
- Partial success is OK.

### Off toggle (H4)

- Key present → set `false`.  
- Key absent → no-op (Melon default is show console).

## API / layering

### `AppConfig`

```csharp
public bool HideMelonConsole { get; set; } // default false
```

### `MelonLoaderConfigOptimizer`

- **Does not** read AppConfig (no service→config coupling).  
- `SetHideConsole(string gamePath, bool hide)` — Settings toggle path; ensure `[console]` + `hide_console`.  
- `ApplyRecommendedSettings(string gamePath, string? knownUnityVersion = null, bool? hideConsole = null)`  
  - `hideConsole == null` → **do not touch** hide keys (preserves unrelated optimize callers that omit the preference only when they truly should not sync — manager call sites that know AppConfig must pass the current value).  
  - `hideConsole != null` → write that value.  
  - **Must apply on the MinimalLoaderCfg create + early-return path** (review finding **M1**): new cfg files must include `[console] hide_console = …` when `hideConsole` is non-null; when null, omit the section (default show).

Manager call sites that already call `ApplyRecommendedSettings` (install Melon, dual-store sync, TryOptimize, CLI install, etc.) must pass `HideMelonConsole` from AppConfig so reinstall/optimize does not drop a user’s hide preference.

## UI / i18n

- Settings: checkbox near portable-data / check-updates-on-startup.  
- Hint: hide uses Melon config; logs remain in `MelonLoader\Latest.log`; do not close the console with X; **restart game to apply**.  
- On successful launch logging (**M3**):  
  - If hide is off: keep existing `LogMelonConsoleHint` / `LogMelonLaunchVerifyHint` (black console expected).  
  - If hide is on: do **not** claim a black console should appear; use alternate strings pointing at Latest.log / injection status.  
- Strings in zh / en / ru / ja / de resx.

## Setup / installer (M2 — scoped out)

Document for maintainers: thin Setup / `Install-MelonLoader.ps1` optimize may write a Loader.cfg without `hide_console`. After such a run, opening the manager and leaving the Settings toggle on (or toggling once) restores hide via `SetHideConsole` / next optimize with `hideConsole: true`.

## Tests

- `SetHideConsole` true / false / create `[console]` when missing.  
- `ApplyRecommendedSettings` with `hideConsole: true` on **new** MinimalLoaderCfg path includes hide.  
- `hideConsole: null` leaves existing hide line unchanged (and does not add one on fresh minimal cfg).  
- Optional: path-list dedupe helper if extracted.

## Acceptance

1. Check on → cfg `hide_console = true` on deduped roots; after game restart, no Melon console; mods still load.  
2. Check off → `false` or default-visible; console returns after restart.  
3. Check on → install Melon / ApplyRecommendedSettings → hide remains true.  
4. Check on → launch log does not tell the user to look for a black Melon console.  
5. Unit tests above green.

## Review trail

Auto-gated-workflow Discussion (2026-09-13) flagged M1–M3 / H1–H4; this revision absorbs them. User approved revised design 2026-09-13.
