# 宇宙拓荒 · 星辰之子 · 电影感学习教学 PPTX — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `demos/outer-wilds-cinematic-pptx/` into a **9-slide, 78.3 s** cinematic short-poem `.pptx`: polished full-bleed plates (curated game extract + HQ stills + AI refine), a real trailing meteor shower, the user's five "children of the stars" lines verbatim, and the purchased OST **Main Title** — never color-blob starfields.

**Architecture:** Media-first. Curated UnityPy extracts and optional HQ stills become `elements/`; an AI refine pass driven by the per-plate prompt library in this plan produces 16:9 `plates/`; the meteor shower lives as a rendered sequence/video in `fx/`. A thin COM `build_deck.py` lays plates + ghost latin + Chinese lines + FX + audio, with auto-advance from `timing.json` whose nine holds sum to 78.3 s.

**Tech Stack:** Python 3.12, UnityPy, Pillow, pywin32 (PowerPoint COM), pytest; Cursor `GenerateImage` for refine passes; ffmpeg only if present.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-11-outer-wilds-cinematic-pptx-design.md` (approved; revised 20:37 for Main Title + five-line copy).
- Purpose: personal learning/teaching only; no store packaging; never commit `media/*.mp3` or raw game dumps.
- Game dir: `D:\steam\steamapps\common\Outer Wilds`
- OST dir: `D:\steam\steamapps\music\Outer Wilds - Original Soundtrack`
- Main audio: `OST\07 - Main Title.mp3`, measured **78.3 s**. Staged as `media/main-title.mp3`.
- **Copy law:** only the five lines in §Copy below, **verbatim**, one line per scene (lines 3 and 5 may split across two slides at the given break). No other narrative text. Ghost latin words are decorative, not copy.
- **Global craft law (every slide):** polished main visual required; ban color-block stars/planets/meteors, big type on blank/flat plates, flash-without-trail FX, raw full-bleed screenshots as finished plates.
- Meteor FX: ≥ 8 staggered diagonal streaks with visible tails (video/sequence) — not ovals.
- Visual language (spec §5.1): deep-space teal-violet base, one warm campfire-amber focus ≤ 1/4 of frame, soft volumetric light, stardust grain, a built-in sense of motion, and a low-contrast text zone (lower third or one side).
- Prompt deny-list: UI, HUD, watermark, logo, text, low-poly, sticker cartoon, flat color blocks, neon cyberpunk.
- Do not continue `signal-station-pptx` deluxe COM work; do not touch Mod Manager code or `demos/bocchi-ppt-demo/`.
- All shell steps are PowerShell-native (`;` not `&&`, `Select-String` not `grep`).
- Canvas 16:9 = 960×540 pt; plates rendered at 1920×1080.

---

## Copy (verbatim, the only text allowed)

| key | text |
|-----|------|
| `L1` | 我们身体里的铁，来自璀璨的超新星爆炸 |
| `L2` | 我们血液里的锌，源自两次中子星对撞后喷射向宇宙的尘埃 |
| `L3a` | 我们那微量的铜，更是需要见证一颗白矮星的死亡， |
| `L3b` | 即使是最微不足道的钴，也源自几十亿光年外的星云 |
| `L4` | 某种意义上讲，人类对星空怀有好奇，是正常的。 |
| `L5a` | 渴望见证星河大海的极限， |
| `L5b` | 因为我们本就是 星辰的孩子。 |

`L3a+L3b` and `L5a+L5b` concatenate exactly to the user's original sentences.

---

## Frozen slide map (9 slides · 78.3 s · Main Title)

| i | role | hold_s | plate_key | copy | ghost_en | fx |
|---|------|--------|-----------|------|----------|-----|
| 1 | cold_open | 7.0 | `dust_awakening` | — | STARDUST | drift |
| 2 | iron_supernova | 10.0 | `supernova_bloom` | L1 | IRON | bloom |
| 3 | zinc_neutron | 10.0 | `neutron_collision` | L2 | ZINC | spiral |
| 4 | copper_white_dwarf | 9.0 | `white_dwarf_dusk` | L3a | COPPER | embrace |
| 5 | cobalt_far_nebula | 8.0 | `distant_nebula` | L3b | COBALT | slow_push |
| 6 | curiosity_campfire | 9.0 | `campfire_gaze` | L4 | CURIOSITY | ember_pulse |
| 7 | longing_meteor_sea | 10.0 | `meteor_sea` | L5a | LONGING | **meteors** |
| 8 | children_of_stars | 9.0 | `children_of_stars` | L5b | CHILDREN OF THE STARS | breath |
| 9 | ember_afterglow | 6.3 | `ember_afterglow` | — | — | fade_out |

Sum = 7+10+10+9+8+9+10+9+6.3 = **78.3**. Slide 7 **must** host the trailing meteor FX.

---

## Plate prompt library (used verbatim by Task 4; refine iteratively)

Every prompt is prefixed with the shared **STYLE BLOCK**:

> Cinematic, painterly sci-fi inspired by hand-crafted indie space exploration games; deep-space teal-violet palette (#0B1530 to #3B2A6B) with a single warm campfire-amber focal light (#E8B45A / #FF9A3C) occupying at most a quarter of the frame; soft volumetric light, drifting nebula haze, fine stardust particles, subtle film grain; a gentle, breathing sense of motion already present in the composition; keep the lower third (or one side, as noted) low-contrast and quiet for overlaid Chinese typography; 16:9 full-bleed presentation background. No UI, no HUD, no watermark, no logo, no text, no low-poly, no sticker cartoon, no flat color blocks, no neon cyberpunk.

| plate_key | Prompt body (append after STYLE BLOCK) | text zone |
|-----------|----------------------------------------|-----------|
| `dust_awakening` | A single mote of stardust igniting in pure darkness, its light slowly unfurling into a thin veil of galactic mist; near-black field, faint cold-white specks, one tiny warm ember at the point of ignition; immense quiet; the beginning of everything. | whole frame quiet; ghost word centered |
| `supernova_bloom` | The afterglow of a supernova seen from far away: a vast luminous ring expanding outward, filaments of iron-tinted ember-red and gold scattering into cold violet space, shockwave haze catching light; majestic, not violent; the birth of iron. | lower third |
| `neutron_collision` | Two neutron stars at the instant of merging, a spiralling double arm of incandescent dust flung outward into deep space, tidal streams of pale zinc-white and electric blue; a kilonova glow at the core; awe and silence. | lower third |
| `white_dwarf_dusk` | A dying white dwarf, cold pearl-white and tiny, cradled by a slow swirl of copper-rose and violet nebula; the last light of a star fading like dusk; melancholic, tender. | left or lower third |
| `distant_nebula` | A nebula billions of light-years away, a soft lantern of cobalt blue and faint gold hanging in an ocean of dark; the viewer is far, the light is old; a sense of looking back across time. | lower third |
| `campfire_gaze` | Travelers' campfire at the edge of a small forested world under an impossibly deep starry sky; warm amber flame lighting only a corner, stargazers' silhouettes small and unhurried, the sky filling the upper two-thirds with galaxies and slow-drifting stardust; curiosity, belonging. | lower-left near the fire |
| `meteor_sea` | A whole night sky torn open by a meteor shower, dozens of long luminous trails sweeping from upper-right to lower-left over a sea of stars, the Milky Way like surf; a thin horizon of dark hills; longing and wonder; keep the trails painterly and graceful. | lower third (dark hills) |
| `children_of_stars` | An open palm or a campfire ember in the foreground glowing warm amber, and far behind it a nebula glowing the very same color, the two lights answering each other across the void; intimate and cosmic at once. | lower third |
| `ember_afterglow` | The sky slowly darkening to near-black, a single ember on the ground and a single star above it breathing in unison, everything else dissolved into quiet violet dusk; a last breath, peace. | whole frame quiet; ghost word optional |

Refine loop per plate (Task 4): generate → judge against §Global craft law and text-zone rule → if muddy / UI-like / flat / wrong color balance, rewrite the specific failing phrase and regenerate (max 3) → Pillow grade (contrast, vignette, teal-amber unify) → save.

---

## File map

| Path | Role |
|------|------|
| `demos/outer-wilds-cinematic-pptx/.gitignore` | Ignore `media/`, `out/`, `sources/game-extract/`, `__pycache__/`, `_tmp/` |
| `demos/outer-wilds-cinematic-pptx/README.md` | Build/play notes + personal-learning disclaimer |
| `demos/outer-wilds-cinematic-pptx/brief.json` | One-job-per-slide + must_include five lines |
| `demos/outer-wilds-cinematic-pptx/timing.json` | 9 slides, holds sum 78.3 s, roles + plate/fx keys |
| `demos/outer-wilds-cinematic-pptx/copy.json` | The seven copy keys above + ghost_en per slide |
| `demos/outer-wilds-cinematic-pptx/prompts.json` | STYLE BLOCK + per-plate prompt bodies (mirrors this plan) |
| `demos/outer-wilds-cinematic-pptx/sources/web-hq/SOURCES.md` | Notes for any HQ stills used |
| `demos/outer-wilds-cinematic-pptx/tools/extract_textures.py` | Curated UnityPy extract |
| `demos/outer-wilds-cinematic-pptx/tools/measure_audio.py` | MP3 duration helper |
| `demos/outer-wilds-cinematic-pptx/tools/compose_plate.py` | Pillow grade + completeness helpers |
| `demos/outer-wilds-cinematic-pptx/tools/make_meteor_fx.py` | Trailing meteor sequence/video |
| `demos/outer-wilds-cinematic-pptx/tools/audit_craft.py` | Final gates |
| `demos/outer-wilds-cinematic-pptx/deck/__init__.py` | Package marker |
| `demos/outer-wilds-cinematic-pptx/deck/timing_load.py` | Load/validate timing + copy |
| `demos/outer-wilds-cinematic-pptx/deck/extract_filter.py` | Texture name whitelist |
| `demos/outer-wilds-cinematic-pptx/deck/audio_mount.py` | Stage Main Title + COM mount |
| `demos/outer-wilds-cinematic-pptx/build_deck.py` | COM assemble |
| `demos/outer-wilds-cinematic-pptx/tests/test_timing.py` | Hold sum / count / meteor slide |
| `demos/outer-wilds-cinematic-pptx/tests/test_copy.py` | Five lines verbatim, nothing else |
| `demos/outer-wilds-cinematic-pptx/tests/test_extract_filter.py` | Whitelist logic |
| `demos/outer-wilds-cinematic-pptx/tests/test_audio_copy.py` | Main Title resolve |
| `demos/outer-wilds-cinematic-pptx/conftest.py` | sys.path |
| `demos/outer-wilds-cinematic-pptx/plates/*.png` | 9 refined plates (generated) |
| `demos/outer-wilds-cinematic-pptx/fx/meteors.*` | Trailing meteor FX (generated) |
| `demos/outer-wilds-cinematic-pptx/out/星辰之子-学习教学.pptx` | Deliverable |

---

### Task 1: Scaffold + timing/copy/prompts JSON + validators

**Files:**
- Create: `conftest.py`, `.gitignore`, `deck/__init__.py`, `deck/timing_load.py`, `tools/measure_audio.py`
- Create: `timing.json`, `copy.json`, `brief.json`, `prompts.json`
- Create: `tests/test_timing.py`, `tests/test_copy.py`

**Interfaces:**
- Produces: `measure_mp3_duration(path: Path) -> float`; `load_timing(path) -> dict`; `validate_timing(data) -> list[str]`; `load_copy(path) -> dict`; `copy_is_exactly_five_lines(copy) -> list[str]` (returns violations).

- [ ] **Step 1: Failing tests**

```python
# tests/test_timing.py
from pathlib import Path
from deck.timing_load import load_timing, validate_timing
ROOT = Path(__file__).resolve().parents[1]

def test_nine_slides_sum_to_main_title():
    data = load_timing(ROOT / "timing.json")
    assert validate_timing(data) == []
    assert len(data["slides"]) == 9
    assert abs(sum(s["hold_sec"] for s in data["slides"]) - 78.3) < 0.05

def test_meteor_slide_is_seven():
    data = load_timing(ROOT / "timing.json")
    s7 = [s for s in data["slides"] if s["i"] == 7][0]
    assert s7["fx"] == "meteors"
```

```python
# tests/test_copy.py
from pathlib import Path
from deck.timing_load import load_copy, copy_is_exactly_five_lines
ROOT = Path(__file__).resolve().parents[1]

def test_copy_is_the_five_lines_and_nothing_else():
    copy = load_copy(ROOT / "copy.json")
    assert copy_is_exactly_five_lines(copy) == []
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
Set-Location demos/outer-wilds-cinematic-pptx; python -m pytest tests/test_timing.py tests/test_copy.py -q
```

- [ ] **Step 3: Implement**

`deck/timing_load.py`:
```python
from __future__ import annotations
import json
from pathlib import Path

FIVE_LINES = {
    "L1": "我们身体里的铁，来自璀璨的超新星爆炸",
    "L2": "我们血液里的锌，源自两次中子星对撞后喷射向宇宙的尘埃",
    "L3a": "我们那微量的铜，更是需要见证一颗白矮星的死亡，",
    "L3b": "即使是最微不足道的钴，也源自几十亿光年外的星云",
    "L4": "某种意义上讲，人类对星空怀有好奇，是正常的。",
    "L5a": "渴望见证星河大海的极限，",
    "L5b": "因为我们本就是 星辰的孩子。",
}
FORBIDDEN = ["鹿人", "挪麦", "哈斯", "余烬双星", "黑棘", "穷举", "幽灵物质"]

def load_timing(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))

def validate_timing(data: dict) -> list[str]:
    errs: list[str] = []
    slides = data.get("slides") or []
    if len(slides) != 9:
        errs.append(f"expected 9 slides, got {len(slides)}")
    total = round(sum(float(s["hold_sec"]) for s in slides), 3)
    if abs(total - float(data["audio_duration_sec"])) > 0.05:
        errs.append(f"hold sum {total} != audio {data['audio_duration_sec']}")
    if not any(s.get("i") == 7 and s.get("fx") == "meteors" for s in slides):
        errs.append("slide 7 must carry meteors fx")
    for s in slides:
        if not s.get("plate_key"):
            errs.append(f"slide {s.get('i')} missing plate_key")
    return errs

def load_copy(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))

def copy_is_exactly_five_lines(copy: dict) -> list[str]:
    errs: list[str] = []
    lines = copy.get("lines") or {}
    for k, v in FIVE_LINES.items():
        if lines.get(k) != v:
            errs.append(f"{k} missing or altered")
    extra = set(lines) - set(FIVE_LINES)
    if extra:
        errs.append(f"unexpected copy keys: {sorted(extra)}")
    blob = json.dumps(copy, ensure_ascii=False)
    for w in FORBIDDEN:
        if w in blob:
            errs.append(f"forbidden narrative term present: {w}")
    return errs
```

`timing.json`: `{"title":"星辰之子","ost_track":"07 - Main Title.mp3","audio_duration_sec":78.3,"slides":[...frozen map...]}` — each slide has `i, role, hold_sec, plate_key, copy_key (or null), ghost_en, fx`.

`copy.json`: `{"lines": {...FIVE_LINES...}, "slides": {"2":"L1","3":"L2","4":"L3a","5":"L3b","6":"L4","7":"L5a","8":"L5b"}}`.

`prompts.json`: `{"style_block": "<STYLE BLOCK text>", "plates": {"dust_awakening": {"body": "...", "text_zone": "..."}, ...}}` copied from the prompt library.

`brief.json`: `{"mode":"A","purpose":"personal_learning","audio":"OST/07 - Main Title.mp3","must_include":["five stellar lines verbatim","true trailing meteor shower on slide 7","campfire warmth vs deep-space cool"],"must_exclude":["color-block stars","flash FX","raw screenshots as plates","any narrative beyond the five lines"]}`.

- [ ] **Step 4: Run — expect PASS**

---

### Task 2: Curated UnityPy extract

**Files:** `deck/extract_filter.py`, `tools/extract_textures.py`, `tests/test_extract_filter.py`, `sources/web-hq/SOURCES.md`

**Interfaces:** `name_is_useful(name: str) -> bool`; `extract_curated(game_data_dir: Path, out_dir: Path, limit: int = 60) -> list[Path]`

- [ ] **Step 1: Failing test**

```python
from deck.extract_filter import name_is_useful

def test_whitelist_denylist():
    assert name_is_useful("Skybox_Stars_Albedo")
    assert name_is_useful("Campfire_Ember")
    assert name_is_useful("Nebula_Cloud")
    assert not name_is_useful("UI_Prompt_A")
    assert not name_is_useful("FontAtlas_Latin")
```

- [ ] **Step 2: Implement**

Whitelist (ci): `sky`, `star`, `nebula`, `sun`, `camp`, `fire`, `ember`, `planet`, `timber`, `hearth`, `cloud`, `dust`, `glow`, `aurora`, `ring`, `comet`. Deny: `ui_`, `font`, `cursor`, `prompt`, `icon`, `logo`, `text`, `button`.

Extractor exports Texture2D/Sprite matches as PNG to `sources/game-extract/`, dedupes by name, **cap 60**, writes `_index.txt`.

- [ ] **Step 3: Run extract**

```powershell
Set-Location demos/outer-wilds-cinematic-pptx
python tools/extract_textures.py --game "D:\steam\steamapps\common\Outer Wilds\OuterWilds_Data" --out sources/game-extract --limit 60
```

Expected: `_index.txt` present; ≥ 10 PNGs; no mass dump.

- [ ] **Step 4: Triage** — append a "keep for plates" shortlist (≥ 8 names) to `_index.txt` after visual inspection; prefer sky/nebula/campfire/star textures.

---

### Task 3: HQ still fallback (only for gaps)

- [ ] **Step 1: Gap check** against the nine plate_keys: do we have campfire-with-sky, nebula, supernova-like glow, meteor-sky references? Missing → search **HQ** gameplay stills (≥ 1920 px wide preferred; no blur, no heavy HUD, no watermark).
- [ ] **Step 2:** Save under `sources/web-hq/`; one `SOURCES.md` row each (file, where found, date, why kept).
- [ ] **Step 3:** These are **raw reference only** for crops/refine — never a finished plate.

---

### Task 4: Element crops + AI-refined plates (the craft core)

**Files:** `tools/compose_plate.py`, `elements/*.png`, `plates/<plate_key>.png` ×9

**Interfaces:** `grade(img_path: Path, out_path: Path, warm_hex="#E8B45A", cool_hex="#0B1530") -> Path`; `missing_plates(timing_path, plates_dir) -> list[str]`

- [ ] **Step 1:** Load `prompts.json`; assert 9 keys match `timing.json` plate_keys.
- [ ] **Step 2: Per plate — refine loop**
  1. Pick 1–3 references (extract shortlist / web-hq) and crop **elements** (subject, glow, dust) to `elements/<plate_key>_*.png`.
  2. `GenerateImage` with `STYLE BLOCK + body`, `aspect_ratio: "16:9"`, pass crops as `reference_image_paths` where helpful.
  3. Judge against Global craft law + text-zone rule; if failing, rewrite the failing phrase and regenerate (max 3).
  4. `grade()` → 1920×1080 → `plates/<plate_key>.png`.
  5. Record a one-line verdict per plate in `plates/_verdicts.md` (what was rejected and why).
- [ ] **Step 3: Completeness**

```powershell
python -c "from pathlib import Path; from tools.compose_plate import missing_plates; m=missing_plates(Path('timing.json'), Path('plates')); print('missing', m); raise SystemExit(1 if m else 0)"
```

Expected: `missing []`.

---

### Task 5: Real trailing meteor shower

**Files:** `tools/make_meteor_fx.py`, `fx/meteors.mp4` or `fx/meteors_frames/*.png`

- [ ] **Step 1: Acceptance (write into the script docstring):** ≥ 8 streaks, upper-right → lower-left, varying length/alpha/speed, staggered starts across ~8 s, gaussian-soft tails, black background for screen/add blend or alpha PNGs.
- [ ] **Step 2: Render** with Pillow: per frame draw tapered polylines with additive glow; 24 fps × 8 s; optionally assemble `meteors.mp4` with ffmpeg if available.
- [ ] **Step 3: Spot-check** 3 frames + playback; must read as meteors crossing the sky, not flashing ovals.

---

### Task 6: Audio staging

**Files:** `deck/audio_mount.py`, `tests/test_audio_copy.py`

**Interfaces:** `resolve_main_title(ost_root: Path) -> Path`; `stage_media(ost_root: Path, media_dir: Path) -> Path` (→ `media/main-title.mp3`)

- [ ] **Step 1: Failing test**

```python
from pathlib import Path
from deck.audio_mount import resolve_main_title

def test_resolve_main_title():
    p = resolve_main_title(Path(r"D:\steam\steamapps\music\Outer Wilds - Original Soundtrack"))
    assert p.name == "07 - Main Title.mp3" and p.is_file()
```

- [ ] **Step 2: Implement** (`OST/07 - Main Title.mp3`; `shutil.copy2` → `media/main-title.mp3`).
- [ ] **Step 3:** pytest PASS; run stage once; confirm `git check-ignore media/main-title.mp3` prints the path.

---

### Task 7: COM build

**Files:** `build_deck.py`

**CLI:** `python build_deck.py --out "out/星辰之子-学习教学.pptx" [--subset 1,6,7,9]`

- [ ] **Step 1: Implement**
  1. `Presentations.Add()`, 16:9.
  2. Per slide: blank layout; full-bleed `plates/<plate_key>.png`.
  3. Ghost latin (`ghost_en`): large, 70–85 % transparent, placed behind copy.
  4. Chinese line from `copy.json` in the plate's text zone, ≥ 24 pt, with soft shadow for legibility; slides 1 and 9 carry no Chinese copy.
  5. Slide 7: insert `fx/meteors.mp4` (or frame sequence) over the sky region, play automatically, loop until advance.
  6. Per-slide motion per spec §5.2 (slow drift / bloom / breath) using image-layer motion, not shape flashes.
  7. Transitions: FadeSmoothly; Morph only where the same plate element persists.
  8. `AdvanceOnTime=True`, `AdvanceTime=hold_sec`.
  9. Mount `media/main-title.mp3` on slide 1, play across slides, hide icon.
  10. Use live COM constants (`win32com.client.constants`), never hard-coded enums.
- [ ] **Step 2: Build full + subset**

```powershell
Set-Location demos/outer-wilds-cinematic-pptx
python build_deck.py --out "out/星辰之子-学习教学.pptx"
python build_deck.py --subset 1,6,7,9 --out "out/星辰之子-m0-preview.pptx"
```

- [ ] **Step 3:** Open in PowerPoint; confirm no blank or color-blob slide.

---

### Task 8: Human checkpoint

- [ ] **Step 1:** Report both pptx paths and `plates/_verdicts.md` summary to the user. Stop.
- [ ] **Step 2:** On user continue, apply polish requests before Task 9.

---

### Task 9: README + craft audit

**Files:** `README.md`, `tools/audit_craft.py`

- [ ] **Step 1: README** — build/play, OST path, personal-learning disclaimer, copy law, craft law.
- [ ] **Step 2: audit** — 9 plates exist; `fx/meteors.*` exists; `validate_timing` empty; `copy_is_exactly_five_lines` empty; `git check-ignore media/main-title.mp3` succeeds.
- [ ] **Step 3:** Run audit — exit 0.

---

### Task 10: Handover

- [ ] **Step 1:** `handover.md`: slide count, audio used (Main Title 78.3 s), plate source mix, meteor method, rejected-generation count, known gaps, "personal learning only".
- [ ] **Step 2:** Spec §8 checklist filled PASS/FAIL.

---

## Done means

- `out/星辰之子-学习教学.pptx` opens, auto-advances 78.3 s with Main Title.
- 9 refined plates; slide 7 shows trailing meteors.
- Five lines verbatim, nothing else; no committed mp3.

## Plan self-review

| Spec item | Task |
|-----------|------|
| Five-line copy law | 1, 7, 9 |
| Main Title 78.3 s | 1, 6, 7 |
| Craft law / romantic language | Prompt library, 4, 5, 7, 9 |
| Game extract + HQ fallback | 2–3 |
| Real meteors on slide 7 | 5, 7 |
| Early visible checkpoint | 7 subset + 8 |
| Personal-use README | 9–10 |

No Commit steps in this plan.
