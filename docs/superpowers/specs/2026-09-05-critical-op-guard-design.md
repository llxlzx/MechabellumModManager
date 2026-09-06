# Critical-op guard + layered update/close + crash recovery — Design

Date: 2026-09-05  
Status: draft for user review (approach **2** approved)  
Related: `2026-09-05-session-lock-task-progress-design.md`, `2026-09-05-settle-confirm-gate-chain.md`

## Problem

While the manager is mid branch/mode switch (disk writes) or waiting on Steam, users may:

1. Click in-app **检查更新** → open Setup URL → run installer while the process is still writing.
2. Close the main window / kill the app mid-write.
3. Restart into a half-finished dual-folder / settle state with weak guidance.

Session lock already blocks deploy/branch mutations. It does **not** block update checks. Busy dialog blocks close only while that dialog is open. Settle wait (`IsAwaitingSteamSettle`) is recoverable via persisted `WizardStep` but exit/update during settle still risks confusion.

## Goals

1. **Layered gates** for update + close:
   - **Hard** during true disk-critical work.
   - **Soft** (strong confirm) during recoverable wait states (Steam settle / paused wizard waiting on user/Steam).
2. **Installer** messaging when Setup sees the manager still running.
3. **Crash / kill recovery**: persist a critical-op marker; on next launch show a **modal gate** until user chooses continue or repair path.
4. Keep logic aligned with existing session lock and settle pipeline — no second competing “busy” story.

## Non-goals

- In-process self-update / silent replace of the running EXE.
- Fake Steam download progress.
- Full transactional journal replay beyond existing branch journal + wizard steps (no approach 3).
- Blocking updates forever while Steam itself is downloading the game (only when manager is in a managed wait/write state).

## Decisions (locked)

| Item | Choice |
|------|--------|
| Gate style | Layered (hard write / soft settle) |
| Scope | In-app + Setup copy + crash marker |
| Recovery UX | Startup **modal** must choose continue or repair before normal ops |
| Implementation shape | Approach 2: `CriticalOpGuard` lifecycle + reuse existing wizard/settle state |

---

## 1. State model — `CriticalOpGuard`

Small service (or thin helper owned by VM + AppData file) that tracks **durable** critical work:

```text
CriticalOpKind:
  None
  BranchDiskWrite      // move folders, junction, live ACF write, teardown move
  MelonInstall         // Melon extract/write into game tree
  OrphanRepair         // repair orphan dual layout / journal repair writes
  DeployWrite          // optional: profile deploy into Mods (see policy below)

CriticalOpPhase:
  Idle
  Running              // disk-critical in this process
  Interrupted          // marker left from previous process (crash/kill/update)
```

**On-disk marker** (AppData, next to branch config), e.g. `critical-op.json`:

```json
{
  "kind": "BranchDiskWrite",
  "startedUtc": "...",
  "detail": "SwitchToBeta|WizardArchiveB|...",
  "pid": 12345
}
```

### Lifecycle

| Event | Action |
|-------|--------|
| Enter disk-critical path | `Begin(kind, detail)` → write marker **before** first mutating IO |
| Exit success / handled failure | `Complete()` → delete marker |
| Process dies mid-`Running` | marker remains → next launch sees `Interrupted` |
| Enter settle / waiting-download only | **Do not** use `Running` disk marker; rely on `WizardStep` / `IsAwaitingSteamSettle` (recoverable) |

### Hard vs soft classification

| Condition | Close window | Check update / open Setup URL | Notes |
|-----------|--------------|-------------------------------|-------|
| `CriticalOpGuard.Running` OR Busy dialog OR `IsBranchSwitchBusy` (disk segment) | **Hard cancel** + notify | **Hard refuse** + notify | Same family as current `_blockMainClose` |
| `IsAwaitingSteamSettle` OR paused wizard waiting Steam (`WaitingDownloadB` etc.) without active disk write | Soft: confirm with risk text; allow if user insists | Soft: same confirm | State is on disk via config |
| `Interrupted` marker on startup | N/A until modal resolved | Soft or hard until modal done — **prefer hard** until user picks path | Modal first |
| Catalog / diagnostics / check-update self task | No change | Check-update remains non-session-lock for *other* ops, but gated by table above | |

**DeployWrite:** prefer **soft or short hard** only while `ApplyProfile` is actively writing Mods (Busy). Do not leave a durable marker for ordinary deploy unless we already have Busy; YAGNI: tie deploy to Busy/`_blockMainClose` only, **no** durable marker for deploy in v1.

### Relation to session lock

```text
IsSessionLocked (existing)
  ⊇ deploy/branch gates

CriticalOpGuard.Running
  ⊇ hard update/close gates

IsAwaitingSteamSettle / recoverable wizard wait
  ⊇ soft update/close confirms
```

Do **not** collapse soft settle into hard close — users must be able to update the manager while Steam finishes a long download, after acknowledging risk.

---

## 2. In-app update + close behavior

### Check for updates

Today: `CheckForUpdatesAsync` uses `sessionLock: false` and may open Setup URL.

Change:

1. If hard-critical → `Notify(...)` with localized reason; **do not** open URL.
2. If soft-recoverable → `Confirm(risk text)` → only on Yes open URL; log that user accepted risk.
3. Else → existing flow.

