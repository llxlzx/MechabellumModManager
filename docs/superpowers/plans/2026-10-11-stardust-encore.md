# 星辰之子 · 旅者安可版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the approved 9-page layered-romance deck into a 17-page, 229.5 s deck set to *Travelers' Encore*, slower and richer, with 8 wordless interludes cut from official trailer footage.

**Architecture:** A new storyboard module (`deck/encore.py`) reuses the v1 dataclasses and helpers; a new footage renderer (`tools/footage.py`) turns trailer shots into graded, slowed interlude backgrounds with tracked light overlays; `tools/living_bg.py` gains a hue drift, a shared particle helper and encore specs; `build_layered.py` selects an *edition* (`v1` default, unchanged; `encore` new) through `deck/editions.py`.

**Tech Stack:** Python 3.12, numpy, Pillow, imageio(-ffmpeg 7.1), pywin32 PowerPoint COM, pytest.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Work only inside `demos/outer-wilds-real-pptx/` plus this plan and the spec; never touch `demos/outer-wilds-cinematic-pptx`, Mod Manager code, or `demos/bocchi-ppt-demo`.
- No git commits. `media/*.mp3` never committed (already ignored by `demos/outer-wilds-real-pptx/.gitignore`).
- The seven copy lines in `copy.json` stay verbatim; interlude pages carry no text.
- Game imagery only (trailer footage, game screenshots); no NASA/ESA photos, no fan art.
- v1 (`--edition v1`, the default) must build exactly as before; its outputs `out/星辰之子-分层浪漫版.*` are never overwritten.
- Encore outputs: `out/星辰之子-旅者安可版.pptx` / `.mp4`; backgrounds in `fx/encore/`.
- Background video bitrate 8 Mbps.
- Text legibility gate: 90th-percentile background luminance in each text box ≤ 0.45 while shown.
- Run commands from `demos/outer-wilds-real-pptx/` in PowerShell with `$env:PYTHONIOENCODING="utf-8"`.
- Report to the user in Chinese.

---

### Task 1: Encore timeline, transitions and interlude pages

**Files:**
- Create: `deck/encore.py`
- Test: `tests/test_encore.py`

**Interfaces:**
- Consumes: `deck.storyboard` — `ASPECT, Layer, Page, Text, _segments, _square, curve_path, polyline_path, spiral_path`, constants `P1_CORE P2_NOVA P3_CORE P4_BURST P4_KEY P5_ORB P6_FIRE P6_KEY P6_WINDOWS P7_HUT P8_KEY P9_FIRE P9_PLANET`.
- Produces: `MUSIC: str`, `MUSIC_SEC: float`, `STARTS: tuple[float, ...]`, `CLIMAX: float`, `TRANSITIONS: dict[int, tuple[str, float]]`, `TRANSITION_NOTES: dict[int, str]`, `INTERLUDES: tuple[int, ...]`, `bg(i) -> str`, `holds() -> dict[int, float]`, `page_starts() -> dict[int, float]`, `spans() -> dict[int, float]`, `orbit_path(rx, ry, a0, n=48) -> str`, `pages(which: list[int]) -> list[Page]`, private `_BUILDERS: dict[int, Callable[[float], Page]]`, helpers `_keyed`, `_breath`, `_twinkles` used by Tasks 2–3.

- [ ] **Step 1: Write the failing tests** — create `tests/test_encore.py`:

```python
import json
from pathlib import Path

import pytest

from deck.encore import (INTERLUDES, MUSIC_SEC, STARTS, TRANSITIONS, holds, orbit_path, page_starts, pages,
                         spans)
from deck.storyboard import validate

ROOT = Path(__file__).resolve().parents[1]
COPY = json.loads((ROOT / "copy.json").read_text(encoding="utf-8"))


def _path_points(path):
    nums = [float(x) for x in path.replace("M", " ").replace("L", " ").replace("E", " ").split()]
    return list(zip(nums[::2], nums[1::2]))


def test_holds_fill_the_track_exactly():
    h = holds()
    assert sorted(h) == list(range(1, 18))
    assert sum(h.values()) == pytest.approx(MUSIC_SEC)
    assert page_starts()[11] == STARTS[10]


def test_climax_falls_inside_the_sun_interlude():
    st, h = page_starts(), holds()
    assert st[11] < 146.0 < st[11] + h[11]


def test_transitions_are_distinct_and_unhurried():
    assert sorted(TRANSITIONS) == list(range(1, 18))
    effects = [e for e, _ in TRANSITIONS.values()]
    assert len(set(effects)) == 17
    assert all(1.5 <= d <= 2.5 for _, d in TRANSITIONS.values())
    assert not any("Zoom" in e or "Shimmer" in e for e in effects)


def test_spans_hand_transition_time_back():
    s, h = spans(), holds()
    for i in range(1, 17):
        assert s[i] == pytest.approx(h[i] - TRANSITIONS[i + 1][1])
    assert s[17] == h[17]


def test_interludes_are_valid_wordless_pages():
    ps = pages(list(INTERLUDES))
    assert validate(ps) == []
    assert all(not p.texts and p.copy_key is None for p in ps)
    assert all(p.bg_video == f"fx/encore/bg_p{p.i}.mp4" for p in ps)


def test_orbit_path_starts_and_ends_at_home():
    pts = _path_points(orbit_path(0.2, 0.07, 1.0))
    assert pts[0] == pytest.approx((0.0, 0.0), abs=1e-4)
    assert pts[-1] == pytest.approx((0.0, 0.0), abs=1e-4)
    assert max(abs(x) for x, _ in pts) > 0.2


def test_pages_rejects_undesigned_pages():
    with pytest.raises(ValueError):
        pages([18])
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_encore.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.encore'`

- [ ] **Step 3: Implement `deck/encore.py`**

