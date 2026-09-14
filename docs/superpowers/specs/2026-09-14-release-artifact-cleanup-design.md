# Design: release/ artifact cleanup (safe, phased)

Date: 2026-09-14  
Status: approved for execution (local only; no force push until explicit go-ahead)

## Goal

Shrink the repo by removing historical Setup/portable binaries under `release/`, without risking player update channels or uncommitted WIP.

## Policy (safest)

- **In git:** keep only `release/latest.json` and `release/README.md`.
- **On disk (optional):** may keep `release/v1.2.3/` and `release/v1.2.4/` locally; **do not** re-commit exe/zip into git (GitHub Releases already host them).
- **Delete:** other `release/v*`, `release/_release_notes` (tracked + untracked on disk).
- **Remote:** no force push in this session unless the user later explicitly confirms after size verification.

## Why this is safe for players

`UpdateChecker` reads `release/latest.json` from raw GitHub; `setupUrl` / `portableUrl` point at **GitHub Release assets**, not at blobs inside the git tree. Removing old `release/v*` paths does not break download URLs.

## Preconditions (blocking)

1. Local `master` was **behind `origin/master` by 5** — rewrite base must be `origin/master`, not stale local master.
2. Working tree and `.worktrees/pack-122` contain substantial WIP — must be backed up before any reset/worktree removal/`filter-repo`.
3. `v1.2.3` / `v1.2.4` were never in git history (untracked only) — “keep last two versions in history” is impossible; history purge removes through `v1.2.1` and earlier tracked packages.

## Phases

### Phase 0 — Protect

- Snapshot main-repo WIP onto a backup branch (or equivalent patch).
- Snapshot `pack-122` worktree WIP the same way if dirty.
- Create an **external bare clone** backup of the full repo (outside the working tree).
- Record baseline sizes: `.git/`, `release/`.
- Remove linked worktrees only after WIP is confirmed saved.

### Phase 1 — Align

- `git fetch origin`.
- Point cleanup work at **`origin/master`** (clean tree for release paths).

### Phase 2 — Reversible tree cleanup

- `git rm -r` tracked old release package paths and `_release_notes`.
- Delete leftover untracked old `release/v*` dirs from disk (optional keep only local v1.2.3 / v1.2.4).
- Tighten `.gitignore` so future `release/**/*.exe` / `*.zip` are not re-added (keep `latest.json` / `README.md` trackable).
- Single ordinary commit (revertable with `git revert`). **Do not push** unless user asks.

### Phase 3 — Local history rewrite (size)

- Install `git-filter-repo` if missing.
- Run filter to strip historical paths under `release/` except `latest.json` and `README.md`.
- `reflog expire` + `gc --prune=now`.
- Report before/after `.git` size. **Stop. No force push.**

### Phase 4 — Remote (out of scope until confirmed)

- Only after user reviews sizes and explicitly authorizes: force-push rewritten history (and tags if rewritten). Not part of the default “start” run.

## Out of scope

- Deleting `dist/`, `docs/superpowers` history docs, or unrelated code cleanup.
- Changing update-check URLs or release publishing to GitHub Releases.
- Rewriting `git config`.

## Success criteria

- Git tree: `release/` contains only pointer + README (plus ignore rules).
- Phase 2 commit is revertible with normal git.
- Phase 3 shows a clear `.git` size drop; remote unchanged until Phase 4 authorization.
- WIP backup branch/patch still recoverable.
