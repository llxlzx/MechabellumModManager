# Cancel relay — GitHub-direct submit/report

**Status:** Approved (user: 同意取消中转)  
**Date:** 2026-09-03

## Decision

Remove Cloudflare Worker / R2 relay. Authors and reporters use the MechabellumMods GitHub repo directly.

## Behavior

| Action | Flow |
|--------|------|
| 投稿 Mod | Confirm → open README contribute section (Fork + PR) |
| 举报 | Keep category dialog → confirm → open pre-filled `issues/new` |

No `relayBaseUrl`. No in-app DLL upload for submissions.

## Out of scope

Redeploying/deleting the Worker in Cloudflare (local `relay/` marked deprecated only).
