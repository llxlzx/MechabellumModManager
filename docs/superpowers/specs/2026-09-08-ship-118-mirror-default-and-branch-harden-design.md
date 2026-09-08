# Ship v1.1.8: default domestic mirror + branch-switch harden

**Date:** 2026-09-08  
**Status:** Approved (plan attachment)  
**Version:** 1.1.8 (unpublished; fold all work into this release)

## Goal

1. Domestic players get Mod catalog / mod files / manager Setup without VPN once the COS mirror is live.
2. New installs (and upgrades that never set a mirror) pre-fill the known COS root.
3. Publish v1.1.8 so Mod update UI (badge, status column, Update button, startup check) reaches players.
4. Harden one-click official/beta switch for P0/P1 failures across common PC environments.

## A. Default MirrorBaseUrl

**Constant:** `https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com`

| Config value | Meaning |
|---|---|
| `null` (missing key / never written) | Apply built-in default and save |
| `""` (user cleared the field) | Mirror disabled; do **not** re-fill |
| non-empty https URL | Use as-is |

**Rules:**

- `ApplyMirrorBaseUrl` must persist cleared field as `""`, not coerce to `null`.
- Reject non-https URLs (existing behavior).
- Settings hint: pre-filled COS; clear to use GitHub only.
- MelonLoader zip stays on fat Setup redist — not mirrored.

## B. Branch-switch hardening (same release)

1. **P0** `TryRollbackEnableSession`: when Steam/game running, do **not** clear `SessionOwned*`; return fail. VM must not claim full rollback until disk succeeds (align with startup recovery).
2. **P1** Wizard ArchiveB: split “disk ready” from “Steam exited”; auto-continue when disk ready; keep explicit continue when Steam still open.
3. **Tests** for `TryEmergencyRecoverToSingle` and `IsAlignedWith` main paths.
4. **Preflight** before Archive A: same NTFS volume + writable; map junction/cross-volume errors to localized guidance.
5. **P0** `MoveDirectoryWithRetry` copy-ok/delete-fail: leave recoverable flag/journal so emergency can detect duplicate dirs.

Out of scope: cross-volume junctions as a feature, non-NTFS, Melon on COS, splitting to 1.1.9.

## C. Release + mirror

- Rebuild Setup with fat Melon redist; push; `gh release create v1.1.8`.
- `sync-mirror.ps1` with `-IncludeSetup -MirrorBaseUrl` and `-ExpectVersion 1.1.8` verify.
- Acceptance: no-VPN HEAD catalog, hot mods, mirrored `latest.json` version 1.1.8, `setupUrl` on COS Setup downloads.
