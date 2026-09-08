# Ship v1.1.8 Mirror Default + Branch Harden — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v1.1.8 with factory domestic mirror, branch-switch P0/P1 fixes, GitHub Release, and COS IncludeSetup sync.

**Architecture:** Config null-vs-empty distinguishes “never set” from “user cleared”. BranchSwitchService rollback/move/preflight harden in place; wizard readiness split in BranchStoreHealth + MainViewModel. Release tooling unchanged.

**Tech Stack:** .NET WPF, xUnit/FluentAssertions, Inno Setup, PowerShell coscli sync, gh CLI.

## Global Constraints

- Version stays **1.1.8** everywhere (csproj, iss, latest.json).
- Judgment Tasks: `model: inherit` while Fable API unpaid.
- Do not commit Melon zip / `coscli_output` / unrelated historic release churn unless required for this ship.
- TDD: failing test before production code for each behavior change.
- Commits authorized by user “全部完成” for this workstream.

---

### Task 1: Default MirrorBaseUrl

**Files:**
- Create: `tests/MechabellumModManager.Tests/MirrorDefaultTests.cs`
- Modify: `src/MechabellumModManager/Models/AppConfig.cs` (if helper needed)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`ApplyMirrorBaseUrl`, ctor load)
- Modify: `src/MechabellumModManager/Resources/Strings*.resx` (`MirrorBaseUrlHint`)
- Create or modify: `src/MechabellumModManager/Services/DomesticMirrorDefaults.cs` (constant)

**Interfaces:**
- Produces: `DomesticMirrorDefaults.BaseUrl` string constant
- Produces: load path — `null` → default; `""` → disabled; URL → kept

- [ ] **Step 1: Write failing tests** in `MirrorDefaultTests.cs`:
  - `Null_config_applies_built_in_cos_default`
  - `Empty_string_disables_mirror_without_refill`
  - `Custom_https_url_is_preserved`
- [ ] **Step 2: Run tests — expect FAIL**
- [ ] **Step 3: Implement constant + ApplyMirrorBaseUrl null/"" semantics + load**
- [ ] **Step 4: Run tests — expect PASS; update MirrorBaseUrlHint in zh/en (+ other locales)**
- [ ] **Step 5: Commit** `feat: factory-default domestic COS mirror URL`

---

### Task 2: Steam-busy rollback keeps SessionOwned

**Files:**
- Modify: `src/MechabellumModManager/Services/BranchSwitchService.cs` (`TryRollbackEnableSession`)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`RollbackEnableDualToSingle`)
- Modify: `tests/MechabellumModManager.Tests/BranchSwitchServiceTests.cs`
- Modify: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs`

- [ ] **Step 1: Failing test** `RollbackEnableSession_steam_running_does_not_clear_session_ownership`
- [ ] **Step 2: Failing VM test** `RollbackEnableDualToSingle_steam_busy_keeps_session_owned_and_shows_emergency`
- [ ] **Step 3: Run — FAIL**
- [ ] **Step 4: Fix service + VM**
- [ ] **Step 5: PASS + commit** `fix: keep SessionOwned when rollback blocked by Steam`

---

### Task 3: Wizard ArchiveB disk-ready vs Steam exit

**Files:**
- Modify: `src/MechabellumModManager/Services/BranchStoreHealth.cs` (or equivalent readiness helpers)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (wizard poll / auto-continue)
- Modify: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs`

- [ ] **Step 1: Failing test** `Wizard_disk_ready_steam_open_auto_continues_after_archiveB` (or nearest existing hook name documenting intended behavior)
- [ ] **Step 2: FAIL then implement split readiness**
- [ ] **Step 3: PASS + commit** `fix: wizard auto-continue when download disk-ready`

---

### Task 4: Emergency recover + IsAlignedWith tests (+ minimal fixes if red)

**Files:**
- Modify: `tests/MechabellumModManager.Tests/BranchSwitchServiceTests.cs`
- Possibly: `BranchSwitchService.cs` only if tests expose bugs

- [ ] **Step 1: Add tests** for `TryEmergencyRecoverToSingle` (session → teardown → orphan) and `IsAlignedWith` true/false
- [ ] **Step 2: Fix production only if FAIL for wrong reason**
- [ ] **Step 3: Commit** `test: cover emergency recover and branch alignment`

---

### Task 5: Archive A preflight + partial move recoverability

**Files:**
- Modify: `BranchSwitchService.cs` (`MoveDirectoryWithRetry`, Archive A entry, junction error mapping)
- Modify: `JunctionService.cs` if needed for clearer errors
- Modify: tests `BranchSwitchArchiveErrorTests.cs` / `BranchSwitchServiceTests.cs`

- [ ] **Step 1: Failing tests** for cross-volume/preflight message and copy-ok-delete-fail recoverable flag
- [ ] **Step 2: Implement preflight + recoverable residue**
- [ ] **Step 3: PASS + commit** `fix: archive preflight and partial-move recovery marker`

---

### Task 6: Full verify, package, publish, mirror

- [ ] **Step 1:** `dotnet test -c Release` (full suite)
- [ ] **Step 2:** Rebuild Setup per `docs/releasing.md` (fat Melon required)
- [ ] **Step 3:** Refresh `release/v1.1.8/latest.json` notes (mirror default + switch harden + mod update UI)
- [ ] **Step 4:** `git push origin master`
- [ ] **Step 5:** `gh release create v1.1.8` with Setup + latest.json + portable
- [ ] **Step 6:** `sync-mirror.ps1` with `-IncludeSetup -MirrorBaseUrl` + `verify-mirror.ps1 -ExpectVersion 1.1.8`
- [ ] **Step 7:** Paste no-VPN HEAD evidence; finishing handoff

---

## Manual residual (document only, not blocking)

- True Steam multi-library / Unicode path on physical machines
- Player upgrade from 1.1.7 → 1.1.8 then fill-or-default mirror smoke
