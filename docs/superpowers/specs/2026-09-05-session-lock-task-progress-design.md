# Session lock + task progress strip — Design

Date: 2026-09-05  
Status: approved (session lock **A** + progress coverage **C**) + logic self-review before implementation

## Goals

1. While a **critical** manager task is incomplete, disable conflicting write actions (deploy/launch, branch switch, path browse, Melon install, profile apply that deploys, dual-folder mutations). Allow continue/cancel wizard affordances, read-only browsing, and diagnostics where safe.
2. Show a **persistent task progress strip** so players know what is running, how far it is, and when it finished / needs confirmation.
3. Cover as many long operations as practical (wizard, settle, Melon %, deploy, catalog, updates, diagnostics).

## Non-goals (v1)

- Fake Steam download percentage when ACF/Steam APIs do not expose reliable progress.
- Parallel multi-task progress rows (single current task only).
- Replacing all confirmation MessageBoxes with modeless UI.
- Changing Steam/ACF semantics.

## UI

Place a task strip **under** the existing header status border (game ready / Melon CTA / deploy blocked reason):

- Visible only when `HasCurrentTask` is true (or briefly after success for ~1.5s then clear — optional; default: clear on Complete/Fail after short success flash, or keep until next idle if wizard paused with actionable state).
- Fields: `TaskTitle`, `TaskMessage`, `TaskPercent` (nullable), `IsTaskIndeterminate`, optional percent label.
- Style: same steel/amber language as BusyProgressDialog ProgressBar.
- Disk-dangerous ops (folder move, junction) keep **BusyProgressDialog** modal; strip mirrors the same message.

## State model

```csharp
enum TaskKind {
  None,
  BranchWizard, BranchSwitch, BranchSettle, BranchRepair,
  MelonInstall, Deploy, CatalogRefresh, CatalogDownload,
  CheckUpdate, DiagnosticsExport, GenericBusy
}

class CurrentTaskState {
  TaskKind Kind;
  string Title;
  string Message;
  double? Percent;      // 0..100; null => indeterminate
  bool SessionLock;     // critical vs self-only
}
```

VM API (names indicative):

- `BeginTask(kind, title, message, sessionLock)`
- `ReportTask(message, percent?)`
- `CompleteTask(message?)` / `FailTask(message?)`
- `IsSessionLocked` => current task has `SessionLock` **or** legacy flags (`IsBranchSwitchBusy`, `IsAwaitingSteamSettle`, `IsBranchWizardInProgress` with lock policy below)
- `HasCurrentTask`, bindable title/message/percent/indet

Legacy Busy: `RunBusyAsync` calls `BeginTask(..., SessionLock=true)` + existing dialog; `BusyMessage` updates also call `ReportTask`.

## Session lock policy (A)

| Condition | SessionLock | Notes |
|-----------|-------------|--------|
| Wizard in progress (`WizardStep` not None, including WaitingDownloadB even if `Enabled=false`) | Yes | Keep Start/Continue/Cancel/Teardown/Repair as needed via explicit allow-list |
| `IsAwaitingSteamSettle` | Yes | Confirm settle button stays enabled |
| `IsBranchSwitchBusy` / Busy dialog | Yes | |
| Melon install/upgrade | Yes | |
| Deploy / Apply profile (deploy path) | Yes | |
| Orphan repair / teardown long ops | Yes | |
| Catalog refresh / mod download | No (self-reentrancy only) | |
| Check for updates | No (self only) | |
| Diagnostics export | No (self only) | |

**Allow while locked (critical):** Confirm settle, Continue/Resume wizard dialogs already open, Cancel/pause wizard if already exposed, Open logs / copy diagnostics read-only, page navigation for viewing (optional: allow nav).

**Deny while locked:** Apply / Apply+Launch, Switch Official/Beta, Start wizard (if another critical task), Browse game path, Install Melon, Enable dual-folder mutations, Import that writes profiles (prefer deny).

## Progress coverage (C)

