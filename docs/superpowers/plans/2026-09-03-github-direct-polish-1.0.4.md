# GitHub-direct polish + 1.0.4 package

> **For agentic workers:** Implement task-by-task. User approved all improvement suggestions + installer rebuild.

**Goal:** Ship Issue templates, submit guide dialog, catalog CI, clipboard URL, `report` label wiring, release notes, and rebuild Setup 1.0.4.

**Architecture:** Manager opens GitHub with templates/labels; MechabellumMods owns templates + CI; packaging stays on existing `build-installer.ps1`.

**Tech Stack:** WPF .NET 8, GitHub Issue templates, GitHub Actions + Python validator, Inno Setup.

## Global Constraints

- App version stays **1.0.4** (fold into existing release numbering).
- No Cloudflare relay.
- TDD for URL/clipboard/catalog validator logic where unit-testable.
- MelonLoader zip required for real installer build.

---

## Task 1: MechabellumMods Issue template + report label docs

- [ ] Add `.github/ISSUE_TEMPLATE/mod_report.md` (and optional `config.yml`)
- [ ] README: create `report` label; document template
- [ ] Try `gh label create report` if auth works

## Task 2: Manager URL uses template + labels

- [ ] `BuildReportIssueUrl` adds `labels=report` and `template=mod_report.md` when compatible; prefer title+body+labels (markdown template still selectable on repo)
- [ ] Tests updated

## Task 3: Submit guide dialog (3 steps)

- [ ] `SubmitGuideDialog` with localized 3 steps
- [ ] `SubmitMod` shows dialog then opens ContributeGuideUrl
- [ ] Strings in all locales + TSV

## Task 4: Clipboard on report success

- [ ] After open success, copy Issue URL; success string mentions clipboard
- [ ] Fail soft if clipboard fails (still show opened success + log URL)

## Task 5: Catalog CI

- [ ] `MechabellumMods/.github/workflows/validate-catalog.yml`
- [ ] `scripts/validate_catalog.py` — ids unique, file paths exist, required fields

## Task 6: Release notes + installer

- [ ] Update `docs/GitHub-Release更新说明.md` with cancel-relay / GitHub-direct
- [ ] `.\installer\build-installer.ps1`
- [ ] Refresh `release/v1.0.4/` artifacts (Setup, 本体, portable, latest.json notes if needed)
