# Signal Station Starry PPTX Preview — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a 16-slide starry-sky `.pptx` preview deck under `demos/signal-station-pptx/` that auto-advances in sync with the user's local Travelers' encore track.

**Architecture:** `timing.json` is the single source of truth for cues/copy. `build_deck.py` creates an empty 16:9 deck (static twin first), paints starfield/type/glow with `python-pptx`, then post-processes OOXML for transition timings, cross-slide audio on slide 1, and key-page appear/fade staggers. MP3 is copied into gitignored `media/` at build time.

**Tech Stack:** Python 3, `python-pptx`, stdlib `zipfile`/`xml.etree` for OOXML, optional PowerPoint COM only if needed for audio verification.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-11-signal-station-pptx-preview-design.md`
- No Mod Manager app code changes; leave `demos/bocchi-ppt-demo/` alone
- No Outer Wilds names/art in slide copy; no committing `media/*.mp3`
- Default audio source: `d:\KuGou\Andrew Prahlow - Travelers' encore.mp3`
- Widescreen 13.333×7.5 in; ease-out feel; iris only on slide 11
- No Commit steps

---

## File Map

| File | Role |
|------|------|
| `demos/signal-station-pptx/timing.json` | 16 cues + copy + motion class |
| `demos/signal-station-pptx/build_deck.py` | Build + OOXML inject |
| `demos/signal-station-pptx/.gitignore` | `media/`, optionally `out/*.pptx` |
| `demos/signal-station-pptx/README.md` | Build/play/copyright |
| `demos/signal-station-pptx/media/` | Build-time mp3 (ignored) |
| `demos/signal-station-pptx/out/信号站-SIGNAL-preview.pptx` | Output |

---

### Task 1: Scaffold + timing.json + gitignore + README

**Files:** Create the paths above (except pptx).

- [ ] Write `timing.json` with 16 entries: `i`, `start_sec`, `hold_sec`, `role`, `title`, `subtitle`, `motion`, `pattern`
- [ ] Write `.gitignore` ignoring `media/` and `out/*.pptx`
- [ ] Write README with build command, source path override env `SIGNAL_AUDIO`, copyright note
- [ ] Verify holds sum ≈ 229.5

**Accept:** JSON loads; sum of holds within 0.5s of 229.5

---

### Task 2: Static twin generator

**Files:** `build_deck.py`

- [ ] Create presentation 13.333×7.5 in, blank layout
- [ ] Paint each slide: gradient-ish dark rect, scattered star ellipses, optional amber glow, title/subtitle, page index
- [ ] Write to `out/信号站-SIGNAL-preview.pptx` without audio/anim first
- [ ] Run once; open file size > 20KB

**Accept:** 16 slides present; no crash

---

### Task 3: Auto-advance + audio mount

- [ ] Copy mp3 → `media/travelers-encore.mp3` (fail clearly if missing)
- [ ] Inject `p:transition advTm="{hold_ms}"` per slide (last slide may omit auto or use remaining)
- [ ] Embed audio on slide 1 playing across slides (OOXML media part + timing, or documented COM fallback)
- [ ] Rebuild and confirm `[Content_Types].xml` lists audio and media part exists

**Accept:** Unzip pptx shows `ppt/media/*.mp3`; slide XMLs contain `advTm`

---

### Task 4: Key-page motion inject

- [ ] Slides 2–3: fragment title into runs/shapes and stagger appear ~330–660ms
- [ ] Slide 6 / 9: chapter hammer fade+slight rise
- [ ] Slide 11: circular iris approximation (large circle grow + fade under content, or wedge wipe if grow unreliable)
- [ ] Slide 13: three cards stagger
- [ ] Keep other slides soft fade enter only

**Accept:** Slide 2/11/13 XML contain `p:timing` / `p:animEffect` or `p:anim` nodes

---

### Task 5: Verify + polish

- [ ] Rebuild end-to-end
- [ ] Print cue table + output path
- [ ] README matches actual CLI
- [ ] Confirm git status does not require adding mp3

**Accept:** `python build_deck.py` exits 0; README accurate