| Task | Determinate? | Source |
|------|--------------|--------|
| Melon install | Yes when downloading | Existing `MelonLoaderProgress.Percent` |
| Branch Busy (wait Steam exit, move, junction) | Indeterminate | Message stages |
| Wizard phases | Indeterminate + stage title | Map `BranchWizardStep` → localized title |
| Steam settle | Indeterminate | “Waiting for Steam settle — confirm when ready” |
| Deploy | Indeterminate (+ optional stage strings) | Deploy service / VM deploy loop |
| Catalog refresh | Indeterminate | Existing status strings |
| Catalog download | Indeterminate or bytes if available | Catalog download path |
| Check update | Indeterminate | Update checker |
| Diagnostics | Indeterminate | Export service |

## Wizard paused (WaitingDownloadB)

- Show strip: title like “Dual-folder: waiting for other branch download”, message mirroring confirm dialog guidance.
- `IsSessionLocked=true` so Apply/Launch/Melon/path cannot run while incomplete.
- User can still answer Yes/No on the confirm dialog when it is shown; when paused without dialog, show Continue affordance if one already exists in UI.

## i18n

All strip titles/messages via `UiStrings` / resx (zh/en/ja/ru/de).

## Testing

- Unit: `IsSessionLocked` true/false matrix for wizard step / settle / melon / catalog-only.
- Unit: percent null ⇒ indeterminate; percent 0..100 ⇒ determinate bindings helpers.
- Existing branch/deploy CanExecute tests updated for session lock.
- Melon progress: mock `IProgress` updates VM percent (if testable without UI).

## Implementation phases

1. Model + strip UI + `IsSessionLocked` wired into existing `Can*` / Melon / Browse.
2. Hook `RunBusyAsync` + wizard steps + settle.
3. Melon `IProgress` → strip.
4. Deploy, catalog, updates, diagnostics.

## Logic self-review (before coding)

| # | Risk | Mitigation |
|---|------|------------|
| 1 | `WaitingDownloadB` today clears `IsBranchWizardBlocking` so deploy stays enabled — lock A would **change** that | Intentional: strip + session lock must treat wizard-in-progress (incl. paused WaitingDownload) as locked; update `CanDeployOrLaunch` tests/docs |
| 2 | Nested tasks (Busy inside wizard) overwrite strip | Stack depth=1 with “outer kind” preserved, or ignore BeginTask if same session already locked by wizard and only ReportTask; prefer: Busy nested updates message but does not ClearTask on BusyEnd if wizard still active |
| 3 | CompleteTask clears strip while settle still needs Confirm | Settle keeps `HasCurrentTask` until confirm success or cancel settle |
| 4 | Root `IsEnabled=false` bricks Confirm settle | Do **not** disable whole window for session lock; only CanExecute / button IsEnabled. Keep Busy dialog for true modal disk ops only |
| 5 | Catalog “no session lock” but user starts Melon during catalog | Melon BeginTask with SessionLock preempts; catalog op should check cancel or queue — v1: Melon waits if catalog running, or catalog FailTask then Melon (serialize via single CurrentTask; second BeginTask no-ops or waits) |
| 6 | Double BusyBegin + strip | Single writer: RunBusyAsync owns both |
| 7 | i18n missing for new strings | Add all 5 resx in same PR as strip |
| 8 | Success flash vs sticky wizard strip | Wizard/settle sticky until step advances; ephemeral ops clear after Complete |
| 9 | `Enabled=false` paused wizard vs `IsBranchWizardInProgress` | Define InProgress as WizardStep ∉ {None, Ready} **or** explicitly include WaitingDownloadB even when Enabled=false |
| 10 | ApplyProfile early-return vs Command CanExecute desync | NotifyCanExecute on every Begin/Complete/Fail and NotifyBranchGates |

**Self-review verdict:** Design is consistent if WaitingDownload intentionally locks deploy (behavior change called out), nested Busy does not clear wizard task, and lock is CanExecute-based not window-wide.
