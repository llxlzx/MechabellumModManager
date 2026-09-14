# Design: Repo layout Step 1 (cleanup + .NET polish)

Date: 2026-09-14  
Status: Step 1 implementation in progress

## Goal

Make the Mechabellum Mod Manager repo root clearly product-focused: keep a conventional `src/` + `tests/` + root `.sln` layout, remove non-product leftovers from the working tree, and share common MSBuild defaults via `Directory.Build.props`. Packaging and docs reorganization is deferred to Step 2.

## Decisions (locked)

- Overall strategy: **two steps**. This document is **Step 1 only**.
- Step 1 approach: **cleanup + standard .NET polish** (not multi-project split).
- `tools/` / `_samples/` / `relay/`: **hybrid** — archive useful scripts outside the repo; delete confirmed dead weight.
- Archive root: `D:\gongzuo\_archived-from-mod-manager\2026-09-14\`
- Do **not** move `installer/`, `docs/`, `release/`, `publish/`, or `dist/` in Step 1.
- Do **not** split `Services` / `Models` into separate class libraries in Step 1.
- Do **not** introduce central package management (`Directory.Packages.props`) in Step 1.

## Step 1 file actions

| Action | Paths |
|--------|--------|
| Delete | `relay/` (DEPRECATED Worker) |
| Delete | `_samples/` (QuickCamera.dll only) |
| Delete | `tools/check-cursor-shell.ps1` |
| Delete | `.worktrees/`, `.review-phase0-base-wt/` |
| Move out | `tools/add-resource-key.ps1`, `tools/publish-mods-release.ps1`, `tools/sync-mirror.ps1`, `tools/verify-mirror.ps1`, `tools/mirror-hot.txt` → archive `...\2026-09-14\tools\` |
| Remove empty | `tools/` directory after moves/deletes |
| Add | root `Directory.Build.props` |
| Tweak | `.gitignore` if needed; `.sln` only if nesting/display needs a fix |
| Keep paths | `src/MechabellumModManager/`, `tests/MechabellumModManager.Tests/`, root `MechabellumModManager.sln` |

## Target root (after Step 1)

```
MechabellumModManager.sln
Directory.Build.props
README.md, LICENSE, NOTICE.md, .gitignore
src/MechabellumModManager/
tests/MechabellumModManager.Tests/
docs/                 # Step 2
installer/, release/, publish/, dist/   # Step 2
.superpowers/         # tooling retained
.git/
```

Absent from the repo after Step 1: `tools/`, `_samples/`, `relay/`, `.worktrees/`, `.review-phase0-base-wt/`.

## Directory.Build.props

Shared at repo root:

- `Nullable` = enable
- `ImplicitUsings` = enable
- Optional: `LangVersion` = latest

Remove the same properties from both csproj files when duplicated.  
Leave project-specific settings in each csproj: `TargetFramework`, `UseWPF`, `OutputType`, assembly metadata, package references, `InternalsVisibleTo`, test SDK packages.

## Verification

1. Listed delete/move targets are gone from the repo; archive contains the five tool files.
2. `dotnet build MechabellumModManager.sln -c Release` succeeds.
3. `dotnet test` on the solution (or existing primary test project) passes.
4. Diff stays within Step 1 scope (no packaging/docs tree moves).

## Rollback

- Deleted tracked files: recover via git history / prior bare backup if needed.
- Archived tools: copy back from `D:\gongzuo\_archived-from-mod-manager\2026-09-14\tools\`.

## Out of scope (Step 2+)

- Reclassify `docs/`, `installer/`, `release/`, `publish/`, `dist/`
- Multi-project domain split
- Central package version management
- Product behavior or versioning policy changes

## Success criteria

- Root is easier to navigate for app development.
- Build/test still work with unchanged `src`/`tests` project paths.
- Non-product scripts are either archived outside the repo or intentionally deleted.
- Step 2 can start without redoing Step 1 decisions.
