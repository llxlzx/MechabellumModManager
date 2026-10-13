# 星辰之子 · 分层浪漫版（样片 p1 + p4）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a 2-slide sample deck (p1 冷开场, p4 铜) where each slide is assembled layer by layer — living background video, cut-out subjects, light sprites, Nomai spiral, poster typography — with romantic native animations and transitions, plus a PowerPoint-exported mp4 for review.

**Architecture:** `deck/storyboard.py` is the only source of per-page timing/layout data. Asset generators (`tools/glyphs.py`, `tools/split_layers.py`, `tools/living_bg.py`) write PNG/MP4 files under `layers/` and `fx/`. `build_layered.py` reads the storyboard and assembles the deck in real PowerPoint via COM, then optionally exports an mp4 and frame grabs.

**Tech Stack:** Python 3.12, numpy, Pillow, scipy.ndimage, rembg (isnet-general-use), imageio + bundled ffmpeg, pywin32 COM → PowerPoint 16.0, pytest.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Work only inside `demos/outer-wilds-real-pptx/`; never modify `demos/outer-wilds-cinematic-pptx/`, Mod Manager code, or `demos/bocchi-ppt-demo/`.
- No git commits. `media/`, `fx/`, `out/`, `_tmp/`, `layers/` stay untracked (add `layers/` to `.gitignore`).
- Copy lines are used verbatim from `copy.json`; segments of a line must concatenate back to the exact line.
- Slide holds come from `timing.json` (p1 = 7.0 s, p4 = 9.0 s; full deck 78.3 s).
- Slide size 960×540 pt; all rendered images/videos 1920×1080.
- Transitions (entering slide k): 1 FadeSmoothly 2.0 s · 2 Flashbulb · 3 RippleCenter · 4 VortexLeft · 5 Dissolve (Shimmer unavailable via COM) · 6 FlyThroughIn · 7 GlitterDiamondDown · 8 ZoomIn · 9 Fade (through black) 2.5 s. All distinct.
- Every page has ≥ 3 staged entrances; nothing may be fully visible at t = 0 except the background video.
- Only game imagery (+ light AI retouch); personal learning use.
- Run Python from `demos/outer-wilds-real-pptx/` with `$env:PYTHONIOENCODING="utf-8"`.
- ffmpeg: `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe`.

---

### Task 1: Storyboard data model (p1, p4 + all transitions)

**Files:**
- Create: `deck/storyboard.py`
- Test: `tests/test_storyboard.py`

**Interfaces:**
- Produces: dataclasses `Layer`, `Text`, `Page`; `TRANSITIONS: dict[int, tuple[str, float]]`; `TRANSITION_NOTES: dict[int, str]`; `holds() -> dict[int, float]`; `page_starts() -> dict[int, float]`; `pages(which: list[int]) -> list[Page]`; `validate(pages: list[Page]) -> list[str]`; constants `P1_CORE = (0.488, 0.52)`, `P4_BURST = (0.56, 0.30)`.

- [ ] **Step 1: Write the failing tests** — `tests/test_storyboard.py`:

```python
import json
from pathlib import Path

from deck.storyboard import (P1_CORE, TRANSITIONS, Layer, Page, Text, holds, page_starts, pages,
                             validate)

ROOT = Path(__file__).resolve().parents[1]
COPY = json.loads((ROOT / "copy.json").read_text(encoding="utf-8"))


def test_holds_and_starts_follow_timing():
    h = holds()
    assert h[1] == 7.0 and h[4] == 9.0
    assert abs(sum(h.values()) - 78.3) < 1e-6
    s = page_starts()
    assert s[1] == 0.0 and s[2] == 7.0 and s[4] == 27.0 and s[9] == 72.0


def test_transitions_cover_all_pages_and_are_distinct():
    assert sorted(TRANSITIONS) == list(range(1, 10))
    effects = [e for e, _ in TRANSITIONS.values()]
    assert len(set(effects)) == 9
    assert TRANSITIONS[1][0] == "ppEffectFadeSmoothly"
    assert TRANSITIONS[9] == ("ppEffectFade", 2.5)


def test_sample_pages_are_valid():
    ps = pages([1, 4])
    assert [p.i for p in ps] == [1, 4]
    assert validate(ps) == []


def test_p4_text_segments_rebuild_the_copy_line_verbatim():
    p4 = pages([4])[0]
    assert "".join(t.text for t in p4.texts) == COPY["lines"]["L3a"]


def test_every_page_stages_at_least_three_entrances_after_zero():
    for p in pages([1, 4]):
        assert len(p.layers) + len(p.texts) >= 3
        assert all(x.t > 0 for x in (*p.layers, *p.texts))


def test_validate_flags_overrun_and_bad_text():
    bad = Page(4, 9.0, "fx/bg_p4.mp4",
               layers=(Layer("A", "a.png", (0, 0, 1, 1), 8.5, 1.0),),
               texts=(Text("T", "错", (0.1, 0.8, 0.5, 0.1), 1.0, 0.5, 28),))
    errs = validate([bad])
    assert any("overruns" in e for e in errs)
    assert any("verbatim" in e for e in errs)
    assert any("entrances" in e for e in errs)


def test_p1_core_glow_is_centred_on_station_core():
    g = next(l for l in pages([1])[0].layers if l.name == "CoreGlow")
    cx = g.box[0] + g.box[2] / 2
    cy = g.box[1] + g.box[3] / 2
    assert abs(cx - P1_CORE[0]) < 1e-6 and abs(cy - P1_CORE[1]) < 1e-6
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest -q tests/test_storyboard.py`
Expected: collection error `ModuleNotFoundError: No module named 'deck.storyboard'`.

- [ ] **Step 3: Implement** — `deck/storyboard.py`:

