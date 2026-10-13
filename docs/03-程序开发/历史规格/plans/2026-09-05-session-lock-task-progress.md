# Plan: Session lock + task progress strip

> Spec: `docs/superpowers/specs/2026-09-05-session-lock-task-progress-design.md`

## Files

| File | Responsibility |
|------|----------------|
| `Services/TaskProgressSession.cs` (or VM-local) | Current task state + Begin/Report/Complete/Fail |
| `ViewModels/MainViewModel.cs` | IsSessionLocked, wire Can*, RunBusyAsync, ops |
| `MainWindow.xaml` | Task strip under header status |
| `Resources/Strings*.resx` + `UiStrings.cs` | Titles/messages |
| `tests/.../TaskProgressSessionTests.cs` | Lock + percent helpers |
| Update existing CanExecute tests | WaitingDownload locks deploy |

## Task 1 — Core model + tests (TDD)

- RED/GREEN: `TaskProgressSession` Begin/Report/Complete; SessionLock flag; nested BusyEnd does not clear if wizard sticky.
- `IsSessionLocked` combines session + legacy settle/wizard-in-progress.

## Task 2 — Strip UI + Can* wiring

- Bind strip; hide when idle.
- Fold `IsSessionLocked` into `CanDeployOrLaunch`, Melon, Browse, switch, etc. Allow-list settle confirm.

## Task 3 — Busy + wizard + settle hooks

## Task 4 — Melon percent

## Task 5 — Deploy, catalog, updates, diagnostics

## Task 6 — Verify tests + manual checklist