Risk text must name the active state (settle Official vs Beta, wizard step) and say: closing/installing may leave dual-folder mid-flight; prefer wait until settle finishes; if you must update, finish settle after reinstall via continue/repair.

### Window close / Alt+F4

Extend beyond Busy-only `_blockMainClose`:

1. Hard-critical → cancel close + notify (same as Busy).
2. Soft-recoverable → confirm; Yes allows close (marker not needed; wizard/settle config already persisted).
3. Idle → close normally.

Task Manager kill cannot be prevented — durable marker + startup modal covers that for disk-critical paths.

### Startup modal (Interrupted or dirty wizard)

Trigger when **any** of:

- `critical-op.json` exists (`Interrupted`), or
- Branch config shows unfinished wizard / settle that needs attention **and** disk looks inconsistent (reuse `InspectOrphanDualLayout` / existing restore heuristics), or
- Interrupted marker even if wizard says Ready (trust marker until cleared after successful continue/repair/complete).

Modal choices (exact labels i18n):

1. **Continue unfinished work** — route to existing continue/settle UI (set flags, focus branch page, clear Interrupted only after entering a defined safe checkpoint or Complete).
2. **Run repair / safe cleanup guide** — open orphan repair / journal repair path; on success `Complete()` clears marker.
3. Optional tertiary: **Open logs / copy diagnostics** (does not dismiss gate).

Until user picks 1 or 2 and the chosen path reaches a terminal success **or** explicitly clears after repair, main write actions stay locked (same as session lock). Reading catalog OK.

Clear marker rules:

- `Complete()` after successful disk op end.
- After successful settle confirm clear path.
- After successful orphan repair / teardown that leaves a consistent Idle layout.
- Never clear solely because user opened the app.

---

## 3. Installer (Setup) layer

Inno already has CloseApplications / Files in Use.

Enhance:

1. Custom message (zh + default en) when Mechabellum Mod Manager is among apps in use: advise that if the manager was switching modes or installing Melon, **finish or cancel that task first**, then close the manager, then continue Setup.
2. Prefer **Don't force-kill** as the recommended path in copy (user should close cleanly after hard/soft gates).
3. No need to read `critical-op.json` from Setup in v1 (path varies); process-name detection is enough.

---

## 4. Wiring map (where Begin/Complete attach)

| Code path | Begin | Complete |
|-----------|-------|----------|
| Branch switch disk segment (wait Steam exit → move/junction/ACF) | before mutate | finally |
| Wizard archive / link phases that move or junction | before mutate | finally / step persist + Complete when leaving disk segment |
| Teardown dual-folder | before mutate | finally |
| Orphan repair writes | before mutate | finally |
| Melon install into game dir | before extract/write | finally |
| Settle wait only | none | n/a |
| Confirm settle → deploy + snapshot | optional short Busy hard-close only; durable marker **no** for deploy-only | |

`IsBranchSwitchBusy` remains the UI busy flag; Guard marker is the **durable** twin for crash.

---

## 5. Error handling

- Marker write failure: log; still attempt op; prefer fail-closed on update/close if in-memory Running is set even if disk write failed.
- Stale marker + healthy Ready layout: modal still shows; “Continue” no-ops into refresh + clear after inspect says clean; “Repair” runs inspect/repair.
- PID in marker is informational only (do not require PID alive checks for v1).

---

## 6. Testing

| Case | Expect |
|------|--------|
| Begin BranchDiskWrite → CheckForUpdates | hard refuse, no URL |
| Busy/hard → Closing | cancel |
| AwaitingSteamSettle → CheckForUpdates | confirm; Yes opens URL |
| AwaitingSteamSettle → Close → Yes | closes; config still AwaitingSteamSettle |
| Kill mid-Begin (simulate leftover file) → startup | modal; writes locked until choice |
| Complete removes file | next startup no modal from marker |
| Soft settle does not create marker | no Interrupted from settle alone |

---

## 7. i18n keys (indicative)

- `CriticalOpBlockUpdate` / `CriticalOpBlockClose`
- `CriticalOpSoftUpdateConfirm` / `CriticalOpSoftCloseConfirm` (format with branch/step)
- `CriticalOpRecoveryTitle` / `CriticalOpRecoveryBody`
- `CriticalOpRecoveryContinue` / `CriticalOpRecoveryRepair`
- Setup: ChineseSimplified (+ English) Files-in-use / close-apps note for this app

---

## 8. Rollout

Ship in **1.1.2** patch notes: “切换模式/写盘时禁止更新与关闭；等 Steam 时可更新但会警告；异常退出后启动强制继续或修复。”

---

## Spec self-review

| Check | Result |
|-------|--------|
| Placeholders | None left as TBD; DeployWrite explicitly YAGNI (Busy only) |
| Consistency | Soft settle ≠ durable marker; hard write = marker + block update/close; session lock unchanged for deploy |
| Scope | Single feature: guard + gates + modal + Setup copy — one plan |
| Ambiguity | “Continue” clears marker only after safe checkpoint — defined as enter settle UI with persisted step **or** Complete after disk success; stale Ready+marker → inspect then clear |
| Contradiction vs prior settle polish | Steam-open settle snapshot still allowed; this design does not re-block Steam-open for settle confirm |

**Open residual (accepted):** Task Manager / `taskkill` during write still possible; recovery modal is the mitigation, not prevention.