```python
"""Single source of truth for the layered-romance deck: per-page layers, timings, transitions.

Times are seconds from slide start. Boxes are (left, top, width, height) as slide fractions.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASPECT = 16 / 9

ENTER_BASES = ("fade", "wheel", "wipe_left", "wipe_down")


@dataclass(frozen=True)
class Layer:
    name: str
    src: str
    box: tuple[float, float, float, float]
    t: float
    dur: float
    base: str = "fade"
    scale_from: float | None = None
    spin: float | None = None
    move_from: tuple[float, float] | None = None
    exit_t: float | None = None
    exit_dur: float = 0.8


@dataclass(frozen=True)
class Text:
    name: str
    text: str
    box: tuple[float, float, float, float]
    t: float
    dur: float
    size: float
    base: str = "wipe_left"
    align: str = "left"


@dataclass(frozen=True)
class Page:
    i: int
    hold: float
    bg_video: str
    layers: tuple[Layer, ...]
    texts: tuple[Text, ...] = ()
    copy_key: str | None = None
    motif: str = ""


TRANSITIONS: dict[int, tuple[str, float]] = {
    1: ("ppEffectFadeSmoothly", 2.0),
    2: ("ppEffectFlashbulb", 1.0),
    3: ("ppEffectRippleCenter", 1.4),
    4: ("ppEffectVortexLeft", 1.6),
    5: ("ppEffectDissolve", 1.6),
    6: ("ppEffectFlyThroughIn", 1.4),
    7: ("ppEffectGlitterDiamondDown", 1.6),
    8: ("ppEffectZoomIn", 1.2),
    9: ("ppEffectFade", 2.5),
}
TRANSITION_NOTES = {5: "微光(Shimmer) 不在 PowerPoint COM 枚举中，降级为 溶解(Dissolve)"}

P1_CORE = (0.488, 0.52)
P4_BURST = (0.56, 0.30)


def holds() -> dict[int, float]:
    t = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))
    return {int(s["i"]): float(s["hold_sec"]) for s in t["slides"]}


def page_starts() -> dict[int, float]:
    out, acc = {}, 0.0
    for i, h in sorted(holds().items()):
        out[i] = round(acc, 6)
        acc += h
    return out


def _copy_lines() -> dict[str, str]:
    return json.loads((ROOT / "copy.json").read_text(encoding="utf-8"))["lines"]


def _square(center: tuple[float, float], width: float) -> tuple[float, float, float, float]:
    """Box of a square sprite (in pixels) centred on ``center``; ``width`` is a slide fraction."""
    h = width * ASPECT
    return (center[0] - width / 2, center[1] - h / 2, width, h)


def _p1(hold: float) -> Page:
    layers: list[Layer] = [
        Layer("CoreGlow", "layers/sprites/glow_warm.png", _square(P1_CORE, 0.22), 2.0, 1.6, scale_from=0.2),
        Layer("Station", "layers/p1/station.png", (0, 0, 1, 1), 2.8, 2.2, base="wheel", scale_from=0.88, spin=6.0),
        Layer("CrossRays", "layers/sprites/cross_rays.png", _square(P1_CORE, 0.5), 4.0, 1.2, scale_from=0.3),
    ]
    for k, ch in enumerate("STARDUST"):
        left = 0.1 + k * 0.1
        t = 4.6 + k * 0.2
        layers.append(Layer(f"Spark{k}", "layers/sprites/spark.png", _square((left + 0.05, 0.36), 0.03),
                            t, 0.45, move_from=(0.0, -0.3), exit_t=t + 0.45, exit_dur=0.3))
        layers.append(Layer(f"Letter{k}", f"layers/type/letter_{ch}_{k}.png", (left, 0.37, 0.1, 0.222), t + 0.35, 0.6))
    return Page(1, hold, "fx/bg_p1.mp4", tuple(layers), motif="第一缕光")


def _p4(hold: float) -> Page:
    line = _copy_lines()["L3a"]
    seg = ["我们那微量的铜，", "更是需要见证", "一颗白矮星的死亡，"]
    assert "".join(seg) == line, "copy.json L3a changed; update the p4 segments"
    layers = (
        Layer("Ground", "layers/p4/ground.png", (0, 0, 1, 1), 1.5, 2.0),
        Layer("Villager", "layers/p4/villager.png", (0, 0, 1, 1), 3.0, 2.0, move_from=(-0.08, 0.0)),
        Layer("KeyChar", "layers/type/key_铜.png", _square((P4_BURST[0], 0.40), 0.2), 5.5, 1.4, scale_from=1.25),
        Layer("Nomai", "layers/type/nomai_p4.png", _square((0.5, 0.80), 0.16), 5.9, 1.0, base="wheel",
              spin=90.0, exit_t=6.7, exit_dur=0.6),
    )
    texts = (
        Text("Seg1", seg[0], (0.08, 0.72, 0.42, 0.08), 6.6, 0.7, 28),
        Text("Seg2", seg[1], (0.30, 0.80, 0.40, 0.08), 7.1, 0.7, 28),
        Text("Seg3", seg[2], (0.50, 0.88, 0.44, 0.08), 7.6, 0.7, 28),
    )
    return Page(4, hold, "fx/bg_p4.mp4", layers, texts, copy_key="L3a", motif="目送")


_BUILDERS = {1: _p1, 4: _p4}


def pages(which: list[int]) -> list[Page]:
    h = holds()
    missing = [i for i in which if i not in _BUILDERS]
    if missing:
        raise ValueError(f"pages not designed yet: {missing}")
    return [_BUILDERS[i](h[i]) for i in which]


def validate(ps: list[Page]) -> list[str]:
    errs: list[str] = []
    lines = _copy_lines()
    for p in ps:
        items = (*p.layers, *p.texts)
        if len(items) < 3:
            errs.append(f"p{p.i}: needs >= 3 staged entrances, has {len(items)}")
        for x in items:
            end = max(x.t + x.dur, (x.exit_t + x.exit_dur) if getattr(x, "exit_t", None) is not None else 0)
            if x.t <= 0:
                errs.append(f"p{p.i}/{x.name}: starts at t<=0 (would be visible before the background)")
            if end > p.hold + 1e-6:
                errs.append(f"p{p.i}/{x.name}: overruns hold ({end:.2f} > {p.hold})")
            if x.base not in ENTER_BASES:
                errs.append(f"p{p.i}/{x.name}: unknown entrance base {x.base}")
        if p.texts:
            joined = "".join(t.text for t in p.texts)
            if p.copy_key is None or joined != lines.get(p.copy_key):
                errs.append(f"p{p.i}: text segments are not the verbatim copy line")
    return errs
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest -q tests/test_storyboard.py`
Expected: 7 passed.

---

### Task 2: Typography and light sprites (`tools/glyphs.py`)

**Files:**
- Create: `tools/glyphs.py`
- Test: `tests/test_glyphs.py`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `font_path(candidates: list[str]) -> Path`; `glow_sprite(out, size=512, color=(255,236,200)) -> Path`; `spark_sprite(out) -> Path`; `cross_rays(out, size=1024, color=(255,240,215)) -> Path`; `letter_png(ch, out, alpha=0.26, height=300) -> Path`; `key_char_png(text, out, font_candidates=KEY_FONTS, height=420, color=(255,226,170)) -> Path`; `nomai_spiral(out, size=768, seed=0) -> Path`; `font_sheet(text, out) -> Path`; CLI `python -m tools.glyphs sample` writing every PNG the p1/p4 storyboard names under `layers/sprites/` and `layers/type/`.

- [ ] **Step 1: Write the failing tests** — `tests/test_glyphs.py`:

```python
import numpy as np
from PIL import Image

from tools.glyphs import (cross_rays, glow_sprite, key_char_png, letter_png, nomai_spiral,
                          spark_sprite)


def _rgba(p):
    return np.asarray(Image.open(p).convert("RGBA"), dtype=np.float32)


def test_glow_sprite_is_radial_and_transparent_at_edges(tmp_path):
    a = _rgba(glow_sprite(tmp_path / "g.png", size=128))
    assert a.shape == (128, 128, 4)
    assert a[64, 64, 3] > 240 and a[0, 0, 3] < 2 and a[64, 2, 3] < 10


def test_spark_and_rays_have_bright_centre(tmp_path):
    s = _rgba(spark_sprite(tmp_path / "s.png"))
    assert s[s.shape[0] // 2, s.shape[1] // 2, 3] > 240
    r = _rgba(cross_rays(tmp_path / "r.png", size=256))
    assert r[128, 128, 3] > 200
    assert r[80, 80, 3] > r[80, 128, 3] + 30, "rays run along the diagonals"


def test_letter_is_faint_white_on_transparent(tmp_path):
    a = _rgba(letter_png("S", tmp_path / "l.png", alpha=0.26, height=200))
    ink = a[..., 3] > 10
    assert ink.mean() > 0.05
    assert a[..., 3].max() <= 0.26 * 255 + 2
    assert a[ink][:, :3].min() > 200


def test_key_char_has_glow_halo_beyond_ink(tmp_path):
    a = _rgba(key_char_png("铜", tmp_path / "k.png", height=256))
    solid = a[..., 3] > 230
    halo = (a[..., 3] > 15) & ~solid
    assert solid.mean() > 0.04 and halo.sum() > solid.sum() * 0.5


def test_nomai_spiral_is_deterministic_and_blue(tmp_path):
    a = _rgba(nomai_spiral(tmp_path / "a.png", size=256, seed=3))
    b = _rgba(nomai_spiral(tmp_path / "b.png", size=256, seed=3))
    assert np.array_equal(a, b)
    ink = a[..., 3] > 60
    assert ink.mean() > 0.01
    rgb = a[ink][:, :3].mean(axis=0)
    assert rgb[2] > rgb[0] + 30
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest -q tests/test_glyphs.py`
Expected: `ModuleNotFoundError: No module named 'tools.glyphs'`.

- [ ] **Step 3: Implement** — `tools/glyphs.py`:

