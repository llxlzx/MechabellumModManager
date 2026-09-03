# MelonLoader Offline Redist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Formal Setup builds hard-fail without embedded `MelonLoader.x64.zip`; install prefers local zip; GitHub download failures show clear VPN/manual hints.

**Architecture:** Keep existing `Install-MelonLoader.ps1` local-first path. Gate `build-installer.ps1`/`.bat` on zip presence (hard fail; `-SkipMelonRedistCheck` for debug only). Update Inno component/status/error copy and releasing docs.

**Tech Stack:** PowerShell, Inno Setup 6, existing installer scripts.

## Global Constraints

- Do not commit large binaries to git (`.gitignore` unchanged for `installer/redist/**/*.zip`).
- No third-party GitHub mirrors.
- Official zip name: `MelonLoader.x64.zip` under `installer/redist/melonloader/`.
- Hard-fail build when zip missing/empty unless `-SkipMelonRedistCheck`.
- Spec: `docs/superpowers/specs/2026-09-03-melonloader-offline-redist-design.md`

## File map

| File | Role |
|------|------|
| `installer/build-installer.ps1` | Pre-ISCC hard check + switch |
| `installer/build-installer.bat` | Same check for bat path |
| `installer/MechabellumModManager.iss` | Component + status + MsgBox copy |
| `installer/scripts/Install-MelonLoader.ps1` | Download fail message + log hints |
| `installer/redist/README.md` | Melon required for release |
| `docs/releasing.md` | Mandatory Melon zip step |

---

### Task 1: Build hard-fail gate

- [ ] In `build-installer.ps1`, add `param([switch]$SkipMelonRedistCheck)` at top (keep `Set-Location` to repo root).
- [ ] After ensuring redist dirs exist, before ISCC: if not skip, require `installer\redist\melonloader\MelonLoader.x64.zip` exists and `Length -gt 0`; else Write-Error with download URL `https://github.com/LavaGang/MelonLoader/releases` and `exit 3`.
- [ ] Mirror check in `build-installer.bat` (no skip unless `set SKIP_MELON_REDIST_CHECK=1`); missing → echo error, `exit /b 3`.
- [ ] Manual test: rename/remove zip → ps1 exits 3; restore → continues (may need zip present for full ISCC).

### Task 2: Installer UX copy

- [ ] ISS component `melon` Description: mention embedded offline zip, usually no GitHub needed.
- [ ] In `CurStepChanged` melon branch: status text for local install; on failure MsgBox include GitHub/proxy/manual install text + releases URL.
- [ ] `Install-MelonLoader.ps1`: when downloading, log GitHub-access hint; on catch, Write-Error with same guidance (Chinese OK in host messages; keep English Write-Error consistent with file or bilingual one paragraph).

### Task 3: Docs

- [ ] `installer/redist/README.md`: MelonLoader zip **required for release builds**; dotnet still optional.
- [ ] `docs/releasing.md`: step “download MelonLoader.x64.zip into redist/melonloader/ before build-installer”; note build hard-fails without it; do not use Skip for release.

### Task 4: Verify + commit

- [ ] Confirm scripts parse; if Melon zip available on machine, optional full build.
- [ ] Commit with message: `Require MelonLoader offline zip for release builds and clarify GitHub failure hints.`
