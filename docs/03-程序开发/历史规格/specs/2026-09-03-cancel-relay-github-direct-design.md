# Cancel relay — GitHub-direct submit/report

> **Superseded** by email-first community path (manager **1.0.5**). Primary: email to llxmod@foxmail.com.

**Status:** **Superseded** by email-first (1.0.5)
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