```python
"""Light sprites, ghost letters, glowing poster characters and Nomai spirals as transparent PNGs."""
from __future__ import annotations

import argparse
import math
import os
import winreg
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
FONT_DIRS = [Path(os.environ.get("WINDIR", r"C:\Windows")) / "Fonts",
             Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "Windows" / "Fonts"]
KEY_FONTS = ["三极花朝体 粗", "方正姚体", "SimSun & NSimSun", "SimHei"]
LETTER_FONTS = ["Perpetua Titling MT Light", "Garamond"]


def font_path(candidates: list[str]) -> Path:
    """First installed font file whose registry display name starts with a candidate."""
    entries: list[tuple[str, str]] = []
    for root in (winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE):
        try:
            with winreg.OpenKey(root, r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts") as k:
                i = 0
                while True:
                    try:
                        n, v, _ = winreg.EnumValue(k, i)
                    except OSError:
                        break
                    i += 1
                    entries.append((n, v))
        except OSError:
            continue
    for c in candidates:
        for n, v in entries:
            if n.lower().startswith(c.lower()):
                p = Path(v)
                if not p.is_absolute():
                    p = next((d / v for d in FONT_DIRS if (d / v).is_file()), p)
                if p.is_file():
                    return p
    raise LookupError(f"none of {candidates} installed")


def _save(arr_rgba: np.ndarray, out: Path) -> Path:
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(arr_rgba, 0, 255).astype(np.uint8), "RGBA").save(out)
    return out


def _radial(size: int) -> np.ndarray:
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    return np.hypot(x - c, y - c) / (size / 2)


def glow_sprite(out, size: int = 512, color=(255, 236, 200)) -> Path:
    r = _radial(size)
    a = np.exp(-(r / 0.16) ** 2) * 0.75 + np.exp(-(r / 0.45) ** 2) * 0.35
    a = np.clip(a / a.max(), 0, 1) * np.clip((1 - r) / 0.1, 0, 1)
    rgba = np.zeros((size, size, 4), np.float32)
    rgba[..., :3] = color
    rgba[..., 3] = a * 255
    return _save(rgba, out)


def spark_sprite(out, size: int = 96) -> Path:
    return glow_sprite(out, size=size, color=(255, 246, 228))


def cross_rays(out, size: int = 1024, color=(255, 240, 215)) -> Path:
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    u, v = (x - c) / (size / 2), (y - c) / (size / 2)
    a = np.zeros_like(u)
    for d1, d2 in ((u + v, u - v), (u - v, u + v)):
        across = np.abs(d1) / math.sqrt(2)
        along = np.abs(d2) / math.sqrt(2)
        a = np.maximum(a, np.exp(-(across / 0.012) ** 2) * np.clip(1 - along / 0.95, 0, 1) ** 1.6)
    a = np.maximum(a, np.exp(-(np.hypot(u, v) / 0.08) ** 2))
    rgba = np.zeros((size, size, 4), np.float32)
    rgba[..., :3] = color
    rgba[..., 3] = a * 255
    return _save(rgba, out)


def letter_png(ch: str, out, alpha: float = 0.26, height: int = 300) -> Path:
    font = ImageFont.truetype(str(font_path(LETTER_FONTS)), int(height * 0.8))
    w = int(height * 0.8)
    m = Image.new("L", (w, height), 0)
    ImageDraw.Draw(m).text((w / 2, height / 2), ch, font=font, fill=255, anchor="mm")
    rgba = np.zeros((height, w, 4), np.float32)
    rgba[..., :3] = 255
    rgba[..., 3] = np.asarray(m, np.float32) * alpha
    return _save(rgba, out)


def key_char_png(text: str, out, font_candidates=KEY_FONTS, height: int = 420,
                 color=(255, 226, 170)) -> Path:
    font = ImageFont.truetype(str(font_path(list(font_candidates))), int(height * 0.62))
    pad = int(height * 0.25)
    tw = int(font.getlength(text)) + 2 * pad
    m = Image.new("L", (tw, height), 0)
    ImageDraw.Draw(m).text((tw / 2, height / 2), text, font=font, fill=255, anchor="mm")
    ink = np.asarray(m, np.float32) / 255
    glow = np.asarray(m.filter(ImageFilter.GaussianBlur(height * 0.05)), np.float32) / 255
    wide = np.asarray(m.filter(ImageFilter.GaussianBlur(height * 0.14)), np.float32) / 255
    halo = np.clip(glow * 1.6 + wide * 0.9, 0, 1)
    a = np.maximum(ink, halo * 0.55)
    col = np.array(color, np.float32)
    core = np.array((255, 250, 240), np.float32)
    rgb = col[None, None] * (1 - ink[..., None]) + core[None, None] * ink[..., None]
    rgba = np.dstack([rgb, a * 255])
    return _save(rgba, out)


def nomai_spiral(out, size: int = 768, seed: int = 0, color=(120, 200, 255)) -> Path:
    rng = np.random.default_rng(seed)
    s = 4
    big = size * s
    im = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(im)
    c = big / 2
    turns = 2.4
    pts = []
    for k in range(900):
        f = k / 899
        ang = f * turns * 2 * math.pi
        r = big * 0.45 * (1 - f) ** 1.1 + big * 0.02
        pts.append((c + r * math.cos(ang), c + r * math.sin(ang)))
    d.line(pts, fill=255, width=int(big * 0.006), joint="curve")
    for k in range(0, 900, 36):
        x, y = pts[k]
        if rng.random() < 0.7:
            ang = math.atan2(y - c, x - c) + rng.uniform(-0.6, 0.6)
            ln = big * rng.uniform(0.02, 0.06)
            d.line([(x, y), (x + ln * math.cos(ang), y + ln * math.sin(ang))], fill=255, width=int(big * 0.004))
        rr = big * rng.uniform(0.004, 0.009)
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=255)
    im = im.resize((size, size), Image.LANCZOS)
    ink = np.asarray(im, np.float32) / 255
    glow = np.asarray(im.filter(ImageFilter.GaussianBlur(size * 0.012)), np.float32) / 255
    a = np.clip(np.maximum(ink, glow * 1.8), 0, 1)
    col = np.array(color, np.float32)
    rgb = col[None, None] * (1 - ink[..., None] * 0.5) + 255 * ink[..., None] * 0.5
    return _save(np.dstack([rgb, a * 255]), out)


def font_sheet(text: str, out) -> Path:
    """Render ``text`` in every installed KEY_FONTS candidate side by side (for choosing)."""
    tiles = []
    for name in KEY_FONTS:
        try:
            p = key_char_png(text, Path(out).with_name(f"_font_{len(tiles)}.png"), [name], height=300)
        except LookupError:
            continue
        t = Image.new("RGB", Image.open(p).size, (10, 14, 30))
        t.paste(Image.open(p), mask=Image.open(p).split()[3])
        ImageDraw.Draw(t).text((8, 8), name, fill=(255, 255, 0))
        tiles.append(t)
    w = sum(t.width for t in tiles)
    sheet = Image.new("RGB", (w, 300), (10, 14, 30))
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0))
        x += t.width
    sheet.save(out)
    return Path(out)


def build_sample_assets() -> list[Path]:
    L = ROOT / "layers"
    made = [glow_sprite(L / "sprites" / "glow_warm.png"), spark_sprite(L / "sprites" / "spark.png"),
            cross_rays(L / "sprites" / "cross_rays.png")]
    for k, ch in enumerate("STARDUST"):
        made.append(letter_png(ch, L / "type" / f"letter_{ch}_{k}.png"))
    made.append(key_char_png("铜", L / "type" / "key_铜.png"))
    made.append(nomai_spiral(L / "type" / "nomai_p4.png", seed=4))
    return made


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["sample", "fonts"])
    a = ap.parse_args()
    if a.cmd == "fonts":
        print(font_sheet("铜", ROOT / "_tmp" / "font_sheet.png"))
    else:
        for p in build_sample_assets():
            print(p)


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest -q tests/test_glyphs.py`
Expected: 5 passed.

- [ ] **Step 5: Choose the key-character font**

Run: `python -m tools.glyphs fonts`, open `_tmp/font_sheet.png`. Reorder `KEY_FONTS` so the most poster-like, legible face is first (prefer a heavy brush/serif look). Then run `python -m tools.glyphs sample` and confirm 13 files are printed.

---

### Task 3: Layer extraction (`tools/split_layers.py`)

**Files:**
- Create: `tools/split_layers.py`
- Modify: `.gitignore` (add `layers/`)
- Test: `tests/test_split_layers.py`

