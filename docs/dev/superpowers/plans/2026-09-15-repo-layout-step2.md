# Repo Layout Step 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Clear `dist`/`publish`, reorganize docs into user/release/dev, move installer to `packaging/installer/`, keep root `release/`.

**Architecture:** Hybrid artifact archive; git mv for docs and installer; update active path references only.

**Tech Stack:** PowerShell, git, .NET 8

**Working branch:** `chore/release-artifact-cleanup`

**Spec:** `docs/superpowers/specs/2026-09-15-repo-layout-step2-design.md`

---

### Task 1: Artifact cleanup

- [ ] Create `D:\gongzuo\_archived-from-mod-manager\2026-09-15-artifacts\`
- [ ] Copy `dist/*v1.2.3*` and `dist/*v1.2.4*` Setup files into archive
- [ ] Delete entire `publish/` and `dist/`
- [ ] Verify archive has both versions; paths gone from repo
- [ ] Commit if any tracked files affected (usually none — both gitignored)

### Task 2: Docs three-column layout

- [ ] Create `docs/user`, `docs/release`, `docs/dev`
- [ ] `git mv` user guides, release docs, and `superpowers`/`i18n`/`official-requests` into place
- [ ] Add `docs/README.md` index
- [ ] Commit: `docs: split docs into user/release/dev`

### Task 3: Move installer → packaging/installer

- [ ] `git mv installer packaging/installer` (create `packaging` first)
- [ ] Fix relative paths in `build-installer.ps1` / `.bat` / `.iss` if broken
- [ ] Update `.gitignore` redist globs; `.vscode` if present and tracked or leave untracked
- [ ] Update active docs under `docs/release/` for `packaging/installer` paths
- [ ] Commit: `chore: move installer under packaging/`

### Task 4: Verify

- [ ] `dotnet build` + `dotnet test` Release
- [ ] Confirm root tree matches spec; `release/latest.json` still at root
