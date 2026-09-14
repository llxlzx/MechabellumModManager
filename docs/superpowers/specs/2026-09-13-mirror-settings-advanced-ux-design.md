# Mirror Settings — Advanced Expander UX

**Date:** 2026-09-13  
**Status:** Approved (conversation)  
**Approach:** Collapse mirror base URL into an Advanced expander; neutral “content mirror” copy (no “domestic”)

## Goal

Settings should not lead with a long COS URL or the word “国内 / Domestic,” which confuses non-CN players. Keep factory COS behavior; improve presentation only.

## Non-goals

- Changing the default COS URL or fetch/fallback logic  
- Removing the ability to customize or clear the mirror  
- Release / version bump (unless requested separately)

## UI

- Remove the always-visible mirror `TextBox` from the main Settings grid.  
- Add an **Expander** titled **高级 / Advanced** (default **collapsed**).  
- **Collapsed header / summary line** (derived from current `MirrorBaseUrl`):
  - Factory default URL → “内容加速镜像：已启用（出厂默认）” / “Content mirror: on (factory default)”
  - Non-empty custom URL → “内容加速镜像：自定义” / “Content mirror: custom”
  - Empty string (opt-out) → “内容加速镜像：已关闭（仅 GitHub）” / “Content mirror: off (GitHub only)”
- **Expanded content:**
  - Label: 镜像基址 / Mirror base URL  
  - Editable `TextBox` bound to `MirrorBaseUrl` (LostFocus save, same as today)  
  - Hint: factory CDN mirror; clear = GitHub only; same layout as repos  

## Copy (all locales)

| Key | Purpose |
|-----|---------|
| `SettingsAdvanced` | Expander header |
| `MirrorSummaryOnDefault` | Collapsed summary when factory URL |
| `MirrorSummaryCustom` | Collapsed summary when custom URL |
| `MirrorSummaryOff` | Collapsed summary when cleared |
| `MirrorBaseUrl` | Field label (no “国内”) |
| `MirrorBaseUrlHint` | Field hint (no “国内”) |

English avoids “Domestic / Inland.” ZH uses “内容加速镜像,” not “国内镜像.”

## Behavior (unchanged)

- `null` → apply factory `DomesticMirrorDefaults.BaseUrl` (internal type name may stay).  
- `""` → GitHub only.  
- Non-empty → use that base.  
- `ApplyMirrorBaseUrl` / catalog / update checker wiring unchanged.

## Implementation notes

- ViewModel: expose `MirrorSummaryText` (or similar) computed from normalized `MirrorBaseUrl` vs factory default.  
- Refresh summary when `MirrorBaseUrl` changes.  
- Prefer existing Expander / muted text styles in Settings.

## Acceptance

1. Settings default view shows Advanced collapsed with a clear status line, no raw COS URL.  
2. Expanding shows the editable base URL and hint.  
3. Clear field → summary “off (GitHub only)”; restore factory URL → “on (factory default)”.  
4. EN/RU/JA/DE strings do not use “domestic”-style wording.  
5. Catalog / update / Melon redist fetch still use the same mirror rules.
