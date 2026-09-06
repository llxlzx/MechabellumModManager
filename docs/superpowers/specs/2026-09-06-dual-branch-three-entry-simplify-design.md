# Dual-branch three-entry simplify (2026-09-06)

## Goal

Ship a dual-folder Official/Beta feature that is simple enough to promote: three user actions only, no mid-wizard resume, cancel/fail always leaves a known-good single `Mechabellum` when possible.

## User-visible entries

1. **Enable dual** (`启用双服`) — one-shot wizard to `Ready`, or fail/cancel full rollback to single directory.
2. **Switch** (`切换到正式服` / `切换到测试服`) — only when `Enabled && Ready` (hollow escape during settle still allowed via switch).
3. **Emergency recover** (`急救恢复单目录`) — merges teardown + orphan repair; always targets playable single `Mechabellum`.

Removed from UI: separate teardown button, orphan repair button, “start wizard = resume mid-step”.

## State contract

| User state | Config | Disk |
|------------|--------|------|
| Not enabled | `Enabled=false`, `WizardStep=None` | Single `Mechabellum` game root (ideally) |
| Enabling | mid steps / settle for first enable | Transient stores; session-owned |
| Dual ready | `Enabled=true`, `WizardStep=Ready` | Junction + two stores |
| Needs emergency | off or on, but layout broken | Hollow link, leftovers, etc. |

Internal steps (`Declared`…`AwaitingSteamSettle`) remain implementation detail. Closing the app or cancelling during enable = **rollback**, never “continue later”.

## Rollback rules

`RollbackEnableDualToSingle`:

- Stop download poll; clear busy wizard flags.
- Materialize junction if needed; prefer complete session store.
- Delete `Mechabellum_official` / `Mechabellum_beta` only if marked **session-owned** for this enable attempt.
- Clear `Enabled`, `WizardStep=None`, store paths; keep `SteamLinkPath` when valid.
- If link is not a game root after rollback, surface Steam verify guidance (emergency can still try donor materialize).

Soft-close during enable/settle: confirm → rollback (not abandon-with-leftovers).

## Emergency recover

One confirm (honest keep/delete other stores) → exit Steam/game → materialize/hollow-donor repair → clear dual config → optional leftover warn only when user was asked and chose keep.

## Switch

Existing swap + ACF prepare + settle. Gate: `Enabled && Ready && !Busy` for normal; settle hollow escape may still call switch.

## Non-goals

- No rewrite of junction/ACF primitives beyond session ownership + rollback helper.
- No mid-wizard resume after process restart.
- No system .NET uninstall; no deleting Steam’s main `Mechabellum` install as a product action.
