# UI Density Layout Phase 1 (+ Phase 2 outline)

**Date:** 2026-09-05  
**Status:** Self-checked; Phase 1 implementing  
**Style:** A — refine industrial (charcoal + amber); Phase 1 density only

## Self-check (logic)

| Topic | Verdict | Better approach |
|-------|---------|-----------------|
| Catalog `*` list | Already fixed (was MaxHeight 180 void) | Keep; no duplicate page title strip |
| Library same pattern | Needed — list already `*`, detail `MaxHeight=180` OK | Tighten title+filter to one toolbar row |
| Fixed log `120px` | Too tall on maximized window | **GridSplitter** above log + default **~72–80px** (user than collapse toggle) |
| Always-on risk banner | Keep (legal) | No change Phase 1 |
| Warning stack | OK if empty children collapse | Ensure no empty MinHeight on parent |
| Header two rows | Keep profile+launch on all pages | Compress padding only |
| Log collapse toggle | Optional complexity | **Skip** — splitter covers it |

## Phase 1 scope (implement now)

1. **Root shell:** Content `*` → thin **GridSplitter** → Log default height **80** (`MinHeight=48`, user-resizable).
2. **Header:** Reduce padding/margins (~12,8); keep nav + profile + launch.
3. **Catalog:** Keep current Grid (toolbar / list* / detail / status); minor margin align with library.
4. **Library:** Single toolbar row (title + imports + apply); filters row; list `*` (no DataGrid MaxHeight); detail Auto capped ~180.
5. **Settings:** Slightly tighter outer margin/padding inside ScrollViewer.
6. **Warnings row:** No extra MinHeight when empty.

## Phase 1 non-goals

- Color/font/glow/icon redesign (Phase 2)
- Changing exclusive page model
- Moving language out of Settings
- Removing risk banner

## Phase 2 outline (later)

Unified corner radii, border weights, selected-row chrome, empty states, subtle motion on page switch / log — **same information architecture**.

## Success criteria

- [ ] Catalog/Library lists grow with window; no large empty band above a capped grid
- [ ] Log shorter by default; user can drag taller via splitter
- [ ] Header + pages feel denser without cutting actions
- [ ] Build + existing tests still pass