**Interfaces:**
- Consumes: `tools.make_meteor_fx.make_silhouette_mask(bg_arr, *, ratio=1.9, ...) -> HxW float`.
- Produces: `luma_alpha(arr, lo, hi) -> HxW float`; `keep_near(alpha, center, radius) -> HxW float`; `station_cutout(plate_arr, center) -> HxW float`; `fill_from_shift(arr, alpha, shift_px) -> arr`; `ground_alpha(arr, ratio) -> HxW float`; `subject_alpha(arr, box) -> HxW float` (rembg); `save_rgba(arr, alpha, out) -> Path`; CLI `python -m tools.split_layers p1|p4` writing `layers/p1/{station.png,bg_clean.png}` and `layers/p4/{villager.png,ground.png,bg_clean.png}`.

- [ ] **Step 1: Write the failing tests** — `tests/test_split_layers.py`:

```python
import numpy as np

from tools.split_layers import fill_from_shift, keep_near, luma_alpha, station_cutout


def _space_with_station(w=320, h=180):
    rng = np.random.default_rng(1)
    a = np.zeros((h, w, 3), np.float32)
    stars = rng.random((h, w)) > 0.99
    a[stars] = 0.9
    cx, cy = w // 2, h // 2
    a[cy - 3:cy + 4, cx - 60:cx + 61] = 0.45
    a[cy - 50:cy + 51, cx - 3:cx + 4] = 0.45
    yy, xx = np.mgrid[0:h, 0:w]
    a[(xx - cx) ** 2 + (yy - cy) ** 2 < 100] = 1.0
    return a, stars


def test_luma_alpha_ramps_between_thresholds():
    a = np.zeros((2, 3, 3), np.float32)
    a[0, 1] = 0.1
    a[0, 2] = 0.3
    m = luma_alpha(a, 0.05, 0.15)
    assert m[0, 0] == 0 and 0.4 < m[0, 1] < 0.6 and m[0, 2] == 1


def test_station_cutout_keeps_structure_and_drops_isolated_stars():
    a, stars = _space_with_station()
    m = station_cutout(a, (0.5, 0.5))
    h, w = m.shape
    assert m[h // 2, w // 2] > 0.95
    assert m[h // 2, w // 2 + 50] > 0.8, "arms belong to the station"
    far = stars.copy()
    far[h // 2 - 60:h // 2 + 60, w // 2 - 70:w // 2 + 70] = False
    assert m[far].max() < 0.05, "stars are not part of the station"


def test_keep_near_discards_components_outside_radius():
    m = np.zeros((100, 100), np.float32)
    m[45:55, 45:55] = 1
    m[2:6, 2:6] = 1
    k = keep_near(m, (0.5, 0.5), 0.2)
    assert k[50, 50] == 1 and k[3, 3] == 0


def test_fill_from_shift_replaces_masked_area_with_shifted_pixels():
    a = np.zeros((10, 20, 3), np.float32)
    a[:, 15:] = 0.7
    alpha = np.zeros((10, 20), np.float32)
    alpha[:, 5:8] = 1
    out = fill_from_shift(a, alpha, shift_px=10)
    assert np.allclose(out[:, 5:8], 0.7) and np.allclose(out[:, :5], 0)
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest -q tests/test_split_layers.py`
Expected: `ModuleNotFoundError: No module named 'tools.split_layers'`.

- [ ] **Step 3: Implement** — `tools/split_layers.py`:

```python
"""Cut slide plates into layers: subjects as transparent PNGs, backgrounds with the subject removed."""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

from tools.make_meteor_fx import make_silhouette_mask

ROOT = Path(__file__).resolve().parents[1]
LAYERS = ROOT / "layers"


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGB"), np.float32) / 255.0


def luma_alpha(arr: np.ndarray, lo: float, hi: float) -> np.ndarray:
    lum = arr @ np.array([0.299, 0.587, 0.114], np.float32)
    return np.clip((lum - lo) / max(hi - lo, 1e-6), 0, 1).astype(np.float32)


def keep_near(alpha: np.ndarray, center: tuple[float, float], radius: float) -> np.ndarray:
    """Keep only connected components (alpha > 0.5) that reach within ``radius`` (width fraction) of center."""
    h, w = alpha.shape
    lab, n = ndimage.label(alpha > 0.5)
    yy, xx = np.mgrid[0:h, 0:w]
    near = np.hypot(xx - center[0] * w, yy - center[1] * h) < radius * w
    keep = np.unique(lab[near & (lab > 0)])
    return (alpha * np.isin(lab, keep)).astype(np.float32)


def _feather(alpha: np.ndarray, px: float) -> np.ndarray:
    im = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(px)), np.float32) / 255.0


def station_cutout(plate: np.ndarray, center: tuple[float, float]) -> np.ndarray:
    h, w = plate.shape[:2]
    a = luma_alpha(plate, 0.05, 0.16)
    solid = ndimage.binary_opening(a > 0.5, iterations=2)
    body = keep_near(solid.astype(np.float32), center, 0.06)
    body = ndimage.binary_dilation(body > 0.5, iterations=3)
    return np.clip(_feather(body * a, max(w / 1920, 0.6)) * 1.15, 0, 1)


def fill_from_shift(arr: np.ndarray, alpha: np.ndarray, shift_px: int) -> np.ndarray:
    src = np.roll(arr, -shift_px, axis=1)
    return arr * (1 - alpha[..., None]) + src * alpha[..., None]


def ground_alpha(arr: np.ndarray, ratio: float = 1.9) -> np.ndarray:
    return 1.0 - make_silhouette_mask(arr, ratio=ratio, margin=0.0, feather=0.004)


def subject_alpha(arr: np.ndarray, box: tuple[float, float, float, float]) -> np.ndarray:
    from rembg import new_session, remove

    h, w = arr.shape[:2]
    im = Image.fromarray((arr * 255).astype(np.uint8))
    cut = remove(im, session=new_session("isnet-general-use"))
    a = np.asarray(cut, np.float32)[..., 3] / 255.0
    u0, v0, u1, v1 = box
    gate = np.zeros_like(a)
    gate[int(v0 * h):int(v1 * h), int(u0 * w):int(u1 * w)] = 1
    return (a * gate).astype(np.float32)


def save_rgba(arr: np.ndarray, alpha: np.ndarray, out: Path) -> Path:
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    rgba = np.dstack([np.clip(arr, 0, 1), np.clip(alpha, 0, 1)])
    Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(out)
    return out


def save_rgb(arr: np.ndarray, out: Path) -> Path:
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "RGB").save(out)
    return out


def build_p1() -> list[Path]:
    from deck.storyboard import P1_CORE

    plate = load(ROOT / "plates" / "dust_awakening.png")
    a = station_cutout(plate, P1_CORE)
    hole = ndimage.binary_dilation(a > 0.02, iterations=12).astype(np.float32)
    bg = fill_from_shift(plate, _feather(hole, 6), shift_px=plate.shape[1] // 2)
    return [save_rgba(plate, a, LAYERS / "p1" / "station.png"), save_rgb(bg, LAYERS / "p1" / "bg_clean.png")]


def build_p4(ai_patch: Path | None = None) -> list[Path]:
    plate = load(ROOT / "plates" / "white_dwarf_dusk.png")
    vil = subject_alpha(plate, (0.0, 0.40, 0.34, 1.0))
    vil = keep_near(vil, (0.15, 0.80), 0.12) if vil.max() > 0 else vil
    clean = plate
    if ai_patch is not None and Path(ai_patch).is_file():
        patch = np.asarray(Image.open(ai_patch).convert("RGB").resize(plate.shape[1::-1], Image.LANCZOS),
                           np.float32) / 255.0
        hole = _feather(ndimage.binary_dilation(vil > 0.05, iterations=18).astype(np.float32), 10)
        clean = plate * (1 - hole[..., None]) + patch * hole[..., None]
    g = ground_alpha(clean)
    return [save_rgba(plate, vil, LAYERS / "p4" / "villager.png"),
            save_rgba(clean, g, LAYERS / "p4" / "ground.png"),
            save_rgb(clean, LAYERS / "p4" / "bg_clean.png")]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("page", choices=["p1", "p4"])
    ap.add_argument("--ai-patch", default=None, help="AI image with the subject removed (p4)")
    a = ap.parse_args()
    for p in (build_p1() if a.page == "p1" else build_p4(Path(a.ai_patch) if a.ai_patch else None)):
        print(p)


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest -q tests/test_split_layers.py`
Expected: 4 passed.

