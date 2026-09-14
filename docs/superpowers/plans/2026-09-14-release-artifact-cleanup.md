# Release Artifact Cleanup Implementation Plan

> **For agentic workers:** Execute task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove historical `release/` binaries from the git tree and (locally) from history, keeping only `latest.json` + `README.md`, without force-pushing until the user confirms.

**Architecture:** Phased ops: WIP backup → align to `origin/master` on branch `chore/release-artifact-cleanup` → reversible `git rm` commit → local `git filter-repo` + gc → stop for size review. No remote force push in this plan.

**Tech Stack:** git, git-filter-repo, PowerShell

**Working branch:** `chore/release-artifact-cleanup`

---

### Task 0: Protect WIP and baseline

**Files:** none (git ops only); may create `backup/wip-before-release-cleanup-20260914`

- [ ] Record baseline: `.git` size MB, `release/` size MB, `git status -sb`, `git rev-parse master origin/master`
- [ ] On main worktree: create branch `backup/wip-before-release-cleanup-20260914` from current HEAD; `git add -A`; commit with message `backup: WIP before release artifact cleanup` (authorizes this backup commit only)
- [ ] In `.worktrees/pack-122` if dirty: same pattern on `backup/wip-pack122-before-release-cleanup-20260914` or commit onto existing branch as backup commit
- [ ] Bare clone backup to `D:\gongzuo\MechabellumModManager-backup-20260914.git` (outside repo)
- [ ] `git worktree list` then remove each `.worktrees/*` worktree after confirming clean/backed up
- [ ] Verify backup branch exists and bare clone is non-empty

### Task 1: Align cleanup branch

- [ ] `git fetch origin`
- [ ] From main repo (no extra worktrees): `git checkout -B chore/release-artifact-cleanup origin/master`
- [ ] Confirm clean enough for release ops: `git status -sb` shows branch tracking intent; working tree should be clean relative to origin/master (WIP is on backup branch)
- [ ] Confirm `release/latest.json` on this branch matches version 1.2.4

### Task 2: Reversible tree cleanup + gitignore

**Files:**
- Delete tracked paths under `release/v*` and `release/_release_notes`
- `.gitignore` — ignore future release binaries
- Optionally leave local dirs `release/v1.2.3` and `release/v1.2.4` on disk untracked

- [ ] `git rm -r` all tracked `release/v*` and `release/_release_notes` (keep `release/latest.json`, `release/README.md`)
- [ ] Delete untracked old version dirs except `v1.2.3` and `v1.2.4`
- [ ] Update `.gitignore` to ignore `release/**/*.exe`, `release/**/*.zip`, and `release/v*/` while allowing `!release/latest.json` and `!release/README.md` if needed
- [ ] Commit: `chore: stop tracking historical release binaries`
- [ ] Verify `git ls-files release` is only `latest.json` + `README.md`

### Task 3: Local history rewrite (no push)

- [ ] Install `git-filter-repo` if missing (`pip install git-filter-repo`)
- [ ] Run filter-repo to remove from history all `release/**` except `release/latest.json` and `release/README.md`
- [ ] Re-add `origin` remote if filter-repo removed it: `https://github.com/llxlzx/MechabellumModManager.git`
- [ ] `git reflog expire --expire=now --all` and `git gc --prune=now`
- [ ] Report before/after `.git` size; **do not push**

### Task 4: Stop for user confirmation

- [ ] Summarize: backup locations, branch name, `.git` size before/after, `git ls-files release`, remote untouched
- [ ] Ask whether to proceed with Phase 4 force push (not authorized by this plan)
