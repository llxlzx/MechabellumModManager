# One-click manager self-update (Setup + portable)

**Date:** 2026-09-14  
**Status:** Approved (plan accepted)  
**Approach:** In-app download → sha256 verify → channel-specific apply (silent Inno Setup vs portable staging helper)

## Goal

After「检查更新」finds a newer manager version, the player can **one-click** download and finish the update—same spirit as mod install—without manually hunting a browser download.

## Channels

| Detection | Rule | Apply |
|-----------|------|--------|
| Installed | `unins000.exe` exists under `AppContext.BaseDirectory` | Download Setup → `/SILENT /CLOSEAPPLICATIONS /NORESTART /DIR="{app}"` → exit |
| Portable | otherwise | Download portable zip → extract `_update_staging` → spawn helper → exit → helper replaces files (exclude `data\`) → relaunch |

Do **not** use the「便携数据目录」checkbox as channel detection.

## Manifest (`latest.json`)

Additive fields (missing → one-click disabled, fall back to open URL):

- `portableUrl`
- `setupSha256` / `portableSha256` (lowercase hex)

Keep `setupUrl`, `version`, `notes`, `publishedAt`.

Mirror sync uploads portable zip with Setup and rewrites both URLs on the COS copy of `latest.json`.

## Non-goals

- Silent background auto-update without confirm  
- Velopack / delta patches  
- Re-running Melon / dual-branch wizards during self-update  

## Safety

- HTTPS only (`ExternalUrlPolicy`)  
- Same critical-op gates as check-update  
- Hash mismatch aborts; offer open-link fallback  