```python
"""Storyboard for 星辰之子 · 旅者安可版: 17 pages over Travelers' Encore (229.5 s).

Same conventions as deck/storyboard.py: times are seconds from a slide's content start, boxes are slide
fractions, and each entry transition is taken out of the previous page's hold.
"""
from __future__ import annotations

import math
from typing import Callable

from deck.storyboard import (ASPECT, P1_CORE, P2_NOVA, P3_CORE, P4_BURST, P4_KEY, P5_ORB, P6_FIRE, P6_KEY,
                             P6_WINDOWS, P7_HUT, P8_KEY, P9_FIRE, P9_PLANET, Layer, Page, Text, _segments,
                             _square, curve_path, polyline_path, spiral_path)

MUSIC = "media/travelers-encore.mp3"
MUSIC_SEC = 229.5
# Content starts, each snapped to the strongest spectral-flux onset within ±1 s of the planned grid.
STARTS = (0.0, 14.35, 26.08, 37.15, 52.15, 65.2, 80.85, 92.6, 108.25, 121.93, 136.95, 149.98, 166.95, 177.05,
          193.03, 209.33, 218.48)
CLIMAX = 146.0

TRANSITIONS: dict[int, tuple[str, float]] = {
    1: ("ppEffectFadeSmoothly", 2.5),
    2: ("ppEffectRevealBlackLeft", 2.0),
    3: ("ppEffectPanUp", 1.8),
    4: ("ppEffectFlashbulb", 1.5),
    5: ("ppEffectOrbitLeft", 2.0),
    6: ("ppEffectRippleCenter", 2.0),
    7: ("ppEffectVortexLeft", 2.2),
    8: ("ppEffectFlashOnceSlow", 2.0),
    9: ("ppEffectWindLeft", 2.0),
    10: ("ppEffectFlyThroughIn", 2.0),
    11: ("ppEffectWarpIn", 2.0),
    12: ("ppEffectRevealSmoothRight", 2.0),
    13: ("ppEffectFade", 2.0),
    14: ("ppEffectGlitterDiamondDown", 2.0),
    15: ("ppEffectDissolve", 2.0),
    16: ("ppEffectWarpOut", 2.0),
    17: ("ppEffectFlyThroughOut", 2.5),
}
TRANSITION_NOTES: dict[int, str] = {}
INTERLUDES = (2, 3, 5, 7, 9, 11, 13, 16)

SPARK = "layers/sprites/spark.png"
GLOW_WARM = "layers/sprites/glow_warm.png"
GLOW_BLUE = "layers/sprites/glow_blue.png"
STREAK = "layers/sprites/meteor_streak.png"


def bg(i: int) -> str:
    return f"fx/encore/bg_p{i}.mp4"


def holds() -> dict[int, float]:
    ends = STARTS[1:] + (MUSIC_SEC,)
    return {k + 1: round(e - s, 6) for k, (s, e) in enumerate(zip(STARTS, ends))}


def page_starts() -> dict[int, float]:
    return {k + 1: s for k, s in enumerate(STARTS)}


def spans() -> dict[int, float]:
    h = holds()
    return {i: round(h[i] - (TRANSITIONS[i + 1][1] if i + 1 in h else 0.0), 6) for i in h}


def orbit_path(rx: float, ry: float, a0: float, n: int = 48) -> str:
    """One loop round an ellipse, starting and ending at the shape's home (the point at angle ``a0``)."""
    pts = []
    for k in range(n + 1):
        a = a0 + 2 * math.pi * k / n
        pts.append((rx * (math.cos(a) - math.cos(a0)), ry * (math.sin(a) - math.sin(a0))))
    return polyline_path(pts)


def _keyed(page_v1: int, char: str, center: tuple[float, float], width: float, t: float,
           spiral_w: float | None = None) -> tuple[Layer, Layer]:
    """Slow Nomai translation: the spiral unwinds over 1.6 s, the key character surfaces over 1.8 s."""
    return (Layer("Nomai", f"layers/type/nomai_p{page_v1}.png", _square(center, spiral_w or width * 1.15), t, 1.6,
                  base="wheel", spin=120.0, exit_t=t + 1.9, exit_dur=1.2, text_fx=True),
            Layer("KeyChar", f"layers/type/key_{char}.png", _square(center, width), t + 1.0, 1.8,
                  scale_from=1.3, text=True))


def _breath(name: str, key: Layer, t: float) -> Layer:
    """A slightly larger copy of the key swells in and out on top of it, deepening its glow once."""
    l, tp, w, h = key.box
    g = 1.06
    return Layer(name, key.src, (l - w * (g - 1) / 2, tp - h * (g - 1) / 2, w * g, h * g), t, 1.3,
                 exit_t=t + 1.5, exit_dur=1.4, text_fx=True)


def _twinkles(points: list[tuple[float, float]], t0: float, gap: float, width: float = 0.022) -> tuple[Layer, ...]:
    return tuple(Layer(f"Twinkle{k}", SPARK, _square(c, width), t0 + gap * k, 0.8, scale_from=0.3,
                       exit_t=t0 + gap * k + 1.0, exit_dur=0.9) for k, c in enumerate(points))


def _i2(s: float) -> Page:
    fire = (0.25, 0.57)
    embers = tuple(Layer(f"Ember{k}", SPARK, _square((fire[0] + dx, 0.22), 0.02), 2.0 + 1.2 * k, 2.4,
                         move_from=(-dx, fire[1] - 0.22), exit_t=4.4 + 1.2 * k, exit_dur=0.6)
                   for k, dx in enumerate((-0.03, 0.02, -0.01, 0.04)))
    layers = (Layer("FireGlow", GLOW_WARM, _square(fire, 0.22), 0.6, 2.0, scale_from=0.5,
                    exit_t=s - 1.4, exit_dur=1.2), *embers)
    return Page(2, 0, bg(2), layers, motif="营火醒来")


def _i3(s: float) -> Page:
    trails = tuple(Layer(f"Trail{k}", STREAK, (x, 0.18, 0.20, 0.20 * ASPECT / 8), 2.5 + 1.1 * k, 1.6,
                         move_from=(0.0, 0.55), rot=-90.0, exit_t=4.1 + 1.1 * k, exit_dur=0.5)
                   for k, x in enumerate((0.18, 0.62, 0.40)))
    layers = (*trails, Layer("SkyGlow", GLOW_BLUE, _square((0.5, 0.02), 0.8), 4.5, 2.5, scale_from=0.6,
                             exit_t=s - 1.2, exit_dur=1.0))
    return Page(3, 0, bg(3), layers, motif="出发")


def _i5(s: float) -> Page:
    return Page(5, 0, bg(5), _twinkles([(0.62, 0.18), (0.86, 0.30), (0.78, 0.80), (0.45, 0.85)], 1.5, 1.6),
                motif="太阳系全景")


def _i7(s: float) -> Page:
    box = _square((0.55, 0.0), 1.1)
    layers = (Layer("Lightning0", GLOW_BLUE, box, 2.6, 0.12, exit_t=2.75, exit_dur=0.5),
              Layer("Lightning1", GLOW_BLUE, box, 3.0, 0.1, exit_t=3.12, exit_dur=0.7),
              Layer("Lightning2", GLOW_BLUE, box, 6.3, 0.15, exit_t=6.5, exit_dur=0.8))
    return Page(7, 0, bg(7), layers, motif="巨人之海")


def _i9(s: float) -> Page:
    return Page(9, 0, bg(9), _twinkles([(0.72, 0.20), (0.88, 0.45), (0.66, 0.62)], 1.2, 1.8), motif="沙漏双星")


def _i11(s: float) -> Page:
    return Page(11, 0, bg(11), _twinkles([(0.80, 0.12), (0.62, 0.08), (0.92, 0.25)], 1.0, 1.5),
                motif="太阳的余晖")


def _i13(s: float) -> Page:
    return Page(13, 0, bg(13), _twinkles([(0.42, 0.50), (0.55, 0.62), (0.48, 0.70)], 1.2, 1.5), motif="舷窗外")


def _i16(s: float) -> Page:
    eye = (0.44, 0.38)
    layers = (Layer("EyeGlow", GLOW_BLUE, _square(eye, 0.6), 0.4, 1.4, scale_from=0.5,
                    exit_t=2.6, exit_dur=3.4, exit_scale=0.2),
              Layer("EyeCore", SPARK, _square(eye, 0.06), 3.4, 1.0, scale_from=0.3, exit_t=5.0, exit_dur=1.2),
              *_twinkles([(0.12, 0.15), (0.85, 0.75)], 0.8, 1.2))
    return Page(16, 0, bg(16), layers, motif="宇宙之眼")


_BUILDERS: dict[int, Callable[[float], Page]] = {2: _i2, 3: _i3, 5: _i5, 7: _i7, 9: _i9, 11: _i11, 13: _i13,
                                                 16: _i16}


def pages(which: list[int]) -> list[Page]:
    h, s = holds(), spans()
    missing = [i for i in which if i not in _BUILDERS]
    if missing:
        raise ValueError(f"pages not designed yet: {missing}")
    out = []
    for i in which:
        p = _BUILDERS[i](s[i])
        out.append(Page(p.i, h[i], p.bg_video, p.layers, p.texts, p.copy_key, p.motif, s[i]))
    return out
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest tests/test_encore.py -q`
Expected: 7 passed

---

### Task 2: Copy pages 1, 4, 6, 8 (old p1–p4, slowed and enriched)

**Files:**
- Modify: `deck/encore.py` (add `_e1 _e4 _e6 _e8`, register in `_BUILDERS`)
- Test: `tests/test_encore.py` (append)

**Interfaces:**
- Consumes: Task 1 helpers `_keyed(page_v1, char, center, width, t, spiral_w=None)`, `_breath(name, key, t)`, constants `SPARK GLOW_WARM GLOW_BLUE STREAK`, `bg(i)`.
- Produces: `_BUILDERS[1|4|6|8]`; new sprite path `layers/sprites/line.png` (created in Task 4).

- [ ] **Step 1: Append failing tests to `tests/test_encore.py`**

```python
def test_first_copy_pages_are_valid_and_unhurried():
    ps = pages([1, 4, 6, 8])
    assert validate(ps) == []
    for p in ps[1:]:
        assert min(t.t for t in p.texts) >= 9.0
        gaps = [b.t - a.t for a, b in zip(p.texts, p.texts[1:])]
        assert all(g >= 0.95 for g in gaps)


def test_each_copy_page_breathes_after_its_key():
    for p in pages([4, 6, 8]):
        key = next(l for l in p.layers if l.name == "KeyChar")
        br = next(l for l in p.layers if l.name == "KeyBreath")
        assert br.src == key.src and br.text_fx
        assert br.t >= key.t + key.dur - 1.0


def test_title_page_draws_a_constellation_then_a_shooting_star():
    p1 = pages([1])[0]
    names = {l.name for l in p1.layers}
    assert {f"Line{k}" for k in range(6)} <= names and "ShootingStar" in names
    letters_done = max(l.t + l.dur for l in p1.layers
                       if l.name.startswith("Letter") and not l.name.startswith("LetterHalo"))
    assert min(l.t for l in p1.layers if l.name.startswith("Line")) >= letters_done


def test_copper_burst_collapses_into_a_white_dwarf():
    p8 = pages([8])[0]
    burst = next(l for l in p8.layers if l.name == "Burst")
    dwarf = next(l for l in p8.layers if l.name == "WhiteDwarf")
    assert burst.exit_scale is not None and burst.exit_scale < 0.2
    assert dwarf.t >= burst.exit_t
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_encore.py -q`
Expected: FAIL — `ValueError: pages not designed yet: [1, 4, 6, 8]`

- [ ] **Step 3: Implement the four pages** — insert before `_BUILDERS` in `deck/encore.py`, then extend `_BUILDERS`:

```python
LINE = "layers/sprites/line.png"


def _stroke(name: str, a: tuple[float, float], b: tuple[float, float], t: float, exit_t: float) -> Layer:
    """A glowing line drawn from point ``a`` to ``b`` (slide fractions); the sprite is 512x24 px."""
    dx, dy = (b[0] - a[0]) * 16, (b[1] - a[1]) * 9
    length = math.hypot(dx, dy) / 16
    cx, cy = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    h = length * ASPECT / (512 / 24)
    return Layer(name, LINE, (cx - length / 2, cy - h / 2, length, h), t, 0.5, base="wipe_left",
                 rot=math.degrees(math.atan2(dy, dx)), exit_t=exit_t, exit_dur=0.8, text_fx=True)


def _e1(s: float) -> Page:
    layers: list[Layer] = [
        Layer("CoreGlow", GLOW_WARM, _square(P1_CORE, 0.22), 2.4, 2.0, scale_from=0.2),
        Layer("Station", "layers/p1/station.png", (0, 0, 1, 1), 3.4, 3.0, base="wheel", scale_from=0.88, spin=6.0,
              exit_t=s - 1.4, exit_dur=1.0),
        Layer("CrossRays", "layers/sprites/cross_rays.png", _square(P1_CORE, 0.5), 4.8, 1.5, scale_from=0.3),
    ]
    lw, top = 0.062, 0.64
    lh = lw * ASPECT / 0.8
    lefts = [0.05 + k * lw for k in range(4)] + [0.70 + k * lw for k in range(4)]
    for k, (ch, left) in enumerate(zip("STARDUST", lefts)):
        t = 5.4 + k * 0.32
        cx, cy = left + lw / 2, top + lh / 2
        layers += [Layer(f"Spark{k}", SPARK, _square((cx, cy), 0.035), t, 0.7,
                         move_from=(P1_CORE[0] - cx, P1_CORE[1] - cy), exit_t=t + 0.7, exit_dur=0.4, text_fx=True),
                   Layer(f"LetterHalo{k}", f"layers/type/letter_{ch}_{k}_halo.png", (left, top, lw, lh), t + 0.5,
                         0.8, scale_from=1.8, text_fx=True),
                   Layer(f"Letter{k}", f"layers/type/letter_{ch}_{k}.png", (left, top, lw, lh), t + 0.6, 0.8,
                         text=True)]
    # Once the word is lit, a small constellation settles above each half of it.
    lift = (0.0, -0.025, 0.01, -0.015, -0.01, 0.015, -0.025, 0.0)
    pts = [(left + lw / 2, 0.585 + d) for left, d in zip(lefts, lift)]
    for j, p in enumerate(pts):
        layers.append(Layer(f"StarPoint{j}", SPARK, _square(p, 0.018), 9.2 + 0.12 * j, 0.5, scale_from=0.3,
                            exit_t=s - 1.2, exit_dur=0.8, text_fx=True))
    pairs = [(0, 1), (1, 2), (2, 3), (4, 5), (5, 6), (6, 7)]
    for m, (a, b) in enumerate(pairs):
        layers.append(_stroke(f"Line{m}", pts[a], pts[b], 9.4 + 0.15 * m, s - 1.3))
    layers.append(Layer("ShootingStar", STREAK, (0.56, 0.10, 0.22, 0.22 * ASPECT / 8), s - 2.6, 1.2,
                        move_from=(-0.35, -0.08), rot=8.0, exit_t=s - 1.4, exit_dur=0.5))
    return Page(1, 0, bg(1), tuple(layers), motif="第一缕光")


def _e4(s: float) -> Page:
    seg = _segments("L1", ["我们身体里的铁，", "来自璀璨的超新星爆炸"])
    key = _keyed(2, "铁", (0.15, 0.30), 0.17, 7.6)
    layers = (
        Layer("Snow", "layers/p2/snow.png", (0, 0, 1, 1), 1.2, 3.0, move_from=(0.0, 0.12)),
        Layer("NovaGlow", GLOW_BLUE, _square(P2_NOVA, 0.62), 4.2, 3.6, scale_from=0.1, exit_t=s - 1.6, exit_dur=1.2),
        Layer("Supernova", "layers/p2/supernova.png", _square(P2_NOVA, 0.44), 4.5, 3.5, scale_from=0.08),
        Layer("Ring1", "layers/sprites/ring_blue.png", _square(P2_NOVA, 0.55), 6.4, 1.4, scale_from=0.3,
              exit_t=7.6, exit_dur=1.0),
        Layer("Ring2", "layers/sprites/ring_blue.png", _square(P2_NOVA, 0.70), 6.9, 1.5, scale_from=0.2,
              exit_t=8.0, exit_dur=1.0),
        *key,
        _breath("KeyBreath", key[1], 10.0),
    )
    texts = (Text("Seg1", seg[0], (0.66, 0.16, 0.30, 0.07), 9.6, 1.0, align="right"),
             Text("Seg2", seg[1], (0.63, 0.26, 0.33, 0.07), 10.6, 1.2, align="right"))
    return Page(4, 0, bg(4), layers, texts, copy_key="L1", motif="烟花般的盛放")


def _e6(s: float) -> Page:
    seg = _segments("L2", ["我们血液里的锌，", "源自两次中子星对撞后", "喷射向宇宙的尘埃"])
    key = _keyed(3, "锌", (0.16, 0.36), 0.17, 7.8)
    stars = []
    for name, src, w, a0 in (("StarA", GLOW_BLUE, 0.15, 0.0), ("StarB", GLOW_WARM, 0.15, math.pi),
                             ("StarACore", SPARK, 0.04, 0.0), ("StarBCore", SPARK, 0.04, math.pi)):
        stars.append(Layer(name, src, _square(P3_CORE, w), 1.2, 4.6, path=spiral_path(0.30, 2.25, a0),
                           exit_t=5.8, exit_dur=0.4))
    # Three faint gravitational-wave ripples spread from the merger and are gone before the words.
    waves = tuple(Layer(f"Wave{k}", "layers/sprites/ring_blue.png", _square(P3_CORE, w), 6.3 + 0.55 * k, 1.4,
                        scale_from=0.15, exit_t=7.3 + 0.55 * k, exit_dur=0.9)
                  for k, w in enumerate((0.5, 0.62, 0.74)))
    layers = (
        *stars,
        Layer("Embrace", GLOW_WARM, _square(P3_CORE, 0.5), 5.7, 0.5, scale_from=0.1, exit_t=6.2, exit_dur=1.6),
        Layer("EmbraceRays", "layers/sprites/cross_rays.png", _square(P3_CORE, 0.6), 5.75, 0.6, scale_from=0.2,
              exit_t=6.5, exit_dur=1.6),
        Layer("Remnant", "layers/p3/remnant.png", _square(P3_CORE, 0.40), 5.9, 6.0, scale_from=0.1, spin=200.0),
        *waves,
        *key,
        _breath("KeyBreath", key[1], 10.5),
    )
    texts = (Text("Seg1", seg[0], (0.62, 0.60, 0.34, 0.065), 9.8, 0.9, align="right"),
             Text("Seg2", seg[1], (0.62, 0.69, 0.34, 0.065), 10.8, 0.9, align="right"),
             Text("Seg3", seg[2], (0.62, 0.78, 0.34, 0.065), 11.8, 0.9, align="right"))
    return Page(6, 0, bg(6), layers, texts, copy_key="L2", motif="两颗星的相拥")


def _e8(s: float) -> Page:
    seg = _segments("L3a", ["我们那微量的铜，", "更是需要见证", "一颗白矮星的死亡，"])
    key = _keyed(4, "铜", P4_KEY, 0.2, 6.0, spiral_w=0.22)
    layers = (
        # The dying star flares, then its light draws in to a small white dwarf whose glow beats like a heart.
        Layer("Burst", GLOW_WARM, _square(P4_BURST, 0.5), 2.4, 1.4, scale_from=0.2, exit_t=4.2, exit_dur=2.4,
              exit_scale=0.12),
        Layer("WhiteDwarf", GLOW_BLUE, _square(P4_BURST, 0.05), 5.4, 1.2, scale_from=0.3),
        Layer("Ground", "layers/p4/ground.png", (0, 0, 1, 1), 1.8, 2.6),
        Layer("Villager", "layers/p4/villager.png", (0, 0, 1, 1), 3.6, 2.6, move_from=(-0.08, 0.0)),
        *key,
        _breath("KeyBreath", key[1], 10.4),
    )
    texts = (Text("Seg1", seg[0], (0.45, 0.665, 0.50, 0.075), 9.6, 1.0, align="right"),
             Text("Seg2", seg[1], (0.40, 0.765, 0.50, 0.075), 10.6, 0.9, align="right"),
             Text("Seg3", seg[2], (0.45, 0.865, 0.50, 0.075), 11.6, 1.0, align="right"))
    return Page(8, 0, bg(8), layers, texts, copy_key="L3a", motif="目送")
```

Then change the registry line to:

```python
_BUILDERS: dict[int, Callable[[float], Page]] = {1: _e1, 2: _i2, 3: _i3, 4: _e4, 5: _i5, 6: _e6, 7: _i7, 8: _e8,
                                                 9: _i9, 11: _i11, 13: _i13, 16: _i16}
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest tests/test_encore.py -q`
Expected: 11 passed

---

### Task 3: Copy pages 10, 12, 14, 15 and the goodnight page 17

**Files:**
- Modify: `deck/encore.py` (add `_e10 _e12 _e14 _e15 _e17`, final `_BUILDERS`)
- Test: `tests/test_encore.py` (append)

**Interfaces:**
- Consumes: Task 1–2 helpers (`_keyed`, `_breath`, `orbit_path`, `curve_path`, constants).
- Produces: `_BUILDERS` with all 17 pages; `pages(list(range(1, 18)))` valid.

- [ ] **Step 1: Append failing tests**

```python
def test_all_seventeen_pages_are_valid_and_tell_the_copy_in_order():
    ps = pages(list(range(1, 18)))
    assert validate(ps) == []
    order = [p.copy_key for p in ps if p.copy_key]
    assert order == ["L1", "L2", "L3a", "L3b", "L4", "L5a", "L5b"]
    assert sorted(order) == sorted(COPY["slides"].values())


def test_children_of_stars_dust_keeps_orbiting_the_key():
    p15 = pages([15])[0]
    orbits = [l for l in p15.layers if l.name.startswith("Orbit")]
    assert len(orbits) == 6 and all(l.path and l.dur >= 4.0 and l.text_fx for l in orbits)


def test_goodnight_ends_on_one_last_star():
    p17 = pages([17])[0]
    last = max(p17.layers, key=lambda l: (l.exit_t + l.exit_dur) if l.exit_t is not None else l.t + l.dur)
    assert last.name == "LastStar"


def test_meteor_page_sends_a_second_gentle_wave_with_the_words():
    p14 = pages([14])[0]
    echoes = [l for l in p14.layers if l.name.startswith("Echo")]
    assert len(echoes) == 4 and min(e.t for e in echoes) > min(t.t for t in p14.texts)
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_encore.py -q`
Expected: FAIL — `ValueError: pages not designed yet: [10, 12, 14, 15, 17]`

- [ ] **Step 3: Implement** — insert before `_BUILDERS`:

```python
def _e10(s: float) -> Page:
    seg = _segments("L3b", ["即使是最微不足道的钴，", "也源自几十亿光年外的星云"])
    key, far = (0.13, 0.22), (0.70, 0.62)
    kc = Layer("KeyChar", "layers/type/key_钴.png", _square(key, 0.15), 7.3, 1.8, scale_from=0.05,
               move_from=(far[0] - key[0], far[1] - key[1]), text=True)
    layers = (
        Layer("Letter", GLOW_BLUE, _square(P5_ORB, 0.06), 1.4, 3.0, path=curve_path((0.59, -0.20), (0.30, -0.42)),
              exit_t=4.4, exit_dur=0.6),
        Layer("OrbGlow", GLOW_WARM, _square(P5_ORB, 0.24), 4.3, 1.2, scale_from=0.2),
        Layer("Orb", "layers/p5/orb.png", _square(P5_ORB, 0.20), 4.35, 1.5, scale_from=0.7),
        Layer("Figure", "layers/p5/figure.png", (0, 0, 1, 1), 4.8, 2.2),
        # A second letter of light arrives later; the staff's orb flares as it lands.
        Layer("Letter2", GLOW_BLUE, _square(P5_ORB, 0.05), 5.6, 3.0, path=curve_path((0.66, 0.30), (0.40, 0.45)),
              exit_t=8.6, exit_dur=0.6),
        Layer("OrbFlare", GLOW_BLUE, _square(P5_ORB, 0.14), 8.5, 0.6, scale_from=0.4, exit_t=9.2, exit_dur=1.4),
        Layer("Nomai", "layers/type/nomai_p5.png", _square(key, 0.19), 6.6, 1.4, base="wheel", spin=120.0,
              exit_t=8.2, exit_dur=1.0, text_fx=True),
        kc,
        _breath("KeyBreath", kc, 10.0),
    )
    texts = (Text("Seg1", seg[0], (0.02, 0.80, 0.40, 0.06), 9.6, 1.0),
             Text("Seg2", seg[1], (0.56, 0.885, 0.42, 0.06), 10.8, 1.2, align="right"))
    return Page(10, 0, bg(10), layers, texts, copy_key="L3b", motif="来自远方的信")


def _e12(s: float) -> Page:
    seg = _segments("L4", ["某种意义上讲，", "人类对星空怀有好奇，", "是正常的。"])
    key = _keyed(6, "好奇", P6_KEY, 0.22, 8.0)
    windows = tuple(Layer(f"Window{k}", GLOW_WARM, _square(c, 0.05), 4.5 + 0.35 * k, 0.9, scale_from=0.4)
                    for k, c in enumerate(P6_WINDOWS))
    layers = (
        *windows,
        Layer("FireGlow", GLOW_WARM, _square(P6_FIRE, 0.16), 5.1, 0.9, scale_from=0.3),
        Layer("MoonMeteor", STREAK, (0.30, 0.10, 0.16, 0.16 * ASPECT / 8), 7.0, 1.0, move_from=(-0.20, -0.06),
              rot=18.0, exit_t=8.0, exit_dur=0.5),
        *key,
        _breath("KeyBreath", key[1], 11.2),
    )
    texts = (Text("Seg1", seg[0], (0.03, 0.66, 0.34, 0.068), 10.0, 0.9),
             Text("Seg2", seg[1], (0.03, 0.755, 0.34, 0.068), 11.0, 1.0),
             Text("Seg3", seg[2], (0.03, 0.85, 0.34, 0.068), 12.2, 0.8))
    return Page(12, 0, bg(12), layers, texts, copy_key="L4", motif="火星化作星星")


def _e14(s: float) -> Page:
    seg = _segments("L5a", ["渴望见证", "星河大海的极限，"])
    kc = Layer("KeyChar", "layers/type/key_星河大海.png", (0.08, 0.08, 0.44, 0.24), 7.15, 1.6, base="wipe_left",
               text=True)
    echoes = tuple(Layer(f"Echo{k}", STREAK, (x, y, 0.12, 0.12 * ASPECT / 8), 10.6 + 0.5 * k, 1.2,
                         move_from=(-0.18, -0.04), rot=10.0, exit_t=11.8 + 0.5 * k, exit_dur=0.4)
                   for k, (x, y) in enumerate(((0.70, 0.10), (0.82, 0.22), (0.62, 0.30), (0.88, 0.06))))
    layers = (
        Layer("Ground", "layers/p7/ground.png", (0, 0, 1, 1), 1.2, 3.0, move_from=(0.0, 0.10)),
        Layer("HutLight", GLOW_WARM, _square(P7_HUT, 0.09), 4.2, 1.2, scale_from=0.3),
        Layer("WishMeteor", STREAK, (0.40, 0.12, 0.26, 0.26 * ASPECT / 8), 7.0, 1.8, move_from=(-0.42, -0.07),
              rot=5.0, exit_t=8.8, exit_dur=0.7, text_fx=True),
        kc,
        _breath("KeyBreath", kc, 10.2),
        *echoes,
    )
    texts = (Text("Seg1", seg[0], (0.04, 0.76, 0.40, 0.075), 9.8, 1.0),
             Text("Seg2", seg[1], (0.08, 0.86, 0.45, 0.075), 10.8, 1.2))
    return Page(14, 0, bg(14), layers, texts, copy_key="L5a", motif="对流星许愿")


def _e15(s: float) -> Page:
    seg = _segments("L5b", ["因为我们本就是 ", "星辰的孩子。"])
    sources = [(0.12, 0.38), (0.30, 0.62), (0.20, 0.75), (0.08, 0.55), (0.34, 0.30), (0.26, 0.48),
               (0.14, 0.66), (0.36, 0.52), (0.05, 0.42), (0.22, 0.32), (0.31, 0.78), (0.18, 0.58)]
    dust = []
    for k, (sx, sy) in enumerate(sources):
        tx, ty = 0.59 + 0.34 * (k / (len(sources) - 1)), P8_KEY[1] + (0.03 if k % 2 else -0.03)
        start = (sx - tx, sy - ty)
        t = 4.8 + 0.12 * k
        dust.append(Layer(f"Dust{k}", SPARK, _square((tx, ty), 0.03), t, 1.5,
                          path=curve_path(start, (start[0] * 0.4, start[1] * 0.4 - 0.25)), exit_t=t + 1.5,
                          exit_dur=0.5, text_fx=True))
    rx, ry = 0.22, 0.07
    orbits = tuple(Layer(f"Orbit{k}", SPARK, _square((P8_KEY[0] + rx * math.cos(a), P8_KEY[1] + ry * math.sin(a)), 0.022),
                         8.0 + 0.15 * k, 4.5, path=orbit_path(rx, ry, a), exit_t=12.5 + 0.15 * k, exit_dur=0.6,
                         text_fx=True)
                   for k, a in enumerate(2 * math.pi * j / 6 for j in range(6)))
    kc = Layer("KeyChar", "layers/type/key_星辰的孩子.png", _square(P8_KEY, 0.40), 6.4, 1.8, scale_from=1.2, text=True)
    layers = (
        Layer("Sun", "layers/p8/sun.png", (0, 0, 1, 1), 0.4, 3.0, move_from=(-0.18, 0.0)),
        Layer("Structure", "layers/p8/structure.png", (0, 0, 1, 1), 2.4, 3.2, move_from=(0.08, 0.06)),
        *dust,
        kc,
        _breath("KeyBreath", kc, 9.2),
        *orbits,
        Layer("SunFlare", GLOW_WARM, _square((0.05, 0.55), 0.5), 11.2, 1.2, scale_from=0.6, exit_t=12.6,
              exit_dur=1.4),
    )
    texts = (Text("Seg1", seg[0], (0.52, 0.80, 0.44, 0.075), 9.6, 1.0, align="right"),
             Text("Seg2", seg[1], (0.52, 0.89, 0.44, 0.075), 10.8, 1.2, align="right"))
    return Page(15, 0, bg(15), layers, texts, copy_key="L5b", motif="我们本就是星光")


def _e17(s: float) -> Page:
    layers: list[Layer] = [
        Layer("Planet", "layers/p9/planet.png", _square(P9_PLANET, 0.56), 0.5, 3.0, scale_from=0.05,
              exit_t=5.0, exit_dur=4.0, exit_move=(0.08, -0.10), exit_scale=0.35),
        Layer("FireGlow", GLOW_WARM, _square(P9_FIRE, 0.07), 2.4, 0.8, scale_from=0.4, exit_t=5.0, exit_dur=2.4),
    ]
    lw = 0.035
    lh = lw * ASPECT / 0.8
    for k, ch in enumerate("STARDUST"):
        layers.append(Layer(f"Letter{k}", f"layers/type/letter_{ch}_{k}.png", (0.12 + k * 0.04, 0.44, lw, lh),
                            1.8 + 0.18 * k, 0.9, exit_t=5.2 + 0.35 * k, exit_dur=1.0, text=True))
    # One star is left; it blinks once on the last note and goes out.
    layers.append(Layer("LastStar", SPARK, _square((0.50, 0.30), 0.03), 8.8, 0.8, scale_from=0.3,
                        exit_t=10.0, exit_dur=0.9))
    return Page(17, 0, bg(17), tuple(layers), motif="余烬·晚安")
```

Final registry:

```python
_BUILDERS: dict[int, Callable[[float], Page]] = {
    1: _e1, 2: _i2, 3: _i3, 4: _e4, 5: _i5, 6: _e6, 7: _i7, 8: _e8, 9: _i9, 10: _e10, 11: _i11, 12: _e12,
    13: _i13, 14: _e14, 15: _e15, 16: _i16, 17: _e17}
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest tests/test_encore.py tests/test_storyboard.py -q`
Expected: all pass (15 encore + existing storyboard tests)

---

### Task 4: living_bg — shared particle helper, video writer, hue drift, encore specs; line sprite

**Files:**
- Modify: `tools/living_bg.py`
- Modify: `tools/glyphs.py` (add `line_sprite`, include in `build_sample_assets`)
- Test: `tests/test_living_bg.py`, `tests/test_glyphs.py` (append)

**Interfaces:**
- Consumes: `deck.encore.spans()`.
- Produces: `living_bg._draw_particles(add, parts, t, stamps) -> None`, `living_bg.write_video(frames, out, fps, bitrate="8M") -> Path`, `BgSpec.hue_drift: tuple[float, float] | None` (period s, amplitude deg), `living_bg.encore_specs() -> dict[int, BgSpec]`, CLI `python -m tools.living_bg --edition encore 1 4 ...` writing `fx/encore/bg_p{i}.mp4`; `glyphs.line_sprite(out, w=512, h=24, color=(220, 230, 255)) -> Path`.

- [ ] **Step 1: Append failing tests**

`tests/test_living_bg.py`:

```python
def test_hue_drift_rotates_colour_but_keeps_brightness(tmp_path):
    p = _flat(tmp_path, (40, 200, 220))
    f = list(render_frames(BgSpec(str(p), seconds=1.0, fps=4, hue_drift=(1.0, 40.0))))
    a, b = f[0][45, 80].astype(float), f[1][45, 80].astype(float)
    luma = np.array([0.299, 0.587, 0.114])
    assert np.abs(a - b).max() > 12
    assert abs(a @ luma - b @ luma) < 4


def test_encore_specs_cover_copy_pages_and_fit_their_spans():
    from deck.encore import spans
    from tools.living_bg import encore_specs

    sp, s = encore_specs(), spans()
    assert sorted(sp) == [1, 4, 6, 8, 10, 12, 14, 15, 17]
    assert all(sp[i].seconds == s[i] for i in sp)
    assert all(sp[i].push[1] > 1.0 for i in sp if i != 17)
```

`tests/test_glyphs.py`:

