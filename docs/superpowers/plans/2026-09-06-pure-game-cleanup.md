# Pure-Game Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thorough uninstall/cleanup that removes Melon/Mods/manager data and safely tears down dual-folder (Official-complete gate), without ever deleting Steam game binaries or .NET.

**Architecture:** Pure whitelist + planner + executor; CLI `--pure-game-cleanup`; Inno `UninstallRun` + in-app command share the same core. Dual-folder teardown reuses `BranchSwitchService` only after Official `LooksLikeGameRoot`.

**Tech Stack:** .NET 8, xUnit, FluentAssertions, Inno Setup, existing `BranchSwitchService` / `SteamGameLocator` / `PathsService`.

**Spec:** `docs/superpowers/specs/2026-09-06-pure-game-cleanup-design.md`

## Global Constraints

- Never delete `Mechabellum.exe`, `GameAssembly.dll`, `Mechabellum_Data`, Steam manifests, or .NET.
- Official store incomplete → abort dual-folder delete (exit 3); no guessing.
- Allowlist-only deletes; reject `..` and non-child paths.
- Plan before execute; tests use temp fixtures only.

## File map

| File | Responsibility |
|------|----------------|
| Create `Services/PureGameCleanupWhitelist.cs` | Path allow checks |
| Create `Services/PureGameCleanupPlanner.cs` | Build `CleanupPlan` |
| Create `Services/PureGameCleanupExecutor.cs` | Validate + delete / teardown |
| Create `Services/PureGameCleanupCli.cs` | Arg parse + Run exit codes |
| Modify `App.xaml.cs` | Early CLI branch |
| Modify `MainViewModel` + XAML + strings | In-app command |
| Modify `installer/MechabellumModManager.iss` | UninstallRun |
| Create `tests/.../PureGameCleanupWhitelistTests.cs` | Allow/deny matrix |
| Create `tests/.../PureGameCleanupPlannerTests.cs` | Official abort / melon plan |

---

### Task 1: Whitelist + Planner (TDD)

**Files:** create whitelist, planner, tests (as in file map).

**Interfaces:**
- `PureGameCleanupWhitelist.IsAllowedGameChildDirectory(gameRoot, path)`
- `PureGameCleanupWhitelist.IsAllowedGameChildFile(gameRoot, path)`
- `PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(officialStore, otherStore)`
- `PureGameCleanupWhitelist.IsAllowedManagerAppData(path)`
- `PureGameCleanupPlanner.Build(request) → CleanupPlan`

Melon dir names: `MelonLoader`, `Mods`, `Plugins`, `UserLibs`, `UserData`.  
Melon files: `version.dll`, `winhttp.dll`.

- [ ] Implement tests then code; commit `feat: pure-game cleanup whitelist and planner`

### Task 2: Executor + CLI

- [ ] Executor: dry-run / execute; process probe gate; Official gate before teardown
- [ ] CLI flags per spec; wire `App.OnStartup`
- [ ] Commit `feat: pure-game cleanup executor and CLI`

### Task 3: UI + Inno

- [ ] Settings: 「彻底清理」两步确认 → Run executor → exit app on success
- [ ] Inno `[UninstallRun]` with `--confirm-delete-other-store` when user opts in (default on)
- [ ] Commit `feat: pure-game cleanup UI and uninstall hook`

### Task 4: Verify

- [ ] `dotnet test --filter PureGameCleanup`
- [ ] Manual dry-run against fixture
