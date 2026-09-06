# Orphan Dual-Layout Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When dual-folder is off but junction/leftover stores remain, offer「恢复为单目录」with confirms and safety checks.

**Architecture:** Disk-aware `TryRepairOrphanDualLayout` in `BranchSwitchService` (junction materialize + optional leftover delete); VM command gated by orphan detection; reuse teardown move/rollback patterns but **enumerate other stores from disk**, not empty JSON paths.

**Tech Stack:** WPF/.NET 8, xUnit, FluentAssertions, existing JunctionService / BranchSwitchService.

## Global Constraints

- Confirm delete-other default **No** (choice A).
- Dedicated button, orphan-only (choice A).
- Cover junction orphan **and** real Mechabellum + leftovers (choice C).
- Refuse when Steam/game running.
- L leftover = `LooksLikeGameRoot` only (no empty dirs).
- Spec: `docs/superpowers/specs/2026-09-05-orphan-dual-layout-repair-design.md`

---

## Task 1: Detector + service repair (TDD)

**Files:**
- Create/modify: `tests/MechabellumModManager.Tests/OrphanDualLayoutRepairTests.cs`
- Modify: `src/MechabellumModManager/Services/SteamBranchLayout.cs` (detect helpers if needed)
- Modify: `src/MechabellumModManager/Services/BranchSwitchService.cs`

- [ ] Write failing tests: detect J/L; repair junction keep other; repair delete other; leftover-only; steam busy
- [ ] Implement `DetectOrphanDualLayout` / `TryRepairOrphanDualLayout`
- [ ] Green

## Task 2: ViewModel + UI + strings

**Files:**
- Modify: `MainViewModel.cs`, `MainWindow.xaml`, `UiStrings.cs`, `Strings*.resx`
- Tests: `MainViewModelBranchSwitchTests.cs`

- [ ] Failing VM tests for gate + confirm flow
- [ ] Command + CanRepair + button
- [ ] Green

## Task 3: Diagnostics finding (optional small)

- [ ] `orphan_dual_layout` when disabled && (J\|\|L)
- [ ] One export test