```python
def test_line_sprite_is_a_thin_glow_fading_at_both_ends(tmp_path):
    import numpy as np
    from PIL import Image

    from tools.glyphs import line_sprite

    a = np.asarray(Image.open(line_sprite(tmp_path / "line.png")))[..., 3].astype(int)
    assert a.shape == (24, 512)
    assert a[12, 256] > 200 and a[0, 256] < 40
    assert a[12, 0] < 20 and a[12, -1] < 20
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_living_bg.py tests/test_glyphs.py -q`
Expected: FAIL — `TypeError: BgSpec.__init__() got an unexpected keyword argument 'hue_drift'`, `ImportError: cannot import name 'encore_specs'`, `ImportError: cannot import name 'line_sprite'`

- [ ] **Step 3: Implement in `tools/living_bg.py`**

Add the field to `BgSpec` (after `meteor_clear`):

```python
    hue_drift: tuple[float, float] | None = None   # (period s, amplitude degrees): colours sway round the wheel
```

Add helpers above `render_frames`:

```python
_YIQ = np.array([[0.299, 0.587, 0.114], [0.596, -0.274, -0.322], [0.211, -0.523, 0.312]], np.float32)


def _hue_rotate(img: np.ndarray, deg: float) -> np.ndarray:
    a = math.radians(deg)
    rot = np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]], np.float32)
    m = np.linalg.inv(_YIQ) @ rot @ _YIQ
    return img @ m.T.astype(np.float32)


def _draw_particles(add: np.ndarray, parts, t: float, stamps: dict) -> None:
    h, w = add.shape[:2]
    for p, plan in parts:
        c0 = np.array(p.color, np.float32)
        c1 = np.array(p.color_end if p.color_end else p.color, np.float32)
        for t0, x, y, size, ph, gain in plan:
            age = t - t0
            if age < 0 or age > p.life:
                continue
            k = age / p.life
            col = c0 * (1 - k) + c1 * k
            px = x + p.velocity[0] * age + p.sway * math.sin(ph + age * 1.7)
            py = y + p.velocity[1] * age
            key = round(size * 2) / 2
            s = stamps.setdefault(key, _stamp(key))
            r = s.shape[0] // 2
            xi, yi = int(round(px)), int(round(py))
            ya, yb, xa, xb = max(yi - r, 0), min(yi + r + 1, h), max(xi - r, 0), min(xi + r + 1, w)
            if ya >= yb or xa >= xb:
                continue
            fade = math.sin(math.pi * age / p.life) * gain
            patch = s[ya - yi + r:yb - yi + r, xa - xi + r:xb - xi + r] * fade
            add[ya:yb, xa:xb] += patch[..., None] * col
```

In `render_frames`, replace the whole `for p, plan in parts:` loop with `_draw_particles(add, parts, t, stamps)`, and right after the `if bloom_t is not None:` block add:

```python
        if spec.hue_drift:
            period, amp = spec.hue_drift
            img = np.clip(_hue_rotate(img, amp * math.sin(2 * math.pi * t / period)), 0, 1)
```

Replace `render_video` with a writer shared by footage:

```python
def write_video(frames, out: Path, fps: int, bitrate: str = "8M") -> Path:
    import imageio.v2 as imageio

    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_name(out.stem + ".part.mp4")
    wr = imageio.get_writer(str(tmp), fps=fps, codec="libx264", pixelformat="yuv420p", macro_block_size=1,
                            bitrate=bitrate, ffmpeg_params=["-preset", "medium", "-movflags", "+faststart"])
    try:
        for fr in frames:
            wr.append_data(fr)
    finally:
        wr.close()
    tmp.replace(out)
    return out


def render_video(spec: BgSpec, out: Path, bitrate: str = "8M") -> Path:
    return write_video(render_frames(spec), out, spec.fps, bitrate)
```

Add the encore specs after `_specs()`:

```python
def encore_specs() -> dict[int, BgSpec]:
    from deck.encore import spans
    from deck.storyboard import P1_CORE, P2_NOVA, P3_CORE, P4_BURST, P6_FIRE

    s = spans()
    p6 = "layers/p6/"
    fire = (P6_FIRE[0] - 0.02, P6_FIRE[1] - 0.05, P6_FIRE[0] + 0.02, P6_FIRE[1] + 0.01)

    def dust(t0: float, color: tuple[float, float, float]) -> Particles:
        return Particles(50, t0, color, (1.0, 2.2), (5.0, -4.0), 6.0, (0.0, 0.0, 1.0, 1.0), 6.0)

    return {
        1: BgSpec("layers/p1/bg_clean.png", s[1], fade_in=(0.0, 0.9), wake_stars=(0.4, 3.4), twinkle=0.35,
                  end_flash=Flash(P1_CORE, s[1] - 1.0, s[1], (1.0, 0.97, 0.9), radius=0.06, fill=False),
                  particles=(dust(1.0, (0.9, 0.85, 0.75)),), push=(1.0, 1.05)),
        4: BgSpec("layers/p2/bg_clean.png", s[4], twinkle=0.4,
                  pulses=(Pulse(P2_NOVA, 0.26, (0.6, 0.85, 1.0), 4.5, s[4], 0.35, 1.4, 2.6),),
                  particles=(Particles(170, 6.8, (0.75, 0.9, 1.0), (1.5, 3.5), (-8.0, 40.0), 18.0,
                                       (0.0, -0.05, 1.0, 0.6), 5.0),
                             Particles(110, s[4] - 5.0, (0.8, 0.92, 1.0), (2.0, 4.0), (-5.0, 28.0), 22.0,
                                       (0.0, -0.05, 1.0, 0.5), 5.5),
                             Particles(160, 5.0, (0.95, 0.97, 1.0), (0.8, 1.6), (0.0, 0.0), 0.0,
                                       (0.0, 0.62, 1.0, 1.0), 0.8)),
                  push=(1.0, 1.05)),
        6: BgSpec("layers/p3/bg_clean.png", s[6], twinkle=0.4,
                  pulses=(Pulse(P3_CORE, 0.2, (0.65, 0.85, 1.0), 6.0, s[6], 0.3, 1.2, 2.2),),
                  particles=(dust(0.8, (0.75, 0.85, 1.0)),), push=(1.0, 1.07)),
        8: BgSpec("layers/p4/bg_clean.png", s[8], fade_in=(0.0, 1.2), twinkle=0.3, dark_ground=0.12,
                  ground_mask="layers/p4/ground.png",
                  pulses=(Pulse(P4_BURST, 0.16, (1.0, 0.86, 0.92), 5.4, s[8], 0.6, 0.9, 1.7),),
                  particles=(Particles(140, 5.5, (1.0, 0.93, 0.95), (1.2, 3.2), (-6.0, 30.0), 14.0,
                                       (0.05, -0.05, 1.0, 0.55), 5.5),),
                  push=(1.0, 1.05)),
        10: BgSpec("layers/p5/bg_clean.png", s[10], twinkle=0.45, bloom_color=(0.4, 3.6), hue_drift=(9.0, 14.0),
                   particles=(dust(2.0, (0.7, 0.85, 1.0)),), push=(1.0, 1.05)),
        12: BgSpec(p6 + "bg_clean.png", s[12], twinkle=0.35, night=0.1,
                   reveals=(Reveal(p6 + "mask_sky.png", 0.0, 1.5), Reveal(p6 + "mask_moon.png", 1.2, 3.9, "down"),
                            Reveal(p6 + "mask_scene.png", 3.3, 6.0), Reveal(p6 + "mask_fire.png", 5.1, 5.7)),
                   particles=(Particles(110, 5.4, (1.0, 0.7, 0.35), (1.5, 3.0), (6.0, -140.0), 20.0, fire, 4.5,
                                        color_end=(0.85, 0.9, 1.0)),
                              Particles(40, 5.6, (0.30, 0.30, 0.34), (4.0, 8.0), (3.0, -40.0), 14.0, fire, 5.0)),
                   push=(1.0, 1.05)),
        14: BgSpec("layers/p7/bg_clean.png", s[14], twinkle=0.4, meteors=(4.5, 50),
                   meteor_clear=((0.06, 0.07, 0.48, 0.27),), push=(1.0, 1.05)),
        15: BgSpec("layers/p8/bg_clean.png", s[15], twinkle=0.4,
                   pulses=(Pulse((0.02, 0.55), 0.45, (1.0, 0.6, 0.25), 0.6, s[15], 0.2, 1.8, 2.8),),
                   particles=(dust(1.0, (1.0, 0.85, 0.6)),), push=(1.0, 1.05)),
        17: BgSpec("layers/p9/bg_clean.png", s[17], twinkle=0.3, sleep_stars=(2.4, 9.0)),
    }
```

Replace `main()`:

```python
def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("pages", nargs="+", type=int)
    ap.add_argument("--edition", choices=["v1", "encore"], default="v1")
    a = ap.parse_args()
    specs, folder = (encore_specs(), ROOT / "fx" / "encore") if a.edition == "encore" else (_specs(), ROOT / "fx")
    for i in a.pages:
        print(render_video(specs[i], folder / f"bg_p{i}.mp4"))
```

In `tools/glyphs.py`, after `meteor_streak`:

```python
def line_sprite(out, w: int = 512, h: int = 24, color=(220, 230, 255)) -> Path:
    """Thin glowing horizontal stroke with soft ends, for constellation lines."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    v = (y - (h - 1) / 2) / (h / 2)
    u = x / (w - 1)
    a = (np.exp(-(v / 0.18) ** 2) + 0.3 * np.exp(-(v / 0.6) ** 2)) * np.clip(np.minimum(u, 1 - u) / 0.04, 0, 1)
    rgba = np.zeros((h, w, 4), np.float32)
    rgba[..., :3] = color
    rgba[..., 3] = np.clip(a, 0, 1) * 255
    return _save(rgba, out)
```

and append `line_sprite(L / "sprites" / "line.png")` to the `made = [...]` list in `build_sample_assets`.

- [ ] **Step 4: Run tests**

Run: `python -m pytest tests/test_living_bg.py tests/test_glyphs.py -q`
Expected: all pass (existing tests prove the particle refactor kept behaviour)

---

### Task 5: Footage renderer for interlude backgrounds

**Files:**
- Create: `tools/footage.py`
- Test: `tests/test_footage.py`