- [ ] **Step 5: Build p1 layers and inspect**

Run: `python -m tools.split_layers p1`. Composite `layers/p1/station.png` over magenta and view `layers/p1/bg_clean.png`: station fully cut with no black fringe, no stray stars in the cutout, no station ghost left in `bg_clean`. If the core centroid differs from `P1_CORE` by > 0.02, update `P1_CORE` in `deck/storyboard.py`.

- [ ] **Step 6: AI patch for p4, then build p4 layers**

Use Cursor GenerateImage with reference `plates/white_dwarf_dusk.png`, prompt: "Remove the blue-skinned villager in the lower-left foreground completely; continue the wooden boardwalk, rocks, grass and dark tree trunk naturally where the villager stood. Keep everything else exactly the same: composition, the bursting sun, trees, tower, lamp post, lighting and the game's art style. No new objects, no characters, no text." Copy the result to `layers/p4/ai_patch.jpg`, then run `python -m tools.split_layers p4 --ai-patch layers/p4/ai_patch.jpg`. Inspect on magenta: villager clean (no lamp post), ground alpha covers trees/village/boardwalk but not the sky, `bg_clean` has no villager ghost. If the sky leaks into `ground.png`, lower `ratio` in `ground_alpha` (try 1.6) and rerun.

- [ ] **Step 7: Add `layers/` to `.gitignore`** (append the line `layers/`).

---

### Task 4: Living backgrounds (`tools/living_bg.py`)

**Files:**
- Create: `tools/living_bg.py`
- Test: `tests/test_living_bg.py`

**Interfaces:**
- Consumes: `tools.make_meteor_fx.make_silhouette_mask`; images from Task 3.
- Produces: dataclasses `Pulse`, `Particles`, `Flash`, `BgSpec`; `heartbeat(t, t0, t1, period0, period1) -> float`; `render_frames(spec: BgSpec) -> Iterator[np.ndarray]` (uint8 HxWx3); `render_video(spec, out: Path) -> Path`; `_specs() -> dict[int, BgSpec]` for pages 1 and 4; CLI `python -m tools.living_bg 1 4` writing `fx/bg_p1.mp4`, `fx/bg_p4.mp4`.

- [ ] **Step 1: Write the failing tests** — `tests/test_living_bg.py`:

```python
import numpy as np
from PIL import Image

from tools.living_bg import BgSpec, Flash, Particles, Pulse, heartbeat, render_frames


def _base(tmp_path, w=160, h=90):
    rng = np.random.default_rng(0)
    a = np.zeros((h, w, 3), np.uint8)
    a[:] = (8, 12, 40)
    a[rng.random((h, w)) > 0.98] = 230
    p = tmp_path / "b.png"
    Image.fromarray(a).save(p)
    return p, a


def test_frame_count_and_determinism(tmp_path):
    p, _ = _base(tmp_path)
    spec = BgSpec(str(p), seconds=1.0, fps=8, twinkle=0.4)
    a = list(render_frames(spec))
    b = list(render_frames(spec))
    assert len(a) == 8 and a[0].shape == (90, 160, 3)
    assert all(np.array_equal(x, y) for x, y in zip(a, b))


def test_fade_in_starts_black(tmp_path):
    p, _ = _base(tmp_path)
    f = list(render_frames(BgSpec(str(p), seconds=1.0, fps=8, fade_in=(0.0, 0.8))))
    assert f[0].max() <= 2 and f[-1].mean() > 5


def test_wake_stars_go_from_hidden_to_shown(tmp_path):
    p, base = _base(tmp_path)
    stars = base[..., 0] > 200
    f = list(render_frames(BgSpec(str(p), seconds=2.0, fps=4, wake_stars=(0.25, 1.5))))
    assert f[0][stars].mean() < 60 and f[-1][stars].mean() > 150


def test_heartbeat_slows_down():
    ts = np.arange(0, 8, 0.01)
    env = np.array([heartbeat(t, 0, 8, 0.9, 1.8) for t in ts])
    peaks = [ts[i] for i in range(1, len(ts) - 1) if env[i] > env[i - 1] and env[i] >= env[i + 1] and env[i] > 0.8]
    gaps = np.diff(peaks)
    assert len(peaks) >= 4 and gaps[-1] > gaps[0] * 1.3


def test_pulse_particles_and_flash_add_light(tmp_path):
    p, base = _base(tmp_path)
    spec = BgSpec(str(p), seconds=2.0, fps=4,
                  pulses=(Pulse((0.5, 0.5), 0.2, (1.0, 0.9, 0.8), 0.0, 2.0, 0.8, 0.6, 1.2),),
                  particles=(Particles(40, 0.0, (1.0, 0.95, 0.95), (1.5, 3.0), (0.0, 30.0), 6.0,
                                       (0.0, 0.0, 1.0, 0.5), 2.0),),
                  end_flash=Flash((0.5, 0.5), 1.25, 2.0, (1.0, 1.0, 1.0)))
    f = list(render_frames(spec))
    assert f[2][45, 80].mean() > base[45, 80].mean() + 20
    assert f[-1].mean() > 200
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest -q tests/test_living_bg.py`
Expected: `ModuleNotFoundError: No module named 'tools.living_bg'`.

- [ ] **Step 3: Implement** — `tools/living_bg.py`:

```python
"""Render a slide's living background: a still layer brought to life with light, stars and particles."""
from __future__ import annotations

import argparse
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

import numpy as np
from PIL import Image, ImageFilter

from tools.make_meteor_fx import make_silhouette_mask

ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Pulse:
    center: tuple[float, float]
    radius: float
    color: tuple[float, float, float]
    t0: float
    t1: float
    gain: float
    period0: float
    period1: float


@dataclass(frozen=True)
class Particles:
    count: int
    t0: float
    color: tuple[float, float, float]
    size: tuple[float, float]
    velocity: tuple[float, float]
    sway: float
    region: tuple[float, float, float, float]
    life: float


@dataclass(frozen=True)
class Flash:
    center: tuple[float, float]
    t0: float
    t1: float
    color: tuple[float, float, float]


@dataclass(frozen=True)
class BgSpec:
    base: str
    seconds: float
    fps: int = 24
    seed: int = 7
    fade_in: tuple[float, float] | None = None
    wake_stars: tuple[float, float] | None = None
    twinkle: float = 0.0
    dark_ground: float | None = None
    ground_ratio: float = 1.9
    pulses: tuple[Pulse, ...] = ()
    particles: tuple[Particles, ...] = ()
    end_flash: Flash | None = None
    push: tuple[float, float] = (1.0, 1.0)


def _smooth(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def heartbeat(t: float, t0: float, t1: float, period0: float, period1: float) -> float:
    """Lub-dub envelope (peaks ~1) whose beat period stretches from period0 to period1 over [t0, t1]."""
    if t < t0:
        return 0.0
    span = max(t1 - t0, 1e-6)
    k = (period1 - period0) / span
    tau = t - t0
    phase = tau / period0 if abs(k) < 1e-9 else math.log1p(k * tau / period0) / k
    frac = phase - math.floor(phase)
    period = period0 + k * tau
    x = frac * period
    return float(math.exp(-(x / 0.07) ** 2) + 0.6 * math.exp(-((x - 0.24) / 0.08) ** 2)
                 + math.exp(-((x - period) / 0.07) ** 2))


def _split_stars(arr: np.ndarray, thr: float = 0.08) -> tuple[np.ndarray, np.ndarray]:
    im = Image.fromarray((arr * 255).astype(np.uint8))
    med = np.asarray(im.filter(ImageFilter.MedianFilter(5)), np.float32) / 255.0
    stars = np.clip(arr - med, 0, 1)
    stars[stars.max(axis=2) < thr] = 0
    return arr - stars, stars


def _star_field(h: int, w: int, rng, block: int = 4) -> np.ndarray:
    small = rng.random((h // block + 1, w // block + 1)).astype(np.float32)
    return np.kron(small, np.ones((block, block), np.float32))[:h, :w]


def _stamp(size: float) -> np.ndarray:
    r = max(1, int(math.ceil(size * 2.5)))
    y, x = np.mgrid[-r:r + 1, -r:r + 1].astype(np.float32)
    return np.exp(-(x * x + y * y) / (2 * (size * 0.6) ** 2))


def _plan_particles(p: Particles, seconds: float, w: int, h: int, rng) -> np.ndarray:
    n = p.count
    u0, v0, u1, v1 = p.region
    return np.stack([rng.uniform(p.t0, max(p.t0, seconds - 0.1), n), rng.uniform(u0 * w, u1 * w, n),
                     rng.uniform(v0 * h, v1 * h, n), rng.uniform(*p.size, n), rng.uniform(0, 2 * math.pi, n),
                     rng.uniform(0.6, 1.0, n)], axis=1)


def _push(arr: np.ndarray, scale: float) -> np.ndarray:
    if abs(scale - 1.0) < 1e-4:
        return arr
    h, w = arr.shape[:2]
    cw, ch = w / scale, h / scale
    x0, y0 = (w - cw) / 2, (h - ch) / 2
    im = Image.fromarray((arr * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC, box=(x0, y0, x0 + cw, y0 + ch))
    return np.asarray(im, np.float32) / 255.0


def render_frames(spec: BgSpec) -> Iterator[np.ndarray]:
    base = np.asarray(Image.open(ROOT / spec.base if not Path(spec.base).is_absolute() else spec.base)
                      .convert("RGB"), np.float32) / 255.0
    h, w = base.shape[:2]
    rng = np.random.default_rng(spec.seed)
    starless, stars = _split_stars(base)
    wake_t = _star_field(h, w, rng)
    phase = _star_field(h, w, rng) * 2 * math.pi
    ground = None
    if spec.dark_ground is not None:
        sky = make_silhouette_mask(base, ratio=spec.ground_ratio)
        ground = (sky + (1 - sky) * spec.dark_ground)[..., None]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    parts = [(p, _plan_particles(p, spec.seconds, w, h, rng)) for p in spec.particles]
    stamps: dict[float, np.ndarray] = {}
    total = int(round(spec.fps * spec.seconds))
    for f in range(total):
        t = f / spec.fps
        st = stars
        if spec.wake_stars:
            a0, a1 = spec.wake_stars
            on = _smooth((t - (a0 + wake_t * (a1 - a0) * 0.85)) / max((a1 - a0) * 0.15, 1e-3))
            st = st * on[..., None]
        if spec.twinkle:
            st = st * (1 + spec.twinkle * np.sin(t * 2.6 + phase))[..., None]
        img = starless + st
        if ground is not None:
            img = img * ground
        add = np.zeros_like(img)
        for p in spec.pulses:
            if p.t0 <= t <= p.t1:
                env = 0.35 + 0.65 * min(heartbeat(t, p.t0, p.t1, p.period0, p.period1), 1.3)
                d = np.hypot(xx - p.center[0] * w, yy - p.center[1] * h) / (p.radius * w)
                add += (np.exp(-d * d) * env * p.gain)[..., None] * np.array(p.color, np.float32)
        lum = np.zeros((h, w), np.float32)
        tint = np.zeros_like(img)
        for p, plan in parts:
            for t0, x, y, size, ph, gain in plan:
                age = t - t0
                if age < 0 or age > p.life:
                    continue
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
                lum[ya:yb, xa:xb] += patch
                tint[ya:yb, xa:xb] += patch[..., None] * np.array(p.color, np.float32)
        add += tint
        if spec.end_flash and t >= spec.end_flash.t0:
            fl = spec.end_flash
            s = float(_smooth((t - fl.t0) / max(fl.t1 - fl.t0, 1e-3)))
            d = np.hypot(xx - fl.center[0] * w, yy - fl.center[1] * h) / (w * (0.05 + 1.6 * s))
            add += (np.exp(-d * d) * (0.4 + 2.5 * s))[..., None] * np.array(fl.color, np.float32)
            add += s ** 3 * np.array(fl.color, np.float32)
        img = 1 - (1 - np.clip(img, 0, 1)) * (1 - np.clip(add, 0, 1))
        if spec.push != (1.0, 1.0):
            img = _push(img, spec.push[0] + (spec.push[1] - spec.push[0]) * (t / max(spec.seconds, 1e-6)))
        if spec.fade_in:
            img = img * float(_smooth((t - spec.fade_in[0]) / max(spec.fade_in[1] - spec.fade_in[0], 1e-3)))
        yield np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)


def render_video(spec: BgSpec, out: Path, bitrate: str = "12M") -> Path:
    import imageio.v2 as imageio

    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_name(out.stem + ".part.mp4")
    wr = imageio.get_writer(str(tmp), fps=spec.fps, codec="libx264", pixelformat="yuv420p", macro_block_size=1,
                            bitrate=bitrate, ffmpeg_params=["-preset", "medium", "-movflags", "+faststart"])
    try:
        for fr in render_frames(spec):
            wr.append_data(fr)
    finally:
        wr.close()
    tmp.replace(out)
    return out


def _specs() -> dict[int, BgSpec]:
    from deck.storyboard import P1_CORE, P4_BURST, holds

    h = holds()
    return {
        1: BgSpec("layers/p1/bg_clean.png", h[1], fade_in=(0.0, 0.6), wake_stars=(0.3, 2.6), twinkle=0.35,
                  end_flash=Flash(P1_CORE, h[1] - 0.7, h[1], (1.0, 0.97, 0.9)), push=(1.0, 1.03)),
        4: BgSpec("layers/p4/bg_clean.png", h[4], fade_in=(0.0, 0.8), twinkle=0.3, dark_ground=0.12,
                  pulses=(Pulse(P4_BURST, 0.16, (1.0, 0.86, 0.92), 0.0, h[4], 0.55, 0.85, 1.9),),
                  particles=(Particles(140, 4.0, (1.0, 0.93, 0.95), (1.2, 3.2), (-6.0, 38.0), 14.0,
                                       (0.05, -0.05, 1.0, 0.55), 4.5),),
                  push=(1.0, 1.035)),
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("pages", nargs="+", type=int)
    a = ap.parse_args()
    specs = _specs()
    for i in a.pages:
        print(render_video(specs[i], ROOT / "fx" / f"bg_p{i}.mp4"))


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest -q tests/test_living_bg.py`
Expected: 5 passed.

- [ ] **Step 5: Render and inspect**

Run: `python -m tools.living_bg 1 4`. Extract frames at p1 t = 0.5, 2.0, 6.8 and p4 t = 0.5, 3.0, 7.0 with ffmpeg (`-ss <t> -frames:v 1`), build a contact sheet in `_tmp/bg_frames.jpg`, check: p1 stars wake gradually and the last frame is near white; p4 ground starts dark, burst beats visibly, pale flakes drift down.

---

### Task 5: Layered deck builder (`build_layered.py`)

**Files:**
- Create: `build_layered.py`
- Test: `tests/test_build_layered_plan.py` (pure helpers only)

**Interfaces:**
- Consumes: `deck.storyboard.{pages, validate, TRANSITIONS, TRANSITION_NOTES, page_starts, Layer, Text, Page}`; `deck.build_plan.{SLIDE_W, SLIDE_H, CN_FONT_PRIORITY, installed_fonts, pick_font}`; `fx/bg_p{i}.mp4`; `layers/...` PNGs; `media/main-title.mp3`.
- Produces: `slide_box(box) -> (left, top, width, height)` in points; `fit_box(box_pt, aspect) -> box_pt` (image fills the box width, keeps its aspect, centred vertically); `motion_path(dx, dy) -> str`; `build(page_ids, out, video: bool) -> dict` report; CLI `python build_layered.py --pages 1,4 --out "out/星辰之子-样片-p1p4.pptx" --video`.

- [ ] **Step 1: Write the failing tests** — `tests/test_build_layered_plan.py`:

