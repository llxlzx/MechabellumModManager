# Repo Layout Step 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean non-product leftovers from the repo and add shared `Directory.Build.props` without moving packaging/docs or splitting projects.

**Architecture:** Hybrid delete/archive of `tools`/`_samples`/`relay`/dev residue; keep `src/` + `tests/` + root `.sln`; share Nullable/ImplicitUsings/LangVersion via root Directory.Build.props.

**Tech Stack:** git, PowerShell, .NET 8 / MSBuild

**Working branch:** `chore/release-artifact-cleanup` (or `chore/repo-layout-step1` if branched later)

**Spec:** `docs/superpowers/specs/2026-09-14-repo-layout-step1-design.md`

---

### Task 1: Archive tools + delete dead weight

**Files:** `tools/`, `_samples/`, `relay/`, `.worktrees/`, `.review-phase0-base-wt/`

- [x] Create `D:\gongzuo\_archived-from-mod-manager\2026-09-14\tools\`
- [x] Move: `add-resource-key.ps1`, `publish-mods-release.ps1`, `sync-mirror.ps1`, `verify-mirror.ps1`, `mirror-hot.txt`
- [x] Delete: `tools/check-cursor-shell.ps1`, empty `tools/`, `_samples/`, `relay/`, `.worktrees/`, `.review-phase0-base-wt/`
- [x] Verify archive has 5 files; repo paths absent
- [x] Commit: `chore: archive tools and remove dead sample/relay residue`

### Task 2: Directory.Build.props + csproj dedupe + gitignore

**Files:** `Directory.Build.props` (new), `src/.../MechabellumModManager.csproj`, `tests/.../MechabellumModManager.Tests.csproj`, `.gitignore`

- [x] Add root `Directory.Build.props` with Nullable, ImplicitUsings, LangVersion=latest
- [x] Remove duplicated Nullable/ImplicitUsings from both csproj files
- [x] Ensure `.gitignore` still ignores `.worktrees/`; no need to resurrect tools/
- [x] Commit: `build: share common MSBuild defaults via Directory.Build.props`

### Task 3: Verify build and tests

- [x] `dotnet build MechabellumModManager.sln -c Release`
- [x] `dotnet test MechabellumModManager.sln -c Release --no-build` (or build+test)
- [x] Confirm diff scope excludes installer/docs/release/publish/dist tree moves