**Interfaces:**
- Consumes: `tools.living_bg` — `Particles, _draw_particles, _plan_particles, _push, _smooth, write_video`; `deck.encore` — `CLIMAX, INTERLUDES, page_starts, spans`.
- Produces: `Shot(src, t0, t1, speed, crop=None)`, `OrbitRing(radius, tilt, t0, draw, color, gain=0.5)`, `Burst(t, radius, color, gain=1.0, rise=0.35, decay=1.6)`, `FootageSpec(...)`, `shot_filter(shot, fps, size) -> str`, `iter_sequence(shots, fps, size, xfade, total) -> Iterator[np.ndarray]`, `grade(img, name) -> np.ndarray`, `bright_centre(img, prev, thr=0.85) -> tuple[float, float] | None`, `burst_env(t, b) -> float`, `render_frames(spec) -> Iterator[np.ndarray]`, `encore_specs() -> dict[int, FootageSpec]`, CLI `python -m tools.footage 2 3 5 ...` writing `fx/encore/bg_p{i}.mp4`.

- [ ] **Step 1: Write failing tests** — `tests/test_footage.py`:

```python
import imageio.v2 as imageio
import numpy as np
import pytest

from tools.footage import (Burst, FootageSpec, OrbitRing, Shot, bright_centre, burst_env, encore_specs, grade,
                           iter_sequence, render_frames, shot_filter)


def _clip(tmp_path, name="c.mp4", seconds=2.0, fps=60, vx=60.0, bg=10, w=160, h=90):
    """Dark clip with a bright 10x10 dot whose left edge moves as x = 20 + vx * t px."""
    p = tmp_path / name
    wr = imageio.get_writer(str(p), fps=fps, codec="libx264", pixelformat="yuv420p", macro_block_size=1,
                            ffmpeg_params=["-crf", "12"])
    for k in range(int(seconds * fps)):
        a = np.full((h, w, 3), bg, np.uint8)
        x = int(20 + vx * k / fps)
        a[40:50, x:x + 10] = 255
        wr.append_data(a)
    wr.close()
    return p


def _dot_x(fr):
    xs = np.nonzero(fr.mean(2).max(0) > 150)[0]
    return xs.mean()


def test_slow_shot_stretches_time(tmp_path):
    p = _clip(tmp_path)
    frames = list(iter_sequence((Shot(str(p), 0.0, 1.0, 0.5),), 24, (160, 90), 0.0, 48))
    assert len(frames) == 48
    assert _dot_x(frames[24]) == pytest.approx(20 + 30 + 4.5, abs=4)


def test_sequence_crossfades_shots_then_holds_the_last_frame(tmp_path):
    a = _clip(tmp_path, "a.mp4", seconds=0.5, bg=10)
    b = _clip(tmp_path, "b.mp4", seconds=0.5, bg=200)
    frames = list(iter_sequence((Shot(str(a), 0.0, 0.5, 1.0), Shot(str(b), 0.0, 0.5, 1.0)), 24, (160, 90), 0.25, 40))
    assert len(frames) == 40
    assert 40 < frames[9][5, 5].mean() < 180
    assert np.array_equal(frames[-1], frames[-2])


def test_shot_filter_interpolates_only_very_slow_shots():
    assert "minterpolate" in shot_filter(Shot("x", 0, 1, 0.3), 24, (1920, 1080))
    assert "minterpolate" not in shot_filter(Shot("x", 0, 1, 0.5), 24, (1920, 1080))
    assert "crop=" in shot_filter(Shot("x", 0, 1, 1.0, crop=(0.1, 0.0, 0.8, 0.8)), 24, (1920, 1080))


def test_grade_crushes_blacks_and_tints_warm():
    dark = np.full((4, 4, 3), 0.02, np.float32)
    assert grade(dark, "night").max() < 0.01
    grey = np.full((4, 4, 3), 0.5, np.float32)
    g = grade(grey, "warm")[0, 0]
    assert g[0] > g[2] + 0.05


def test_bright_centre_finds_and_smooths_the_light():
    img = np.zeros((90, 160, 3), np.float32)
    img[40:50, 100:110] = 1.0
    c = bright_centre(img, None)
    assert c == pytest.approx((105 / 160, 45 / 90), abs=0.03)
    img2 = np.zeros_like(img)
    img2[40:50, 20:30] = 1.0
    c2 = bright_centre(img2, c)
    assert 25 / 160 < c2[0] < c[0]
    assert bright_centre(np.zeros_like(img), c) == c


def test_burst_envelope_rises_peaks_and_decays():
    b = Burst(2.0, 0.3, (1.0, 1.0, 1.0))
    assert burst_env(1.0, b) == 0.0
    assert burst_env(2.0, b) == pytest.approx(1.0)
    assert 0.0 < burst_env(3.0, b) < 0.6


def test_orbit_ring_lights_a_circle_round_the_tracked_light(tmp_path):
    p = _clip(tmp_path, vx=0.0)
    base = dict(seconds=1.0, grade="neutral", push=(1.0, 1.0), vignette=0.0, grain=0.0, size=(160, 90))
    plain = list(render_frames(FootageSpec((Shot(str(p), 0.0, 1.0, 1.0),), **base)))
    ringed = list(render_frames(FootageSpec((Shot(str(p), 0.0, 1.0, 1.0),),
                                            rings=(OrbitRing(0.2, 1.0, 0.0, 0.2, (1.0, 1.0, 1.0), gain=1.0),),
                                            **base)))
    x, y = 25 + 32, 45
    assert ringed[12][y, x].mean() > plain[12][y, x].mean() + 20


def test_encore_specs_cover_every_interlude_with_16x9_crops():
    from deck.encore import INTERLUDES, spans

    sp, s = encore_specs(), spans()
    assert sorted(sp) == list(INTERLUDES)
    assert all(sp[i].seconds == s[i] for i in sp)
    for spec in sp.values():
        for shot in spec.shots:
            assert shot.t1 > shot.t0 and 0.2 <= shot.speed <= 1.0
            if shot.crop:
                assert shot.crop[2] == pytest.approx(shot.crop[3])
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_footage.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'tools.footage'`

- [ ] **Step 3: Implement `tools/footage.py`**

