# Design: Repo layout Step 2 (artifacts + docs + packaging)

Date: 2026-09-15  
Status: approved for spec; implementation pending user review of this file

## Goal

Finish the two-step repo cleanup: remove bulky local build artifacts, reorganize `docs/` into clear `user` / `release` / `dev` sections, and move installer sources under `packaging/installer/` while **keeping** root `release/` (update-pointer contract for GitHub raw).

## Decisions (locked)

- Approach: **Step 2 option 2** — orderly + safe (not full packaging one-pot).
- Artifacts: **hybrid** — archive newest 1–2 Setup builds, delete the rest of `dist/`; **delete** `publish/` (re-publish anytime).
- Keep Setup versions in archive: **v1.2.3** and **v1.2.4**.
- Archive root: `D:\gongzuo\_archived-from-mod-manager\2026-09-15-artifacts\`
- Docs layout: **three columns** — `docs/user/`, `docs/release/`, `docs/dev/`.
- Packaging: move `installer/` → `packaging/installer/`; **do not** move root `release/`.
- Do not change app `UpdateChecker` raw URL for `release/latest.json`.
- Historical links inside old `docs/dev/superpowers/**` plans may stay; update **active** release docs and build entry points.

## Target tree (after Step 2)

```
src/  tests/  MechabellumModManager.sln  Directory.Build.props
release/latest.json  release/README.md
packaging/installer/          # former installer/
docs/
  README.md
  user/                       # end-user guides
  release/                    # shipping / GitHub Release docs
  dev/
    superpowers/
    i18n/
    official-requests/
```

Absent from the working tree: `publish/`, `dist/`, root `installer/`.

## File actions

### Artifacts

| Action | Paths |
|--------|--------|
| Delete | Entire `publish/` |
| Copy then delete | Matching `dist/*v1.2.3*` and `dist/*v1.2.4*` Setup files → archive folder, then delete entire `dist/` |

### Docs moves

| Destination | Sources (from current `docs/`) |
|-------------|--------------------------------|
| `docs/user/` | `分发-使用说明.md`, `分发-使用说明.txt`, `国内镜像搭建.md` |
| `docs/release/` | `releasing.md`, `GitHub-Release更新说明.md`, `latest.example.json` |
| `docs/dev/` | `superpowers/`, `i18n/`, `official-requests/` |
| `docs/README.md` | New index pointing at the three columns |

### Packaging

| Action | Paths |
|--------|--------|
| Move | `installer/` → `packaging/installer/` |
| Update refs | `build-installer.ps1` / `.bat` / `.iss` as needed after move; active docs under `docs/release/`; `.vscode` paths mentioning `installer/redist` |
| Keep | Root `release/` unchanged |

### Gitignore

- Keep ignoring `dist/` and `publish/`.
- Adjust any hardcoded `installer/` ignore globs to `packaging/installer/` if present (redist binary ignores).

## Verification

1. Repo has no `publish/`, `dist/`, or root `installer/`.
2. Archive contains v1.2.3 and v1.2.4 Setup files.
3. `docs/{user,release,dev}` and `docs/README.md` exist; root `release/latest.json` still present.
4. `dotnet build MechabellumModManager.sln -c Release` and `dotnet test` succeed.
5. Documented path to `packaging/installer/build-installer.ps1` is accurate.

## Rollback

- Tracked moves: git restore / revert commits.
- Deleted `dist`/`publish`: restore from `2026-09-15-artifacts` archive, re-publish, or GitHub Releases.

## Out of scope

- Moving `release/` under `packaging/` or `docs/`.
- Changing player update URLs or release publishing to a new host.
- Rewriting every historical superpowers plan path.
- Product feature work.

## Success criteria

- Root is scannable: product code, update pointer, packaging sources, docs index.
- Local disk no longer holds full historical Setup trees or stale publish output.
- Installer build entry remains discoverable and path-consistent after the move.
- Step 1 decisions remain intact (`src`/`tests`/`Directory.Build.props`).