```python
import pytest

from build_layered import fit_box, motion_path, slide_box


def test_slide_box_scales_fractions_to_points():
    assert slide_box((0.0, 0.0, 1.0, 1.0)) == (0.0, 0.0, 960.0, 540.0)
    assert slide_box((0.1, 0.5, 0.2, 0.25)) == pytest.approx((96.0, 270.0, 192.0, 135.0))


def test_fit_box_keeps_aspect_and_centres_vertically():
    assert fit_box((100.0, 100.0, 200.0, 200.0), 2.0) == pytest.approx((100.0, 150.0, 200.0, 100.0))
    assert fit_box((0.0, 0.0, 960.0, 540.0), 16 / 9) == pytest.approx((0.0, 0.0, 960.0, 540.0))


def test_motion_path_starts_offset_and_ends_home():
    assert motion_path(-0.08, 0.0) == "M -0.08 0 L 0 0 E"
    assert motion_path(0.0, -0.3) == "M 0 -0.3 L 0 0 E"
```

- [ ] **Step 2: Run to verify failure**

Run: `python -m pytest -q tests/test_build_layered_plan.py`
Expected: `ModuleNotFoundError: No module named 'build_layered'`.

- [ ] **Step 3: Implement** — `build_layered.py`:

```python
"""Assemble the layered-romance deck in PowerPoint via COM from deck/storyboard.py.

    python build_layered.py --pages 1,4 --out "out/星辰之子-样片-p1p4.pptx" --video
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from deck.build_plan import CN_FONT_PRIORITY, SLIDE_H, SLIDE_W, installed_fonts, pick_font  # noqa: E402
from deck.storyboard import TRANSITION_NOTES, TRANSITIONS, Layer, Page, Text, page_starts, pages, validate  # noqa: E402

COPY_RGB = (0xF6, 0xEE, 0xE2)
GLOW_RGB = (0xE8, 0xB4, 0x5A)
LOG: list[str] = []
DEGRADED: list[str] = list(TRANSITION_NOTES.values())


def log(msg: str) -> None:
    LOG.append(msg)
    print(msg, flush=True)


def rgb(c: tuple[int, int, int]) -> int:
    return c[0] + c[1] * 256 + c[2] * 65536


def _num(x: float) -> str:
    return f"{x:g}"


def slide_box(box: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    l, t, w, h = box
    return (l * SLIDE_W, t * SLIDE_H, w * SLIDE_W, h * SLIDE_H)


def motion_path(dx: float, dy: float) -> str:
    return f"M {_num(dx)} {_num(dy)} L 0 0 E"


def fit_box(box_pt: tuple[float, float, float, float], aspect: float) -> tuple[float, float, float, float]:
    """Fill the box width with an image of ``aspect`` (w/h), vertically centred in the box."""
    l, t, w, h = box_pt
    nh = w / aspect
    return (l, t + (h - nh) / 2, w, nh)


class Com:
    def __init__(self, constants):
        self.c = constants

    def __call__(self, name: str):
        v = getattr(self.c, name, None)
        if v is None:
            raise KeyError(name)
        return v


def _with_prev(K):
    return K("msoAnimTriggerWithPrevious")


def mount_bg_video(slide, K, mp4: Path):
    shp = slide.Shapes.AddMediaObject2(str(mp4.resolve()), False, True, 0, 0, SLIDE_W, SLIDE_H)
    shp.Name = "LivingBg"
    shp.LockAspectRatio = False
    shp.Left, shp.Top, shp.Width, shp.Height = 0, 0, SLIDE_W, SLIDE_H
    ps = shp.AnimationSettings.PlaySettings
    ps.PlayOnEntry = True
    ps.PauseAnimation = False
    ps.LoopUntilStopped = False
    ps.HideWhileNotPlaying = False
    shp.MediaFormat.Muted = True
    seq = slide.TimeLine.MainSequence
    effs = [seq.Item(i) for i in range(1, seq.Count + 1) if seq.Item(i).Shape.Name == shp.Name]
    if not effs:
        effs = [seq.AddEffect(shp, K("msoAnimEffectMediaPlay"), 0, _with_prev(K))]
    for e in effs:
        e.Timing.TriggerType = _with_prev(K)
        e.Timing.TriggerDelayTime = 0
        e.MoveTo(1)
    return shp


def mount_audio_clip(slide, K, mp3: Path, start: float, hold: float):
    shp = slide.Shapes.AddMediaObject2(str(mp3.resolve()), False, True, -90, -90, 24, 24)
    shp.Name = "MainTitleAudio"
    shp.Left, shp.Top = -60, -60
    shp.MediaFormat.StartPoint = int(start * 1000)
    shp.MediaFormat.EndPoint = int((start + hold) * 1000)
    ps = shp.AnimationSettings.PlaySettings
    ps.PlayOnEntry = True
    ps.PauseAnimation = False
    ps.StopAfterSlides = 1
    ps.HideWhileNotPlaying = True
    seq = slide.TimeLine.MainSequence
    effs = [seq.Item(i) for i in range(1, seq.Count + 1) if seq.Item(i).Shape.Name == shp.Name]
    if not effs:
        effs = [seq.AddEffect(shp, K("msoAnimEffectMediaPlay"), 0, _with_prev(K))]
    for e in effs:
        e.Timing.TriggerType = _with_prev(K)
        e.Timing.TriggerDelayTime = 0
    return shp


def _entrance(seq, K, shp, item, t: float, dur: float):
    base = item.base
    effect = {"fade": "msoAnimEffectFade", "wheel": "msoAnimEffectWheel",
              "wipe_left": "msoAnimEffectWipe", "wipe_down": "msoAnimEffectWipe"}[base]
    e = seq.AddEffect(shp, K(effect), 0, _with_prev(K))
    if base == "wipe_left":
        e.EffectParameters.Direction = K("msoAnimDirectionLeft")
    elif base == "wipe_down":
        e.EffectParameters.Direction = K("msoAnimDirectionUp")
    e.Timing.TriggerDelayTime = t
    e.Timing.Duration = dur
    e.Timing.SmoothStart = True
    e.Timing.SmoothEnd = True
    return e


def add_layer(slide, seq, K, lay: Layer):
    from PIL import Image

    src = ROOT / lay.src
    with Image.open(src) as im:
        aspect = im.width / im.height
    l, t, w, h = fit_box(slide_box(lay.box), aspect)
    shp = slide.Shapes.AddPicture(str(src.resolve()), False, True, l, t, w, h)
    shp.Name = lay.name
    shp.LockAspectRatio = False
    shp.Left, shp.Top, shp.Width, shp.Height = l, t, w, h
    e = _entrance(seq, K, shp, lay, lay.t, lay.dur)
    if lay.scale_from:
        b = e.Behaviors.Add(K("msoAnimTypeScale"))
        b.ScaleEffect.FromX = b.ScaleEffect.FromY = lay.scale_from * 100
        b.ScaleEffect.ToX = b.ScaleEffect.ToY = 100
    if lay.spin:
        b = e.Behaviors.Add(K("msoAnimTypeRotation"))
        b.RotationEffect.From = -lay.spin
        b.RotationEffect.To = 0
    if lay.move_from:
        b = e.Behaviors.Add(K("msoAnimTypeMotion"))
        b.MotionEffect.Path = motion_path(*lay.move_from)
    if lay.exit_t is not None:
        x = seq.AddEffect(shp, K("msoAnimEffectFade"), 0, _with_prev(K))
        x.Exit = True
        x.Timing.TriggerDelayTime = lay.exit_t
        x.Timing.Duration = lay.exit_dur
    return shp


def add_text(slide, seq, K, tx: Text, font_cn: str):
    l, t, w, h = slide_box(tx.box)
    shp = slide.Shapes.AddTextbox(K("msoTextOrientationHorizontal"), l, t, w, h)
    shp.Name = tx.name
    tf = shp.TextFrame2
    tf.WordWrap = False
    tf.AutoSize = K("msoAutoSizeNone")
    tf.MarginLeft = tf.MarginRight = tf.MarginTop = tf.MarginBottom = 0
    tf.VerticalAnchor = K("msoAnchorMiddle")
    tr = tf.TextRange
    tr.Text = tx.text
    f = tr.Font
    f.Name = f.NameFarEast = font_cn
    f.Size = tx.size
    f.Spacing = 2.0
    f.Fill.Solid()
    f.Fill.ForeColor.RGB = rgb(COPY_RGB)
    try:
        f.Glow.Radius = 8
        f.Glow.Color.RGB = rgb(GLOW_RGB)
        f.Glow.Transparency = 0.78
    except Exception as ex:  # noqa: BLE001
        DEGRADED.append(f"text glow skipped ({ex.__class__.__name__})")
    tr.ParagraphFormat.Alignment = K("msoAlignLeft" if tx.align == "left" else "msoAlignCenter")
    sh = shp.Shadow
    sh.Visible = True
    sh.Style = K("msoShadowStyleOuterShadow")
    sh.ForeColor.RGB = 0
    sh.Transparency = 0.25
    sh.Blur = 10
    sh.OffsetX, sh.OffsetY = 0, 2
    shp.Left, shp.Top, shp.Width, shp.Height = l, t, w, h
    _entrance(seq, K, shp, tx, tx.t, tx.dur)
    return shp


def check_slide(slide, K, page: Page) -> list[str]:
    probs = []
    names = {s.Name for s in slide.Shapes}
    if "LivingBg" not in names:
        probs.append(f"p{page.i}: no living background video")
    want = {x.name for x in (*page.layers, *page.texts)}
    if not want <= names:
        probs.append(f"p{page.i}: missing shapes {sorted(want - names)}")
    n_eff = slide.TimeLine.MainSequence.Count
    if n_eff < len(want) + 1:
        probs.append(f"p{page.i}: only {n_eff} timeline effects for {len(want)} items")
    tr = slide.SlideShowTransition
    if tr.EntryEffect != K(TRANSITIONS[page.i][0]):
        probs.append(f"p{page.i}: transition is {tr.EntryEffect}, wanted {TRANSITIONS[page.i][0]}")
    if abs(tr.AdvanceTime - page.hold) > 1e-3 or not tr.AdvanceOnTime:
        probs.append(f"p{page.i}: advance timing wrong ({tr.AdvanceTime})")
    return probs


def export_video(pres, out_mp4: Path, timeout: float = 900) -> Path:
    if out_mp4.exists():
        out_mp4.unlink()
    pres.CreateVideo(str(out_mp4.resolve()), True, 5, 720, 24, 85)
    t0 = time.time()
    while pres.CreateVideoStatus in (1, 2):   # queued, in progress
        if time.time() - t0 > timeout:
            raise TimeoutError("PowerPoint video export timed out")
        time.sleep(2)
    if pres.CreateVideoStatus != 3:
        raise RuntimeError(f"video export failed (status {pres.CreateVideoStatus})")
    return out_mp4


def build(page_ids: list[int], out: Path, video: bool) -> dict:
    import win32com.client as wc

    ps = pages(page_ids)
    errs = validate(ps)
    if errs:
        raise SystemExit("refusing to build: " + "; ".join(errs))
    for p in ps:
        for path in [ROOT / p.bg_video, *(ROOT / l.src for l in p.layers)]:
            if not path.is_file():
                raise SystemExit(f"missing asset {path}")
    starts = page_starts()
    mp3 = ROOT / "media" / "main-title.mp3"
    font_cn = pick_font(CN_FONT_PRIORITY, installed_fonts(), default="SimSun")
    app = wc.gencache.EnsureDispatch("PowerPoint.Application")
    wc.gencache.EnsureModule("{2DF8D04C-5BFA-101B-BDE5-00AA0044DE52}", 0, 2, 8)
    K = Com(wc.constants)
    pres = app.Presentations.Add(False)
    problems: list[str] = []
    try:
        pres.PageSetup.SlideWidth, pres.PageSetup.SlideHeight = SLIDE_W, SLIDE_H
        for idx, p in enumerate(ps, 1):
            log(f"slide {idx}: p{p.i} {p.motif} hold={p.hold}s layers={len(p.layers)} texts={len(p.texts)}")
            slide = pres.Slides.Add(idx, K("ppLayoutBlank"))
            slide.FollowMasterBackground = False
            slide.Background.Fill.Solid()
            slide.Background.Fill.ForeColor.RGB = 0
            mount_bg_video(slide, K, ROOT / p.bg_video)
            seq = slide.TimeLine.MainSequence
            for lay in p.layers:
                add_layer(slide, seq, K, lay)
            for tx in p.texts:
                add_text(slide, seq, K, tx, font_cn)
            if mp3.is_file():
                mount_audio_clip(slide, K, mp3, starts[p.i], p.hold)
            tr = slide.SlideShowTransition
            eff, dur = TRANSITIONS[p.i]
            tr.EntryEffect = K(eff)
            tr.Duration = dur
            tr.AdvanceOnTime = True
            tr.AdvanceTime = p.hold
            tr.AdvanceOnClick = True
            problems += check_slide(slide, K, p)
        ss = pres.SlideShowSettings
        ss.AdvanceMode = K("ppSlideShowUseSlideTimings")
        out.parent.mkdir(parents=True, exist_ok=True)
        if out.exists():
            out.unlink()
        pres.SaveAs(str(out.resolve()))
        log(f"saved {out} ({out.stat().st_size / 1e6:.1f} MB)")
        mp4 = None
        if video:
            mp4 = export_video(pres, out.with_suffix(".mp4"))
            log(f"exported {mp4} ({mp4.stat().st_size / 1e6:.1f} MB)")
    finally:
        pres.Close()
        if app.Presentations.Count == 0:
            app.Quit()
    return dict(out=str(out), video=str(mp4) if video else None, problems=problems, degraded=DEGRADED)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--pages", default="1,4")
    ap.add_argument("--out", default="out/星辰之子-样片-p1p4.pptx")
    ap.add_argument("--video", action="store_true")
    a = ap.parse_args(argv)
    out = Path(a.out)
    if not out.is_absolute():
        out = ROOT / out
    res = build([int(x) for x in a.pages.split(",")], out, a.video)
    print(json.dumps(res, ensure_ascii=False, indent=2))
    return 1 if res["problems"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run unit tests**

Run: `python -m pytest -q tests/test_build_layered_plan.py`
Expected: 3 passed.

- [ ] **Step 5: Build the sample and export video**

Run: `python build_layered.py --pages 1,4 --out "out/星辰之子-样片-p1p4.pptx" --video`
Expected: JSON report with `"problems": []`, the pptx and `out/星辰之子-样片-p1p4.mp4` exist. If a COM call raises (e.g. a behaviour property name), fix that call, note it in `DEGRADED`, rerun.

- [ ] **Step 6: Full test suite**

Run: `python -m pytest -q`
Expected: all tests pass (previous 38 + new ones).

---

### Task 6: Visual review and delivery

**Files:**
- Create: `_tmp/sample_frames.jpg` (review sheet, untracked)

- [ ] **Step 1: Frame grabs from the exported mp4**

p1 occupies 0–7 s (+ transition), p4 starts after it. Grab frames at p1 t = 1.0, 2.5, 3.8, 5.2, 6.6 and p4 t = 1.0, 2.5, 4.2, 6.2, 8.2 (offset p4 by 7.0 + p4 transition 1.6 s) with ffmpeg `-ss <t> -frames:v 1`, then paste them into a 5×2 sheet `_tmp/sample_frames.jpg` with time labels.

- [ ] **Step 2: Check against the storyboard**

Confirm in the sheet: p1 — stars only → core glow → station unfolding → rays + STARDUST lighting → white. p4 — sky + burst only, ground dark → ground revealed → villager sliding in → 铜 + Nomai → three staggered text segments. Look for cutout fringes, layer misalignment, animations that did not run (e.g. scale behaviour ignored). Fix and rebuild until clean.

- [ ] **Step 3: Deliver**

Copy `_tmp/sample_frames.jpg` to `C:\Users\Administrator\.cursor\projects\d-gongzuo-mod\assets\sample_p1p4_frames.jpg` and open it plus the mp4 with `& "D:\gongzuo\cursor\resources\app\bin\cursor.cmd" -r <files>`; launch the pptx with `Start-Process`. Report to the user what each beat does, any degraded effects (`DEGRADED`), and ask for approval before designing pages 2, 3, 5–9.