```python
"""Turn trailer shots into slow, graded interlude backgrounds with light that follows the brightest source."""
from __future__ import annotations

import argparse
import math
import subprocess
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

import numpy as np

from tools.living_bg import Particles, _draw_particles, _plan_particles, _push, _smooth, write_video

ROOT = Path(__file__).resolve().parents[1]
FFMPEG = Path(r"C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Lib\site-packages"
              r"\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe")
LUMA = np.array([0.299, 0.587, 0.114], np.float32)
REVEAL = "sources/web-hq/trailer/ow_reveal.mp4"
ACCOLADES = "sources/web-hq/trailer/ow_accolades.mp4"
EOTE = "sources/web-hq/trailer/eote_launch.mp4"

# lift (crushed blacks), gamma, tint, saturation
GRADES = {
    "night": (0.035, 1.12, (0.92, 0.98, 1.06), 0.92),
    "warm": (0.03, 1.06, (1.06, 0.99, 0.90), 1.0),
    "teal": (0.035, 1.08, (0.94, 1.02, 1.04), 0.95),
    "neutral": (0.0, 1.0, (1.0, 1.0, 1.0), 1.0),
}


@dataclass(frozen=True)
class Shot:
    src: str
    t0: float
    t1: float
    speed: float
    crop: tuple[float, float, float, float] | None = None   # (left, top, width, height) source fractions


@dataclass(frozen=True)
class OrbitRing:
    radius: float      # semi-major axis as a frame-width fraction
    tilt: float        # minor / major axis
    t0: float
    draw: float        # seconds to sweep the whole ellipse
    color: tuple[float, float, float]
    gain: float = 0.5


@dataclass(frozen=True)
class Burst:
    t: float           # peak time
    radius: float
    color: tuple[float, float, float]
    gain: float = 1.0
    rise: float = 0.35
    decay: float = 1.6


@dataclass(frozen=True)
class FootageSpec:
    shots: tuple[Shot, ...]
    seconds: float
    fps: int = 24
    grade: str = "night"
    push: tuple[float, float] = (1.0, 1.06)
    vignette: float = 0.35
    grain: float = 0.02
    xfade: float = 0.6
    fade_in: float = 0.0
    particles: tuple[Particles, ...] = ()
    rings: tuple[OrbitRing, ...] = ()
    bursts: tuple[Burst, ...] = ()
    seed: int = 11
    size: tuple[int, int] = (1920, 1080)


def _path(p: str) -> Path:
    q = Path(p)
    return q if q.is_absolute() else ROOT / q


def shot_filter(shot: Shot, fps: int, size: tuple[int, int]) -> str:
    parts = []
    if shot.crop:
        l, t, w, h = shot.crop
        parts.append(f"crop=iw*{w:g}:ih*{h:g}:iw*{l:g}:ih*{t:g}")
    parts.append(f"scale={size[0]}:{size[1]}:flags=lanczos")
    if shot.speed < 0.4:
        parts.append(f"minterpolate=fps={fps / shot.speed:g}:mi_mode=mci:mc_mode=aobmc:vsbmc=1")
    parts += [f"setpts=PTS/{shot.speed:g}", f"fps={fps}"]
    return ",".join(parts)


def iter_shot(shot: Shot, fps: int, size: tuple[int, int]) -> Iterator[np.ndarray]:
    w, h = size
    cmd = [str(FFMPEG), "-loglevel", "error", "-ss", f"{shot.t0:.3f}", "-t", f"{shot.t1 - shot.t0:.3f}",
           "-i", str(_path(shot.src)), "-vf", shot_filter(shot, fps, size), "-an", "-f", "rawvideo",
           "-pix_fmt", "rgb24", "-"]
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    n = w * h * 3
    try:
        while True:
            buf = p.stdout.read(n)
            if len(buf) < n:
                break
            yield np.frombuffer(buf, np.uint8).reshape(h, w, 3)
    finally:
        p.kill()
        p.wait()


def iter_sequence(shots, fps: int, size: tuple[int, int], xfade: float, total: int) -> Iterator[np.ndarray]:
    """Shots back to back, each joined to the next by a crossfade, padded with the last frame to ``total``."""
    k = int(round(xfade * fps))
    n, last, tail = 0, None, []
    for si, shot in enumerate(shots):
        frames = iter_shot(shot, fps, size)
        for j, a in enumerate(tail):
            b = next(frames, a)
            wgt = (j + 1) / (len(tail) + 1)
            last = (a.astype(np.float32) * (1 - wgt) + b.astype(np.float32) * wgt).astype(np.uint8)
            yield last
            n += 1
            if n >= total:
                return
        held: deque = deque()
        for fr in frames:
            if si < len(shots) - 1 and k:
                held.append(fr)
                if len(held) <= k:
                    continue
                fr = held.popleft()
            last = fr
            yield fr
            n += 1
            if n >= total:
                return
        tail = list(held)
    while last is not None and n < total:
        yield last
        n += 1


def grade(img: np.ndarray, name: str) -> np.ndarray:
    lift, gamma, tint, sat = GRADES[name]
    x = np.clip((img - lift) / (1 - lift), 0, 1) ** gamma
    grey = (x @ LUMA)[..., None]
    x = grey + (x - grey) * sat
    return np.clip(x * np.array(tint, np.float32), 0, 1)


def bright_centre(img: np.ndarray, prev, thr: float = 0.85):
    small = img[::8, ::8] @ LUMA
    ys, xs = np.nonzero(small >= thr)
    if xs.size < 2:
        return prev
    c = (xs.mean() * 8 / img.shape[1], ys.mean() * 8 / img.shape[0])
    if prev is None:
        return c
    return (prev[0] * 0.8 + c[0] * 0.2, prev[1] * 0.8 + c[1] * 0.2)


def burst_env(t: float, b: Burst) -> float:
    if t < b.t - b.rise:
        return 0.0
    if t <= b.t:
        return float(_smooth((t - (b.t - b.rise)) / b.rise))
    return math.exp(-(t - b.t) / b.decay)


def _draw_ring(add: np.ndarray, ring: OrbitRing, centre, t: float) -> None:
    if t < ring.t0:
        return
    h, w = add.shape[:2]
    cover = float(_smooth((t - ring.t0) / max(ring.draw, 1e-3))) * 2 * math.pi
    rx = ring.radius * w
    ry = rx * ring.tilt
    cx, cy = centre[0] * w, centre[1] * h
    x0, x1 = max(int(cx - rx - 4), 0), min(int(cx + rx + 5), w)
    y0, y1 = max(int(cy - ry - 4), 0), min(int(cy + ry + 5), h)
    if x0 >= x1 or y0 >= y1:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    dx, dy = (xx - cx) / rx, (yy - cy) / ry
    d = np.abs(np.hypot(dx, dy) - 1.0) * ry
    ang = np.mod(np.arctan2(dy, dx), 2 * math.pi)
    a = np.exp(-(d / 1.6) ** 2) * (ang <= cover) * ring.gain
    add[y0:y1, x0:x1] += a[..., None] * np.array(ring.color, np.float32)


def _draw_burst(add: np.ndarray, b: Burst, centre, t: float, xx: np.ndarray, yy: np.ndarray) -> None:
    env = burst_env(t, b)
    if env < 0.01:
        return
    h, w = add.shape[:2]
    r = np.hypot(xx - centre[0] * w, yy - centre[1] * h) / (b.radius * w)
    add += (np.exp(-r * r) * env * b.gain)[..., None] * np.array(b.color, np.float32)


def _vignette(w: int, h: int, amount: float) -> np.ndarray:
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2)) / math.sqrt(2)
    return 1 - amount * np.clip((r - 0.35) / 0.65, 0, 1) ** 1.5


def render_frames(spec: FootageSpec) -> Iterator[np.ndarray]:
    w, h = spec.size
    total = int(round(spec.fps * spec.seconds))
    rng = np.random.default_rng(spec.seed)
    parts = [(p, _plan_particles(p, spec.seconds, w, h, rng)) for p in spec.particles]
    stamps: dict = {}
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32) if spec.bursts else (None, None)
    vig = _vignette(w, h, spec.vignette)[..., None]
    centre = None
    for f, fr in enumerate(iter_sequence(spec.shots, spec.fps, spec.size, spec.xfade, total)):
        t = f / spec.fps
        img = grade(fr.astype(np.float32) / 255.0, spec.grade)
        img = _push(img, spec.push[0] + (spec.push[1] - spec.push[0]) * t / max(spec.seconds, 1e-6))
        add = np.zeros_like(img)
        if spec.rings or spec.bursts:
            centre = bright_centre(img, centre)
            if centre is not None:
                for ring in spec.rings:
                    _draw_ring(add, ring, centre, t)
                for b in spec.bursts:
                    _draw_burst(add, b, centre, t, xx, yy)
        _draw_particles(add, parts, t, stamps)
        img = (1 - (1 - img) * (1 - np.clip(add, 0, 1))) * vig
        if spec.grain:
            img = img + rng.normal(0.0, spec.grain, (h, w, 1)).astype(np.float32)
        if spec.fade_in:
            img = img * float(_smooth(t / spec.fade_in))
        yield np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)


def encore_specs() -> dict[int, FootageSpec]:
    from deck.encore import CLIMAX, page_starts, spans

    s, st = spans(), page_starts()
    pale = (0.75, 0.88, 1.0)
    return {
        2: FootageSpec((Shot(REVEAL, 9.0, 18.0, 0.75),), s[2], grade="warm", fade_in=0.8,
                       particles=(Particles(36, 0.3, (1.0, 0.72, 0.38), (5.0, 12.0), (3.0, -10.0), 10.0,
                                            (0.0, 0.1, 1.0, 1.0), 5.0),)),
        3: FootageSpec((Shot(REVEAL, 59.0, 62.5, 0.3, crop=(0.14, 0.0, 0.72, 0.72)),), s[3], grade="teal",
                       push=(1.0, 1.08),
                       particles=(Particles(60, 2.0, (0.85, 0.92, 1.0), (1.0, 2.5), (0.0, -90.0), 6.0,
                                            (0.0, 0.3, 1.0, 1.1), 3.0),)),
        5: FootageSpec((Shot(REVEAL, 37.2, 41.6, 0.4),), s[5], grade="night",
                       rings=(OrbitRing(0.10, 0.32, 2.0, 1.6, pale), OrbitRing(0.17, 0.32, 3.4, 1.8, pale),
                              OrbitRing(0.26, 0.32, 4.9, 2.0, pale))),
        7: FootageSpec((Shot(REVEAL, 22.5, 33.3, 0.9),), s[7], grade="teal"),
        9: FootageSpec((Shot(REVEAL, 42.0, 46.5, 0.5), Shot(ACCOLADES, 11.0, 12.6, 0.5)), s[9], grade="warm",
                       particles=(Particles(220, 0.5, (1.0, 0.85, 0.6), (0.8, 1.8), (-70.0, 10.0), 4.0,
                                            (0.1, 0.0, 1.1, 1.0), 3.0),)),
        11: FootageSpec((Shot(EOTE, 3.8, 12.8, 0.75),), s[11], grade="warm",
                        bursts=(Burst(CLIMAX - st[11], 0.35, (1.0, 0.85, 0.65), gain=1.1, rise=0.4, decay=1.8),)),
        13: FootageSpec((Shot(ACCOLADES, 1.0, 5.0, 0.35, crop=(0.30, 0.15, 0.70, 0.70)),), s[13], grade="night",
                        particles=(Particles(50, 0.5, (0.85, 0.9, 1.0), (0.8, 1.8), (-12.0, 2.0), 2.0,
                                             (0.25, 0.30, 0.70, 0.85), 5.0),)),
        16: FootageSpec((Shot(ACCOLADES, 22.1, 23.7, 0.25),), s[16], grade="neutral", push=(1.0, 1.10)),
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("pages", nargs="+", type=int)
    a = ap.parse_args()
    specs = encore_specs()
    for i in a.pages:
        spec = specs[i]
        print(write_video(render_frames(spec), ROOT / "fx" / "encore" / f"bg_p{i}.mp4", spec.fps))


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest tests/test_footage.py -q`
Expected: 8 passed

---

### Task 6: Editions — build either deck from one builder

**Files:**
- Create: `deck/editions.py`
- Modify: `build_layered.py`
- Test: `tests/test_editions.py`

**Interfaces:**
- Consumes: `deck.storyboard` (`pages, page_starts, TRANSITIONS, TRANSITION_NOTES`), `deck.encore` (same names + `MUSIC`).
- Produces: `Edition` dataclass, `edition(name) -> Edition`; `build_layered.transition_duration(ed, page_i) -> float`, `build_layered.check_slide(slide, K, page, ed)`, `build_layered.build(ed, page_ids, out, video) -> dict`, CLI `--edition {v1,encore}`.

- [ ] **Step 1: Write failing tests** — `tests/test_editions.py`:

```python
import pytest

from build_layered import TRANSITION_LAG, transition_duration
from deck.editions import edition


def test_v1_edition_is_unchanged():
    ed = edition("v1")
    assert ed.ids == tuple(range(1, 10)) and ed.default_pages == (1, 4)
    assert ed.music == "media/main-title.mp3"
    assert ed.transitions[8] == ("ppEffectWarpIn", 1.0)
    assert ed.default_out == "out/星辰之子-样片-p1p4.pptx"


def test_encore_edition_covers_seventeen_pages():
    ed = edition("encore")
    assert ed.ids == tuple(range(1, 18)) and ed.default_pages == ed.ids
    assert ed.music == "media/travelers-encore.mp3"
    assert ed.default_out == "out/星辰之子-旅者安可版.pptx"
    assert ed.export_timeout >= 1800


def test_transition_duration_subtracts_the_measured_lag():
    ed = edition("v1")
    assert transition_duration(ed, 2) == round(0.8 - TRANSITION_LAG["ppEffectFlashbulb"], 2)


def test_unknown_edition_is_rejected():
    with pytest.raises(ValueError):
        edition("v2")
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest tests/test_editions.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.editions'`

- [ ] **Step 3: Implement `deck/editions.py`**

