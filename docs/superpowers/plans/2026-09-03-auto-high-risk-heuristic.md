# Auto High-Risk Name Heuristic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically set `ModPackage.HighRisk` from name/author/filename keywords on import and every `ReloadMods`, with temporary manual toggle until the next reload.

**Architecture:** Pure `RiskHeuristic.Evaluate(ModPackage)` decides HighRisk from a fixed keyword list. `ModLibraryService` applies it before writing `package.json` on import. `MainViewModel.ReloadMods` re-evaluates every listed package and persists changes. `ToggleHighRisk` updates memory/disk without re-running the heuristic so the toggle stays visible until the next natural reload.

**Tech Stack:** .NET 8, xUnit, FluentAssertions, existing WPF mod manager.

## Global Constraints

- Keyword matching: case-insensitive substring (`OrdinalIgnoreCase`)
- Do not include bare `win`
- Manual toggle is temporary; next `ReloadMods` overwrites
- No DLL IL scanning
- Chinese UI/log messages

---

### Task 1: RiskHeuristic + unit tests

**Files:**
- Create: `src/MechabellumModManager/Services/RiskHeuristic.cs`
- Create: `tests/MechabellumModManager.Tests/RiskHeuristicTests.cs`

**Interfaces:**
- Produces: `RiskHeuristicResult { bool HighRisk; string? MatchedKeyword }`
- Produces: `RiskHeuristic.Evaluate(ModPackage package) -> RiskHeuristicResult`
- Produces: `RiskHeuristic.Keywords` (read-only list for docs/tests)

- [ ] **Step 1: Write failing tests** covering EN hit, CN hit, QuickCamera miss, BetterWindow miss (no bare win), case-insensitive `Damage`

- [ ] **Step 2: Implement RiskHeuristic** scanning DisplayName, Id, Author, directory name, each `Files[].RelativePathInPackage` filename

- [ ] **Step 3: `dotnet test --filter RiskHeuristic` PASS**

- [ ] **Step 4: Commit** `feat: add RiskHeuristic name keyword detector`

---

### Task 2: Apply on import + ReloadMods; fix ToggleHighRisk

**Files:**
- Modify: `src/MechabellumModManager/Services/ModLibraryService.cs` (before `WritePackageJson` in finalize)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`ReloadMods`, `ToggleHighRisk`)
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs` (notify HighRiskLabel after in-place toggle)
- Modify: `README.md` (one-line heuristic disclaimer)
- Test: extend `MainViewModelTests` or add library test if cheap

**Interfaces:**
- Consumes: `RiskHeuristic.Evaluate`
- `ReloadMods` re-evaluates and `PersistPackageMeta` when value changes; logs first matched keyword
- `ToggleHighRisk`: flip + persist + refresh item labels **without** calling heuristic (do not call full `ReloadMods`, or call `ReloadMods(applyHeuristic: false)`)

- [ ] **Step 1: Wire import finalize** to set `pkg.HighRisk` from heuristic before `WritePackageJson`

- [ ] **Step 2: Wire ReloadMods** to re-evaluate all real (non-missing) packages

- [ ] **Step 3: Fix ToggleHighRisk** so manual flip is visible until next heuristic reload

- [ ] **Step 4: README note + tests PASS + commit** `feat: auto-mark high-risk mods by name keywords`

- [ ] **Step 5: Publish exe**

---

## Spec coverage

| Spec item | Task |
|-----------|------|
| Keyword table / no bare win | 1 |
| Import-time apply | 2 |
| ReloadMods overwrite | 2 |
| Temporary ToggleHighRisk | 2 |
| Log on change | 2 |
| README disclaimer | 2 |
| No IL scan | both (omitted) |
