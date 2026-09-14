# UI Visual Polish Phase 2

**Date:** 2026-09-05  
**Status:** Done  
**Style:** A — refine industrial (charcoal + amber); same information architecture as Phase 1

## Scope

1. Unified corner radii (panels/controls ≈ 4) and shared selection brushes
2. DataGrid / ListBox selected-row chrome (amber left edge + fill)
3. Content card style for catalog; PanelChrome on settings
4. Empty states for catalog and library (localized)
5. Subtle opacity fade on page switch
6. Log panel title accent bar

## Non-goals

- Color theme rewrite / heavy glow
- Changing exclusive page model or density layout from Phase 1
- GridSplitter on log (still deferred)

## Success criteria

- [x] Build + existing tests pass (297)
- [x] Empty catalog/library show centered hint
- [x] Selected rows read with amber edge without looking like a new skin