```python
"""The decks this project can build: storyboard, music and output defaults for each."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from deck.storyboard import Page


@dataclass(frozen=True)
class Edition:
    name: str
    ids: tuple[int, ...]
    default_pages: tuple[int, ...]
    pages: Callable[[list[int]], list[Page]]
    page_starts: Callable[[], dict[int, float]]
    transitions: dict[int, tuple[str, float]]
    notes: dict[int, str]
    music: str
    default_out: str
    export_timeout: float


def edition(name: str) -> Edition:
    if name == "v1":
        from deck import storyboard as sb

        return Edition("v1", tuple(range(1, 10)), (1, 4), sb.pages, sb.page_starts, sb.TRANSITIONS,
                       sb.TRANSITION_NOTES, "media/main-title.mp3", "out/星辰之子-样片-p1p4.pptx", 900)
    if name == "encore":
        from deck import encore as en

        ids = tuple(range(1, 18))
        return Edition("encore", ids, ids, en.pages, en.page_starts, en.TRANSITIONS, en.TRANSITION_NOTES,
                       en.MUSIC, "out/星辰之子-旅者安可版.pptx", 2400)
    raise ValueError(f"unknown edition {name!r}")
```

- [ ] **Step 4: Rewire `build_layered.py`**

Replace the storyboard import and the `DEGRADED` line:

```python
from deck.build_plan import SLIDE_H, SLIDE_W  # noqa: E402
from deck.editions import Edition, edition  # noqa: E402
from deck.storyboard import TEXT_GLINT_TAIL, TEXT_LEAD, Layer, Page, Text, validate  # noqa: E402

LOG: list[str] = []
```

Replace `transition_duration`:

```python
def transition_duration(ed: Edition, page_i: int) -> float:
    eff, dur = ed.transitions[page_i]
    return round(dur - TRANSITION_LAG.get(eff, 0.0), 2)
```

In `check_slide`, change the signature to `def check_slide(slide, K, page: Page, ed: Edition) -> list[str]:` and its transition check to use `ed.transitions[page.i][0]` (both occurrences).

Change `export_video(pres, out_mp4, timeout=900)` callers to pass `ed.export_timeout`.

Replace the head of `build` and its body references:

```python
def build(ed: Edition, page_ids: list[int], out: Path, video: bool) -> dict:
    import win32com.client as wc

    from deck.legibility import check_page

    ps = ed.pages(page_ids)
    errs = validate(ps)
    for p in ps:
        for path in [ROOT / p.bg_video, *(ROOT / l.src for l in p.layers)]:
            if not path.is_file():
                errs.append(f"missing asset {path}")
    if not errs:
        for p in ps:
            errs += check_page(p)
    if errs:
        raise SystemExit("refusing to build:\n  " + "\n  ".join(errs))
    starts = ed.page_starts()
    continuous = [p.i for p in ps] == sorted(starts)
    mp3 = ROOT / ed.music
```

and inside the slide loop use `tr.EntryEffect = K(ed.transitions[p.i][0])`, `tr.Duration = transition_duration(ed, p.i)`, `problems += check_slide(slide, K, p, ed)`, `mp4 = export_video(pres, out.with_suffix(".mp4"), ed.export_timeout)`, and return `degraded=list(ed.notes.values())`.

Replace `main`:

```python
def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--edition", choices=["v1", "encore"], default="v1")
    ap.add_argument("--pages", default=None)
    ap.add_argument("--out", default=None)
    ap.add_argument("--video", action="store_true")
    a = ap.parse_args(argv)
    ed = edition(a.edition)
    page_ids = [int(x) for x in a.pages.split(",")] if a.pages else list(ed.default_pages)
    out = Path(a.out or ed.default_out)
    if not out.is_absolute():
        out = ROOT / out
    res = build(ed, page_ids, out, a.video)
    print(json.dumps(res, ensure_ascii=False, indent=2))
    return 1 if res["problems"] else 0
```

- [ ] **Step 5: Run the whole suite**

Run: `python -m pytest -q`
Expected: all pass (80 previous + new)

---

### Task 7: Render, build, calibrate sync, verify, deliver

**Files:**
- Create: `_tmp/measure_sync.py` (verification tooling, not shipped)
- Modify: `build_layered.py` (`TRANSITION_LAG` entries for new transition types)
- Output: `fx/encore/bg_p*.mp4`, `out/星辰之子-旅者安可版.pptx`, `out/星辰之子-旅者安可版.mp4`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Regenerate sprites (adds `line.png`) and render copy-page backgrounds**

Run: `python -m tools.glyphs sample; python -m tools.living_bg --edition encore 1 4 6 8 10 12 14 15 17`
Expected: nine paths `fx/encore/bg_p{1,4,6,8,10,12,14,15,17}.mp4` printed.

- [ ] **Step 2: Render interlude backgrounds**

Run: `python -m tools.footage 2 3 5 7 9 11 13 16`
Expected: eight paths `fx/encore/bg_p{2,3,5,7,9,11,13,16}.mp4` printed.

- [ ] **Step 3: Review interlude frames before building** — contact sheet of 4 frames per interlude:

```powershell
python -c "
import subprocess
from PIL import Image, ImageDraw
from deck.encore import INTERLUDES, spans
ff=r'C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
s=spans(); W,H=400,225
sheet=Image.new('RGB',(4*W,len(INTERLUDES)*H))
for r,i in enumerate(INTERLUDES):
    for c in range(4):
        t=s[i]*(0.1+0.27*c); p=f'_tmp/il_{i}_{c}.png'
        subprocess.run([ff,'-y','-loglevel','error','-ss',f'{t:.2f}','-i',f'fx/encore/bg_p{i}.mp4','-frames:v','1',p],check=True)
        im=Image.open(p).resize((W,H)); ImageDraw.Draw(im).text((5,5),f'P{i} t={t:.1f}',fill=(255,230,120)); sheet.paste(im,(c*W,r*H))
sheet.save('_tmp/encore_interludes.jpg',quality=86)
"
```

Check by eye: no cockpit HUD or rating icon visible on p3/p13; orbit rings centred on the sun on p5; burst on the sun near t≈9 s on p11; no minterpolate warping on p3/p13/p16 (if warped, change that shot's speed to ≥0.4 and lengthen `t1` where the source allows, otherwise accept frame blending by replacing the `minterpolate` filter with `minterpolate=fps={fps/speed:g}:mi_mode=blend`). Re-render only changed pages.

- [ ] **Step 4: First build and export**

Run: `python build_layered.py --edition encore --video`
Expected: `saved ...星辰之子-旅者安可版.pptx`, `exported ...星辰之子-旅者安可版.mp4`, `"problems": []`. If the legibility gate refuses a page, move that page's text box or retime the overlapping layer, re-run `python -m pytest tests/test_encore.py -q`, rebuild.

- [ ] **Step 5: Measure transition lag** — create `_tmp/measure_sync.py`:

```python
import sys

import imageio
import numpy as np

sys.path.insert(0, ".")
from deck.editions import edition

ed = edition(sys.argv[2] if len(sys.argv) > 2 else "encore")
S, T = ed.page_starts(), ed.transitions
lead = T[1][1]
r = imageio.get_reader(sys.argv[1])
fps = r.get_meta_data()["fps"]
prev, d = None, []
for f in r:
    g = f[::8, ::8].astype(np.float32).mean(2)
    d.append(0.0 if prev is None else float(np.abs(g - prev).mean()))
    prev = g
d = np.array(d)
still = d < 0.15
ends, k = [], 0
while k < len(d):
    if still[k]:
        j = k
        while j < len(d) and still[j]:
            j += 1
        if j - k >= 3:
            ends.append(j / fps)
        k = j
    else:
        k += 1
drift = {}
for i in sorted(S)[1:]:
    exp = lead + S[i] - T[i][1]
    near = [e for e in ends if abs(e - exp) < 1.5]
    if near:
        drift[i] = min(near, key=lambda e: abs(e - exp)) - exp
for i in sorted(drift):
    nxt = drift.get(i + 1)
    extra = None if nxt is None else round(nxt - drift[i], 2)
    print(f"p{i:2d} {T[i][0]:28s} drift {drift[i]:+.2f}  residual into p{i} -> {extra}")
print("total", round(len(d) / fps, 2), "s")
```

Run: `python _tmp/measure_sync.py "out/星辰之子-旅者安可版.mp4"`

For each page i that has a residual, the true lag of `T[i][0]` is `TRANSITION_LAG.get(T[i][0], 0) + residual`. Write those values into `TRANSITION_LAG` in `build_layered.py` (keep the v1 entries; add the new effect names). Rebuild (Step 4) and re-measure until every drift equals the first one within ±0.1 s.

- [ ] **Step 6: Frame review** — one sheet per half of the deck, 4 frames per page at 0.2/0.45/0.7/0.92 of each span, sampling at `lead_offset + start + t` where `lead_offset` is the measured content start of page 1:

```powershell
python -c "
import subprocess, sys
from PIL import Image, ImageDraw
from deck.encore import page_starts, spans
ff=r'C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
S,s=page_starts(),spans(); lead=2.5+float(sys.argv[1]) if len(sys.argv)>1 else 2.5
W,H=400,225
for half,ids in (('a',range(1,10)),('b',range(10,18))):
    ids=list(ids); sheet=Image.new('RGB',(4*W,len(ids)*H))
    for r,i in enumerate(ids):
        for c,q in enumerate((0.2,0.45,0.7,0.92)):
            t=s[i]*q; p=f'_tmp/enc_{i}_{c}.png'
            subprocess.run([ff,'-y','-loglevel','error','-ss',f'{lead+S[i]+t:.2f}','-i','out/星辰之子-旅者安可版.mp4','-frames:v','1',p],check=True)
            im=Image.open(p).resize((W,H)); ImageDraw.Draw(im).text((5,5),f'P{i} t={t:.1f}s',fill=(255,230,120)); sheet.paste(im,(c*W,r*H))
    sheet.save(f'_tmp/encore_frames_{half}.jpg',quality=86)
"
```

Check: animation order per page, text never under glare, constellation on p1, white dwarf on p8, orbiting dust on p15, last star on p17, interlude grading consistent. Fix, rebuild, re-check.

- [ ] **Step 7: Run all tests and deliver**

Run: `python -m pytest -q` — expected all pass.

Then copy `_tmp/encore_frames_a.jpg`, `_tmp/encore_frames_b.jpg` and the MP4 to `C:\Users\Administrator\.cursor\projects\d-gongzuo-mod\assets\`, open each with `& "D:\gongzuo\cursor\resources\app\bin\cursor.cmd" -r <path>`, launch the PPTX with `Start-Process`, and report in Chinese: page list and beats, sync result (measured drift), file sizes, anything degraded.
