> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

# 信号站 SIGNAL · Deluxe Spectacle PPTX — Implementation Plan

**Goal:** Rebuild `demos/signal-station-pptx/` into a 26-slide real `.pptx` whose spectacle is maximal under PowerPoint's own engine — Morph-driven starfield parallax drift, ambient meteor showers on custom motion paths, Morph-assembled kinetic type, layered glow stacks, three signature chapter/iris transitions and a punchy Proof Trio — while keeping the existing 16-beat audio cue map, the §0.0 IP bans, and the readable deadlines.

**Architecture:** `timing.json` stays the single source of truth, but each beat may now split into **Morph twin slides** whose holds sum to the parent beat's hold, so the audio cue map is untouched. The builder is rewritten from one 490-line script into a `deck/` package: pure-python modules (`geometry`, `sync`, `palette`, `comconst`) that are unit-tested without PowerPoint, and COM modules (`stage`, `motion`, `transitions`, `audio`, `scenes/`) driven by a thin `build_deck.py` CLI that can build a subset of beats for fast iteration. A separate `verify_pptx.py` unzips the result and audits it without COM.

**Tech Stack:** Python 3.12, `pywin32` (PowerPoint 16 COM), `pytest` 9, stdlib `zipfile`/`re` for the audit. No `python-pptx` (it cannot write Morph, motion paths, or media-across-slides).

**The default build is silent.** No audio file is named anywhere in the repo. A sound bed is an explicit opt-in (`--audio`) that needs both the `SIGNAL_AUDIO` env var *and* a filled-in `audio.rights.json`; a reference-work track can only ever be declared `personal_preview`, which `verify_pptx.py` reports as a **failing** row. See §0.0 reconciliation below and D6.

**Scale:** 26 slides, **2,867 predicted shapes**, **~670 animation effects**. Both numbers are derived from one frozen density table (§Frozen density table) rather than estimated, and `verify_pptx.py` checks each slide's shape count against that table exactly.

**Working branch:** none (working tree only, no commits)

## Human process override (2026-10-11 19:39 · binding)

The user changed the professional-pipeline order for this task. **This section outranks the default Stage2-dual → human-gate → Stage3-impl sequence until the visible checkpoint is delivered.**

### New order

1. **Finish the current plan fix only** — close Stage 2 reject **1/3** rev-2 Criticals in this file; stamp `<!-- PLAN_COMPLETE tasks=18 rev=2 -->` and set `demos/signal-station-pptx/_plan_heartbeat.json` to `status=complete`, `rev=2`. Do **not** expand scope beyond Critical closures.
2. **Do not start Stage 2 round-2 dual yet.** Skip Dual-A/Dual-B re-review until step 4.
3. **M0 visible build (human checkpoint)** — implement enough of Tasks 1–14 (and Task 15 if time allows) under the **current** rev-2 plan to produce a real PowerPoint file the user can open and watch:
   - Required output: `demos/signal-station-pptx/out/信号站-SIGNAL-m0-preview.pptx`
   - Minimum scope: silent build covering beats that show Morph starfield + at least one signature transition + the iris path (prefer `--beats 1,2,6,11` or fuller if COM is stable). Full 26-slide density is welcome but not required for M0.
   - Allowed shortcuts for M0 only: `--fast` density, incomplete `verify_pptx` (may exit non-zero), no Stage 4 dual, no final `handover.md` polish.
   - Forbidden for M0: more plan rewrite loops, inventing a second plan, waiting on dual PASS before showing pixels.
4. **Human look** — stop and tell the user the path + what is still missing. Wait for an explicit continue signal (`继续双检` / `继续原流程` / equivalent).
5. **Resume original flow** — only after that signal: Stage 2 dual round 2 → (reject loop ≤ remaining budget) → human plan gate → full Tasks 1–18 → Stage 4 dual → final accept.

### Why

Plan-only revision was running 30+ minutes without a watchable half-product. The user needs a real intermediate deck before more dual-review / polish cycles.

### Control file

`demos/signal-station-pptx/_process_override.md` repeats this order for the parent agent. Heartbeat `phase` values: `plan_rev2` → `m0_build` → `await_human_look` → `resume_dual`.

## §0.0 reconciliation — why this deck has no default soundtrack

The guide's §0.0 item 5 bans reference-work audio outright. An earlier draft of this plan configured a commercial soundtrack as the default build input, which is a direct contradiction: a ban cannot be satisfied by a deviation row. The reconciliation is structural, not editorial:

1. **No track is named in the repo.** `deck/audio.py` has no default path. `timing.json` carries `"audio_source": "env:SIGNAL_AUDIO"` and a duration, nothing else. `brief.json` keeps `reference_audio` in `must_exclude` and the build honours it, because the build embeds nothing by default.
2. **Silence is the default.** `python build_deck.py` produces a silent deck and needs no mp3 on disk. The 26 `advTm` values still describe a 229.5 s open-loop timeline, so the deck is playable and auditable with no audio at all.
3. **Audio is a two-key opt-in.** `--audio` mounts a track only when `SIGNAL_AUDIO` points at a real file **and** `audio.rights.json` declares `rights` ∈ {`personal_preview`, `licensed`}. A missing or `none` rights file means the flag is refused with an error, not silently ignored.
4. **`personal_preview` is a failure, by design.** `verify_pptx.py`'s `RIGHTS` row fails whenever a media part is present and `rights != "licensed"`, so a locally-synced preview exits non-zero. That is the honest state: it plays on this machine and it cannot be delivered. D6 records it as a delivery blocker.
5. **A reference-work track can never reach `licensed`.** `audio.rights.json` is a human declaration; the tool cannot verify a licence and never writes `licensed` itself. Task 18 must state which value was used.

## Why the current deck looks plain (root cause — read before Task 1)

`build_deck.py` hard-codes COM constants that are wrong for PowerPoint 16. Verified against the live type library on this machine:

| Name in current script | Value used | What that value actually is | Correct value |
|---|---|---|---|
| `MSO_ANIM_TRIGGER_AFTER_PREVIOUS` | 1 | `msoAnimTriggerOnPageClick` | 3 |
| `MSO_ANIM_EFFECT_WHEEL` | 61 | `msoAnimEffectSpin` | 21 |
| `MSO_ANIM_EFFECT_ZOOM` | 53 | `msoAnimEffectFold` | 23 |
| `MSO_ANIM_EFFECT_FLOAT_UP` | 37 | `msoAnimEffectUnfold` | `msoAnimEffectRiseUp` = 34 |
| `MSO_ANIM_EFFECT_MEDIA_PLAY` | 150 | `msoAnimEffectMediaPlayFromBookmark` | 83 |
| `PP_TRANSITION_SPEED_SLOW` | 3 | `ppTransitionSpeedFast` | 1 |
| `PP_TRANSITION_FADE` | 0x10A7 = 4263 | not a `ppEntryEffect` value | `ppEffectFadeSmoothly` = 3849 |

So most of the deck's "after previous" chain was actually **click-gated** in an auto-advance deck (§Step 2 law 3 — a measured bug), the iris was a spin, and the transitions ran at fast speed. The fix is not "add more effects": it is to stop hard-coding constants (Task 1–2), then build the spectacle on a correct foundation.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-11-signal-station-pptx-preview-design.md`; guide: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md`
- Guide §0.0 hard ban is normative: no reference game branding, names, taglines, key art, costumes, lore, world rules, or identity system. No reference file or template as a base — every deck is `Presentations.Add()` from empty.
- Mode A, `advance: auto`. Deck duration is fixed by the track: **229.5 s**.
- Audio source: **no default**. `--audio` reads `SIGNAL_AUDIO` and requires `audio.rights.json` with `rights` ≠ `none`; the copy lands at `media/<source stem>.mp3`. `media/` is gitignored; **never commit an audio file**. Without `--audio` nothing is read, copied or embedded.
- Canvas 13.333 × 7.5 in = **960 × 540 points**. All coordinates in this plan are points.
- Visual tokens (topic-derived — a night signal station): field `#070B14`, lift `#0C1628`, ink `#F2F4F7`, ghost cool `#7EB0C8`, signal amber `#E8B45A`, hot amber `#FFD68C`, dim `#8C96A5`. Amber is the only strong accent.
- Type floors: anything meant to be read ≥ **18 pt**; meta marks ≤ **12 pt**. Nothing in 12.5–18 pt.
- Motion physics (§2.4): ease-out on every entrance (`Accelerate=0.0`, `Decelerate=0.7`); no bounce; hero signal final by **1.5 s**; every other readable event final by **5 s** or the start of the hold; only declared ambient layers keep moving.
- Two motion densities only (§10.1 law 3): **kinetic identity** on `cold_open`/`title`/`title_lock`/`chapter`/`spike`, **soft editorial** everywhere else.
- One driver per object (§Step 2 law 2): on a Morph twin's B slide, Morph moves the persisting objects and they get **no entrance effects**; ambient particles are deleted and re-seeded instead of being Morph-paired.
- Beat grid for stagger: **0.33 s / 0.66 s** (≈91 BPM); kinetic glyph cascades at 0.12 s steps.
- Do not touch `demos/bocchi-ppt-demo/` or any Mod Manager app code.
- No Commit steps. All work stays in the working tree.
- **Every command in this plan is PowerShell-native.** This machine's shell is PowerShell on Windows, where `cd a && b` is not portable (it fails outright on 5.1) and `grep` does not exist. So: use `;` to sequence, `Set-Location` once per step, and `Select-String` instead of `grep` (`Select-String -Pattern p -Path f`, `(Select-String … ).Count` for `grep -c`, `-List` for `grep -l`, `Get-ChildItem -Recurse | Select-String` for `grep -r`). Multi-line `python -c "…"` heredocs do not survive PowerShell quoting either, which is why the four COM smoke runs live in `smoke.py` as subcommands instead of inline scripts.
- **Density is read, never retyped.** Star / meteor / twinkle / breath / fixture counts come from §Frozen density table via `AMBIENT` in `deck/scenes/common.py`. A number in this plan that contradicts that table is a defect in the plan.
- **Every `N passed` in this plan is a floor, not an equality.** Read each one as "at least N, and no failures and no unexpected skips". Tests added while fixing a review finding must not be deleted to make an arithmetic line come out even; update the floor instead.

## Declared deviations (write these into `handover.md`, Task 18 — do not hide them)

| # | Deviation | Why it is taken | Cap that keeps it honest |
|---|---|---|---|
| D1 | Mode A at 229.5 s exceeds §0.3's last Mode A band (120 s); the guide says re-scope rather than extrapolate a row | The deck's purpose is a capability preview on a fixed 229.5 s open-loop timeline; the duration is not a content choice | 26 slides, avg 8.8 s/slide; **exactly 1 declared wordless plate (slide 8, `"wordless": true`) and 3 near-wordless plates carrying ≤ 2 readable runs (slides 7, 23, 24)**, both machine-checked by the `WORDLESS` row; logged as a deviation, never reported as "inside the budget". The earlier claim that slides 2 and 26 were wordless was false — they are full-text Morph twins |
| D2 | Transition variety is high for §2.3 ("prefer hard cut + internal motion") | The user's blocking feedback is that the deck reads monotone; transitions are the only cross-slide spectacle PowerPoint gives | Exactly 3 signature transitions in the whole deck (2 chapter doors + 1 iris), 12 Morph, the rest `FadeSmoothly`; never two signatures adjacent |
| D3 | The iris disc on slide 17→18 scales far past the 110 % ceiling of §2.4 rule 3 | §4 pattern 7 / §Step 5 `spike` class explicitly sanction an iris climax, which cannot be executed inside 110 % | One object, one beat, once in the deck. No other shape exceeds 106 % |
| D4 | Meteor travel is linear, not ease-out | Rule 1 governs **entrances**; a streak that decelerates reads as broken physics | Linear only on `travel` behaviors of shapes in the ambient layer |
| D5 | Motion paths appear on **all 26 slides** (1–4 particles each, 51 paths total), far beyond §2.4 rule 5's "opener particles" | Meteor showers are the user's headline ask, and a shower that only runs on the opener is not a shower | Particles only: transparency ≥ 0.45, no text, no information, exit by **ambient** fade (never charged to `readable_end`), every path geometrically proven clear of the slide's type safe zone (`path_clears_zone`, Task 6, parametrized over all 12 roles). The earlier "up to 10 slides" wording was false and is retracted |
| D6 | A locally-supplied soundtrack may be embedded for preview. If that track is a reference work, §0.0 item 5 and §7.1 row 7 make the built file **not deliverable** | The user asked for a local preview synced to their own copy of a fixed track | **The default build is silent and names no track.** Audio needs `--audio` + `SIGNAL_AUDIO` + `audio.rights.json`. `verify_pptx.py`'s `RIGHTS` row **fails (exit 1)** whenever a media part exists and `rights != "licensed"`, so a `personal_preview` build can never be reported as passing. `handover.md` states the rights value used and, for anything but `licensed`, states the delivery blocker in both §1 and §5 |
| D7 | Build takes minutes, not seconds (2,867 shapes, ~670 animation effects over COM) | Density is the ask | `--fast` halves the star/meteor counts for iteration; `--beats` builds a subset. A `--fast` build fails `S11` on purpose — only a full-density build may be audited |

---

## Revised success criteria — the spectacle checklist

Every row is checked by `verify_pptx.py` (Task 16) unless marked *human*. A row that cannot be checked counts as failed.

| # | Must appear | Measurable form |
|---|---|---|
| S1 | Morph everywhere it earns continuity | ≥ 12 slides carry a Morph transition; ≥ 10 of them `byObject` |
| S2 | Starfield parallax drift | On **every genuine Morph-B twin** (the 9 slides whose `twin == "b"`, read from `timing.json` — not "every slide with a morph transition"), **all** `STARS[role]` shapes named `!!star_*` have an `a:off` differing from the A twin, and the 10 largest stars (largest `a:ext`, i.e. nearest) average a greater displacement than the 10 smallest |
| S2c | Carry-slide drift | The 2 declared **carry** slides (5, 25) inherit their predecessor's star list by name and displace every one of them; checked by the `CARRY` row, which uses the predecessor's star budget, not their own |
| S3 | Meteor showers | ≥ 40 `<p:animMotion>` nodes spread over **all 26** slides (the frozen table predicts 51); every meteor path clears its slide's type safe zone (unit-proven for all 12 roles, Task 6); every meteor exit is ambient (S13's `readable_end` is unaffected) |
| S4 | Kinetic type that assembles | Title beat: 3 glyph boxes scattered on A, locked on B, paired by Morph; plus a ≥ 12-fragment cascade at 0.12 s steps |
| S5 | Layered glow | ≥ 6 slides carry a 4-layer concentric glow stack (`!!glow_*`), soft-edged where COM exposes it |
| S6 | Exactly one iris climax | Slide 17 transition is `CircleOut`; slide 17→18 Morph expands the `!!iris_core` donut so that **its aperture (hole) radius clears the frame corners** — `IRIS_HOLE × IRIS_GROWTH ≥ hypot(480, 270) = 550.61 pt`, unit-proven (Task 12). Outer diameter alone is not the test: an iris that only grows its outer ring still leaves a dark annulus over the corners |
| S7 | Exactly two chapter doors | Slide 9 `RippleCenter`, slide 14 `WindowVertical`, and no other non-fade / non-Morph transition |
| S8 | Proof cards with punch | 3 cards cascade at 0.16 s steps over big ghost numerals, then Morph re-racks them tighter with an amber rule extending |
| S9 | Ambient breath | ≤ 1 **breath** per slide, ≤ 106 %, ≥ 6 s, repeating. A breath is identified by **effect semantics**, not by the presence of `<p:animScale>`: entrance effects (`FadedZoom`, `RiseUp`, `Zoom`) all emit `animScale` too. A node counts as a breath only when its enclosing `<p:cTn>` carries `autoRev="1"` **and** (`repeatCount="indefinite"` or `dur ≥ 6000`). Separately: **no** `animScale` anywhere may have `<p:to x>` above 106 % |
| S10 | Node density in the corpus range | 420 ≤ count of `<p:cTn …presetID="…">` ≤ 1200 — one such node per `AddEffect`, so this is an effect count, not a raw element count (raw `<p:cTn>` runs ≈ 3× higher and is not the metric). The frozen table predicts ~670 |
| S11 | Visual density | **Every slide's shape count equals `expected_shapes(spec)` exactly** (from the one frozen density table; slide 1 is +1 for the media shape when `--audio` is used), sanity band 95 ≤ n ≤ 150, and ≥ 2,800 shapes total (predicted 2,867) |
| S12 | Tracked latin ghost type | ≥ 8 slides have a run with `spc` ≥ 400 (4 pt tracking) |
| S13 | Readability survives the spectacle | No run in 12.5–18 pt on **any** slide; every slide has ≥ 1 run ≥ 18 pt **except slides whose beat declares `"wordless": true`** (slide 8 only), which must have **no** run ≥ 18 pt; build log reports hero end ≤ 1.5 s and readable end ≤ 5 s per slide |
| S14 | Auto-advance clears the motion | Every slide: `advTm` ≥ non-ambient `readable_end` + 0.5 s (§10.1 law 8), asserted in the build |
| S15 | Static twin still works | `out/信号站-SIGNAL-static-twin.pdf` exists, 26 pages, readable with motion gone (*human*) |
| S16 | Sync holds at the two watch points | *human*: at 1:00 ±1.5 s the 校准 chapter door fires (slide 9); at 2:04 ±1.5 s the iris fires (slide 17) |

Plus these rows, which are not spectacle but are hard gates:

| # | Must hold | Measurable form |
|---|---|---|
| MAP | Every slide's transition is the one `timing.json` declares | XML local element name matches `TRANS_EXPECT[spec.trans]`, read through `mc:AlternateContent` |
| CHROME | The meta index was re-texted on every twin | Slide N's `<a:t>` runs contain `NN / 26`; a duplicated B twin that still reads its A twin's number fails |
| WORDLESS | D1's plate claim is true | Exactly 1 slide with no run ≥ 18 pt and it is the declared one; exactly 3 slides with ≤ 2 runs ≥ 18 pt |
| BAN | §0.0 item 1 | No banned term in **any slide's `<a:t>` text**, in `ppt/media/*` part names, or in `timing.json` / `brief.json` — not only in `timing.json` |
| AUDIO | The sound bed, if present, is mounted once | ≤ 1 media part; if present it is on slide 1 and named `!!audio_000` |
| RIGHTS | §0.0 item 5 / §7.1 row 7 | **Fails** when a media part exists and `audio.rights.json` does not declare `rights == "licensed"`. Passes trivially on a silent build |

**Exit-code law (closes the "a row that cannot be checked counts as failed" hole).** `verify_pptx.py` exits 0 **only** when every machine row is `PASS` **and** every `MANUAL` row (S15, S16) has a human `PASS` recorded in `tests/manual-verdicts.json`. With no verdict file, or with any verdict missing or `FAIL`, the script prints the rows and exits **1**. A `RIGHTS` or `BAN` failure likewise forces exit 1. There is no flag that downgrades a `RIGHTS` failure.

---

## Revised beat / slide map

16 beats unchanged (same `start_sec`, same `hold_sec`, same roles as the approved spec). Ten of them split into Morph twins. **Twin law: the twins' holds sum exactly to the parent beat's hold**, so no cue moves.

| Beat | Beat start | Beat hold | Role | Slides | Twin holds | Transition into each slide (duration) |
|---|---|---|---|---|---|---|
| 1 | 0:00 | 8 | cold_open | 1, 2 | 4 + 4 | `cut` (0) → `morph_obj` (2.0) |
| 2 | 0:08 | 16 | title | 3, 4 | 8 + 8 | `fade` (1.0) → `morph_obj` (2.0) |
| 3 | 0:24 | 16 | title_lock | 5, 6 | 8 + 8 | `morph_word` (1.6) **carry** → `morph_obj` (2.0) |
| 4 | 0:40 | 12 | thesis | 7 | 12 | `fade` (1.2) |
| 5 | 0:52 | 8 | breath | 8 | 8 | `fade` (1.2) — **`wordless: true`** |
| 6 | **1:00** | 12 | chapter 校准 | **9**, 10 | 6 + 6 | **`door_ripple` (1.4)** → `morph_obj` (2.0) |
| 7 | 1:12 | 12 | concept 远灯 | 11, 12 | 6 + 6 | `fade` (1.0) → `morph_obj` (2.0) |
| 8 | 1:24 | 12 | concept 航迹 | 13 | 12 | `fade` (1.0) |
| 9 | 1:36 | 8 | chapter 倾听 | 14 | 8 | **`door_window` (1.4)** |
| 10 | 1:44 | 20 | concept 叠层 | 15, 16 | 10 + 10 | `fade` (1.0) → `morph_obj` (2.0) |
| 11 | **2:04** | 12 | spike 锁定 | **17**, 18 | 6 + 6 | **`iris` (1.2)** → `morph_obj` (2.0) |
| 12 | 2:16 | 16 | afterglow | 19, 20 | 8 + 8 | `fade` (1.6) → `morph_obj` (2.0) |
| 13 | 2:32 | 16 | proof | 21, 22 | 8 + 8 | `fade` (1.0) → `morph_obj` (1.6) |
| 14 | 2:48 | 12 | settle | 23 | 12 | `fade` (1.2) |
| 15 | 3:00 | 16 | settle | 24 | 16 | `fade` (1.6) |
| 16 | 3:16 | 33.5 | end | 25, 26 | 16 + 17.5 | `morph_obj` (2.0) **carry** → `morph_obj` (2.5) |

26 slides, holds sum to **229.5 s**, 12 Morph transitions (11 `morph_obj` + 1 `morph_word`), 3 signature transitions, **10** `FadeSmoothly`, 1 `cut`. (An earlier draft said 11 fades; the arithmetic is 1 + 12 + 3 + 10 = 26, and `test_morph_and_signature_transition_budget` now asserts the fade count too.)

**How holds split (the rule a twin must obey):** a twin pair holds the same beat visually across two slides. The A slide carries the beat's entrances and its hero signal; the B slide carries no entrance on any persisting object — Morph alone moves the field, the glow and the type, which is what produces continuous drift rather than a cut. The B slide's own hold must still clear its Morph: `hold_b ≥ trans_dur + 1.5 s`. The shortest pair in this map is beat 1 (4 + 4 against a 2.0 s Morph), which satisfies it.

**Two slides Morph without a twin, and they are declared.** Slides **5** (beat 3 `title_lock`, `morph_word`) and **25** (beat 16 `end`, `morph_obj`) take a Morph transition from the *previous beat's* last slide. They are not duplicates, so there is no identity pairing and nothing guarantees a shape has a partner — an earlier draft left this unstated, which would have produced two unexplained cross-fades plus a law-2 conflict (Morph moving an object that also has an entrance). Both are now marked `"carry": true` in `timing.json`, which means exactly three things:

1. The carry slide's star layer is **inherited**, not regenerated: `stars = geometry.drifted(prev_build.stars, *DRIFT[role])`. Same count, same order, same `!!star_nnn` names, new positions — so Morph pairs them **by name** (that is what the `!!` naming law in Task 7 is for) and the field drifts across the beat boundary instead of jumping.
2. Its star budget is therefore its **predecessor's** budget, not its own row's. The frozen table sets `title_lock` and `end` to 88 so the two agree either way.
3. Nothing that persists may carry an entrance. On both carry slides every entrance is on a genuinely new object (the title, the ghost latin, the dot); the inherited field, vignette, chrome and stars get ambient effects only. Unpaired objects from the previous slide (beat 2's glyph boxes, beat 15's line) are simply absent and Morph fades them out, which is correct.

Carry slides are audited separately from true twins by the `CARRY` row, because S2's "all of `STARS[role]`" is measured against the predecessor's budget here.

**Transition time is not free.** The 26 transitions total **39.5 s**. If PowerPoint's `advTm` timer starts *after* the transition finishes, every hold must be shortened by its own transition duration or the deck ends 39.5 s late. Task 5 measures which model is real instead of guessing; `sync.py` carries both.

---

## Effect vocabulary per slide class

Shared by every slide: field stack (2 translucent rects) → vignette (4 edge rects) → star layer (`!!star_000…`) → meteor layer (`!!meteor_00…`) → glow stack (`!!glow_*`) → type → chrome (`NN / 26` index + tracked `SIGNAL` ghost, both ≤ 12 pt).

| Class | Slides | Density | Entrances (readable) | Ambient layer | Morph job on the B twin |
|---|---|---|---|---|---|
| `cold_open` | 1, 2 | kinetic | amber bar `Wipe` right 0.8 s; title fade 0.8 s @0.4; sub fade 0.6 s @1.1 | per the frozen table | stars drift down-left 22 pt, bar lengthens, title holds; **ambient reseed** |
| `title` | 3, 4 | kinetic | 12-fragment cascade (fade 0.4 s, 0.12 s steps) then 3 glyph boxes `FadedZoom` 0.7 s on the 0.33 grid | per the frozen table | **glyph assemble**: A's scattered/rotated glyphs → B's locked centre line; **ambient reseed** |
| `title_lock` | 5, 6 | kinetic | title fade 0.6 s (hero); tracked latin ghost fade 1.0 s @0.3 | per the frozen table | `morph_word` glides the latin ghost; stars drift right 18 pt; **ambient reseed** |
| `thesis` | 7 | soft | one sentence fade 0.8 s (hero @0.0); meta fade 0.4 s @1.0 | per the frozen table | — |
| `breath` | 8 | soft | nothing readable at all — the meta mark is ≤ 12 pt, so this plate is **declared `wordless`** and S13's "≥ 18 pt somewhere" is waived for it (and only it) | per the frozen table | — (wordless plate) |
| `chapter` | 9, 10, 14 | kinetic | ghost latin fade 0.5 s; chapter word `FadedZoom` 0.8 s @0.3 (hero lands 1.1 s); amber rule `Wipe` 0.6 s @0.9 | per the frozen table | word holds, rule extends to a horizon line, stars drift up 16 pt; **ambient reseed** |
| `concept` | 11, 12, 13, 15, 16 | soft | section label 0.4 s; title 0.7 s @0.2; body 0.5 s @1.4; trail ovals cascade 0.33 s steps | per the frozen table | glow core travels, trail extends by 2 ovals, stars drift; **ambient reseed** |
| `spike` | 17, 18 | spike | `iris` transition is the hero event; inverted dark type fade 0.5 s @0.6 | per the frozen table | **`!!iris_core` donut aperture expands clear of the frame corners** (D3), type exits under it; **ambient reseed** |
| `afterglow` | 19, 20 | soft | title 0.8 s; sub 0.5 s @1.2 | per the frozen table | glow dims (transparency animation), stars thin by drifting out; **ambient reseed** |
| `proof` | 21, 22 | soft | 3 ghost numerals fade 0.4 s @0.16 steps; 3 cards `RiseUp` 0.6 s @0.16 steps; labels fade with their card | per the frozen table | cards re-rack tighter, amber rule on card 2 extends; **ambient reseed** |
| `settle` | 23, 24 | soft | short line fade 0.8 s; meta 0.4 s @1.0 | per the frozen table | — |
| `end` | 25, 26 | soft | title fade 1.0 s; sub 0.6 s @1.4; amber dot fade 0.5 s @2.0 | per the frozen table | 17.5 s slow drift: stars sink, glow fades, dot holds; **ambient reseed** |

**"Ambient reseed" is a named obligation, not a nicety.** `clear_timeline()` deletes *every* effect on a B twin, which in an earlier draft left the twin with a frozen starfield and no breathing glow — the slide would have gone visually dead for its whole hold while Morph ran for 2 s. Every `morph()` therefore calls `common.reseed_ambient(ctx, prev)`, which re-adds, in this order: `twinkle` on the drifted star shapes (found by `!!star_` prefix), `breath` on the glow core when the role declares one, then a fresh meteor set. All three are ambient, so none of them touches `readable_end`, and none of them is an entrance on a persisting object.

---

## Frozen density table — the single source of truth for S2, S3, S10 and S11

Three checklist rows and three modules used to carry their own copies of these numbers, and they disagreed: `S2` demanded ≥ 56 displaced stars while four roles were budgeted below 56, and `S11` demanded 80–170 shapes per slide while the scene budgets produced 44–89. **This table is now the only place any of it is written down.** It lives in code as `AMBIENT` in `deck/scenes/common.py` (Task 11) and is imported by `verify_pptx.py` (Task 16) and by the tests — never retyped.

`fixtures` is every non-particle shape the role paints, **including** the 8 shared ones (2 field + 4 vignette + 2 chrome). `expected = stars + meteors + fixtures`.

| role | `stars` | `meteors` | `twinkle` | `breath` | `fixtures` | fixtures breakdown | expected shapes |
|---|---|---|---|---|---|---|---|
| `cold_open` | 96 | 2 | 20 | no | 11 | 8 + rule + title + sub | **109** |
| `title` | 88 | 3 | 18 | no | 23 | 8 + 12 frags + 3 glyphs | **114** |
| `title_lock` | 88 | 2 | 18 | yes | 14 | 8 + 4 glow + title + ghost | **104** |
| `thesis` | 92 | 1 | 18 | no | 10 | 8 + line + meta | **103** |
| `breath` | 120 | 4 | 30 | yes | 13 | 8 + 4 glow + meta | **137** |
| `chapter` | 96 | 2 | 18 | no | 11 | 8 + ghost + word + rule | **109** |
| `concept` | 92 | 2 | 16 | yes | 21 | 8 + 4 glow + 3 type + 6 trail | **115** (B: 117, +2 trail) |
| `spike` | 112 | 3 | 20 | no | 15 | 8 + 4 glow + donut + word + mark | **130** |
| `afterglow` | 84 | 1 | 14 | yes | 14 | 8 + 4 glow + title + sub | **99** |
| `proof` | 80 | 1 | 14 | no | 19 | 8 + 3 numerals + 3 cards + 3 labels + accent + ghost title | **100** |
| `settle` | 88 | 1 | 16 | no | 10 | 8 + line + meta | **99** |
| `end` | 88 | 2 | 22 | yes | 15 | 8 + 4 glow + title + sub + dot | **105** |

Reading down the 26 slides: 109, 109, 114, 114, 104, 104, 103, 137, 109, 109, 115, 117, 115, 109, 115, 117, 130, 130, 99, 99, 100, 100, 99, 99, 105, 105 — **2,867 shapes**, every slide inside 95–150, which is what S11 asserts (plus +1 on slide 1 when `--audio` mounts the media shape).

Three consequences that the earlier draft got wrong and this table fixes:

- **S2 is now satisfiable everywhere.** The smallest star budget is `proof`'s 80, comfortably above the old ≥ 56 floor; S2 no longer uses a floor at all, it demands the role's whole budget.
- **Every `concept` beat now sets `"glow": true`** in `timing.json` (beat 8 did not), so `fixtures` is 21 for all five concept slides and the table has no per-slide exceptions.
- **Meteors are on all 26 slides** (51 paths), which is what D5 now declares instead of the retracted "up to 10 slides".

Effect counts follow from the same table — `twinkle` + 2 per meteor (travel + ambient exit fade) + the role's entrances + the breath — and sum to **~670**, inside S10's 420–1200 band. `--fast` halves `stars`, `meteors` and `twinkle` (floor 2), which is why a `--fast` build cannot pass S11.

---

## File map

| File | Responsibility |
|---|---|
| `demos/signal-station-pptx/timing.json` | **Modify** — 16 beats + twin splits + transition per slide + watch points |
| `demos/signal-station-pptx/brief.json` | **Create** — guide §Step 0 Brief JSON (mode, status, advance, must_exclude, source_packet) |
| `demos/signal-station-pptx/build_deck.py` | **Rewrite** — thin CLI orchestrator (`--beats`, `--fast`, `--no-audio`, `--video`, `--out`) |
| `demos/signal-station-pptx/probe_com.py` | **Create** — PowerPoint capability probe → `COM-NOTES.md` |
| `demos/signal-station-pptx/calibrate_timing.py` | **Create** — measures whether `advTm` includes the transition |
| `demos/signal-station-pptx/verify_pptx.py` | **Create** — unzip + audit (no COM), exit 1 on any FAIL |
| `demos/signal-station-pptx/deck/__init__.py` | **Create** — empty package marker |
| `demos/signal-station-pptx/deck/comconst.py` | **Create** — constant resolution: live type library over a verified fallback table |
| `demos/signal-station-pptx/deck/geometry.py` | **Create** — LCG, `Rect`, starfield, parallax drift, meteor routing, motion-path strings, stagger (pure python) |
| `demos/signal-station-pptx/deck/palette.py` | **Create** — colour/type/zone tokens (pure python) |
| `demos/signal-station-pptx/deck/sync.py` | **Create** — beat → slide expansion, `advTm` math, validation, sync ledger (pure python) |
| `demos/signal-station-pptx/deck/stage.py` | **Create** — COM shape primitives: field, vignette, star, meteor, glow stack, text, ghost latin |
| `demos/signal-station-pptx/deck/motion.py` | **Create** — COM effect helpers + per-slide readable/ambient end tracking |
| `demos/signal-station-pptx/deck/transitions.py` | **Create** — transition table, twin duplication, timeline clearing, drift repositioning |
| `demos/signal-station-pptx/deck/audio.py` | **Create** — offstage media mount + play-across-slides |
| `demos/signal-station-pptx/deck/scenes/__init__.py` | **Create** — `SCENES` registry + `scene_plate` fallback |
| `demos/signal-station-pptx/deck/scenes/common.py` | **Create** — `SceneContext`, `SlideBuild`, chrome, ambient-layer helper |
| `demos/signal-station-pptx/deck/scenes/identity.py` | **Create** — `cold_open`, `title`, `title_lock` |
| `demos/signal-station-pptx/deck/scenes/editorial.py` | **Create** — `thesis`, `breath`, `concept`, `settle` |
| `demos/signal-station-pptx/deck/scenes/spike.py` | **Create** — `chapter`, `spike` |
| `demos/signal-station-pptx/deck/scenes/proof.py` | **Create** — `proof`, `afterglow`, `end` |
| `demos/signal-station-pptx/smoke.py` | **Create** — the four COM smoke runs as subcommands (`stage`, `motion`, `twin`, `audio`), so no step needs a multi-line shell heredoc on PowerShell |
| `demos/signal-station-pptx/audio.rights.json` | **Create** (Task 10, tracked) — the rights declaration: `rights` ∈ `none` \| `personal_preview` \| `licensed`, plus `declared_by` / `date` / `note`. Carries **no** file path, so the repo never names a track |
| `demos/signal-station-pptx/conftest.py` | **Create** (Task 2, before the first test runs) — empty; its *presence* makes pytest prepend this directory to `sys.path`, which is the only thing that lets `deck.*`, `build_deck` and `verify_pptx` import from any cwd. Listed here because the file map previously omitted it even though 10 test files depend on it |
| `demos/signal-station-pptx/tests/manual-verdicts.json` | **Create** (Task 15, tracked) — the human verdicts for S15/S16. `verify_pptx.py` refuses to exit 0 without it |
| `demos/signal-station-pptx/tests/fixtures/transition-morph.xml` | **Create** (Task 14) — a **real** `<mc:AlternateContent>` transition block cut from the built deck's `ppt/slides/slide2.xml` |
| `demos/signal-station-pptx/tests/fixtures/transition-ripple.xml` | **Create** (Task 14) — real block from `slide9.xml` |
| `demos/signal-station-pptx/tests/fixtures/transition-window.xml` | **Create** (Task 14) — real block from `slide14.xml` |
| `demos/signal-station-pptx/tests/fixtures/transition-circle.xml` | **Create** (Task 14) — real block from `slide17.xml` |
| `demos/signal-station-pptx/tests/fixtures/slide-breath.xml` | **Create** (Task 14) — a whole real slide, for `parse_slide_xml` text/size/tracking/breath coverage |
| `demos/signal-station-pptx/tests/test_comconst.py` | **Create** (Task 2) — live-library-over-fallback resolution |
| `demos/signal-station-pptx/tests/test_geometry.py` | **Create** (Task 6) — determinism, zones, parallax ordering, drift, path clearance, path strings |
| `demos/signal-station-pptx/tests/test_sync.py` | **Create** (Task 5) — twin sums, carry flags, `advTm` models, watch points, validation errors |
| `demos/signal-station-pptx/tests/test_timing_data.py` | **Create** (Task 3) — the real `timing.json` satisfies every law |
| `demos/signal-station-pptx/tests/test_palette.py` | **Create** (Task 4) — colour packing, type floors, zones inside canvas |
| `demos/signal-station-pptx/tests/test_stage_names.py` | **Create** (Task 7) — `!!` naming law, `Caps` parsing |
| `demos/signal-station-pptx/tests/test_motion_clock.py` | **Create** (Task 8) — the readability clock, ambient vs readable marks |
| `demos/signal-station-pptx/tests/test_transitions.py` | **Create** (Task 9) — transition constants, drift vectors, twin/carry planning |
| `demos/signal-station-pptx/tests/test_audio_paths.py` | **Create** (Task 10) — source resolution, rights gating, duration measurement |
| `demos/signal-station-pptx/tests/test_scenes_contract.py` | **Create** (Task 11) — every scene law, against fake Stage/Motion/Slide |
| `demos/signal-station-pptx/tests/test_cli_args.py` | **Create** (Task 13) — flags, beat subsets, renumbering |
| `demos/signal-station-pptx/tests/test_verify_rules.py` | **Create** (Task 16) — every audit row, plus the real XML fixtures |
| `demos/signal-station-pptx/COM-NOTES.md` | **Create** — probe output + honest "COM can / cannot" table |
| `demos/signal-station-pptx/handover.md` | **Create** — ship report, audits, deviation ledger, watch-point checklist |
| `demos/signal-station-pptx/README.md` | **Modify** — new CLI, play steps, limits, copyright |
| `demos/signal-station-pptx/.gitignore` | **Modify** — also ignore `out/*.pdf`, `out/*.mp4`, `out/_*.pptx`, `out/*.txt` |

Deleted at the end of Task 15: nothing. The old `build_deck.py` body is replaced in place.

---

### Task 1: PowerPoint capability probe → `COM-NOTES.md`

Nothing else in this plan may be written against a guessed API. This task establishes what this machine's PowerPoint actually supports, and its output is read by Tasks 2, 7, 8, 9 and 10.

**Files:**
- Create: `demos/signal-station-pptx/probe_com.py`
- Create: `demos/signal-station-pptx/COM-NOTES.md`

**Interfaces:**
- Produces: `COM-NOTES.md` with a `## Capability results` table whose rows are `OK` / `FAIL: <reason>`; later tasks branch on the `AddEffect(msoAnimEffectCustom)`, `RepeatUntilEndOfSlide`, `Glow`, `SoftEdge`, `TextFrame2.Font.Spacing` and `StopAfterSlides` rows.

- [ ] **Step 1: Write the probe**

`demos/signal-station-pptx/probe_com.py`:

```python
#!/usr/bin/env python3
"""Probe this machine's PowerPoint COM surface and write COM-NOTES.md.

Run once before building. Every capability the deck relies on is exercised on a
scratch presentation that is never saved.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import win32com.client as w32

ROOT = Path(__file__).resolve().parent
NOTES = ROOT / "COM-NOTES.md"


class _Skip(Exception):
    """A capability that is not needed in the default (silent) configuration."""

NEEDED_CONSTANTS = [
    "ppLayoutBlank", "ppSaveAsOpenXMLPresentation", "ppAlertsNone",
    "ppTransitionSpeedSlow", "ppFixedFormatTypePDF", "ppSlideShowUseSlideTimings",
    "ppEffectCut", "ppEffectFadeSmoothly", "ppEffectCircleOut",
    "ppEffectRippleCenter", "ppEffectWindowVertical",
    "ppEffectMorphByObject", "ppEffectMorphByWord",
    "msoAnimTriggerWithPrevious", "msoAnimTriggerAfterPrevious",
    "msoAnimTypeMotion", "msoAnimTypeScale", "msoAnimTypeRotation",
    "msoAnimEffectCustom", "msoAnimEffectFade", "msoAnimEffectRiseUp",
    "msoAnimEffectFadedZoom", "msoAnimEffectGrowShrink", "msoAnimEffectWipe",
    "msoAnimEffectTransparency", "msoAnimEffectMediaPlay",
    "msoAnimDirectionRight",
]


def live_constants() -> dict[str, int]:
    out: dict[str, int] = {}
    for mod in w32.constants.__dicts__:
        for key, val in mod.items():
            if isinstance(val, int):
                out[key] = val
    return out


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    app = w32.gencache.EnsureDispatch("PowerPoint.Application")
    consts = live_constants()
    rows: list[tuple[str, str]] = []

    missing = [n for n in NEEDED_CONSTANTS if n not in consts]
    rows.append(("constants", "OK" if not missing else f"FAIL: missing {missing}"))

    prs = None
    try:
        prs = app.Presentations.Add(WithWindow=True)
        prs.PageSetup.SlideWidth = 960
        prs.PageSetup.SlideHeight = 540
        slide = prs.Slides.Add(1, consts["ppLayoutBlank"])
        shape = slide.Shapes.AddShape(9, 400, 200, 80, 80)  # msoShapeOval
        shape.Name = "!!probe"
        seq = slide.TimeLine.MainSequence

        def check(label, fn):
            try:
                fn()
                rows.append((label, "OK"))
            except _Skip as exc:
                rows.append((label, f"SKIP: {exc}"))
            except Exception as exc:  # noqa: BLE001 - probe reports, never raises
                rows.append((label, f"FAIL: {type(exc).__name__}: {exc}"))

        def custom_motion():
            eff = seq.AddEffect(shape, consts["msoAnimEffectCustom"], 0,
                                consts["msoAnimTriggerWithPrevious"])
            beh = eff.Behaviors.Add(consts["msoAnimTypeMotion"])
            beh.MotionEffect.Path = "M 0 0 L 0.2500 0.1800 E"
            eff.Timing.Duration = 1.1
            eff.Timing.Accelerate = 0.0
            eff.Timing.Decelerate = 0.0

        def path_effect_fallback():
            eff = seq.AddEffect(shape, 127, 0, consts["msoAnimTriggerWithPrevious"])  # PathDown
            for i in range(1, eff.Behaviors.Count + 1):
                beh = eff.Behaviors.Item(i)
                if beh.Type == consts["msoAnimTypeMotion"]:
                    beh.MotionEffect.Path = "M 0 0 L 0.2500 0.1800 E"

        def repeat_until_end():
            eff = seq.AddEffect(shape, consts["msoAnimEffectGrowShrink"], 0,
                                consts["msoAnimTriggerWithPrevious"])
            eff.Timing.Duration = 6.0
            eff.Timing.RepeatUntilEndOfSlide = True
            eff.Timing.AutoReverse = True
            for i in range(1, eff.Behaviors.Count + 1):
                beh = eff.Behaviors.Item(i)
                if beh.Type == consts["msoAnimTypeScale"]:
                    beh.ScaleEffect.ByX = 106.0
                    beh.ScaleEffect.ByY = 106.0

        def exit_effect():
            eff = seq.AddEffect(shape, consts["msoAnimEffectFade"], 0,
                                consts["msoAnimTriggerWithPrevious"])
            eff.Exit = True
            eff.Timing.Duration = 0.3

        def transparency_anim():
            eff = seq.AddEffect(shape, consts["msoAnimEffectTransparency"], 0,
                                consts["msoAnimTriggerWithPrevious"])
            eff.Timing.Duration = 1.2

        def glow():
            shape.Glow.Radius = 14
            shape.Glow.Color.RGB = 0x5AB4E8
            shape.Glow.Transparency = 0.45

        def soft_edge():
            shape.SoftEdge.Type = 6

        def tracking():
            box = slide.Shapes.AddTextbox(1, 100, 420, 400, 40)
            box.TextFrame2.TextRange.Text = "SIGNAL"
            box.TextFrame2.TextRange.Font.Spacing = 6.0
            box.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = 0xC8B07E
            box.TextFrame2.TextRange.Font.Fill.Transparency = 0.55

        def donut():
            d = slide.Shapes.AddShape(18, 300, 100, 200, 200)  # msoShapeDonut
            d.Name = "!!iris_probe"
            d.Adjustments.SetItem(1, 0.35)  # hole radius as a fraction of the ring

        def transition_morph():
            t = slide.SlideShowTransition
            t.EntryEffect = consts["ppEffectMorphByObject"]
            t.Duration = 2.0
            t.AdvanceOnTime = True
            t.AdvanceOnClick = False
            t.AdvanceTime = 4.0

        def duplicate_slide():
            dup = prs.Slides(1).Duplicate()
            dup.Item(1).Delete()

        def gradient():
            bg = slide.Shapes.AddShape(1, 0, 0, 960, 540)
            bg.Fill.TwoColorGradient(7, 1)  # msoGradientFromCenter, variant 1
            bg.Delete()

        check("AddEffect(msoAnimEffectCustom) + MotionEffect.Path", custom_motion)
        check("AddEffect(PathDown) + rewrite MotionEffect.Path", path_effect_fallback)
        check("Timing.RepeatUntilEndOfSlide + AutoReverse + ScaleEffect", repeat_until_end)
        check("Effect.Exit", exit_effect)
        check("msoAnimEffectTransparency", transparency_anim)
        check("Shape.Glow", glow)
        check("Shape.SoftEdge", soft_edge)
        check("TextFrame2.Font.Spacing + Font.Fill.Transparency", tracking)
        check("msoShapeDonut + Adjustments", donut)
        check("SlideShowTransition morph + Duration", transition_morph)
        check("Slides.Duplicate()", duplicate_slide)
        check("Fill.TwoColorGradient", gradient)

        def media():
            # No track is named here. The probe exercises AddMediaObject2 only
            # when the operator has already opted in via SIGNAL_AUDIO; a silent
            # build never needs this row, so its absence is SKIP, not FAIL.
            raw = os.environ.get("SIGNAL_AUDIO")
            if not raw:
                raise _Skip("SIGNAL_AUDIO is not set; the default build is silent")
            mp3 = Path(raw)
            if not mp3.is_file():
                raise FileNotFoundError(f"SIGNAL_AUDIO points at {mp3}, which does not exist")
            snd = slide.Shapes.AddMediaObject2(str(mp3), LinkToFile=False,
                                               SaveWithDocument=True,
                                               Left=-160, Top=-160, Width=24, Height=24)
            ps = snd.AnimationSettings.PlaySettings
            ps.PlayOnEntry = True
            ps.HideWhileNotPlaying = True
            ps.StopAfterSlides = 26
            seq.AddEffect(snd, consts["msoAnimEffectMediaPlay"], 0,
                          consts["msoAnimTriggerWithPrevious"])

        check("AddMediaObject2 + PlaySettings.StopAfterSlides", media)
    finally:
        if prs is not None:
            try:
                prs.Saved = True
                prs.Close()
            except Exception:
                pass
        try:
            app.Quit()
        except Exception:
            pass

    lines = ["# COM capability notes (PowerPoint on this machine)", "",
             "Generated by `python probe_com.py`. Re-run after an Office update.", "",
             "## Capability results", "", "| Capability | Result |", "|---|---|"]
    for label, result in rows:
        lines.append(f"| `{label}` | {result} |")
    lines += ["", "## Resolved constants", "", "| Name | Value |", "|---|---|"]
    for name in NEEDED_CONSTANTS:
        lines.append(f"| `{name}` | {consts.get(name, 'MISSING')} |")
    NOTES.write_text("\n".join(lines) + "\n", encoding="utf-8")

    failed = [f"{label}: {result}" for label, result in rows
              if not result.startswith(("OK", "SKIP"))]
    print("\n".join(f"{label}: {result}" for label, result in rows))
    print(f"\nwrote {NOTES}")
    return 0 if not failed else 2


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Check the file parses before launching Office**

Run: `Set-Location demos/signal-station-pptx; python -c "import ast,pathlib;ast.parse(pathlib.Path('probe_com.py').read_text(encoding='utf-8'));print('parses')"`
Expected: `parses`

- [ ] **Step 3: Run the probe**

Run: `Set-Location demos/signal-station-pptx; python probe_com.py`
Expected: a table printed to stdout and `COM-NOTES.md` written, exit code 0. The `media` row reads `SKIP: SIGNAL_AUDIO is not set` unless the operator has deliberately opted into audio — the default build is silent, so that row is not needed and must not be treated as a failure. Every other row must read `OK`, except possibly `AddEffect(msoAnimEffectCustom)` — if that one fails, the `PathDown` fallback row must be `OK`, and Task 8 uses the fallback.

- [ ] **Step 4: Append the honesty table to `COM-NOTES.md`**

Add this section verbatim at the end of `COM-NOTES.md` (it is the answer to "what can COM really do", and Task 18 quotes it):

```markdown
## What PowerPoint COM can and cannot do (honest scope)

**Can — and this deck uses it:**
- Morph transitions (`byObject` / `byWord` / `byChar`) with an explicit duration, which interpolates position, size, rotation and colour of paired objects. Pairing is by object identity on a duplicated slide, or by identical `!!`-prefixed shape names.
- Arbitrary motion paths per shape (`Behaviors.Add(msoAnimTypeMotion).MotionEffect.Path`, `"M 0 0 L dx dy E"` in slide fractions).
- Scale, rotation, colour and transparency behaviours; entrance and exit effects; per-effect delay, duration, accelerate/decelerate; repeat until end of slide.
- Per-slide auto-advance time and transition duration; embedded audio that plays across N slides (`PlaySettings.StopAfterSlides`).
- Per-run font, colour, fill transparency and letter tracking (`TextFrame2.Font.Spacing`); shape glow and soft edges where the build exposes them.
- Export of a static PDF (`ExportAsFixedFormat`) and a rendered MP4 (`CreateVideo`).

**Cannot — do not promise these:**
- No particle system. "Meteor shower" is N hand-placed shapes with N motion paths; the count is a build-time cost, not a runtime emitter.
- No additive/screen blend modes. Every glow is faked by stacking translucent fills; a bright core over a bright star will not bloom.
- No motion blur, no depth of field, no animated blur radius. Streaks are elongated ovals, not blurred points.
- No per-frame easing curves — only accelerate/decelerate fractions. No spring or inertia.
- No animated masks. An "iris" is either the `CircleOut` slide transition or a donut shape whose hole is Morphed; you cannot mask arbitrary content mid-slide.
- Morph does not interpolate unpaired objects, text it cannot match, or shape-type changes; those cross-fade instead.
- No audio-reactive triggers. Sync is open-loop: a fixed `advTm` per slide against a fixed track.
- Fidelity is PowerPoint-specific. WPS and older Office builds degrade Morph to a cross-fade and may ignore `StopAfterSlides`.
- This is not After Effects parity and must never be described as such.
```

**Accept:** `COM-NOTES.md` exists, contains a `## Capability results` table with **no `FAIL` row at all** (the `media` row reads `SKIP: SIGNAL_AUDIO is not set` on a default silent configuration, and `SKIP` is not a failure — `Caps` treats it as absent, which is correct, because a silent build never calls `AddMediaObject2`), a `## Resolved constants` table with no `MISSING`, and the honesty section.

---

### Task 2: `deck/comconst.py` — never hard-code a constant again

**Files:**
- Create: `demos/signal-station-pptx/deck/__init__.py` (empty)
- Create: `demos/signal-station-pptx/conftest.py` (empty — its presence puts the demo directory on `sys.path` so `import deck` works from any cwd)
- Create: `demos/signal-station-pptx/deck/comconst.py`
- Create: `demos/signal-station-pptx/tests/test_comconst.py`

**Interfaces:**
- Produces: `class Consts` with attribute access (`K.ppEffectMorphByObject`), `Consts.get(name, default)`, and `load_constants(app=None) -> Consts`. Every other COM module takes a `Consts` instance named `K`.

- [ ] **Step 1: Write the failing test**

`demos/signal-station-pptx/tests/test_comconst.py`:

```python
import pytest

from deck.comconst import Consts, VERIFIED


def test_verified_table_has_the_values_measured_on_powerpoint_16():
    assert VERIFIED["ppEffectMorphByObject"] == 3954
    assert VERIFIED["ppEffectFadeSmoothly"] == 3849
    assert VERIFIED["ppEffectCircleOut"] == 3845
    assert VERIFIED["msoAnimTriggerAfterPrevious"] == 3
    assert VERIFIED["msoAnimEffectMediaPlay"] == 83
    assert VERIFIED["ppTransitionSpeedSlow"] == 1


def test_attribute_access_falls_back_to_the_verified_table():
    k = Consts({})
    assert k.msoAnimTypeMotion == 1


def test_live_typelib_wins_over_the_fallback():
    k = Consts({"ppEffectFadeSmoothly": 9999})
    assert k.ppEffectFadeSmoothly == 9999


def test_unknown_constant_raises_with_a_repair_hint():
    k = Consts({})
    with pytest.raises(AttributeError, match="probe_com.py"):
        k.ppEffectNoSuchThing
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_comconst.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/comconst.py`:

```python
"""COM constant resolution.

The live PowerPoint type library is authoritative. VERIFIED is a fallback table
read off PowerPoint 16 on the build machine (see COM-NOTES.md) so pure-python
tests can run without Office.
"""
from __future__ import annotations

VERIFIED: dict[str, int] = {
    # presentation / layout / save
    "ppLayoutBlank": 12,
    "ppSaveAsOpenXMLPresentation": 24,
    "ppAlertsNone": 1,
    "ppTransitionSpeedSlow": 1,
    "ppTransitionSpeedMedium": 2,
    # transitions used by this deck
    "ppEffectNone": 0,
    "ppEffectCut": 257,
    "ppEffectFade": 1793,
    "ppEffectFadeSmoothly": 3849,
    "ppEffectCircleOut": 3845,
    "ppEffectRippleCenter": 3867,
    "ppEffectWindowVertical": 3886,
    "ppEffectMorphByObject": 3954,
    "ppEffectMorphByWord": 3955,
    "ppEffectMorphByChar": 3956,
    # animation triggers / types
    "msoAnimTriggerWithPrevious": 2,
    "msoAnimTriggerAfterPrevious": 3,
    "msoAnimTriggerOnPageClick": 1,
    "msoAnimTypeMotion": 1,
    "msoAnimTypeColor": 2,
    "msoAnimTypeScale": 3,
    "msoAnimTypeRotation": 4,
    "msoAnimTypeProperty": 5,
    "msoAnimTypeSet": 8,
    # animation effects
    "msoAnimEffectCustom": 0,
    "msoAnimEffectAppear": 1,
    "msoAnimEffectFade": 10,
    "msoAnimEffectWipe": 22,
    "msoAnimEffectZoom": 23,
    "msoAnimEffectRiseUp": 34,
    "msoAnimEffectFadedZoom": 48,
    "msoAnimEffectGrowShrink": 59,
    "msoAnimEffectSpin": 61,
    "msoAnimEffectTransparency": 62,
    "msoAnimEffectMediaPlay": 83,
    "msoAnimEffectPathDown": 127,
    # directions
    "msoAnimDirectionRight": 2,
    "msoAnimDirectionUp": 1,
    # shapes / text
    "msoShapeRectangle": 1,
    "msoShapeOval": 9,
    "msoShapeDonut": 18,
    "msoTextOrientationHorizontal": 1,
    # export
    "ppFixedFormatTypePDF": 2,
    "ppSlideShowUseSlideTimings": 2,
}

_HINT = ("not found in the live PowerPoint type library or the verified table; "
         "run `python probe_com.py` and add it to VERIFIED in deck/comconst.py")


class Consts:
    """Attribute access over a merged {live typelib} ∪ {verified fallback} map."""

    def __init__(self, live: dict[str, int] | None = None) -> None:
        merged = dict(VERIFIED)
        merged.update(live or {})
        self._values = merged

    def get(self, name: str, default: int | None = None) -> int | None:
        return self._values.get(name, default)

    def __getattr__(self, name: str) -> int:
        try:
            return self._values[name]
        except KeyError:
            raise AttributeError(f"COM constant {name!r} {_HINT}") from None


def load_constants(app=None) -> Consts:
    """Read the live type library if pywin32 is importable; fall back silently."""
    live: dict[str, int] = {}
    try:
        import win32com.client as w32  # noqa: PLC0415 - optional at import time

        for mod in w32.constants.__dicts__:
            for key, val in mod.items():
                if isinstance(val, int):
                    live[key] = val
    except Exception:  # noqa: BLE001 - pure-python callers have no Office
        pass
    return Consts(live)
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_comconst.py -q`
Expected: `4 passed`

**Accept:** `python -m pytest tests/test_comconst.py -q` passes, and `python -c "from deck.comconst import load_constants as L; print(L().ppEffectMorphByObject)"` prints `3954`.

---

### Task 3: `timing.json` + `brief.json` — the 26-slide twin map

**Files:**
- Modify: `demos/signal-station-pptx/timing.json` (full rewrite of the structure)
- Create: `demos/signal-station-pptx/brief.json`
- Create: `demos/signal-station-pptx/tests/test_timing_data.py`

**Interfaces:**
- Produces: `timing.json` keys `title`, `audio_source`, `audio_rights_file`, `audio_duration_sec`, `bpm_est`, `beat_sec`, `transition_additive`, `watch_points`, `beats[]`. Each beat: `b`, `start_sec`, `hold_sec`, `role`, `pattern`, `motion`, `title`, `subtitle`, plus optional `glyphs`, `cards`, `glow`, `wordless`, and `twins[]` where each twin is `{t, hold_sec, trans, trans_dur}` plus optional `carry`. A beat without `twins` is a solo slide and carries `trans` / `trans_dur` directly.
- **No audio path anywhere.** `audio_source` is the literal string `"env:SIGNAL_AUDIO"`; `audio_duration_sec` is the timeline length the 26 `advTm` values describe, which a replacement track must match within ±1.5 s (Task 10 measures it and refuses otherwise).
- `carry: true` marks a Morph into a slide that is **not** a duplicate twin (slides 5 and 25). `wordless: true` marks the one plate that deliberately carries no readable type (slide 8) and is the only slide S13 waives.

- [ ] **Step 1: Write the failing data test**

`demos/signal-station-pptx/tests/test_timing_data.py`:

```python
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))

SIGNATURE = {"door_ripple", "door_window", "iris"}
MORPH = {"morph_obj", "morph_word", "morph_char"}


def slides():
    out = []
    for beat in TIMING["beats"]:
        twins = beat.get("twins")
        if twins:
            out.extend((beat, t) for t in twins)
        else:
            out.append((beat, {"t": "solo", "hold_sec": beat["hold_sec"],
                               "trans": beat["trans"], "trans_dur": beat["trans_dur"]}))
    return out


def test_sixteen_beats_and_twenty_six_slides():
    assert len(TIMING["beats"]) == 16
    assert len(slides()) == 26


def test_twin_holds_sum_to_the_beat_hold():
    for beat in TIMING["beats"]:
        twins = beat.get("twins")
        if twins:
            assert round(sum(t["hold_sec"] for t in twins), 3) == beat["hold_sec"], beat["b"]


def test_beat_starts_are_the_running_sum_of_holds():
    clock = 0.0
    for beat in TIMING["beats"]:
        assert beat["start_sec"] == clock, beat["b"]
        clock = round(clock + beat["hold_sec"], 3)
    assert clock == TIMING["audio_duration_sec"]


def test_every_hold_clears_its_own_transition_by_at_least_1_5s():
    for beat, twin in slides():
        assert twin["hold_sec"] >= twin["trans_dur"] + 1.5, (beat["b"], twin["t"])


def test_morph_and_signature_transition_budget():
    kinds = [t["trans"] for _, t in slides()]
    assert sum(k in MORPH for k in kinds) == 12
    assert sum(k == "morph_obj" for k in kinds) == 11
    assert sorted(k for k in kinds if k in SIGNATURE) == ["door_ripple", "door_window", "iris"]
    # the arithmetic has to close: 1 cut + 12 morph + 3 signature + 10 fade = 26
    assert kinds.count("fade") == 10
    assert kinds.count("cut") == 1
    assert len(kinds) == 26


def test_every_morph_is_either_a_b_twin_or_a_declared_carry():
    # a Morph into a slide that is neither a duplicate twin nor declared as a
    # carry has no pairing guarantee and would read as an unexplained crossfade
    for beat, twin in slides():
        if twin["trans"] not in MORPH:
            continue
        assert twin["t"] == "b" or twin.get("carry") is True, (beat["b"], twin["t"])


def test_the_two_carry_slides_are_the_declared_ones():
    rows = slides()
    carried = [i + 1 for i, (_, t) in enumerate(rows) if t.get("carry")]
    assert carried == [5, 25]


def test_a_carry_slide_never_also_claims_to_be_a_twin_b():
    for _, twin in slides():
        assert not (twin.get("carry") and twin["t"] == "b")


def test_exactly_one_beat_is_declared_wordless_and_it_has_no_title():
    wordless = [b for b in TIMING["beats"] if b.get("wordless")]
    assert [b["b"] for b in wordless] == [5]       # beat 5 == slide 8
    assert wordless[0]["title"] == ""


def test_every_concept_beat_declares_a_glow_so_the_density_table_has_no_exception():
    for beat in TIMING["beats"]:
        if beat["role"] == "concept":
            assert beat.get("glow") is True, beat["b"]


def test_timing_json_names_no_audio_file():
    # 0.0 item 5: the repo must not configure a reference track anywhere
    assert TIMING["audio_source"] == "env:SIGNAL_AUDIO"
    assert TIMING["audio_rights_file"] == "audio.rights.json"
    blob = json.dumps(TIMING, ensure_ascii=False).lower()
    for marker in (".mp3", ".wav", ".m4a", ":\\", ":/"):
        assert marker not in blob, marker


def test_no_two_adjacent_signature_transitions():
    kinds = [t["trans"] for _, t in slides()]
    assert not any(a in SIGNATURE and b in SIGNATURE for a, b in zip(kinds, kinds[1:]))


def test_watch_points_name_the_right_slides():
    assert TIMING["watch_points"]["chapter_lift"] == {"slide": 9, "at_sec": 60.0}
    assert TIMING["watch_points"]["spike"] == {"slide": 17, "at_sec": 124.0}


def test_slide_nine_is_the_chapter_door_and_slide_seventeen_is_the_iris():
    rows = slides()
    beat9, twin9 = rows[8]
    beat17, twin17 = rows[16]
    assert (beat9["role"], twin9["trans"]) == ("chapter", "door_ripple")
    assert (beat17["role"], twin17["trans"]) == ("spike", "iris")


def test_copy_carries_no_reference_branding():
    blob = json.dumps(TIMING, ensure_ascii=False).lower()
    for banned in ("outer wilds", "outerwilds", "nomai", "hearthian", "mobius", "timber hearth"):
        assert banned not in blob
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_timing_data.py -q`
Expected: FAIL — `KeyError: 'beats'` (the current file still has a flat `slides` array)

- [ ] **Step 3: Write the new `timing.json`**

```json
{
  "title": "信号站 SIGNAL",
  "audio_source": "env:SIGNAL_AUDIO",
  "audio_rights_file": "audio.rights.json",
  "audio_duration_sec": 229.5,
  "bpm_est": 91,
  "beat_sec": 0.66,
  "transition_additive": true,
  "watch_points": {
    "chapter_lift": {"slide": 9, "at_sec": 60.0},
    "spike": {"slide": 17, "at_sec": 124.0}
  },
  "beats": [
    {"b": 1, "start_sec": 0.0, "hold_sec": 8.0, "role": "cold_open", "pattern": "Cold Grain", "motion": "kinetic",
     "title": "信号站", "subtitle": "SIGNAL · 夜航校准",
     "twins": [{"t": "a", "hold_sec": 4.0, "trans": "cut", "trans_dur": 0.0},
               {"t": "b", "hold_sec": 4.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 2, "start_sec": 8.0, "hold_sec": 16.0, "role": "title", "pattern": "Title Fracture", "motion": "kinetic",
     "title": "信号站", "subtitle": "", "glyphs": ["信", "号", "站"],
     "twins": [{"t": "a", "hold_sec": 8.0, "trans": "fade", "trans_dur": 1.0},
               {"t": "b", "hold_sec": 8.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 3, "start_sec": 24.0, "hold_sec": 16.0, "role": "title_lock", "pattern": "Title Lock", "motion": "kinetic",
     "title": "信号站", "subtitle": "SIGNAL", "glow": true,
     "twins": [{"t": "a", "hold_sec": 8.0, "trans": "morph_word", "trans_dur": 1.6, "carry": true},
               {"t": "b", "hold_sec": 8.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 4, "start_sec": 40.0, "hold_sec": 12.0, "role": "thesis", "pattern": "Thesis Whisper", "motion": "soft",
     "title": "把噪声收成航线。", "subtitle": "THESIS", "trans": "fade", "trans_dur": 1.2},
    {"b": 5, "start_sec": 52.0, "hold_sec": 8.0, "role": "breath", "pattern": "Breath", "motion": "soft",
     "title": "", "subtitle": "LISTEN", "wordless": true, "trans": "fade", "trans_dur": 1.2},
    {"b": 6, "start_sec": 60.0, "hold_sec": 12.0, "role": "chapter", "pattern": "Chapter Hammer", "motion": "kinetic",
     "title": "校准", "subtitle": "01  CALIBRATE",
     "twins": [{"t": "a", "hold_sec": 6.0, "trans": "door_ripple", "trans_dur": 1.4},
               {"t": "b", "hold_sec": 6.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 7, "start_sec": 72.0, "hold_sec": 12.0, "role": "concept", "pattern": "Metaphor Field", "motion": "soft",
     "title": "远灯", "subtitle": "一盏暖核，落在深空里", "glow": true,
     "twins": [{"t": "a", "hold_sec": 6.0, "trans": "fade", "trans_dur": 1.0},
               {"t": "b", "hold_sec": 6.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 8, "start_sec": 84.0, "hold_sec": 12.0, "role": "concept", "pattern": "Metaphor Field", "motion": "soft",
     "title": "航迹", "subtitle": "把离散脉冲连成可跟的线", "glow": true, "trans": "fade", "trans_dur": 1.0},
    {"b": 9, "start_sec": 96.0, "hold_sec": 8.0, "role": "chapter", "pattern": "Chapter Hammer", "motion": "kinetic",
     "title": "倾听", "subtitle": "02  LISTEN", "trans": "door_window", "trans_dur": 1.4},
    {"b": 10, "start_sec": 104.0, "hold_sec": 20.0, "role": "concept", "pattern": "Double Lock", "motion": "soft",
     "title": "叠层", "subtitle": "同一夜空，两层可读信号", "glow": true,
     "twins": [{"t": "a", "hold_sec": 10.0, "trans": "fade", "trans_dur": 1.0},
               {"t": "b", "hold_sec": 10.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 11, "start_sec": 124.0, "hold_sec": 12.0, "role": "spike", "pattern": "Iris Spike", "motion": "spike",
     "title": "锁定", "subtitle": "IRIS",
     "twins": [{"t": "a", "hold_sec": 6.0, "trans": "iris", "trans_dur": 1.2},
               {"t": "b", "hold_sec": 6.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 12, "start_sec": 136.0, "hold_sec": 16.0, "role": "afterglow", "pattern": "Afterglow", "motion": "soft",
     "title": "余晖", "subtitle": "高潮之后，只留航向", "glow": true,
     "twins": [{"t": "a", "hold_sec": 8.0, "trans": "fade", "trans_dur": 1.6},
               {"t": "b", "hold_sec": 8.0, "trans": "morph_obj", "trans_dur": 2.0}]},
    {"b": 13, "start_sec": 152.0, "hold_sec": 16.0, "role": "proof", "pattern": "Proof Trio", "motion": "soft",
     "title": "三频", "subtitle": "PROOF", "cards": ["收束噪声", "标定航向", "保持倾听"],
     "twins": [{"t": "a", "hold_sec": 8.0, "trans": "fade", "trans_dur": 1.0},
               {"t": "b", "hold_sec": 8.0, "trans": "morph_obj", "trans_dur": 1.6}]},
    {"b": 14, "start_sec": 168.0, "hold_sec": 12.0, "role": "settle", "pattern": "Settle", "motion": "soft",
     "title": "回落", "subtitle": "声音变薄，星点仍在", "trans": "fade", "trans_dur": 1.2},
    {"b": 15, "start_sec": 180.0, "hold_sec": 16.0, "role": "settle", "pattern": "Settle", "motion": "soft",
     "title": "疏句", "subtitle": "不必更响，只要更准", "trans": "fade", "trans_dur": 1.6},
    {"b": 16, "start_sec": 196.0, "hold_sec": 33.5, "role": "end", "pattern": "End Lock", "motion": "soft",
     "title": "信号站", "subtitle": "DEMO · END LOCK", "glow": true,
     "twins": [{"t": "a", "hold_sec": 16.0, "trans": "morph_obj", "trans_dur": 2.0, "carry": true},
               {"t": "b", "hold_sec": 17.5, "trans": "morph_obj", "trans_dur": 2.5}]}
  ]
}
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_timing_data.py -q`
Expected: `16 passed`

- [ ] **Step 5: Write `brief.json`** (guide §Step 0 requires it before visuals; Task 18 quotes it)

```json
{
  "topic": "信号站 SIGNAL — 夜航星空下的信号校准（自拟题材能力预览）",
  "audience": "PPT 能力评审者；本机放映",
  "duration_sec": 229.5,
  "mode": "A",
  "status": "final",
  "viewing": "projected",
  "promise": "在真实 .pptx 约束内，星空短片感与跟轨自动换片可以同时成立。",
  "must_include": [
    "Morph 星场视差漂移", "流星雨（运动路径粒子）", "碎字 Morph 拼合",
    "两道章节门 + 唯一一次 iris", "Proof Trio 冲击", "跨幻灯片单一音床"
  ],
  "must_exclude": [
    "character_cards", "lore_or_world_rules", "identifiable_key_art",
    "reference_copy", "reference_audio", "reference_binaries",
    "reference_identity_system", "reference_file_as_base",
    "任何参考游戏的名称/术语/造型/封面构图"
  ],
  "delivery": "pptx",
  "advance": "auto",
  "advance_notes": "每页 advTm 来自 timing.json 的 hold_sec，减去该页自身的转场时长（transition_additive=true 时）；Esc 停止放映，点击不前进以保护编排。",
  "audio": {
    "default": "silent",
    "default_note": "默认构建不读取、不复制、不嵌入任何音频；仓库任何位置都不写音轨文件名或路径。",
    "opt_in": "构建时加 --audio，且同时满足：环境变量 SIGNAL_AUDIO 指向本机文件 + audio.rights.json 的 rights != none。",
    "rights_values": ["none", "personal_preview", "licensed"],
    "gate": "verify_pptx.py 的 RIGHTS 行：只要存在媒体部件且 rights != licensed 即 FAIL（退出码 1）。personal_preview 永远不会被报成通过。",
    "timeline_note": "26 页 advTm 描述一条 229.5s 的开环时间线；替换音轨若时长偏差 > ±1.5s，mount() 直接报错而不是静默失配。"
  },
  "source_packet": {
    "provided": true,
    "items": [
      {"id": "S1", "what": "本机 PowerPoint 16 COM 能力探测结果 COM-NOTES.md", "from": "own_notes", "date": "2026-10"},
      {"id": "S2", "what": "本机 advTm/转场叠加模型实测 _calibration.json", "from": "own_notes", "date": "2026-10"}
    ],
    "gaps": [
      "无授权可再分发的音轨（D6：默认静音交付；任何参考作品音轨只能声明为 personal_preview，且因此不可对外交付）"
    ]
  },
  "claims": [],
  "claims_note": "本片为纯氛围能力预览，幻灯片不承载任何事实性断言（数字/日期/机构/奖项/引语），故 §7 Truth 组声明为 N/A。verify_pptx.py 的禁用词与 §7.1 row 9 逐页来源行用于验证该声明。"
}
```

`must_exclude` keeps `reference_audio`, and that is now a true statement rather than a contradiction: the default build embeds nothing and no file in the repo names a track. The `audio` block above documents the opt-in and its gate, so the brief configures a *mechanism*, never a specific protected work.

**Accept:** both JSON files parse, `python -m pytest tests/test_timing_data.py -q` reports 16 passed, `python -c "import json;d=json.load(open('timing.json',encoding='utf-8'));print(sum(b['hold_sec'] for b in d['beats']))"` prints `229.5`, and `Select-String -Pattern "\.mp3|KuGou|Travelers" -Path timing.json,brief.json` returns nothing.

---

### Task 4: `deck/palette.py` — the only place a colour, a type size or a safe zone is written down

Pure python, no COM. Every later task reads tokens from here; a hex literal appearing anywhere in `deck/stage.py` or `deck/scenes/` in Task 15's review is a defect.

Two traps this module exists to close:
1. **PowerPoint's `.RGB` is BGR-packed.** `0x070B14` set on a shape is *not* `#070B14`. `rgb()` does the byte swap once, here.
2. **The 12.5–18 pt dead band** (§Type floors). `TYPE` is the whole type scale and a test asserts nothing lands in the band, so no scene can invent an off-scale size.

**Files:**
- Verify (created in Task 2): `demos/signal-station-pptx/deck/__init__.py` — stays empty
- Create: `demos/signal-station-pptx/deck/palette.py`
- Create: `demos/signal-station-pptx/tests/test_palette.py`

**Interfaces:**
- Produces: `rgb(hex_str) -> int` (BGR int for COM), `COLOR: dict[str,str]`, `C: dict[str,int]` (pre-packed), `TYPE: dict[str, TypeSpec]` where `TypeSpec = (size_pt, font_name, color_key, tracking_pt)`, `ZONES: dict[str, tuple[float,float,float,float]]` (type safe zone per slide role, as `left, top, width, height` in points), `CANVAS_W = 960.0`, `CANVAS_H = 540.0`, `READ_FLOOR = 18.0`, `META_CEIL = 12.0`.
- Consumed by: `deck/geometry.py` (Task 6 turns a `ZONES` tuple into a `Rect`), `deck/stage.py` (Task 7), every scene module (Tasks 11–12), `verify_pptx.py` (Task 16 re-derives the dead band from `READ_FLOOR` / `META_CEIL`).

- [ ] **Step 1: Write the failing test**

`demos/signal-station-pptx/tests/test_palette.py`:

```python
import pytest

from deck.palette import (
    C, CANVAS_H, CANVAS_W, COLOR, META_CEIL, READ_FLOOR, TYPE, ZONES, rgb,
)


def test_rgb_packs_bgr_for_com_not_rgb():
    # #E8B45A -> COM wants 0x5AB4E8
    assert rgb("#E8B45A") == 0x5AB4E8
    assert rgb("070B14") == 0x140B07
    assert rgb("#FFFFFF") == 0xFFFFFF
    assert rgb("#000000") == 0x000000


def test_rgb_rejects_anything_that_is_not_six_hex_digits():
    for bad in ("#FFF", "#GGGGGG", "E8B45A7", ""):
        with pytest.raises(ValueError):
            rgb(bad)


def test_every_named_colour_is_pre_packed_once():
    assert set(C) == set(COLOR)
    for key, hex_str in COLOR.items():
        assert C[key] == rgb(hex_str), key


def test_the_topic_tokens_are_the_ones_the_plan_declared():
    assert COLOR["field"] == "#070B14"
    assert COLOR["lift"] == "#0C1628"
    assert COLOR["ink"] == "#F2F4F7"
    assert COLOR["ghost"] == "#7EB0C8"
    assert COLOR["amber"] == "#E8B45A"
    assert COLOR["amber_hot"] == "#FFD68C"
    assert COLOR["dim"] == "#8C96A5"


def test_amber_is_the_only_strong_accent():
    accents = [k for k in COLOR if k.startswith("amber")]
    assert sorted(accents) == ["amber", "amber_hot"]


def test_no_type_size_lands_in_the_dead_band():
    for name, spec in TYPE.items():
        assert not (META_CEIL < spec.size < READ_FLOOR), (name, spec.size)


def test_readable_roles_are_at_or_above_the_floor_and_meta_at_or_below_the_ceiling():
    for name, spec in TYPE.items():
        if name.startswith("meta_"):
            assert spec.size <= META_CEIL, name
        else:
            assert spec.size >= READ_FLOOR, name


def test_every_type_spec_points_at_a_real_colour():
    for name, spec in TYPE.items():
        assert spec.color in COLOR, (name, spec.color)


def test_tracked_latin_ghost_clears_the_four_point_tracking_bar():
    # S12 wants spc >= 400 (hundredths of a point) == 4.0 pt
    assert TYPE["ghost_latin"].tracking >= 4.0
    assert TYPE["meta_signal"].tracking >= 4.0


def test_every_zone_is_inside_the_canvas_and_has_real_area():
    for role, (left, top, width, height) in ZONES.items():
        assert width > 0 and height > 0, role
        assert 0 <= left and 0 <= top, role
        assert left + width <= CANVAS_W, role
        assert top + height <= CANVAS_H, role


def test_there_is_a_zone_for_every_slide_role_in_the_effect_vocabulary():
    assert set(ZONES) == {
        "cold_open", "title", "title_lock", "thesis", "breath", "chapter",
        "concept", "spike", "afterglow", "proof", "settle", "end",
    }


def test_the_wordless_plate_has_the_smallest_zone():
    areas = {r: w * h for r, (_, _, w, h) in ZONES.items()}
    assert min(areas, key=areas.get) == "breath"
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_palette.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.palette'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/palette.py`:

```python
"""Visual tokens: colour, type scale, safe zones. Pure python, no COM.

Nothing downstream may write a hex literal or a point size of its own. Two
invariants are enforced by tests/test_palette.py:
  * COM takes BGR ints, so every colour goes through rgb() exactly once;
  * no type size may land in the 12.5-18 pt dead band.
"""
from __future__ import annotations

import re
from typing import NamedTuple

CANVAS_W = 960.0
CANVAS_H = 540.0

READ_FLOOR = 18.0   # anything meant to be read
META_CEIL = 12.0    # meta marks: index, tracked ghost latin

_HEX = re.compile(r"^#?[0-9A-Fa-f]{6}$")


def rgb(hex_str: str) -> int:
    """'#E8B45A' -> 0x5AB4E8. PowerPoint's .RGB property is BGR-packed."""
    if not _HEX.match(hex_str or ""):
        raise ValueError(f"expected 6 hex digits, got {hex_str!r}")
    h = hex_str.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    return (b << 16) | (g << 8) | r


# Topic-derived: a night signal station. Amber is the only strong accent.
COLOR: dict[str, str] = {
    "field": "#070B14",      # deepest ground
    "lift": "#0C1628",       # second field layer, barely lighter
    "ink": "#F2F4F7",        # readable type
    "ghost": "#7EB0C8",      # cool ghost type, far stars
    "amber": "#E8B45A",      # signal accent: rules, dots, glow cores
    "amber_hot": "#FFD68C",  # the hottest 1 % (iris core, hero dot)
    "dim": "#8C96A5",        # meta marks, captions
}

C: dict[str, int] = {k: rgb(v) for k, v in COLOR.items()}


class TypeSpec(NamedTuple):
    size: float
    font: str
    color: str      # key into COLOR
    tracking: float  # points; TextFrame2.Font.Spacing


_CJK = "Microsoft YaHei"
_LATIN = "Bahnschrift SemiLight"  # falls back to Segoe UI if absent; see README

TYPE: dict[str, TypeSpec] = {
    # readable (>= READ_FLOOR)
    "hero": TypeSpec(96.0, _CJK, "ink", 8.0),
    "hero_glyph": TypeSpec(128.0, _CJK, "ink", 0.0),
    "chapter_word": TypeSpec(110.0, _CJK, "ink", 12.0),
    "title": TypeSpec(54.0, _CJK, "ink", 4.0),
    "subtitle": TypeSpec(26.0, _CJK, "dim", 2.0),
    "sentence": TypeSpec(40.0, _CJK, "ink", 2.0),
    "body": TypeSpec(22.0, _CJK, "dim", 0.0),
    "card_label": TypeSpec(24.0, _CJK, "ink", 1.0),
    "proof_numeral": TypeSpec(180.0, _LATIN, "lift", 0.0),  # big ghost numerals
    "spike_word": TypeSpec(140.0, _CJK, "field", 10.0),     # inverted dark type
    "ghost_latin": TypeSpec(32.0, _LATIN, "ghost", 10.0),
    "end_title": TypeSpec(72.0, _CJK, "ink", 6.0),
    # meta (<= META_CEIL)
    "meta_index": TypeSpec(10.0, _LATIN, "dim", 2.0),
    "meta_signal": TypeSpec(11.0, _LATIN, "ghost", 6.0),
    "meta_mark": TypeSpec(12.0, _LATIN, "dim", 4.0),
}

# Type safe zone per slide role: left, top, width, height (points).
# A meteor path must clear its slide's zone -- geometry.path_clears_zone, Task 6.
ZONES: dict[str, tuple[float, float, float, float]] = {
    "cold_open": (96.0, 300.0, 600.0, 170.0),
    "title":     (160.0, 170.0, 640.0, 230.0),
    "title_lock": (160.0, 190.0, 640.0, 200.0),
    "thesis":    (120.0, 210.0, 720.0, 150.0),
    "breath":    (400.0, 470.0, 160.0, 40.0),   # meta mark only (wordless plate)
    "chapter":   (110.0, 160.0, 700.0, 250.0),
    "concept":   (110.0, 180.0, 560.0, 220.0),
    "spike":     (180.0, 180.0, 600.0, 200.0),
    "afterglow": (120.0, 220.0, 620.0, 160.0),
    "proof":     (80.0, 190.0, 800.0, 230.0),
    "settle":    (140.0, 230.0, 560.0, 130.0),
    "end":       (160.0, 200.0, 640.0, 190.0),
}
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_palette.py -q`
Expected: `12 passed`

- [ ] **Step 5: Confirm the pack order against PowerPoint once** (cheap, catches a swapped channel before 2,600 shapes are painted)

Run: `Set-Location demos/signal-station-pptx; python -c "from deck.palette import C;print(hex(C['amber']), hex(C['field']))"`
Expected: `0x5ab4e8 0x140b07`

**Accept:** `python -m pytest tests/test_comconst.py tests/test_palette.py -q` passes (16 tests), `deck/palette.py` contains no point size in 12.5–18, and `Select-String -Pattern "0x|#[0-9A-Fa-f]{6}" -Path deck/palette.py` shows hex only inside `COLOR` / `rgb`.

---

### Task 5: `calibrate_timing.py` + `deck/sync.py` — settle the 39.5 s question by measuring it

From §"Transition time is not free": the 26 transitions total 39.5 s. If PowerPoint starts a slide's `advTm` countdown only *after* its transition has finished, then setting `AdvanceTime = hold_sec` makes the deck run 39.5 s long and both watch points (S16) miss by a mile. If `advTm` is measured from the moment the transition *starts*, `AdvanceTime = hold_sec` is correct.

This is not guessable and the two models differ by 17 % of the runtime. `calibrate_timing.py` builds a scratch deck with absurdly long transitions and times the real slideshow; `deck/sync.py` implements **both** models and takes the answer from `timing.json`'s `transition_additive` flag, so the whole deck re-times by flipping one boolean.

**Files:**
- Create: `demos/signal-station-pptx/calibrate_timing.py`
- Create: `demos/signal-station-pptx/deck/sync.py`
- Create: `demos/signal-station-pptx/tests/test_sync.py`

**Interfaces:**
- Produces: `SlideSpec` (frozen dataclass: `idx`, `beat`, `twin`, `role`, `pattern`, `motion`, `hold`, `trans`, `trans_dur`, `beat_start`, `slide_start`, `is_b`, `data`), `expand(timing) -> list[SlideSpec]`, `adv_time(spec, additive) -> float`, `deck_runtime(specs, additive) -> float`, `validate(specs, timing, additive) -> list[str]`, `ledger(specs, additive) -> str`, `stagger(n, step, start=0.0) -> list[float]` re-exported from `geometry` is **not** done here (Task 6 owns stagger).
- Consumes: `timing.json` (Task 3), `transition_additive`.
- Consumed by: `build_deck.py` (Task 13 sets `AdvanceTime` from `adv_time`), `transitions.py` (Task 9), `verify_pptx.py` (Task 16 recomputes `advTm` independently and compares).
- `validate()` is the enforcement point for S14 (`advTm ≥ readable_end + 0.5`) at the data level; the per-slide runtime half of S14 is asserted in `motion.py` (Task 8).

- [ ] **Step 1: Write the failing test**

`demos/signal-station-pptx/tests/test_sync.py`:

```python
import json
from pathlib import Path

import pytest

from deck.sync import SlideSpec, adv_time, deck_runtime, expand, ledger, validate

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))


def test_expand_produces_twenty_six_slides_numbered_from_one():
    specs = expand(TIMING)
    assert len(specs) == 26
    assert [s.idx for s in specs] == list(range(1, 27))


def test_twin_b_slides_are_flagged_and_solo_slides_are_not():
    specs = expand(TIMING)
    assert [s.idx for s in specs if s.is_b] == [2, 4, 6, 10, 12, 16, 18, 20, 22, 26]
    assert len([s for s in specs if s.twin == "solo"]) == 6


def test_slide_starts_are_the_running_sum_of_holds():
    specs = expand(TIMING)
    clock = 0.0
    for spec in specs:
        assert spec.slide_start == pytest.approx(clock, abs=1e-6)
        clock = round(clock + spec.hold, 3)
    assert clock == pytest.approx(229.5)


def test_twin_a_starts_at_its_beat_start():
    for spec in expand(TIMING):
        if spec.twin in ("a", "solo"):
            assert spec.slide_start == pytest.approx(spec.beat_start)


def test_additive_model_subtracts_the_transition_from_the_hold():
    spec = expand(TIMING)[1]  # slide 2: hold 4.0, morph_obj 2.0
    assert spec.hold == 4.0 and spec.trans_dur == 2.0
    assert adv_time(spec, additive=True) == pytest.approx(2.0)
    assert adv_time(spec, additive=False) == pytest.approx(4.0)


def test_non_additive_model_is_the_hold_untouched():
    specs = expand(TIMING)
    assert deck_runtime(specs, additive=False) == pytest.approx(229.5)


def test_additive_model_also_lands_on_the_track_length():
    # the whole point: under either model the deck must still end at 229.5 s
    specs = expand(TIMING)
    assert deck_runtime(specs, additive=True) == pytest.approx(229.5)


def test_adv_time_never_goes_below_the_floor():
    spec = SlideSpec(idx=1, beat=1, twin="a", role="cold_open", pattern="x",
                     motion="soft", hold=1.0, trans="morph_obj", trans_dur=2.5,
                     beat_start=0.0, slide_start=0.0, is_b=False, data={})
    assert adv_time(spec, additive=True) == pytest.approx(0.1)


def test_watch_points_land_where_timing_json_promises():
    specs = expand(TIMING)
    by_idx = {s.idx: s for s in specs}
    for name, wp in TIMING["watch_points"].items():
        assert by_idx[wp["slide"]].slide_start == pytest.approx(wp["at_sec"]), name


def test_validate_is_silent_on_the_real_timing_file():
    specs = expand(TIMING)
    assert validate(specs, TIMING, additive=TIMING["transition_additive"]) == []


def test_validate_catches_a_hold_that_cannot_clear_its_morph():
    broken = json.loads(json.dumps(TIMING))
    broken["beats"][0]["twins"][1]["hold_sec"] = 1.0
    broken["beats"][0]["twins"][0]["hold_sec"] = 7.0
    problems = validate(expand(broken), broken, additive=True)
    assert any("clear its transition" in p for p in problems)


def test_validate_catches_twin_holds_that_do_not_sum():
    broken = json.loads(json.dumps(TIMING))
    broken["beats"][0]["twins"][0]["hold_sec"] = 5.0
    problems = validate(expand(broken), broken, additive=True)
    assert any("twin holds" in p for p in problems)


def test_validate_catches_two_adjacent_signature_transitions():
    broken = json.loads(json.dumps(TIMING))
    broken["beats"][5]["twins"][1]["trans"] = "iris"
    problems = validate(expand(broken), broken, additive=True)
    assert any("adjacent signature" in p for p in problems)


def test_validate_catches_a_drifted_watch_point():
    broken = json.loads(json.dumps(TIMING))
    broken["watch_points"]["spike"]["at_sec"] = 130.0
    problems = validate(expand(broken), broken, additive=True)
    assert any("watch point" in p for p in problems)


def test_expand_marks_the_two_carry_slides_and_nothing_else():
    carried = [s.idx for s in expand(TIMING) if s.carry]
    assert carried == [5, 25]


def test_every_morph_slide_either_is_a_b_twin_or_inherits_by_carry():
    for spec in expand(TIMING):
        if spec.is_morph:
            assert spec.inherits_field, spec.idx


def test_validate_catches_a_morph_with_nothing_to_pair_against():
    # drop the carry declaration from slide 5 and the law must fire
    broken = json.loads(json.dumps(TIMING))
    del broken["beats"][2]["twins"][0]["carry"]
    problems = validate(expand(broken), broken, additive=True)
    assert any("neither a duplicate twin nor declared" in p for p in problems)


def test_only_the_breath_beat_is_wordless():
    assert [s.idx for s in expand(TIMING) if s.wordless] == [8]


def test_ledger_is_one_line_per_slide_plus_a_header_and_a_total():
    text = ledger(expand(TIMING), additive=True)
    lines = text.strip().splitlines()
    assert len(lines) == 26 + 2
    assert "229.5" in lines[-1]
    assert "slide" in lines[0]
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_sync.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.sync'`

- [ ] **Step 3: Implement `deck/sync.py`**

```python
"""Beat -> slide expansion and auto-advance math. Pure python, no COM.

Two models of how PowerPoint counts a slide's auto-advance time:
  additive=True   advTm starts when the transition ENDS -> advTm = hold - trans
  additive=False  advTm starts when the transition BEGINS -> advTm = hold
calibrate_timing.py measures which one is real on this machine; timing.json
carries the answer in `transition_additive`. Everything else reads it from here.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

MORPH = {"morph_obj", "morph_word", "morph_char"}
SIGNATURE = {"door_ripple", "door_window", "iris"}
ADV_FLOOR = 0.1          # PowerPoint rejects 0; keep a visible minimum
MORPH_CLEARANCE = 1.5    # a B twin must hold its Morph plus this (plan: twin law)
READABLE_SLACK = 0.5     # S14 / guide 10.1 law 8


@dataclass(frozen=True)
class SlideSpec:
    idx: int
    beat: int
    twin: str            # "a" | "b" | "solo"
    role: str
    pattern: str
    motion: str          # "kinetic" | "soft" | "spike"
    hold: float
    trans: str
    trans_dur: float
    beat_start: float
    slide_start: float
    is_b: bool
    data: dict[str, Any] = field(default_factory=dict)
    carry: bool = False  # Morph into a non-duplicate slide (slides 5, 25)

    @property
    def is_morph(self) -> bool:
        return self.trans in MORPH

    @property
    def wordless(self) -> bool:
        """S13 waives the >=18pt rule only for a beat that declares this."""
        return bool(self.data.get("wordless"))

    @property
    def inherits_field(self) -> bool:
        """True when the star layer comes from the previous slide rather than
        from starfield(): a duplicate twin or a declared carry."""
        return self.is_b or self.carry

    @property
    def zone_role(self) -> str:
        return self.role


def expand(timing: dict) -> list[SlideSpec]:
    """16 beats -> 26 SlideSpecs, in show order, with absolute slide starts."""
    specs: list[SlideSpec] = []
    clock = 0.0
    idx = 0
    for beat in timing["beats"]:
        twins = beat.get("twins") or [{
            "t": "solo", "hold_sec": beat["hold_sec"],
            "trans": beat["trans"], "trans_dur": beat["trans_dur"],
        }]
        for twin in twins:
            idx += 1
            specs.append(SlideSpec(
                idx=idx,
                beat=beat["b"],
                twin=twin["t"],
                role=beat["role"],
                pattern=beat["pattern"],
                motion=beat["motion"],
                hold=float(twin["hold_sec"]),
                trans=twin["trans"],
                trans_dur=float(twin["trans_dur"]),
                beat_start=float(beat["start_sec"]),
                slide_start=round(clock, 3),
                is_b=(twin["t"] == "b"),
                carry=bool(twin.get("carry")),
                data={k: v for k, v in beat.items()
                      if k not in ("twins", "trans", "trans_dur")},
            ))
            clock = round(clock + float(twin["hold_sec"]), 3)
    return specs


def adv_time(spec: SlideSpec, additive: bool) -> float:
    """What to write into SlideShowTransition.AdvanceTime for this slide."""
    value = spec.hold - spec.trans_dur if additive else spec.hold
    return round(max(value, ADV_FLOOR), 3)


def deck_runtime(specs: list[SlideSpec], additive: bool) -> float:
    """Wall-clock length of the show under the given model."""
    total = 0.0
    for spec in specs:
        total += adv_time(spec, additive) + (spec.trans_dur if additive else 0.0)
    return round(total, 3)


def validate(specs: list[SlideSpec], timing: dict, additive: bool) -> list[str]:
    """Every law this plan states about the slide map, as checkable strings."""
    problems: list[str] = []

    for beat in timing["beats"]:
        twins = beat.get("twins")
        if twins and round(sum(t["hold_sec"] for t in twins), 3) != beat["hold_sec"]:
            problems.append(
                f"beat {beat['b']}: twin holds do not sum to the beat hold "
                f"({sum(t['hold_sec'] for t in twins)} vs {beat['hold_sec']})")

    for spec in specs:
        need = spec.trans_dur + (MORPH_CLEARANCE if spec.is_morph else READABLE_SLACK)
        if spec.hold < need:
            problems.append(
                f"slide {spec.idx} (beat {spec.beat}{spec.twin}): hold {spec.hold}s "
                f"does not clear its transition ({spec.trans} {spec.trans_dur}s, "
                f"needs >= {need}s)")
        if adv_time(spec, additive) <= ADV_FLOOR:
            problems.append(
                f"slide {spec.idx}: advTm collapsed to the floor under "
                f"additive={additive}; shorten the transition or lengthen the hold")
        if spec.is_morph and not (spec.is_b or spec.carry):
            problems.append(
                f"slide {spec.idx} (beat {spec.beat}{spec.twin}): {spec.trans} "
                f"into a slide that is neither a duplicate twin nor declared "
                f'"carry": true. Morph has nothing to pair against here and '
                f"would read as an unexplained crossfade -- either demote it to "
                f"fade or declare the carry so the field is inherited")
        if spec.carry and spec.is_b:
            problems.append(
                f"slide {spec.idx}: cannot be both a B twin and a carry")

    kinds = [s.trans for s in specs]
    for i, (a, b) in enumerate(zip(kinds, kinds[1:]), start=1):
        if a in SIGNATURE and b in SIGNATURE:
            problems.append(f"slides {i}-{i + 1}: adjacent signature transitions "
                            f"({a}, {b}); D2 forbids it")
    if sum(k in SIGNATURE for k in kinds) != 3:
        problems.append(f"D2 cap: expected exactly 3 signature transitions, "
                        f"found {sum(k in SIGNATURE for k in kinds)}")

    by_idx = {s.idx: s for s in specs}
    for name, wp in timing["watch_points"].items():
        spec = by_idx.get(wp["slide"])
        if spec is None:
            problems.append(f"watch point {name}: no slide {wp['slide']}")
        elif abs(spec.slide_start - wp["at_sec"]) > 1e-6:
            problems.append(f"watch point {name}: slide {wp['slide']} starts at "
                            f"{spec.slide_start}s, timing.json promises {wp['at_sec']}s")

    runtime = deck_runtime(specs, additive)
    if abs(runtime - timing["audio_duration_sec"]) > 0.05:
        problems.append(f"deck runtime {runtime}s != track {timing['audio_duration_sec']}s "
                        f"under additive={additive}")
    return problems


def ledger(specs: list[SlideSpec], additive: bool) -> str:
    """Human-readable sync table; build_deck.py writes it to out/_sync-ledger.txt."""
    head = (f"{'slide':>5} {'beat':>5} {'twin':>5} {'role':<10} {'start':>7} "
            f"{'hold':>6} {'trans':<12} {'dur':>5} {'advTm':>6}")
    rows = [head]
    for s in specs:
        rows.append(f"{s.idx:>5} {s.beat:>5} {s.twin:>5} {s.role:<10} "
                    f"{s.slide_start:>7.1f} {s.hold:>6.1f} {s.trans:<12} "
                    f"{s.trans_dur:>5.1f} {adv_time(s, additive):>6.2f}"
                    + ("  carry" if s.carry else "")
                    + ("  wordless" if s.wordless else ""))
    rows.append(f"total runtime {deck_runtime(specs, additive):.1f}s "
                f"(additive={additive})")
    return "\n".join(rows) + "\n"
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_sync.py -q`
Expected: `19 passed`

- [ ] **Step 5: Write the calibrator**

`demos/signal-station-pptx/calibrate_timing.py`:

```python
#!/usr/bin/env python3
"""Measure whether a slide's advTm starts before or after its transition.

Builds a 6-slide scratch deck: 3 s holds with 3 s Morph transitions. The
measurement is **the interval between SlideIndex changes**, not the total time
until the show closes. Timing the whole show is unusable: the final black slide,
the "end of show" click-to-exit screen and PowerPoint's teardown all land inside
the same window, so the earlier timeout-based version was measuring Office's
shutdown as if it were choreography.

Per-advance deltas are the real signal:
  additive=True  -> each delta ~= hold + trans = 6.0 s
  additive=False -> each delta ~= hold        = 3.0 s
Taking the median of 4 interior deltas also throws away the first advance (which
carries window warm-up) and the last (which runs into the end-of-show screen).

Writes _calibration.json and prints the verdict. It never touches timing.json
for you -- flip `transition_additive` by hand so the change is reviewable.
"""
from __future__ import annotations

import json
import statistics
import time
from pathlib import Path

import win32com.client as w32

from deck.comconst import load_constants

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "_calibration.json"

SLIDES = 6
HOLD = 3.0
TRANS = 3.0
POLL = 0.05        # 50ms: 1.7% of the smaller expected delta
GIVE_UP = 90.0


def main() -> int:
    app = w32.gencache.EnsureDispatch("PowerPoint.Application")
    K = load_constants(app)
    app.DisplayAlerts = K.ppAlertsNone
    prs = app.Presentations.Add(WithWindow=True)
    prs.PageSetup.SlideWidth = 960
    prs.PageSetup.SlideHeight = 540

    for i in range(1, SLIDES + 1):
        slide = prs.Slides.Add(i, K.ppLayoutBlank)
        box = slide.Shapes.AddTextbox(K.msoTextOrientationHorizontal, 60, 220, 840, 120)
        box.Name = "!!probe_mark"
        box.TextFrame2.TextRange.Text = str(i)
        box.TextFrame2.TextRange.Font.Size = 120
        box.Left = 60 + (i - 1) * 180  # give Morph something to interpolate
        t = slide.SlideShowTransition
        t.EntryEffect = K.ppEffectMorphByObject if i > 1 else K.ppEffectCut
        t.Duration = TRANS if i > 1 else 0.0
        t.AdvanceOnClick = False
        t.AdvanceOnTime = True
        t.AdvanceTime = HOLD

    settings = prs.SlideShowSettings
    settings.AdvanceMode = K.ppSlideShowUseSlideTimings
    settings.StartingSlide = 1
    settings.EndingSlide = SLIDES
    settings.LoopUntilStopped = False

    window = settings.Run()

    # Sample SlideIndex and record the wall clock at every change. The show's
    # own end is irrelevant -- we stop as soon as we have seen the last slide.
    marks: list[tuple[int, float]] = []
    seen = None
    start = time.perf_counter()
    while time.perf_counter() - start < GIVE_UP:
        try:
            current = int(window.View.Slide.SlideIndex)
        except Exception:
            break                      # the show tore itself down; we are done
        if current != seen:
            marks.append((current, time.perf_counter()))
            seen = current
            if current >= SLIDES:
                break
        time.sleep(POLL)

    deltas = [round(b[1] - a[1], 3) for a, b in zip(marks, marks[1:])]
    interior = deltas[1:-1] if len(deltas) >= 3 else deltas
    if not interior:
        raise RuntimeError(
            f"saw only {len(marks)} slide changes ({marks}); the show never "
            f"advanced. Check AdvanceMode/AdvanceOnTime before trusting any "
            f"verdict -- do not guess transition_additive")
    measured = round(statistics.median(interior), 3)

    additive_expect = HOLD + TRANS
    flat_expect = HOLD
    additive = abs(measured - additive_expect) < abs(measured - flat_expect)
    margin = abs(abs(measured - additive_expect) - abs(measured - flat_expect))
    ambiguous = margin < 0.8    # the two models are 3.0s apart; 0.8s is generous

    prs.Saved = True
    prs.Close()
    app.Quit()

    result = {
        "slide_changes": [i for i, _ in marks],
        "deltas_sec": deltas,
        "interior_deltas_sec": interior,
        "median_advance_sec": measured,
        "additive_expect_sec": additive_expect,
        "flat_expect_sec": flat_expect,
        "transition_additive": additive,
        "ambiguous": ambiguous,
        "slides": SLIDES, "hold_sec": HOLD, "trans_sec": TRANS,
    }
    OUT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if ambiguous:
        print("\nAMBIGUOUS: the median advance sits between the two models. "
              "Close every other PowerPoint window and re-run. Do not edit "
              "timing.json from an ambiguous run.")
        return 2
    print(f"\nSet \"transition_additive\": {str(additive).lower()} in timing.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 6: Run the calibrator and reconcile `timing.json`**

Run: `Set-Location demos/signal-station-pptx; python calibrate_timing.py`
Expected: exit code 0, `_calibration.json` with `"ambiguous": false`, four `interior_deltas_sec` that agree with each other to within ~0.3 s, and a `median_advance_sec` near 6.0 (additive) or 3.0 (flat). Exit code 2 means the run was ambiguous — close every other PowerPoint window and re-run; **do not** edit `timing.json` from an ambiguous run.

Then:
- If the verdict is `true`, leave `timing.json`'s `transition_additive` at `true`. Under this model the holds in Task 3 already include their transitions, so `advTm = hold - trans_dur` and `deck_runtime` lands on 229.5 s.
- If the verdict is `false`, set `transition_additive` to `false` **and** re-run `tests/test_sync.py`; `test_additive_model_also_lands_on_the_track_length` and `test_non_additive_model_is_the_hold_untouched` both still pass because the holds are the single source of truth either way — only `adv_time` changes.

- [ ] **Step 7: Record the verdict in `COM-NOTES.md`**

Append to the `## Capability results` table in `COM-NOTES.md`:

```markdown
| `advTm vs transition (calibrate_timing.py)` | OK: transition_additive=<true\|false>, median advance <N>s vs additive <A>s / flat <F>s, interior deltas <list>, ambiguous=false |
```

- [ ] **Step 8: Full pure-python suite still green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests -q`
Expected: at least `comconst 4 + palette 12 + sync 19 + timing_data 16 = 51 passed`, 0 failed

**Accept:** `python -m pytest tests -q` reports ≥ 51 passed and 0 failed; `_calibration.json` exists with `"ambiguous": false` and an unambiguous verdict; `python -c "import json;from deck.sync import expand,deck_runtime as R;t=json.load(open('timing.json',encoding='utf-8'));print(R(expand(t), t['transition_additive']))"` prints `229.5`; `COM-NOTES.md` carries the calibration row.

---

### Task 6: `deck/geometry.py` — the starfield, the parallax, and the proof that no meteor crosses the type

The whole spectacle is arithmetic, and all of it is testable without Office. Three things in particular must be *proven* rather than eyeballed, because by Task 15 there are 2,600 shapes and nobody can inspect them by hand:

1. **Determinism.** The same seed must produce the same starfield, or a Morph twin's B slide will not line up with its A slide and S2 collapses into noise. Stdlib `random` is seeded globally and a scene that calls it out of order silently shifts every later star, so this module ships its own LCG with an explicit, per-call state.
2. **Parallax ordering** (S2): near stars (`depth` → 1.0) must displace *more* than far stars (`depth` → 0.0), **every** star must displace by something, and the displacement must be the same for a given star every time it is recomputed. The direction of that inequality is easy to invert in a test — this module's tests sort ascending by `depth` and therefore read the *far* stars first.
3. **Path clearance** (S3 / D5): every meteor path must be geometrically clear of its slide's type safe zone — proven by segment-vs-rect intersection over the whole swept path, not by a start/end point check.

**Files:**
- Create: `demos/signal-station-pptx/deck/geometry.py`
- Create: `demos/signal-station-pptx/tests/test_geometry.py`

**Interfaces:**
- Produces: `LCG(seed)` with `.next_float()`, `.uniform(a, b)`, `.randint(a, b)`, `.choice(seq)`; `Rect(left, top, width, height)` with `.right`, `.bottom`, `.inflate(pad)`, `.contains(x, y)`; `zone_rect(role, pad=0.0) -> Rect`; `Star(x, y, size, depth, transparency)`; `starfield(seed, count, zone=None) -> list[Star]`; `parallax(stars, dx, dy) -> list[tuple[float, float]]`; `drifted(stars, dx, dy) -> list[Star]` (the carry-slide form); `frame_corner_dist() -> float` (the iris clearance target, 550.61 pt); `Meteor(start, end, length, angle_deg, transparency, duration, delay)`; `meteors(seed, count, role) -> list[Meteor]`; `path_string(dx, dy) -> str`; `path_clears_zone(start, end, thickness, zone) -> bool`; `stagger(count, step, start=0.0) -> list[float]`; `scatter_offsets(seed, count, spread) -> list[tuple[float, float, float]]` (dx, dy, rotation — the Task 11 glyph scatter).
- Consumed by: `stage.py` (Task 7 paints `Star` / `Meteor`), `motion.py` (Task 8 turns a `Meteor` into a motion path and `stagger` into delays), `transitions.py` (Task 9 re-seeds and re-places drifted stars on the B twin), every scene module.

Units convention, stated once: `Star`/`Meteor` coordinates are **points** on the 960 × 540 canvas; `path_string` takes a delta in **points** and emits PowerPoint's **slide-fraction** path syntax. The conversion lives only in `path_string`.

- [ ] **Step 1: Write the failing test**

`demos/signal-station-pptx/tests/test_geometry.py`:

```python
import pytest

from deck.geometry import (
    LCG, Rect, drifted, frame_corner_dist, meteors, parallax, path_clears_zone,
    path_string, scatter_offsets, stagger, starfield, zone_rect,
)
from deck.palette import CANVAS_H, CANVAS_W, ZONES


def test_lcg_is_deterministic_and_independent_of_global_random():
    import random
    random.seed(1)
    a = [LCG(7).next_float() for _ in range(3)]
    random.seed(99)
    b = [LCG(7).next_float() for _ in range(3)]
    assert a == b
    assert len(set(a)) == 1  # a fresh LCG(7) always yields the same first value


def test_lcg_stays_in_range_and_moves():
    g = LCG(2026)
    values = [g.next_float() for _ in range(500)]
    assert all(0.0 <= v < 1.0 for v in values)
    assert len(set(values)) > 450
    assert 0.4 < sum(values) / len(values) < 0.6


def test_lcg_helpers_respect_their_bounds():
    g = LCG(5)
    assert all(3.0 <= g.uniform(3.0, 4.0) <= 4.0 for _ in range(100))
    assert all(2 <= g.randint(2, 5) <= 5 for _ in range(100))
    assert all(g.choice("abc") in "abc" for _ in range(50))


def test_rect_edges_and_inflate():
    r = Rect(100.0, 50.0, 200.0, 80.0)
    assert (r.right, r.bottom) == (300.0, 130.0)
    assert r.contains(150.0, 60.0) and not r.contains(99.0, 60.0)
    big = r.inflate(10.0)
    assert (big.left, big.top, big.right, big.bottom) == (90.0, 40.0, 310.0, 140.0)


def test_zone_rect_matches_the_palette_tuple():
    r = zone_rect("concept")
    assert (r.left, r.top, r.width, r.height) == ZONES["concept"]


def test_starfield_is_deterministic_for_a_seed():
    a = starfield(seed=11, count=56)
    b = starfield(seed=11, count=56)
    c = starfield(seed=12, count=56)
    assert [(s.x, s.y, s.size, s.depth) for s in a] == [(s.x, s.y, s.size, s.depth) for s in b]
    assert a != c


def test_every_star_is_on_canvas_with_a_usable_size_and_depth():
    for star in starfield(seed=3, count=200):
        assert 0.0 <= star.x <= CANVAS_W and 0.0 <= star.y <= CANVAS_H
        assert 1.0 <= star.size <= 7.0
        assert 0.0 <= star.depth <= 1.0
        assert 0.3 <= star.transparency <= 0.95


def test_starfield_thins_inside_a_type_zone_but_never_empties_the_plate():
    zone = zone_rect("thesis")
    stars = starfield(seed=4, count=120, zone=zone)
    inside = [s for s in stars if zone.contains(s.x, s.y)]
    assert len(stars) == 120
    assert len(inside) <= 6  # a few faint far stars are allowed behind type


def test_stars_in_a_zone_are_the_faintest_ones():
    zone = zone_rect("thesis")
    stars = starfield(seed=4, count=120, zone=zone)
    inside = [s for s in stars if zone.contains(s.x, s.y)]
    for star in inside:
        assert star.transparency >= 0.8, (star.x, star.y)


def test_parallax_moves_near_stars_further_than_far_stars():
    # depth 0.0 == far, depth 1.0 == near (see Star). Sorting ascending by depth
    # therefore puts the FAR stars first -- an earlier draft of this test had the
    # two slices labelled the other way round and asserted min(far) > max(near),
    # which can never hold.
    stars = starfield(seed=9, count=80)
    moved = parallax(stars, dx=-22.0, dy=14.0)
    assert len(moved) == len(stars)
    pairs = sorted(zip(stars, moved), key=lambda p: p[0].depth)
    dists = [abs(m[0] - s.x) + abs(m[1] - s.y) for s, m in pairs]
    far, near = dists[:10], dists[-10:]
    assert min(near) > max(far)


def test_parallax_displacement_rises_monotonically_with_depth():
    stars = starfield(seed=9, count=120)
    moved = parallax(stars, dx=-22.0, dy=14.0)
    pairs = sorted(((s.depth, abs(m[0] - s.x) + abs(m[1] - s.y))
                    for s, m in zip(stars, moved)), key=lambda p: p[0])
    dists = [d for _, d in pairs]
    # non-decreasing, modulo the 2-decimal rounding in parallax()
    assert all(b >= a - 0.02 for a, b in zip(dists, dists[1:]))


def test_every_star_is_displaced_even_at_the_smallest_drift_vector():
    # S2 asks for ALL of a role's stars to move. The smallest DRIFT magnitude in
    # the deck is proof's (-10, -6), and the shallowest depth factor is 0.15.
    stars = starfield(seed=13, count=80)
    for star, (x, y) in zip(stars, parallax(stars, -10.0, -6.0)):
        assert (x, y) != (star.x, star.y), star


def test_drifted_returns_real_stars_so_a_carry_slide_can_repaint_them():
    stars = starfield(seed=9, count=40)
    moved = drifted(stars, 18.0, 0.0)
    assert len(moved) == len(stars)
    for before, after in zip(stars, moved):
        # position changes; identity (size/depth/transparency) does not, or the
        # carry slide would paint a different-looking field and Morph would
        # interpolate size as well as position
        assert after.x != before.x
        assert (after.size, after.depth, after.transparency) == (
            before.size, before.depth, before.transparency)
    assert [p for p in parallax(stars, 18.0, 0.0)] == [(s.x, s.y) for s in moved]


def test_frame_corner_distance_is_the_iris_clearance_target():
    # S6/D3: the iris aperture must clear the corners, not just the edges
    assert frame_corner_dist() == pytest.approx(550.6132, abs=1e-3)


def test_parallax_is_pure_and_repeatable():
    stars = starfield(seed=9, count=40)
    first = parallax(stars, -22.0, 14.0)
    second = parallax(stars, -22.0, 14.0)
    assert first == second
    assert [s.x for s in stars] == [s.x for s in starfield(seed=9, count=40)]


def test_parallax_never_pushes_a_star_more_than_the_requested_drift():
    stars = starfield(seed=9, count=120)
    for star, (x, y) in zip(stars, parallax(stars, -22.0, 14.0)):
        assert abs(x - star.x) <= 22.0 + 1e-9
        assert abs(y - star.y) <= 14.0 + 1e-9


def test_path_string_is_powerpoint_fraction_syntax():
    assert path_string(240.0, -135.0) == "M 0 0 L 0.250000 -0.250000 E"
    assert path_string(0.0, 0.0) == "M 0 0 L 0.000000 0.000000 E"


def test_path_clears_zone_rejects_a_segment_that_crosses_the_zone():
    zone = Rect(300.0, 200.0, 300.0, 140.0)
    assert not path_clears_zone((200.0, 150.0), (700.0, 400.0), 6.0, zone)


def test_path_clears_zone_accepts_a_segment_that_passes_above_the_zone():
    zone = Rect(300.0, 200.0, 300.0, 140.0)
    assert path_clears_zone((200.0, 40.0), (700.0, 120.0), 6.0, zone)


def test_path_clears_zone_accounts_for_streak_thickness():
    zone = Rect(300.0, 200.0, 300.0, 140.0)
    near_miss = ((200.0, 180.0), (700.0, 190.0))
    assert path_clears_zone(*near_miss, 2.0, zone)
    assert not path_clears_zone(*near_miss, 60.0, zone)


def test_path_clears_zone_rejects_a_segment_fully_inside_the_zone():
    zone = Rect(300.0, 200.0, 300.0, 140.0)
    assert not path_clears_zone((350.0, 250.0), (500.0, 280.0), 4.0, zone)


@pytest.mark.parametrize("role", sorted(ZONES))
def test_every_generated_meteor_clears_its_slides_type_zone(role):
    # D5's honesty cap, proven for all 12 roles at the counts the deck uses
    zone = zone_rect(role, pad=12.0)
    for seed in range(1, 40):
        for m in meteors(seed=seed, count=4, role=role):
            assert path_clears_zone(m.start, m.end, m.length * 0.5 + 6.0, zone), (
                role, seed, m.start, m.end)


def test_meteors_are_deterministic_and_travel_a_real_distance():
    a = meteors(seed=21, count=3, role="breath")
    b = meteors(seed=21, count=3, role="breath")
    assert a == b
    for m in a:
        travel = abs(m.end[0] - m.start[0]) + abs(m.end[1] - m.start[1])
        assert travel >= 180.0
        assert m.transparency >= 0.45  # D5: particles stay faint
        assert 0.8 <= m.duration <= 4.5


def test_meteor_delays_spread_across_the_slide_rather_than_firing_together():
    ms = meteors(seed=33, count=4, role="title")
    delays = sorted(m.delay for m in ms)
    assert delays[0] >= 0.0
    assert all(b - a >= 0.3 for a, b in zip(delays, delays[1:]))


def test_stagger_walks_the_beat_grid():
    assert stagger(3, 0.33) == [0.0, 0.33, 0.66]
    assert stagger(3, 0.16, start=0.4) == [0.4, 0.56, 0.72]
    assert stagger(0, 0.33) == []
    assert stagger(12, 0.12)[-1] == pytest.approx(1.32)


def test_scatter_offsets_are_deterministic_and_bounded():
    a = scatter_offsets(seed=8, count=3, spread=160.0)
    assert a == scatter_offsets(seed=8, count=3, spread=160.0)
    for dx, dy, rot in a:
        assert abs(dx) <= 160.0 and abs(dy) <= 160.0
        assert abs(rot) <= 24.0
    assert any(abs(dx) > 40.0 for dx, _, _ in a)  # a real scatter, not a nudge
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_geometry.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.geometry'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/geometry.py`:

```python
"""Starfield, parallax, meteor routing and path math. Pure python, no COM.

Coordinates are points on the 960x540 canvas. The only place that converts to
PowerPoint's slide-fraction motion-path syntax is path_string().
"""
from __future__ import annotations

import math
from typing import NamedTuple, Sequence

from deck.palette import CANVAS_H, CANVAS_W, ZONES


class LCG:
    """Numerical Recipes LCG. Own state, so a scene cannot disturb another's field."""

    _A = 1664525
    _C = 1013904223
    _M = 2 ** 32

    def __init__(self, seed: int) -> None:
        self._state = (int(seed) * 2654435761 + 12345) % self._M

    def next_float(self) -> float:
        self._state = (self._A * self._state + self._C) % self._M
        return self._state / self._M

    def uniform(self, a: float, b: float) -> float:
        return a + (b - a) * self.next_float()

    def randint(self, a: int, b: int) -> int:
        return a + int(self.next_float() * (b - a + 1)) % (b - a + 1)

    def choice(self, seq: Sequence):
        return seq[int(self.next_float() * len(seq)) % len(seq)]


class Rect(NamedTuple):
    left: float
    top: float
    width: float
    height: float

    @property
    def right(self) -> float:
        return self.left + self.width

    @property
    def bottom(self) -> float:
        return self.top + self.height

    def inflate(self, pad: float) -> "Rect":
        return Rect(self.left - pad, self.top - pad,
                    self.width + 2 * pad, self.height + 2 * pad)

    def contains(self, x: float, y: float) -> bool:
        return self.left <= x <= self.right and self.top <= y <= self.bottom


def zone_rect(role: str, pad: float = 0.0) -> Rect:
    return Rect(*ZONES[role]).inflate(pad)


class Star(NamedTuple):
    x: float
    y: float
    size: float
    depth: float          # 0.0 = far, 1.0 = near; drives parallax and size
    transparency: float


def starfield(seed: int, count: int, zone: Rect | None = None) -> list[Star]:
    """`count` stars, deterministic for `seed`, thinned and faded behind `zone`."""
    g = LCG(seed * 7919 + count)
    stars: list[Star] = []
    behind_zone = 0
    attempts = 0
    while len(stars) < count and attempts < count * 60:
        attempts += 1
        x = g.uniform(4.0, CANVAS_W - 4.0)
        y = g.uniform(4.0, CANVAS_H - 4.0)
        depth = g.next_float() ** 1.6          # most stars far, a few near
        if zone is not None and zone.contains(x, y):
            # keep at most a handful, and only the faintest far ones
            if behind_zone >= 6 or depth > 0.25:
                continue
            behind_zone += 1
            size = 1.0 + depth * 2.0
            stars.append(Star(round(x, 2), round(y, 2), round(size, 2),
                              round(depth, 4), round(g.uniform(0.80, 0.95), 3)))
            continue
        size = 1.0 + depth * 6.0
        transparency = 0.90 - depth * 0.60     # near stars are brighter
        stars.append(Star(round(x, 2), round(y, 2), round(min(size, 7.0), 2),
                          round(depth, 4), round(max(transparency, 0.30), 3)))
    return stars


def parallax(stars: Sequence[Star], dx: float, dy: float) -> list[tuple[float, float]]:
    """Positions on the Morph-B twin. Near stars take the full drift (S2).

    depth 0.0 is far and takes 15% of the drift; depth 1.0 is near and takes
    all of it. The 0.15 floor is deliberate: S2 asks for *every* star to be
    displaced, so no star may take zero drift.
    """
    out: list[tuple[float, float]] = []
    for star in stars:
        k = 0.15 + 0.85 * star.depth
        out.append((round(star.x + dx * k, 2), round(star.y + dy * k, 2)))
    return out


def drifted(stars: Sequence[Star], dx: float, dy: float) -> list[Star]:
    """`parallax` as real Stars, for a carry slide that must *repaint* the field.

    A B twin is a duplicate, so its stars are moved in place by
    transitions.drift_stars(). A carry slide (slides 5, 25) is a fresh slide and
    has to paint the inherited field itself -- same count, same order, same
    !!star_nnn names, same sizes, new positions. Returning Stars rather than
    bare coordinates is what keeps size/depth/transparency identical, so Morph
    interpolates position only.
    """
    return [star._replace(x=x, y=y)
            for star, (x, y) in zip(stars, parallax(stars, dx, dy))]


def frame_corner_dist() -> float:
    """Distance from the canvas centre to a corner: 550.61pt.

    The iris clearance target (S6/D3). An aperture whose radius only reaches the
    nearer *edge* (270pt) still leaves a dark annulus over all four corners.
    """
    return math.hypot(CANVAS_W / 2.0, CANVAS_H / 2.0)


class Meteor(NamedTuple):
    start: tuple[float, float]
    end: tuple[float, float]
    length: float          # streak length in points (an elongated oval)
    angle_deg: float
    transparency: float
    duration: float
    delay: float


# Lanes a streak may travel in, as fractions of the canvas height. Chosen so a
# lane can be rejected wholesale when a role's type zone covers it.
_LANES = (0.06, 0.14, 0.22, 0.30, 0.74, 0.82, 0.90)


def meteors(seed: int, count: int, role: str) -> list[Meteor]:
    """`count` streaks that provably clear `role`'s type zone (D5)."""
    g = LCG(seed * 104729 + count)
    zone = zone_rect(role, pad=12.0)
    out: list[Meteor] = []
    delay = g.uniform(0.2, 0.9)
    guard = 0
    while len(out) < count and guard < 4000:
        guard += 1
        lane = g.choice(_LANES)
        y0 = CANVAS_H * lane + g.uniform(-10.0, 10.0)
        travel = g.uniform(240.0, 520.0)
        drop = g.uniform(40.0, 150.0)
        rightward = g.next_float() < 0.5
        x0 = g.uniform(-120.0, CANVAS_W * 0.45) if rightward else \
            g.uniform(CANVAS_W * 0.55, CANVAS_W + 120.0)
        x1 = x0 + (travel if rightward else -travel)
        y1 = y0 + drop
        length = g.uniform(26.0, 74.0)
        if not path_clears_zone((x0, y0), (x1, y1), length * 0.5 + 6.0, zone):
            continue
        angle = math.degrees(math.atan2(y1 - y0, x1 - x0))
        out.append(Meteor(
            start=(round(x0, 2), round(y0, 2)),
            end=(round(x1, 2), round(y1, 2)),
            length=round(length, 2),
            angle_deg=round(angle, 2),
            transparency=round(g.uniform(0.45, 0.72), 3),
            duration=round(g.uniform(1.1, 4.0), 2),
            delay=round(delay, 2),
        ))
        delay += g.uniform(0.35, 1.4)
    if len(out) < count:
        raise ValueError(f"could not route {count} meteors clear of the {role!r} "
                         f"zone from seed {seed}; widen _LANES or shrink the zone")
    return out


def path_string(dx: float, dy: float) -> str:
    """Points -> PowerPoint motion path in slide fractions. 'M 0 0 L fx fy E'."""
    return (f"M 0 0 L {dx / CANVAS_W:.6f} {dy / CANVAS_H:.6f} E")


def _segments_cross(p1, p2, p3, p4) -> bool:
    def orient(a, b, c) -> float:
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    d1, d2 = orient(p3, p4, p1), orient(p3, p4, p2)
    d3, d4 = orient(p1, p2, p3), orient(p1, p2, p4)
    if ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)):
        return True
    return any(abs(d) < 1e-12 and _on_span(a, b, c)
               for d, (a, b, c) in ((d1, (p3, p4, p1)), (d2, (p3, p4, p2)),
                                    (d3, (p1, p2, p3)), (d4, (p1, p2, p4))))


def _on_span(a, b, c) -> bool:
    return (min(a[0], b[0]) - 1e-9 <= c[0] <= max(a[0], b[0]) + 1e-9 and
            min(a[1], b[1]) - 1e-9 <= c[1] <= max(a[1], b[1]) + 1e-9)


def path_clears_zone(start: tuple[float, float], end: tuple[float, float],
                     thickness: float, zone: Rect) -> bool:
    """True when the swept streak never touches `zone`.

    The streak is treated as a segment inflated by `thickness`, which is the
    same as testing the bare segment against a zone grown by `thickness`.
    """
    z = zone.inflate(thickness)
    if z.contains(*start) or z.contains(*end):
        return False
    corners = [(z.left, z.top), (z.right, z.top), (z.right, z.bottom), (z.left, z.bottom)]
    for i in range(4):
        if _segments_cross(start, end, corners[i], corners[(i + 1) % 4]):
            return False
    return True


def stagger(count: int, step: float, start: float = 0.0) -> list[float]:
    """Delays on the beat grid: stagger(12, 0.12) for a glyph cascade."""
    return [round(start + i * step, 3) for i in range(max(count, 0))]


def scatter_offsets(seed: int, count: int,
                    spread: float) -> list[tuple[float, float, float]]:
    """(dx, dy, rotation) per glyph for the Task 11 title scatter on the A twin."""
    g = LCG(seed * 7901 + count)
    out: list[tuple[float, float, float]] = []
    for i in range(count):
        sign = 1.0 if i % 2 == 0 else -1.0
        dx = sign * g.uniform(spread * 0.35, spread)
        dy = g.uniform(-spread * 0.55, spread * 0.55)
        rot = g.uniform(-24.0, 24.0)
        out.append((round(dx, 2), round(dy, 2), round(rot, 2)))
    return out
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_geometry.py -q`
Expected: `38 passed` (the parametrized clearance test contributes 12). If `meteors()` raises for a role, its zone is too tall for `_LANES` — widen the lane table rather than relaxing the clearance, which is D5's whole honesty cap.

- [ ] **Step 5: Eyeball the field once as ASCII** (catches a field that is technically valid and visually clumped)

Run: `Set-Location demos/signal-station-pptx; python smoke.py field`
Expected: a scatter with a visibly sparse band where the `thesis` zone sits, no empty quadrant, no obvious grid. (`smoke.py field` is pure python — it is the only smoke subcommand that does not open PowerPoint.)

**Accept:** `python -m pytest tests -q` reports ≥ 89 passed, 0 failed; `python -c "from deck.geometry import path_string;print(path_string(240,-135))"` prints `M 0 0 L 0.250000 -0.250000 E`; the parametrized clearance test passes for all 12 roles; `python -c "from deck.geometry import frame_corner_dist;print(round(frame_corner_dist(),2))"` prints `550.61`.

---

### Task 7: `deck/stage.py` — every shape the deck can paint, and nothing else

The first COM module. It owns **shape creation only**: no animation (Task 8), no transitions (Task 9). Each primitive returns the COM shape so a scene can hand it to `motion.py`.

Two rules this module enforces structurally:

- **Naming is the Morph contract.** Anything that must survive a Morph twin is named `!!<kind>_<nnn>` with a zero-padded index, assigned at creation. Morph pairs by object identity on a duplicated slide and by name otherwise, so a mismatched name is a silent cross-fade instead of a drift. Every name is produced by `_name()`, never typed in a scene.
- **Capability branching happens here, once.** Tasks 1's `COM-NOTES.md` rows for `Shape.Glow`, `Shape.SoftEdge` and `TextFrame2.Font.Spacing` are read by `Caps` and the fallback is chosen inside `glow_stack()` / `text()`. No scene ever asks "does this PowerPoint have Glow".

**Files:**
- Create: `demos/signal-station-pptx/deck/stage.py`
- Create: `demos/signal-station-pptx/tests/test_stage_names.py`

**Interfaces:**
- Produces: `Caps.from_notes(path) -> Caps` with `.glow`, `.soft_edge`, `.tracking`, `.custom_motion`, `.donut`, `.gradient`, `.media`; `name_of(kind, i) -> str`; `class Stage(slide, K, caps)` with `field()`, `vignette()`, `star(star, i)`, `meteor(m, i)`, `glow_stack(cx, cy, radius, color_key, i, layers=4)`, `text(spec_key, text, rect, align="l", name=None, color_key=None, size=None)`, `ghost_latin(text, rect, i=0)`, `chrome(idx, total)`, `donut(cx, cy, outer, hole, color_key)`, `rule(x, y, width, height=2.0, color_key="amber")`, `oval(cx, cy, r, color_key, transparency)`, `retext(shape, body)` (re-texts a duplicated twin's meta index — see the `CHROME` row), and `.shapes` (an ordered list of every shape it created, for the build log).
- Consumes: `deck/palette.py` (Task 4), `deck/geometry.py` (Task 6: `Star`, `Meteor`, `Rect`), `deck/comconst.Consts` (Task 2), `COM-NOTES.md` (Task 1).
- Consumed by: every scene module (Tasks 11–12), `transitions.py` (Task 9 re-seeds stars by name).

- [ ] **Step 1: Write the failing test** (naming and `Caps` parsing are pure; the painting is proven by Task 14's real build)

`demos/signal-station-pptx/tests/test_stage_names.py`:

```python
from pathlib import Path

from deck.stage import Caps, name_of

ROOT = Path(__file__).resolve().parents[1]


def test_names_are_morph_pairable_and_zero_padded():
    assert name_of("star", 7) == "!!star_007"
    assert name_of("meteor", 0) == "!!meteor_000"
    assert name_of("glow", 3) == "!!glow_003"
    assert name_of("iris_core", 0) == "!!iris_core_000"


def test_every_name_carries_the_morph_prefix():
    for kind in ("star", "meteor", "glow", "title", "card", "rule"):
        assert name_of(kind, 1).startswith("!!")


def test_caps_reads_ok_rows_as_true_and_fail_rows_as_false():
    notes = """# notes
## Capability results

| Capability | Result |
|---|---|
| `Shape.Glow` | OK |
| `Shape.SoftEdge` | FAIL: AttributeError: SoftEdge |
| `TextFrame2.Font.Spacing + Font.Fill.Transparency` | OK |
| `AddEffect(msoAnimEffectCustom) + MotionEffect.Path` | OK |
| `msoShapeDonut + Adjustments` | OK |
| `Fill.TwoColorGradient` | OK |
| `AddMediaObject2 + PlaySettings.StopAfterSlides` | FAIL: FileNotFoundError |
"""
    caps = Caps.from_text(notes)
    assert caps.glow is True
    assert caps.soft_edge is False
    assert caps.tracking is True
    assert caps.custom_motion is True
    assert caps.donut is True
    assert caps.media is False


def test_caps_defaults_to_pessimistic_when_a_row_is_absent():
    caps = Caps.from_text("## Capability results\n\n| Capability | Result |\n|---|---|\n")
    assert caps.glow is False and caps.soft_edge is False and caps.tracking is False


def test_caps_from_the_real_notes_file_has_glow_and_tracking():
    # requires Task 1 to have run on this machine
    caps = Caps.from_notes(ROOT / "COM-NOTES.md")
    assert caps.glow and caps.tracking and caps.custom_motion
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_stage_names.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.stage'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/stage.py`:

```python
"""COM shape primitives. Creation only -- animation lives in deck/motion.py.

Naming law: anything a Morph twin must keep is named "!!<kind>_<nnn>" by
name_of(). Capability law: the COM-NOTES.md rows from Task 1 are read once into
Caps and the fallback is chosen here, never in a scene.
"""
from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

from deck.geometry import Meteor, Rect, Star
from deck.palette import C, CANVAS_H, CANVAS_W, TYPE

_ROWS = {
    "glow": "Shape.Glow",
    "soft_edge": "Shape.SoftEdge",
    "tracking": "TextFrame2.Font.Spacing",
    "custom_motion": "AddEffect(msoAnimEffectCustom)",
    "path_fallback": "AddEffect(PathDown)",
    "donut": "msoShapeDonut",
    "gradient": "Fill.TwoColorGradient",
    "media": "AddMediaObject2",
    "repeat": "Timing.RepeatUntilEndOfSlide",
}


@dataclass(frozen=True)
class Caps:
    glow: bool = False
    soft_edge: bool = False
    tracking: bool = False
    custom_motion: bool = False
    path_fallback: bool = False
    donut: bool = False
    gradient: bool = False
    media: bool = False
    repeat: bool = False

    @classmethod
    def from_text(cls, text: str) -> "Caps":
        found: dict[str, bool] = {}
        for line in text.splitlines():
            m = re.match(r"^\|\s*`([^`]+)`\s*\|\s*(.+?)\s*\|\s*$", line)
            if not m:
                continue
            label, result = m.group(1), m.group(2)
            for key, needle in _ROWS.items():
                if needle in label:
                    found[key] = result.strip().startswith("OK")
        return cls(**{k: found.get(k, False) for k in _ROWS})

    @classmethod
    def from_notes(cls, path: Path) -> "Caps":
        return cls.from_text(Path(path).read_text(encoding="utf-8"))


def name_of(kind: str, index: int) -> str:
    """The Morph pairing key. Zero-padded so names sort in creation order."""
    return f"!!{kind}_{index:03d}"


class Stage:
    """Paints one slide. Hold on to the returned shapes for deck/motion.py."""

    def __init__(self, slide, K, caps: Caps) -> None:
        self.slide = slide
        self.K = K
        self.caps = caps
        self.shapes: list = []

    # -- internals ---------------------------------------------------------
    def _track(self, shape):
        self.shapes.append(shape)
        return shape

    def _solid(self, shape, color_key: str, transparency: float = 0.0):
        shape.Fill.Visible = True
        shape.Fill.Solid()
        shape.Fill.ForeColor.RGB = C[color_key]
        shape.Fill.Transparency = transparency
        shape.Line.Visible = False
        shape.Shadow.Visible = False
        return shape

    # -- ground ------------------------------------------------------------
    def field(self) -> list:
        """Two translucent plates. The deepest layer of every slide."""
        out = []
        base = self.slide.Shapes.AddShape(self.K.msoShapeRectangle, 0, 0,
                                          CANVAS_W, CANVAS_H)
        base.Name = name_of("field", 0)
        self._solid(base, "field", 0.0)
        out.append(self._track(base))

        lift = self.slide.Shapes.AddShape(self.K.msoShapeRectangle, -40, 120,
                                          CANVAS_W + 80, CANVAS_H)
        lift.Name = name_of("field", 1)
        self._solid(lift, "lift", 0.55)
        out.append(self._track(lift))
        return out

    def vignette(self) -> list:
        """Four edge plates that darken the frame. Cheaper than a gradient and
        does not depend on the Fill.TwoColorGradient capability row."""
        bands = (
            (0.0, 0.0, CANVAS_W, 110.0),
            (0.0, CANVAS_H - 130.0, CANVAS_W, 130.0),
            (0.0, 0.0, 150.0, CANVAS_H),
            (CANVAS_W - 150.0, 0.0, 150.0, CANVAS_H),
        )
        out = []
        for i, (left, top, width, height) in enumerate(bands):
            band = self.slide.Shapes.AddShape(self.K.msoShapeRectangle,
                                              left, top, width, height)
            band.Name = name_of("vignette", i)
            self._solid(band, "field", 0.42)
            out.append(self._track(band))
        return out

    # -- particles ---------------------------------------------------------
    def star(self, star: Star, index: int):
        s = self.slide.Shapes.AddShape(self.K.msoShapeOval,
                                       star.x - star.size / 2,
                                       star.y - star.size / 2,
                                       star.size, star.size)
        s.Name = name_of("star", index)
        key = "ink" if star.depth > 0.75 else ("ghost" if star.depth > 0.35 else "dim")
        self._solid(s, key, star.transparency)
        if self.caps.glow and star.depth > 0.88:
            s.Glow.Radius = 6
            s.Glow.Color.RGB = C["ghost"]
            s.Glow.Transparency = 0.6
        return self._track(s)

    def meteor(self, meteor: Meteor, index: int):
        """An elongated, rotated oval. COM has no motion blur (COM-NOTES)."""
        thickness = max(meteor.length * 0.07, 2.2)
        s = self.slide.Shapes.AddShape(self.K.msoShapeOval,
                                       meteor.start[0], meteor.start[1],
                                       meteor.length, thickness)
        s.Name = name_of("meteor", index)
        self._solid(s, "ghost", meteor.transparency)
        s.Rotation = meteor.angle_deg
        if self.caps.soft_edge:
            s.SoftEdge.Type = 4
        return self._track(s)

    def oval(self, cx: float, cy: float, r: float, color_key: str,
             transparency: float, index: int = 0, kind: str = "dot"):
        s = self.slide.Shapes.AddShape(self.K.msoShapeOval,
                                       cx - r, cy - r, r * 2, r * 2)
        s.Name = name_of(kind, index)
        self._solid(s, color_key, transparency)
        return self._track(s)

    # -- glow --------------------------------------------------------------
    def glow_stack(self, cx: float, cy: float, radius: float,
                   color_key: str = "amber", index: int = 0,
                   layers: int = 4) -> list:
        """S5: four concentric translucent discs, brightest core last.

        There are no blend modes in PowerPoint (COM-NOTES), so a glow is a
        stack. Shape.Glow / SoftEdge are applied on top when available, which
        is the only part of the look that depends on a capability row.
        """
        out = []
        for layer in range(layers):
            t = layer / max(layers - 1, 1)
            r = radius * (1.0 - 0.68 * t)
            transparency = 0.88 - 0.42 * t
            key = color_key if t < 0.99 else "amber_hot"
            disc = self.slide.Shapes.AddShape(self.K.msoShapeOval,
                                              cx - r, cy - r, r * 2, r * 2)
            disc.Name = name_of("glow", index * 10 + layer)
            self._solid(disc, key, max(transparency, 0.12))
            if self.caps.soft_edge:
                disc.SoftEdge.Type = 6
            if self.caps.glow and layer == layers - 1:
                disc.Glow.Radius = 28
                disc.Glow.Color.RGB = C[color_key]
                disc.Glow.Transparency = 0.35
            out.append(self._track(disc))
        return out

    @staticmethod
    def donut_adj(outer: float, hole: float) -> float:
        """The `adj` that gives a donut of radius `outer` a hole of `hole`.

        Donut's single adjustment is the RING THICKNESS as a fraction of the
        shape's smaller side, and the shape's side is the DIAMETER (2*outer).
        So thickness = adj * 2*outer and hole = outer - thickness, giving

            adj = (outer - hole) / (2 * outer)

        An earlier draft wrote `adj = hole / outer`, which for the iris
        (outer 150, hole 54) set adj = 0.36 -> thickness 108 -> a 42pt
        aperture, not 54. After the x11 growth that is 462pt, SHORT of the
        550.61pt frame corner, so the climax would have settled as a dark ring
        in the corners and every unit test around IRIS_GROWTH would still have
        passed, because none of them looked at the real geometry. Clamp is
        0..0.5 because thickness cannot exceed the radius.
        """
        return max(min((outer - hole) / (2.0 * outer), 0.5), 0.0)

    def donut(self, cx: float, cy: float, outer: float, hole: float,
              color_key: str = "field", index: int = 0):
        """The iris (S6/D3). Requires the Donut cap -- there is no soft fallback.

        The fallback used to be an unfilled oval with an amber outline. That
        shape has no hole, so Morphing it bigger reveals nothing and S6 would
        have been audited against a shape that cannot satisfy it. A missing
        Donut cap is a hard build failure: fix the capability or drop the beat,
        do not ship a spike that silently is not one.
        """
        if not self.caps.donut:
            raise RuntimeError(
                "msoShapeDonut + Adjustments is not available on this build "
                "(COM-NOTES.md 'donut' row). The iris climax (S6/D3) needs a "
                "real aperture; an outlined oval has no hole and cannot be "
                "Morphed open. Re-run probe_com.py; if the cap is genuinely "
                "absent, this machine cannot build the spike beat.")
        d = self.slide.Shapes.AddShape(self.K.msoShapeDonut,
                                       cx - outer, cy - outer, outer * 2, outer * 2)
        d.Name = name_of("iris_core", index)
        self._solid(d, color_key, 0.05)
        d.Adjustments.SetItem(1, self.donut_adj(outer, hole))
        return self._track(d)

    def rule(self, x: float, y: float, width: float, height: float = 2.0,
             color_key: str = "amber", index: int = 0):
        """The amber rule that Wipes in and Morph-extends on the B twin."""
        r = self.slide.Shapes.AddShape(self.K.msoShapeRectangle, x, y, width, height)
        r.Name = name_of("rule", index)
        self._solid(r, color_key, 0.1)
        return self._track(r)

    # -- type --------------------------------------------------------------
    def text(self, spec_key: str, body: str, rect: Rect, align: str = "l",
             name: str | None = None, color_key: str | None = None,
             size: float | None = None, index: int = 0):
        """One text box, styled from palette.TYPE. Never pass a raw size unless
        a scene needs a declared variant -- the dead band is tested in Task 4."""
        spec = TYPE[spec_key]
        box = self.slide.Shapes.AddTextbox(self.K.msoTextOrientationHorizontal,
                                           rect.left, rect.top, rect.width, rect.height)
        box.Name = name or name_of(spec_key, index)
        frame = box.TextFrame2
        frame.WordWrap = True
        frame.AutoSize = 0
        frame.MarginLeft = frame.MarginRight = frame.MarginTop = frame.MarginBottom = 0
        rng = frame.TextRange
        rng.Text = body
        rng.Font.Size = size or spec.size
        rng.Font.Name = spec.font
        rng.Font.NameFarEast = spec.font
        rng.Font.Fill.ForeColor.RGB = C[color_key or spec.color]
        rng.ParagraphFormat.Alignment = {"l": 1, "c": 2, "r": 3}[align]
        if self.caps.tracking and spec.tracking:
            rng.Font.Spacing = spec.tracking
        return self._track(box)

    def ghost_latin(self, body: str, rect: Rect, index: int = 0):
        """S12: tracked latin ghost type, faded by fill transparency."""
        box = self.text("ghost_latin", body, rect, align="l",
                        name=name_of("ghost", index))
        rng = box.TextFrame2.TextRange
        if self.caps.tracking:
            rng.Font.Fill.Transparency = 0.55
        return box

    def chrome(self, idx: int, total: int) -> list:
        """The two meta marks every slide carries. Both <= META_CEIL."""
        out = [
            self.text("meta_index", f"{idx:02d} / {total}",
                      Rect(CANVAS_W - 150.0, CANVAS_H - 46.0, 110.0, 24.0),
                      align="r", name=name_of("meta_index", 0)),
            self.text("meta_signal", "S I G N A L",
                      Rect(56.0, CANVAS_H - 46.0, 220.0, 24.0),
                      align="l", name=name_of("meta_signal", 0)),
        ]
        if self.caps.tracking:
            out[1].TextFrame2.TextRange.Font.Fill.Transparency = 0.5
        return [self._track(s) if s not in self.shapes else s for s in out]

    def retext(self, shape, body: str):
        """Replace a shape's text, keeping every other property.

        Needed because a B twin is a *duplicate*: without this its
        !!meta_index_000 still reads its A twin's number, so slides 2, 4, 6, 10,
        12, 16, 18, 20, 22 and 26 would all display the wrong "NN / 26". The
        CHROME audit row exists to catch exactly that.
        """
        shape.TextFrame2.TextRange.Text = body
        return shape
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_stage_names.py -q`
Expected: `5 passed`. If `test_caps_from_the_real_notes_file_has_glow_and_tracking` fails, Task 1 has not been run on this machine — run `python probe_com.py` first; do **not** weaken the test.

- [ ] **Step 5: Paint one throwaway slide to confirm the primitives are real**

This is the first time COM paints anything, so prove each primitive before 26 scenes depend on it.

**Write `demos/signal-station-pptx/smoke.py` now** (it is reused by Tasks 8, 9 and 10). One file, five subcommands, no shell quoting: PowerShell cannot carry the multi-line `python -c "…"` blocks an earlier draft used, and an inline script cannot be re-run after a fix without retyping it.

```python
#!/usr/bin/env python3
"""COM smoke runs. `python smoke.py <stage|motion|twin|audio|field>`.

These are the checks that cannot be unit-tested because they assert that
PowerPoint itself kept a property across a save/reopen. Each one saves into
out/_<name>-smoke.pptx (gitignored) and prints numbers the plan's Expected
lines name exactly.
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "out"


def _app():
    import win32com.client as w32
    from deck.comconst import load_constants
    app = w32.gencache.EnsureDispatch("PowerPoint.Application")
    K = load_constants(app)
    app.DisplayAlerts = K.ppAlertsNone
    return app, K


def _deck(app, K):
    prs = app.Presentations.Add(WithWindow=True)
    prs.PageSetup.SlideWidth = 960
    prs.PageSetup.SlideHeight = 540
    return prs


def _save(prs, K, name: str) -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    path = (OUT / f"_{name}-smoke.pptx").resolve()
    prs.SaveAs(str(path), K.ppSaveAsOpenXMLPresentation)
    return path


def cmd_field() -> int:
    """Pure python: ASCII-render one starfield to catch visual clumping."""
    from deck.geometry import starfield, zone_rect
    zone = zone_rect("thesis")
    grid = [[" "] * 96 for _ in range(27)]
    for star in starfield(7, 92, zone):
        grid[int(star.y / 20)][int(star.x / 10)] = "*" if star.depth > 0.5 else "."
    print("\n".join("".join(row) for row in grid))
    return 0


def cmd_stage() -> int:
    from deck.geometry import Rect, meteors, starfield
    from deck.stage import Caps, Stage
    app, K = _app()
    caps = Caps.from_notes(ROOT / "COM-NOTES.md")
    prs = _deck(app, K)
    st = Stage(prs.Slides.Add(1, K.ppLayoutBlank), K, caps)
    st.field()
    st.vignette()
    for i, star in enumerate(starfield(7, 92)):
        st.star(star, i)
    for i, m in enumerate(meteors(7, 3, "thesis")):
        st.meteor(m, i)
    st.glow_stack(300, 270, 150)
    st.donut(640, 270, 120, 60)
    st.rule(110, 400, 240)
    st.text("sentence", "把噪声收成航线。", Rect(110, 220, 720, 90))
    st.ghost_latin("SIGNAL STATION", Rect(110, 330, 500, 46))
    st.chrome(1, 26)
    print("tracked:", len(st.shapes), "/ slide reports", prs.Slides(1).Shapes.Count)
    print("saved:", _save(prs, K, "stage"))
    prs.Close()
    app.Quit()
    return 0


COMMANDS = {"field": cmd_field, "stage": cmd_stage}   # motion/twin/audio: Tasks 8-10


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if len(argv) != 2 or argv[1] not in COMMANDS:
        print(f"usage: python smoke.py <{'|'.join(sorted(COMMANDS))}>",
              file=sys.stderr)
        return 2
    return COMMANDS[argv[1]]()


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py stage`

Expected: `tracked: 111 / slide reports 111` — 2 field + 4 vignette + 92 stars + 3 meteors + 4 glow + 1 donut + 1 rule + 1 text + 1 ghost + 2 chrome = 111. Both numbers must match each other *and* the hand count, because S11 audits predicted-vs-actual shape counts and a silently untracked shape would make that prediction unauditable.

- [ ] **Step 6: Human look at the smoke slide**

Open `out/_stage-smoke.pptx`. Confirm: the field is near-black and not flat grey, stars read as depth rather than one size, meteors are thin tilted streaks, the glow stack reads as one soft core and not four hard rings, the donut has a visible hole, the sentence is comfortably readable at 40 pt, and the two meta marks are small and quiet. Note anything wrong in `COM-NOTES.md` under a `## Stage smoke notes` heading — the glow stack's layer count and transparencies are the knobs.

**Accept:** `python -m pytest tests -q` reports ≥ 94 passed, 0 failed; `smoke.py` exists with a `field` and a `stage` subcommand; `out/_stage-smoke.pptx` exists and opens; the two shape counts in Step 5 agree with each other and with 111; `(Select-String -Pattern "C\[" -Path deck/stage.py).Count` is non-zero while `Select-String -Pattern "#[0-9A-Fa-f]{6}" -Path deck/stage.py` returns nothing.

---

### Task 8: `deck/motion.py` — effects, motion paths, and the clock that keeps the deck readable

Where the §0.0-correct constants from Task 2 finally buy something: the root-cause table says the old deck's "after previous" chain was `msoAnimTriggerOnPageClick`, which in an auto-advance deck means **the chain never ran**. This module never passes a literal trigger; it takes them from `K`.

It also owns the **readability clock**, which is the only mechanism keeping D5/D7's density honest:

- `hero_end` — when the slide's hero signal is final. §2.4: ≤ **1.5 s**.
- `readable_end` — when the last non-ambient event is final. ≤ **5 s** or the start of the hold.
- `ambient_end` — ignored by both gates; only declared ambient layers may still be moving.

`assert_timing()` raises on a violation, so an over-animated scene fails the build instead of shipping an unreadable slide. That assertion is the build-side half of S13/S14.

**Files:**
- Create: `demos/signal-station-pptx/deck/motion.py`
- Create: `demos/signal-station-pptx/tests/test_motion_clock.py`

**Interfaces:**
- Produces: `class Clock` (pure: `mark(end, ambient=False, hero=False)`, `.hero_end`, `.readable_end`, `.ambient_end`, `check(hold, adv, label) -> list[str]`); `class Motion(slide, K, caps, clock)` with entrance helpers `fade`, `wipe`, `faded_zoom`, `rise_up`, `appear`; ambient helpers `travel`, `breath`, `twinkle`, `dim`, `spin_slow`; `cascade(shapes, helper, step, start=0.0, **kw)`; `media_play(shape)`; and `assert_timing(spec, adv)`.
- Constants it fixes in one place: `EASE_OUT = (0.0, 0.7)` (§2.4 rule 1), `LINEAR = (0.0, 0.0)` (D4 — only on `travel`), `BREATH_MIN_SEC = 6.0`, `BREATH_MAX_PCT = 106.0` (S9).
- Consumes: `deck/comconst.Consts`, `deck/stage.Caps`, `deck/geometry` (`Meteor`, `path_string`, `stagger`).
- Consumed by: every scene module (Tasks 11–12), `transitions.py` (Task 9 clears a B twin's timeline), `build_deck.py` (Task 13 logs `hero_end` / `readable_end` per slide).

**The `Custom` branch from Task 1.** `travel()` prefers `AddEffect(msoAnimEffectCustom)` + `Behaviors.Add(msoAnimTypeMotion)`. If `COM-NOTES.md` reports that row as `FAIL`, it falls back to `AddEffect(msoAnimEffectPathDown)` and rewrites the existing motion behavior's `Path`. If **both** rows failed, it raises — the meteor shower is the user's headline ask and silently dropping it is worse than failing the build.

- [ ] **Step 1: Write the failing test** (the clock is pure and carries the laws; the COM calls are proven by Task 14)

`demos/signal-station-pptx/tests/test_motion_clock.py`:

```python
import pytest

from deck.motion import BREATH_MAX_PCT, BREATH_MIN_SEC, EASE_OUT, LINEAR, Clock


def test_entrances_ease_out_and_only_travel_is_linear():
    assert EASE_OUT == (0.0, 0.7)
    assert LINEAR == (0.0, 0.0)


def test_a_fresh_clock_is_at_zero():
    c = Clock()
    assert (c.hero_end, c.readable_end, c.ambient_end) == (0.0, 0.0, 0.0)


def test_readable_marks_advance_the_readable_end_only():
    c = Clock()
    c.mark(1.2)
    c.mark(0.8)
    assert c.readable_end == pytest.approx(1.2)
    assert c.ambient_end == 0.0


def test_ambient_marks_do_not_touch_the_readable_end():
    c = Clock()
    c.mark(1.0)
    c.mark(19.5, ambient=True)
    assert c.readable_end == pytest.approx(1.0)
    assert c.ambient_end == pytest.approx(19.5)


def test_hero_marks_count_as_readable_too():
    c = Clock()
    c.mark(1.4, hero=True)
    assert c.hero_end == pytest.approx(1.4)
    assert c.readable_end == pytest.approx(1.4)


def test_check_is_silent_on_a_well_behaved_slide():
    c = Clock()
    c.mark(1.3, hero=True)
    c.mark(2.6)
    c.mark(30.0, ambient=True)
    assert c.check(hold=8.0, adv=6.0, label="slide 3") == []


def test_check_catches_a_hero_that_lands_after_1_5s():
    c = Clock()
    c.mark(2.1, hero=True)
    problems = c.check(hold=8.0, adv=6.0, label="slide 3")
    assert any("hero" in p and "1.5" in p for p in problems)


def test_check_catches_a_readable_event_after_5s():
    c = Clock()
    c.mark(1.0, hero=True)
    c.mark(6.2)
    problems = c.check(hold=12.0, adv=10.0, label="slide 7")
    assert any("readable" in p and "5" in p for p in problems)


def test_check_catches_an_advance_that_cuts_a_readable_event():
    # S14 / law 8: advTm must clear readable_end by 0.5s
    c = Clock()
    c.mark(1.0, hero=True)
    c.mark(3.9)
    problems = c.check(hold=4.0, adv=4.0, label="slide 2")
    assert any("advTm" in p for p in problems)


def test_check_allows_ambient_motion_to_outlive_the_advance():
    c = Clock()
    c.mark(1.0, hero=True)
    c.mark(999.0, ambient=True)
    assert c.check(hold=8.0, adv=6.0, label="slide 8") == []


def test_breath_caps_are_the_s9_numbers():
    assert BREATH_MIN_SEC == 6.0
    assert BREATH_MAX_PCT == 106.0


def test_an_ambient_exit_fade_does_not_advance_the_readable_end():
    # the meteor exit bug: an exit fade at 3.4s must not become the slide's
    # readable deadline, or every slide fails S13/S14 on particle motion
    c = Clock()
    c.mark(1.2, hero=True)
    c.mark(3.9, ambient=True)          # what fade(..., ambient=True) books
    assert c.readable_end == pytest.approx(1.2)
    assert c.check(hold=8.0, adv=6.0, label="slide 1") == []


def test_meteor_travel_books_everything_on_the_ambient_clock():
    from deck.geometry import Meteor
    from deck.motion import Motion

    class NullSeq:
        def AddEffect(self, *a, **kw):
            return NullEffect()

    class NullTiming:
        Duration = TriggerDelayTime = Accelerate = Decelerate = 0.0
        AutoReverse = RepeatUntilEndOfSlide = False

    class NullBehaviors:
        Count = 0

        def Add(self, _type):
            return NullEffect()

    class NullEffect:
        Exit = False
        MotionEffect = type("M", (), {"Path": ""})()

        def __init__(self):
            self.Timing = NullTiming()
            self.Behaviors = NullBehaviors()

    class NullSlide:
        TimeLine = type("T", (), {"MainSequence": NullSeq()})()

    class K:
        msoAnimTriggerWithPrevious = 2
        msoAnimEffectCustom = 32
        msoAnimEffectFade = 10
        msoAnimTypeMotion = 1

    class Caps:
        custom_motion = True
        path_fallback = False
        repeat = True

    clock = Clock()
    mo = Motion(NullSlide(), K(), Caps(), clock)
    mo.meteor_travel(object(), Meteor((0.0, 0.0), (300.0, 120.0), 40.0, 21.8,
                                      0.5, 2.6, 1.1))
    assert clock.readable_end == 0.0
    assert clock.ambient_end > 3.0
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_motion_clock.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.motion'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/motion.py`:

```python
"""COM animation helpers plus the per-slide readability clock.

Never write a trigger or effect number here -- they come from deck.comconst,
because the old build_deck.py's whole "after previous" chain was actually
msoAnimTriggerOnPageClick and therefore never ran (see the root-cause table).
"""
from __future__ import annotations

from deck.geometry import Meteor, path_string

EASE_OUT = (0.0, 0.7)    # 2.4 rule 1: every entrance decelerates
LINEAR = (0.0, 0.0)      # D4: travel only
HERO_LIMIT = 1.5
READABLE_LIMIT = 5.0
ADV_SLACK = 0.5          # S14 / law 8
BREATH_MIN_SEC = 6.0     # S9
BREATH_MAX_PCT = 106.0   # S9 / 2.4 rule 3


class Clock:
    """Tracks when each class of motion is finished on one slide."""

    def __init__(self) -> None:
        self.hero_end = 0.0
        self.readable_end = 0.0
        self.ambient_end = 0.0

    def mark(self, end: float, ambient: bool = False, hero: bool = False) -> float:
        if ambient:
            self.ambient_end = max(self.ambient_end, end)
        else:
            self.readable_end = max(self.readable_end, end)
            if hero:
                self.hero_end = max(self.hero_end, end)
        return end

    def check(self, hold: float, adv: float, label: str) -> list[str]:
        problems: list[str] = []
        if self.hero_end > HERO_LIMIT + 1e-6:
            problems.append(f"{label}: hero signal final at {self.hero_end:.2f}s, "
                            f"limit {HERO_LIMIT}s (2.4 rule 2)")
        limit = min(READABLE_LIMIT, hold)
        if self.readable_end > limit + 1e-6:
            problems.append(f"{label}: last readable event final at "
                            f"{self.readable_end:.2f}s, limit {limit}s "
                            f"(readable floor 5s / the hold)")
        if adv < self.readable_end + ADV_SLACK - 1e-6:
            problems.append(f"{label}: advTm {adv:.2f}s does not clear "
                            f"readable_end {self.readable_end:.2f}s + "
                            f"{ADV_SLACK}s (S14 / law 8)")
        return problems


class Motion:
    """Animation helpers for one slide. Every call marks the Clock."""

    def __init__(self, slide, K, caps, clock: Clock | None = None) -> None:
        self.slide = slide
        self.K = K
        self.caps = caps
        self.clock = clock or Clock()
        self.seq = slide.TimeLine.MainSequence

    # -- internals ---------------------------------------------------------
    def _add(self, shape, effect: int, duration: float, delay: float,
             ease=EASE_OUT, exit_: bool = False, trigger: int | None = None):
        eff = self.seq.AddEffect(
            shape, effect, 0,
            trigger if trigger is not None else self.K.msoAnimTriggerWithPrevious)
        if exit_:
            eff.Exit = True
        eff.Timing.Duration = duration
        eff.Timing.TriggerDelayTime = delay
        eff.Timing.Accelerate, eff.Timing.Decelerate = ease
        return eff

    def _behavior(self, eff, type_const):
        for i in range(1, eff.Behaviors.Count + 1):
            beh = eff.Behaviors.Item(i)
            if beh.Type == type_const:
                return beh
        return None

    # -- entrances (readable) ---------------------------------------------
    def fade(self, shape, duration=0.8, delay=0.0, hero=False, exit_=False,
             ambient=False):
        """`ambient=True` books the mark on the ambient clock instead of the
        readable one. Required for particle exits: a meteor that fades out at
        t=3.4s is not a readable event, and charging it to readable_end makes
        every slide fail S13/S14 for no reason."""
        eff = self._add(shape, self.K.msoAnimEffectFade, duration, delay, exit_=exit_)
        self.clock.mark(delay + duration, ambient=ambient, hero=hero)
        return eff

    def wipe(self, shape, duration=0.6, delay=0.0, hero=False,
             direction: int | None = None):
        eff = self._add(shape, self.K.msoAnimEffectWipe, duration, delay)
        try:
            eff.EffectParameters.Direction = (
                direction if direction is not None else self.K.msoAnimDirectionRight)
        except Exception:  # some effects expose no direction parameter
            pass
        self.clock.mark(delay + duration, hero=hero)
        return eff

    def faded_zoom(self, shape, duration=0.7, delay=0.0, hero=False):
        eff = self._add(shape, self.K.msoAnimEffectFadedZoom, duration, delay)
        self.clock.mark(delay + duration, hero=hero)
        return eff

    def rise_up(self, shape, duration=0.6, delay=0.0, hero=False):
        eff = self._add(shape, self.K.msoAnimEffectRiseUp, duration, delay)
        self.clock.mark(delay + duration, hero=hero)
        return eff

    def appear(self, shape, delay=0.0):
        eff = self._add(shape, self.K.msoAnimEffectAppear, 0.01, delay)
        self.clock.mark(delay + 0.01)
        return eff

    def cascade(self, shapes, helper, step: float, start: float = 0.0, **kw):
        """A staggered run of one helper. The 0.12s glyph cascade (S4) and the
        0.16s proof-card cascade (S8) are both this."""
        out = []
        for i, shape in enumerate(shapes):
            out.append(helper(shape, delay=round(start + i * step, 3), **kw))
        return out

    # -- ambient -----------------------------------------------------------
    def travel(self, shape, dx: float, dy: float, duration=2.0, delay=0.0):
        """A motion path in slide fractions. Linear by D4, ambient by S3/D5."""
        if self.caps.custom_motion:
            eff = self._add(shape, self.K.msoAnimEffectCustom, duration, delay,
                            ease=LINEAR)
            beh = eff.Behaviors.Add(self.K.msoAnimTypeMotion)
            beh.MotionEffect.Path = path_string(dx, dy)
        elif self.caps.path_fallback:
            eff = self._add(shape, self.K.msoAnimEffectPathDown, duration, delay,
                            ease=LINEAR)
            beh = self._behavior(eff, self.K.msoAnimTypeMotion)
            if beh is None:
                raise RuntimeError("PathDown exposed no motion behavior to rewrite")
            beh.MotionEffect.Path = path_string(dx, dy)
        else:
            raise RuntimeError(
                "neither msoAnimEffectCustom nor the PathDown fallback is available "
                "on this PowerPoint (COM-NOTES.md); the meteor shower cannot be "
                "built -- fix the environment rather than shipping without S3")
        self.clock.mark(delay + duration, ambient=True)
        return eff

    def meteor_travel(self, shape, meteor: Meteor):
        """Travel + an animated exit fade, both ambient (D4/D5).

        The exit is a real animated fade so the streak dies out instead of
        snapping off. It must be booked as **ambient**: an earlier draft called
        self.fade(..., exit_=True) -- which marks the readable clock -- and then
        "undid" it with `readable_end = max(0.0, readable_end)`, which is a
        no-op. The effect was that every slide's readable_end was pushed to the
        last meteor's exit (up to ~5.5s), so S13's 5s gate and S14's advTm gate
        were being evaluated against ambient particle motion.
        """
        dx = meteor.end[0] - meteor.start[0]
        dy = meteor.end[1] - meteor.start[1]
        eff = self.travel(shape, dx, dy, duration=meteor.duration, delay=meteor.delay)
        exit_dur = min(0.5, meteor.duration * 0.3)
        self.fade(shape, duration=exit_dur,
                  delay=max(meteor.delay, meteor.delay + meteor.duration - exit_dur),
                  exit_=True, ambient=True)
        return eff

    def breath(self, shape, duration=BREATH_MIN_SEC, percent=BREATH_MAX_PCT,
               delay=0.0):
        """S9: at most one per slide, <=106%, >=6s, repeating. Capped here."""
        duration = max(duration, BREATH_MIN_SEC)
        percent = min(percent, BREATH_MAX_PCT)
        eff = self._add(shape, self.K.msoAnimEffectGrowShrink, duration, delay,
                        ease=(0.3, 0.3))
        eff.Timing.AutoReverse = True
        if self.caps.repeat:
            eff.Timing.RepeatUntilEndOfSlide = True
        beh = self._behavior(eff, self.K.msoAnimTypeScale)
        if beh is not None:
            beh.ScaleEffect.ByX = percent
            beh.ScaleEffect.ByY = percent
        self.clock.mark(delay + duration, ambient=True)
        return eff

    def twinkle(self, shapes, duration=3.0, delay_step=0.07, start=0.3):
        """Transparency pulse across the star layer. Ambient, repeating."""
        out = []
        for i, shape in enumerate(shapes):
            eff = self._add(shape, self.K.msoAnimEffectTransparency, duration,
                            round(start + i * delay_step, 3), ease=(0.3, 0.3))
            eff.Timing.AutoReverse = True
            if self.caps.repeat:
                eff.Timing.RepeatUntilEndOfSlide = True
            self.clock.mark(start + i * delay_step + duration, ambient=True)
            out.append(eff)
        return out

    def dim(self, shape, duration=2.0, delay=0.0):
        """Transparency ramp used by the afterglow B twin (glow dims)."""
        eff = self._add(shape, self.K.msoAnimEffectTransparency, duration, delay,
                        ease=(0.2, 0.6))
        self.clock.mark(delay + duration, ambient=True)
        return eff

    def spin_slow(self, shape, duration=12.0, delay=0.0):
        eff = self._add(shape, self.K.msoAnimEffectSpin, duration, delay,
                        ease=LINEAR)
        if self.caps.repeat:
            eff.Timing.RepeatUntilEndOfSlide = True
        self.clock.mark(delay + duration, ambient=True)
        return eff

    # -- media -------------------------------------------------------------
    def media_play(self, shape):
        """msoAnimEffectMediaPlay is 83, not 150 (root-cause table)."""
        eff = self.seq.AddEffect(shape, self.K.msoAnimEffectMediaPlay, 0,
                                 self.K.msoAnimTriggerWithPrevious)
        return eff

    # -- gate --------------------------------------------------------------
    def assert_timing(self, spec, adv: float) -> None:
        problems = self.clock.check(spec.hold, adv, f"slide {spec.idx} ({spec.role})")
        if problems:
            raise AssertionError("; ".join(problems))
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_motion_clock.py -q`
Expected: `13 passed`

- [ ] **Step 5: Animate the smoke slide — prove the motion path and the breath are real**

The one COM claim that cannot be unit-tested is that a rewritten `MotionEffect.Path` survives a save/reopen. Add a `motion` subcommand to `smoke.py` that writes, closes, reopens and reads the paths back:

```python
def cmd_motion() -> int:
    from deck.geometry import Rect, meteors, starfield
    from deck.motion import Clock, Motion
    from deck.stage import Caps, Stage
    app, K = _app()
    caps = Caps.from_notes(ROOT / "COM-NOTES.md")
    prs = _deck(app, K)
    slide = prs.Slides.Add(1, K.ppLayoutBlank)
    st, mo = Stage(slide, K, caps), Motion(slide, K, caps, Clock())
    st.field()
    st.vignette()
    stars = [st.star(s, i) for i, s in enumerate(starfield(7, 92))]
    mo.twinkle(stars[:18])
    for i, m in enumerate(meteors(7, 3, "thesis")):
        mo.meteor_travel(st.meteor(m, i), m)
    mo.breath(st.glow_stack(300, 270, 150)[-1])
    title = st.text("sentence", "把噪声收成航线。", Rect(110, 220, 720, 90))
    mo.fade(title, 0.8, 0.0, hero=True)
    st.chrome(1, 26)
    print(f"hero {mo.clock.hero_end:.2f} readable {mo.clock.readable_end:.2f} "
          f"ambient {mo.clock.ambient_end:.2f}")
    print("effects", slide.TimeLine.MainSequence.Count)
    spec = type("S", (), {"hold": 8.0, "idx": 1, "role": "thesis"})()
    mo.assert_timing(spec, 6.0)
    path = _save(prs, K, "motion")
    prs.Close()
    reopened = app.Presentations.Open(str(path), WithWindow=False)
    seq = reopened.Slides(1).TimeLine.MainSequence
    paths = [seq.Item(i).Behaviors.Item(j).MotionEffect.Path
             for i in range(1, seq.Count + 1)
             for j in range(1, seq.Item(i).Behaviors.Count + 1)
             if seq.Item(i).Behaviors.Item(j).Type == K.msoAnimTypeMotion]
    print("motion paths after reopen:", len(paths))
    print(paths[:3])
    reopened.Close()
    app.Quit()
    return 0
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py motion`

Expected, and read these three numbers carefully because they are the proof that the ambient/readable split works:

- **`hero 0.80 readable 0.80`** — the only readable event is the title. The three meteors contribute a travel *and* an animated exit fade, and **none of the six marks may appear in `readable`**. If `readable` comes back around 3–6 s, `meteor_travel` booked its exit on the readable clock and the fix in Step 3 was not applied.
- **`ambient` ≥ 3.0** — the meteors and the 6 s breath are still running, which is allowed and expected.
- **`effects` ≥ 25** — 18 twinkles + 6 meteor effects + 1 breath + 1 fade.
- **`motion paths after reopen: 3`** with paths like `M 0 0 L 0.41xxxx 0.19xxxx E`. If the reopened paths are empty or reverted to a stock `PathDown` shape, the meteor layer must set `Path` *after* every other property on the effect — record that in `COM-NOTES.md` and keep the order.

`assert_timing` must stay silent. It would previously have raised here, and the correct response to that is to fix the ambient booking, never to raise `READABLE_LIMIT`.

- [ ] **Step 6: Confirm the clock actually fails a bad slide** (the gate is worthless if it never fires)

Run: `Set-Location demos/signal-station-pptx; python -c "from deck.motion import Clock;c=Clock();c.mark(2.4,hero=True);print(c.check(8.0,6.0,'probe'))"`
Expected: a one-item list naming `hero signal final at 2.40s`.

**Accept:** `python -m pytest tests -q` reports ≥ 107 passed, 0 failed; `out/_motion-smoke.pptx` exists; Step 5 prints `readable 0.80` (not a meteor-inflated value) and 3 motion paths after reopen; Step 6 prints a non-empty problem list.

---

### Task 9: `deck/transitions.py` — the twin machine, the three signatures, and the drift

The module that produces the headline effect. A Morph twin is built by **duplicating the A slide**, which is what gives Morph object identity to pair against — rebuilding the B slide from scratch and relying on `!!` names is the fallback, not the plan, because PowerPoint's name-based pairing is weaker than identity pairing on a duplicate.

Five things must happen on a B twin, in this order, or the drift breaks:

1. **Duplicate**, so every shape is paired.
2. **Clear the timeline.** §Step 2 law 2 (one driver per object): if a persisting object still carries its A-slide entrance, the entrance and the Morph fight and the object flickers. `clear_timeline()` deletes every effect on the B slide before anything is re-added.
3. **Re-place, don't re-create.** Stars are moved by assigning `.Left` / `.Top` from `geometry.parallax()`; the glow core, the amber rule and the type are moved/resized in place. Only the **meteor layer** is deleted and re-seeded, because a half-travelled streak has no sensible paired position — which is exactly the §Global Constraints line "ambient particles are deleted and re-seeded instead of being Morph-paired".
4. **Re-seed the ambient layer.** Step 2 deleted the twinkle and the breath along with everything else. They must be re-added (`scenes.common.reseed_ambient`), or the twin holds a frozen starfield and a dead glow for its entire 4–17.5 s hold — the Morph finishes in 2 s and then nothing moves. These are ambient marks, so they do not touch `readable_end` and they are not entrances.
5. **Re-text the meta index.** The duplicate still carries its A twin's `NN / 26`. `retext_meta_index()` fixes it, `build_deck` fails the build if the shape is missing, and the `CHROME` audit row checks the saved XML.

**Files:**
- Create: `demos/signal-station-pptx/deck/transitions.py`
- Create: `demos/signal-station-pptx/tests/test_transitions.py`

**Interfaces:**
- Produces: `TRANS_CONST: dict[str, str]` (plan name → `Consts` attribute name), `DRIFT: dict[str, tuple[float, float]]` (per-role star drift in points, from the §Effect vocabulary "Morph job" column), `effect_const(K, trans) -> int`, `apply_transition(slide, spec, K, adv) -> None`, `duplicate_twin(prs, a_index) -> slide`, `clear_timeline(slide) -> int` (returns how many effects it removed), `shapes_by_prefix(slide, prefix) -> list`, `delete_by_prefix(slide, prefix) -> int`, `drift_stars(slide, stars, dx, dy) -> int`, `retext_meta_index(stage, slide, idx, total) -> bool`, `extend_rule(shape, by) -> None`, `grow_iris(shape, factor) -> None`, `plan_drift(spec) -> tuple | None`.
- Consumes: `deck/comconst.Consts`, `deck/geometry.parallax`, `deck/stage.name_of`, `deck/sync.SlideSpec`.
- Consumed by: `deck/scenes/*` (Tasks 11–12 call the B-twin helpers), `build_deck.py` (Task 13 applies the transition and the `advTm` together).

**Why `apply_transition` also writes `advTm`:** the two are one decision. Separating them is how a deck ends up with a 2.5 s Morph on a slide whose advance is 2.0 s, which PowerPoint renders as a cut. The function takes `adv` from `sync.adv_time()` and refuses a combination that cannot work.

- [ ] **Step 1: Write the failing test**

`demos/signal-station-pptx/tests/test_transitions.py`:

```python
import json
from pathlib import Path

import pytest

from deck.comconst import Consts
from deck.geometry import starfield
from deck.sync import expand
from deck.transitions import (
    DRIFT, TRANS_CONST, effect_const, plan_drift, retext_meta_index,
)

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))


def test_every_transition_name_in_timing_json_has_a_constant():
    kinds = {s.trans for s in expand(TIMING)}
    assert kinds <= set(TRANS_CONST), kinds - set(TRANS_CONST)


def test_the_three_signatures_map_to_the_effects_s6_and_s7_demand():
    assert TRANS_CONST["door_ripple"] == "ppEffectRippleCenter"
    assert TRANS_CONST["door_window"] == "ppEffectWindowVertical"
    assert TRANS_CONST["iris"] == "ppEffectCircleOut"


def test_fade_is_fade_smoothly_not_the_legacy_fade():
    # the root-cause table: the old script used 4263, which is not a ppEntryEffect
    assert TRANS_CONST["fade"] == "ppEffectFadeSmoothly"
    assert effect_const(Consts({}), "fade") == 3849


def test_morph_names_resolve_to_the_morph_effects():
    K = Consts({})
    assert effect_const(K, "morph_obj") == 3954
    assert effect_const(K, "morph_word") == 3955


def test_an_unknown_transition_name_fails_loudly():
    with pytest.raises(KeyError, match="door_trapdoor"):
        effect_const(Consts({}), "door_trapdoor")


def test_every_role_has_a_drift_vector_and_none_of_them_is_zero():
    for role, (dx, dy) in DRIFT.items():
        assert abs(dx) + abs(dy) > 0.0, role
        assert abs(dx) <= 30.0 and abs(dy) <= 30.0, role


def test_the_drift_vectors_match_the_effect_vocabulary_table():
    assert DRIFT["cold_open"] == (-22.0, 22.0)   # down-left 22pt
    assert DRIFT["title_lock"] == (18.0, 0.0)    # right 18pt
    assert DRIFT["chapter"] == (0.0, -16.0)      # up 16pt


def test_plan_drift_returns_a_vector_for_every_slide_that_inherits_a_field():
    for spec in expand(TIMING):
        vector = plan_drift(spec)
        if spec.is_b or spec.carry:
            assert vector is not None, spec.idx
        else:
            assert vector is None, spec.idx


def test_plan_drift_is_the_roles_vector():
    for spec in expand(TIMING):
        if spec.inherits_field:
            assert plan_drift(spec) == DRIFT[spec.role]


def test_every_role_that_can_inherit_a_field_has_a_drift_vector():
    roles = {s.role for s in expand(TIMING) if s.inherits_field}
    assert roles <= set(DRIFT), roles - set(DRIFT)


def test_a_drift_moves_the_whole_star_budget_not_just_fifty_six():
    # S2 is "all of AMBIENT[role].stars", read from the frozen table -- the old
    # ">= 56" floor was below four roles' budgets and above none of them
    from deck.geometry import parallax
    from deck.scenes.common import AMBIENT
    for role in sorted(DRIFT):
        budget = AMBIENT[role].stars
        stars = starfield(seed=5, count=budget)
        moved = parallax(stars, *DRIFT[role])
        changed = sum(1 for s, (x, y) in zip(stars, moved) if (x, y) != (s.x, s.y))
        assert changed == budget, (role, changed, budget)


def test_apply_transition_sets_speed_before_duration():
    # the order is the whole point; a regression here loses 30s of runtime and
    # is invisible in the build log
    import inspect

    import deck.transitions as tr
    src = inspect.getsource(tr.apply_transition)
    assert src.index(".Speed") < src.index(".Duration = spec.trans_dur")
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_transitions.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.transitions'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/transitions.py`:

```python
"""Transitions, Morph twins, and the B-twin drift.

A B twin is a duplicate of its A slide (identity pairing beats name pairing),
with its timeline cleared and its persisting objects re-placed rather than
re-created. Meteors are the exception: they are deleted and re-seeded.
"""
from __future__ import annotations

from deck.geometry import parallax

# plan name -> Consts attribute. Nothing else in the deck names a transition.
TRANS_CONST: dict[str, str] = {
    "cut": "ppEffectCut",
    "fade": "ppEffectFadeSmoothly",
    "morph_obj": "ppEffectMorphByObject",
    "morph_word": "ppEffectMorphByWord",
    "morph_char": "ppEffectMorphByChar",
    "door_ripple": "ppEffectRippleCenter",
    "door_window": "ppEffectWindowVertical",
    "iris": "ppEffectCircleOut",
}

MORPH = {"morph_obj", "morph_word", "morph_char"}

# Star drift per role, in points, straight out of the "Morph job on the B twin"
# column of the effect vocabulary table.
DRIFT: dict[str, tuple[float, float]] = {
    "cold_open": (-22.0, 22.0),
    "title": (-14.0, 10.0),
    "title_lock": (18.0, 0.0),
    "thesis": (-8.0, 6.0),
    "breath": (-10.0, 8.0),
    "chapter": (0.0, -16.0),
    "concept": (12.0, -8.0),
    "spike": (0.0, 24.0),
    "afterglow": (20.0, 14.0),
    "proof": (-10.0, -6.0),
    "settle": (-6.0, 4.0),
    "end": (0.0, 26.0),
}


def effect_const(K, trans: str) -> int:
    try:
        attr = TRANS_CONST[trans]
    except KeyError:
        raise KeyError(f"unknown transition {trans!r}; add it to TRANS_CONST "
                       f"(known: {sorted(TRANS_CONST)})") from None
    return getattr(K, attr)


def plan_drift(spec) -> tuple[float, float] | None:
    """The drift a slide owes its inherited star layer, or None.

    Both kinds of Morph target need one: a duplicate B twin (moved in place by
    drift_stars) and a declared carry slide (repainted from geometry.drifted).
    A slide that inherits nothing gets None.
    """
    return DRIFT[spec.role] if spec.inherits_field else None


def retext_meta_index(stage, slide, idx: int, total: int) -> bool:
    """Fix the chrome on a duplicated twin.

    A B twin is a copy, so its !!meta_index_000 still reads the A twin's number.
    Ten slides would show the wrong "NN / 26" without this. Returns False when
    the shape is missing, which build_deck turns into a hard failure rather than
    a silently wrong deck -- the CHROME audit row checks the same thing in the
    saved XML.
    """
    for shape in shapes_by_prefix(slide, "!!meta_index_"):
        stage.retext(shape, f"{idx:02d} / {total}")
        return True
    return False


def apply_transition(slide, spec, K, adv: float) -> None:
    """Transition and auto-advance are one decision, so they are set together."""
    if spec.trans in MORPH and adv < spec.trans_dur:
        raise AssertionError(
            f"slide {spec.idx}: advTm {adv:.2f}s is shorter than its "
            f"{spec.trans} duration {spec.trans_dur:.2f}s -- PowerPoint would "
            f"render this as a cut. Lengthen the hold in timing.json.")
    t = slide.SlideShowTransition
    t.EntryEffect = effect_const(K, spec.trans)
    # ORDER IS LOAD-BEARING: Speed is the legacy 3-step enum and assigning it
    # overwrites Duration with that step's preset. Setting Duration first and
    # Speed second (as an earlier draft did) silently discards every duration in
    # the deck -- the 2.0s Morphs become ~0.75s and the whole 39.5s transition
    # budget, and with it the sync model, is wrong. Speed first, Duration last.
    t.Speed = K.ppTransitionSpeedSlow          # 1, not 3 (root-cause table)
    t.Duration = spec.trans_dur
    t.AdvanceOnClick = False                   # protect the choreography
    t.AdvanceOnTime = True
    t.AdvanceTime = adv
    t.SoundEffect.Name = ""                    # the track is the only audio

    # verify rather than assume: if Duration did not stick, nothing downstream
    # will notice until the deck plays 30s short
    if abs(float(t.Duration) - spec.trans_dur) > 0.05:
        raise AssertionError(
            f"slide {spec.idx}: Duration came back as {float(t.Duration):.2f}s "
            f"after writing {spec.trans_dur:.2f}s -- something re-applied Speed "
            f"afterwards, or this effect does not support a custom duration")


def duplicate_twin(prs, a_index: int):
    """Duplicate slide `a_index` and return the new slide, placed right after it."""
    dup = prs.Slides(a_index).Duplicate()
    slide = dup.Item(1) if hasattr(dup, "Item") else dup
    if slide.SlideIndex != a_index + 1:
        slide.MoveTo(a_index + 1)
    return slide


def clear_timeline(slide) -> int:
    """Delete every effect on the slide. Law 2: a Morphed object gets no driver."""
    seq = slide.TimeLine.MainSequence
    removed = 0
    while seq.Count > 0:
        seq.Item(1).Delete()
        removed += 1
    for i in range(slide.TimeLine.InteractiveSequences.Count, 0, -1):
        slide.TimeLine.InteractiveSequences.Item(i).Delete()
    return removed


def shapes_by_prefix(slide, prefix: str) -> list:
    return [slide.Shapes.Item(i) for i in range(1, slide.Shapes.Count + 1)
            if slide.Shapes.Item(i).Name.startswith(prefix)]


def delete_by_prefix(slide, prefix: str) -> int:
    """Used for the meteor layer: a half-travelled streak cannot be Morph-paired."""
    victims = shapes_by_prefix(slide, prefix)
    for shape in victims:
        shape.Delete()
    return len(victims)


def drift_stars(slide, stars, dx: float, dy: float) -> int:
    """Re-place the star layer by parallax. Returns how many stars moved (S2)."""
    moved = 0
    targets = parallax(stars, dx, dy)
    existing = {s.Name: s for s in shapes_by_prefix(slide, "!!star_")}
    for i, ((x, y), star) in enumerate(zip(targets, stars)):
        shape = existing.get(f"!!star_{i:03d}")
        if shape is None:
            continue
        shape.Left = x - star.size / 2
        shape.Top = y - star.size / 2
        moved += 1
    return moved


def extend_rule(shape, by: float) -> None:
    """The chapter rule growing into a horizon line; Morph interpolates width."""
    shape.Width = shape.Width + by


def grow_iris(shape, factor: float) -> None:
    """D3: the one object in the deck allowed past 106%. Grown about its centre."""
    cx = shape.Left + shape.Width / 2
    cy = shape.Top + shape.Height / 2
    shape.Width *= factor
    shape.Height *= factor
    shape.Left = cx - shape.Width / 2
    shape.Top = cy - shape.Height / 2
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_transitions.py -q`
Expected: `13 passed`

- [ ] **Step 5: Build a real two-slide Morph twin and watch the drift**

This is the single most important smoke test in the plan: if the twin does not drift, the whole spectacle thesis fails. Add a `twin` subcommand to `smoke.py`:

```python
def cmd_twin() -> int:
    from deck.geometry import Rect, meteors, starfield
    from deck.motion import Clock, Motion
    from deck.scenes.common import AMBIENT
    from deck.stage import Caps, Stage
    from deck.sync import SlideSpec
    from deck.transitions import (DRIFT, apply_transition, clear_timeline,
                                  delete_by_prefix, drift_stars, duplicate_twin,
                                  retext_meta_index, shapes_by_prefix)
    app, K = _app()
    caps = Caps.from_notes(ROOT / "COM-NOTES.md")
    prs = _deck(app, K)

    budget = AMBIENT["cold_open"].stars            # 96, from the frozen table
    stars = starfield(11, budget)
    a = prs.Slides.Add(1, K.ppLayoutBlank)
    sa, ma = Stage(a, K, caps), Motion(a, K, caps, Clock())
    sa.field()
    sa.vignette()
    for i, star in enumerate(stars):
        sa.star(star, i)
    ma.twinkle([s for s in sa.shapes if s.Name.startswith("!!star_")][:20])
    for i, m in enumerate(meteors(11, 2, "cold_open")):
        ma.meteor_travel(sa.meteor(m, i), m)
    bar = sa.rule(96, 430, 180)
    ma.wipe(bar, 0.8, 0.0, hero=True)
    sa.text("hero", "信号站", Rect(96, 300, 600, 130))
    sa.chrome(1, 2)
    spec_a = SlideSpec(1, 1, "a", "cold_open", "Cold Grain", "kinetic", 4.0,
                       "cut", 0.0, 0.0, 0.0, False, {})
    apply_transition(a, spec_a, K, 4.0)

    b = duplicate_twin(prs, 1)
    print("cleared effects:", clear_timeline(b))
    print("deleted meteors:", delete_by_prefix(b, "!!meteor_"))
    print("drifted stars:", drift_stars(b, stars, *DRIFT["cold_open"]), "/", budget)
    sb, mb = Stage(b, K, caps), Motion(b, K, caps, Clock())
    print("retexted meta index:", retext_meta_index(sb, b, 2, 2))
    # the ambient reseed: without it the twin's field freezes for its whole hold
    mb.twinkle(shapes_by_prefix(b, "!!star_")[:20])
    for i, m in enumerate(meteors(12, 2, "cold_open")):
        mb.meteor_travel(sb.meteor(m, i + 500), m)
    for rule in shapes_by_prefix(b, "!!rule_"):
        rule.Width = 420
    spec_b = SlideSpec(2, 1, "b", "cold_open", "Cold Grain", "kinetic", 4.0,
                       "morph_obj", 2.0, 0.0, 4.0, True, {})
    apply_transition(b, spec_b, K, 2.0)
    print("B effects (ambient only):", b.TimeLine.MainSequence.Count)
    print("B readable_end:", f"{mb.clock.readable_end:.2f}")
    print("B transition / duration:", b.SlideShowTransition.EntryEffect,
          f"{float(b.SlideShowTransition.Duration):.2f}")
    print("saved:", _save(prs, K, "twin"))
    prs.Close()
    app.Quit()
    return 0
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py twin`

Expected: `cleared effects:` ≥ 25 (the A twin's twinkles and meteors), `deleted meteors: 2`, **`drifted stars: 96 / 96`** (all of the role's budget, not a floor of 56), `retexted meta index: True`, `B effects (ambient only): 24`, **`B readable_end: 0.00`** (every effect on the twin is ambient — a non-zero value means an entrance leaked onto a persisting object), and `B transition / duration: 3954 2.00` — the `2.00` is the Speed/Duration order check from Step 3.

Then open `out/_twin-smoke.pptx` and run the show (F5):

- The two slides must read as **one continuous shot**: the stars slide down-left, the amber bar lengthens, the title does not move or flicker.
- The near (larger) stars must visibly travel further than the far ones. If every star moves the same distance, `parallax` is not being used — check that `drift_stars` got the same `stars` list the A slide was painted from.
- **The twin must still be alive.** Stars keep twinkling and new meteors keep crossing after the Morph settles. If slide 2 goes visually static, the ambient reseed is missing — `clear_timeline` deletes *everything*, so twinkle and breath have to be re-added, not assumed to survive.
- Slide 2's corner must read `02 / 2`. If it still reads `01 / 2`, `retext_meta_index` did not run.
- If the title *re-animates* on slide 2, `clear_timeline` did not run before the duplicate's effects were inspected.
- If the whole slide cross-fades instead of morphing, the duplicate lost identity pairing — confirm `EntryEffect` reads `3954`.

- [ ] **Step 6: Record the twin result in `COM-NOTES.md`**

Under a `## Morph twin smoke` heading, write one line each for: cleared-effect count, drifted-star count against the budget, the reopened transition duration, whether the twin stayed visually alive after the ambient reseed, whether the meta index re-texted, whether near/far parallax was visible, and whether identity pairing held. Task 18 quotes this.

**Accept:** `python -m pytest tests -q` reports ≥ 120 passed, 0 failed; `out/_twin-smoke.pptx` exists and its slide 2 `EntryEffect` is `3954` with `Duration` 2.00; Step 5 reports 96/96 drifted stars, `B readable_end: 0.00` and `retexted meta index: True`; the human checks in Step 5 confirm continuous drift with visible parallax and a twin that is still moving.

---

### Task 10: `deck/audio.py` — an opt-in sound bed behind a rights gate

**This module's first job is to not run.** The default build is silent; `build_deck.py` only reaches `audio.py` when `--audio` is passed. §0.0 item 5 bans reference-work audio, and a plan cannot satisfy a ban with a deviation row, so the ban is enforced by structure: there is **no default source path anywhere in the repo**, and embedding anything requires two independent, explicit human acts.

**The two keys.**

1. `SIGNAL_AUDIO` must point at an existing local file. There is no fallback — if it is unset, `--audio` is an error, not a silent default.
2. `audio.rights.json` must declare `rights`. The three legal values and what each one means:

| `rights` | Meaning | `--audio` | `verify_pptx.py` `RIGHTS` row |
|---|---|---|---|
| `none` (also: file missing or malformed) | Nothing has been declared | **refused** with an error | `PASS` on a silent deck; `FAIL` if a media part somehow exists |
| `personal_preview` | A local-only preview; the operator is not claiming any redistribution right | allowed | **`FAIL`** — this is D6, and it forces exit 1 |
| `licensed` | The operator declares a licence or ownership covering redistribution | allowed | `PASS` |

The tool never writes `licensed` itself and cannot verify a licence; `is_shippable()` is a lookup on a human declaration, nothing more. A reference-work soundtrack can only honestly be `personal_preview`, which is why such a build can never be reported as passing.

`audio.rights.json` holds **no path and no track name**, only the declaration, so the repo never names a protected work:

```json
{
  "rights": "none",
  "declared_by": "",
  "date": "",
  "note": "Set rights to personal_preview for a local-only sync preview, or to licensed only if you hold redistribution rights. The track itself is supplied at build time via the SIGNAL_AUDIO environment variable and is never recorded here."
}
```

**A replacement track must be measured, not assumed.** The 26 `advTm` values describe a 229.5 s timeline. If the operator points `SIGNAL_AUDIO` at a 180 s track, every cue after the first beat is wrong and nothing in the old plan would have noticed. `mount()` therefore reads the true length from `MediaFormat.Length` and refuses a mismatch beyond ±1.5 s; `mp3_duration()` is a pure-python pre-flight so the error arrives before PowerPoint opens.

Beyond the gate, four COM details decide whether playback actually works, and all four are easy to get wrong:

1. **`StopAfterSlides` must be the slide count, not 0.** `0` means "stop at the end of this slide" in some builds.
2. **The speaker icon must be offstage, not hidden.** A shape at `Left = -160` is off-canvas and cannot be Morph-paired or picked up by a cascade; `Visible = False` on a media shape can stop it from playing.
3. **`msoAnimEffectMediaPlay` is 83.** The old script used 150, which is `msoAnimEffectMediaPlayFromBookmark` — a bookmark effect with no bookmark, i.e. silence. This is the most consequential single row in the root-cause table.
4. **`SaveWithDocument=True`** or the pptx references a path that will not exist on anyone else's machine.

**Files:**
- Create: `demos/signal-station-pptx/deck/audio.py`
- Create: `demos/signal-station-pptx/audio.rights.json` (the declaration above, `rights: "none"`)
- Create: `demos/signal-station-pptx/tests/test_audio_paths.py`

**Interfaces:**
- Produces: `RIGHTS_FILE = "audio.rights.json"`, `RIGHTS_VALUES = ("none", "personal_preview", "licensed")`, `TIMELINE_TOLERANCE_SEC = 1.5`, `read_rights(root) -> dict`, `resolve_source(env=None) -> Path` (raises when `SIGNAL_AUDIO` is unset or missing — **no default**), `media_name(source) -> str`, `stage_media(root, env=None) -> Path`, `mp3_duration(path) -> float | None`, `mount(slide, mp3, K, caps, total_slides, expected_sec, motion) -> shape`, `audio_report(copy, source, rights) -> dict`, `is_shippable(rights) -> bool`, `audio_allowed(root) -> tuple[bool, str]`.
- Produces **no** default path, no track name, and no constant containing a file name.
- Consumes: `SIGNAL_AUDIO` env var, `audio.rights.json`, `deck/stage.Caps` (the `AddMediaObject2` row), `deck/motion.Motion.media_play`.
- Consumed by: `build_deck.py` (Task 13 mounts on slide 1 only when `--audio` is passed and the gate opens), `verify_pptx.py` (Task 16's `RIGHTS` row reads the same file), `handover.md` (Task 18 quotes `audio_report`).

- [ ] **Step 1: Write the failing test** (resolution, the rights gate and the duration maths are all pure)

`demos/signal-station-pptx/tests/test_audio_paths.py`:

```python
import json
import re
from pathlib import Path

import pytest

from deck.audio import (
    RIGHTS_FILE, RIGHTS_VALUES, TIMELINE_TOLERANCE_SEC, audio_allowed,
    audio_report, is_shippable, media_name, read_rights, resolve_source,
)

SRC = Path(__file__).resolve().parents[1] / "deck" / "audio.py"


def write_rights(root: Path, value) -> Path:
    path = root / RIGHTS_FILE
    path.write_text(json.dumps({"rights": value}), encoding="utf-8")
    return path


# -- 0.0 item 5: the module must not name a track -------------------------
def test_the_module_names_no_track_and_no_absolute_path():
    src = SRC.read_text(encoding="utf-8")
    assert "DEFAULT_SOURCE" not in src
    # no drive-letter or POSIX-absolute path literal
    assert not re.search(r"""["'][A-Za-z]:[\\/]""", src)
    assert not re.search(r"""["']/[A-Za-z]""", src)
    # the only media extensions allowed are inside the suffix allow-list, which
    # is a tuple of bare extensions -- never a file name
    for literal in re.findall(r"""["']([^"'\n]*\.(?:mp3|wav|m4a))["']""", src):
        assert literal.startswith("."), literal


def test_there_is_no_default_source_constant():
    import deck.audio as audio
    assert not hasattr(audio, "DEFAULT_SOURCE")


# -- resolution ------------------------------------------------------------
def test_resolve_source_requires_the_env_var(monkeypatch):
    monkeypatch.delenv("SIGNAL_AUDIO", raising=False)
    with pytest.raises(RuntimeError, match="SIGNAL_AUDIO"):
        resolve_source()


def test_resolve_source_reads_the_env_var(tmp_path, monkeypatch):
    mine = tmp_path / "mine.mp3"
    mine.write_bytes(b"ID3")
    monkeypatch.setenv("SIGNAL_AUDIO", str(mine))
    assert resolve_source() == mine


def test_resolve_source_reports_a_missing_env_file(tmp_path, monkeypatch):
    monkeypatch.setenv("SIGNAL_AUDIO", str(tmp_path / "nope.mp3"))
    with pytest.raises(FileNotFoundError, match="SIGNAL_AUDIO"):
        resolve_source()


def test_the_embedded_name_is_derived_from_the_source_not_hard_coded():
    assert media_name(Path("d:/music/My Own Score.mp3")) == "my-own-score.mp3"
    assert media_name(Path("/x/bed.wav")) == "bed.wav"


# -- the rights gate -------------------------------------------------------
def test_the_three_declared_values_are_the_only_ones():
    assert RIGHTS_VALUES == ("none", "personal_preview", "licensed")


def test_a_missing_rights_file_reads_as_none(tmp_path):
    assert read_rights(tmp_path)["rights"] == "none"


def test_a_malformed_rights_file_reads_as_none_rather_than_crashing(tmp_path):
    (tmp_path / RIGHTS_FILE).write_text("{not json", encoding="utf-8")
    assert read_rights(tmp_path)["rights"] == "none"


def test_an_unknown_rights_value_reads_as_none(tmp_path):
    write_rights(tmp_path, "probably_fine")
    assert read_rights(tmp_path)["rights"] == "none"


@pytest.mark.parametrize("value,allowed", [
    ("none", False), ("personal_preview", True), ("licensed", True),
])
def test_audio_is_refused_until_rights_are_declared(tmp_path, value, allowed):
    write_rights(tmp_path, value)
    ok, reason = audio_allowed(tmp_path)
    assert ok is allowed
    if not ok:
        assert RIGHTS_FILE in reason


def test_only_licensed_is_shippable():
    assert is_shippable("licensed") is True
    assert is_shippable("personal_preview") is False
    assert is_shippable("none") is False


def test_audio_report_calls_personal_preview_a_delivery_blocker(tmp_path):
    copy = tmp_path / "bed.mp3"
    copy.write_bytes(b"ID3" + b"\x00" * 64)
    report = audio_report(copy, Path("d:/music/whatever.mp3"), "personal_preview")
    assert report["shippable"] is False
    assert "D6" in report["note"]
    assert report["bytes"] == 67
    assert report["rights"] == "personal_preview"
    assert report["embedded_name"] == "bed.mp3"
    # the report quotes the source the operator chose, but the plan/repo never
    # hard-codes one
    assert report["source"] == "d:/music/whatever.mp3"


def test_audio_report_marks_a_licensed_track_shippable(tmp_path):
    copy = tmp_path / "bed.mp3"
    copy.write_bytes(b"ID3")
    report = audio_report(copy, Path("d:/music/mine.mp3"), "licensed")
    assert report["shippable"] is True
    assert "licence" in report["note"] or "license" in report["note"]


# -- duration --------------------------------------------------------------
def test_the_timeline_tolerance_is_tight_enough_to_catch_a_wrong_track():
    assert TIMELINE_TOLERANCE_SEC == 1.5


def test_mp3_duration_returns_none_for_a_file_it_cannot_parse(tmp_path):
    from deck.audio import mp3_duration
    junk = tmp_path / "x.mp3"
    junk.write_bytes(b"not an mp3 at all")
    assert mp3_duration(junk) is None


def test_mp3_duration_reads_a_constant_bitrate_frame_header(tmp_path):
    from deck.audio import mp3_duration
    # MPEG1 Layer III, 128kbps, 44.1kHz, no padding -> 417-byte frames,
    # 1152 samples each => 100 frames is 1152*100/44100 = 2.612s
    header = bytes([0xFF, 0xFB, 0x90, 0x00])
    frame = header + b"\x00" * 413
    path = tmp_path / "cbr.mp3"
    path.write_bytes(frame * 100)
    assert mp3_duration(path) == pytest.approx(2.612, abs=0.05)
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_audio_paths.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.audio'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/deck/audio.py`:

```python
"""Opt-in embedded audio that plays across all 26 slides.

Nothing here runs on a default build. There is deliberately NO default source
path and no track name in this file: 0.0 item 5 bans reference-work audio, and
the only way to keep that true is to make the repo incapable of naming a track.
Both keys -- SIGNAL_AUDIO and audio.rights.json -- are supplied by a human.

The deck is open-loop: one track starts on slide 1 and every advTm is measured
against it. If the media re-triggers per slide, or if the track is not the
length the timeline was written for, the sync model is void -- so mount()
verifies PlaySettings and the real duration after it writes them.
"""
from __future__ import annotations

import json
import os
import shutil
from pathlib import Path

RIGHTS_FILE = "audio.rights.json"
RIGHTS_VALUES = ("none", "personal_preview", "licensed")
TIMELINE_TOLERANCE_SEC = 1.5
_SUFFIXES = (".mp3", ".wav", ".m4a")


def resolve_source(env: str | None = None) -> Path:
    """The operator's track, from SIGNAL_AUDIO. There is no default."""
    raw = env if env is not None else os.environ.get("SIGNAL_AUDIO")
    if not raw:
        raise RuntimeError(
            "SIGNAL_AUDIO is not set. The default build is silent on purpose; "
            "there is no bundled or default track. Either drop --audio, or set "
            "SIGNAL_AUDIO to a local file you have the right to use.")
    path = Path(raw)
    if not path.is_file():
        raise FileNotFoundError(
            f"SIGNAL_AUDIO points at {path}, which does not exist")
    if path.suffix.lower() not in _SUFFIXES:
        raise ValueError(f"unsupported audio type {path.suffix!r}; "
                         f"expected one of {_SUFFIXES}")
    return path


def media_name(source: Path) -> str:
    """A tidy, path-free embedded name derived from the operator's file."""
    stem = "-".join(Path(source).stem.lower().split())
    return f"{stem}{Path(source).suffix.lower()}"


def read_rights(root: Path) -> dict:
    """The declaration, defaulting hard to `none`.

    Missing, unreadable, malformed and unrecognised all mean `none`. A rights
    gate that fails open is not a gate.
    """
    path = Path(root) / RIGHTS_FILE
    data: dict = {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        data = {}
    value = data.get("rights")
    if value not in RIGHTS_VALUES:
        value = "none"
    return {
        "rights": value,
        "declared_by": data.get("declared_by", ""),
        "date": data.get("date", ""),
        "note": data.get("note", ""),
        "file": str(path),
    }


def audio_allowed(root: Path) -> tuple[bool, str]:
    """(may we embed, why not). build_deck.py refuses --audio on False."""
    rights = read_rights(root)["rights"]
    if rights == "none":
        return False, (
            f"--audio requires a declaration in {RIGHTS_FILE}: set \"rights\" to "
            f"\"personal_preview\" for a local-only preview, or \"licensed\" if "
            f"you hold redistribution rights. Note that \"personal_preview\" "
            f"makes verify_pptx.py fail its RIGHTS row by design (D6).")
    return True, rights


def is_shippable(rights: str) -> bool:
    """True only for an explicit `licensed` declaration (D6).

    This is a lookup on a human claim. The tool cannot verify a licence and
    never writes `licensed` by itself.
    """
    return rights == "licensed"


def mp3_duration(path: Path) -> float | None:
    """Seconds, from the MP3 frame headers. None when it cannot be determined.

    A pure-python pre-flight so a wrong-length track is rejected before
    PowerPoint opens. mount() still re-checks against MediaFormat.Length, which
    is authoritative and also covers wav/m4a.
    """
    rates = {3: (44100, 48000, 32000), 2: (22050, 24000, 16000),
             0: (11025, 12000, 8000)}
    bitrates_v1_l3 = (None, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192,
                      224, 256, 320, None)
    data = Path(path).read_bytes()
    i = 0
    if data[:3] == b"ID3" and len(data) > 10:
        # ID3v2 size is a 28-bit synchsafe integer
        i = 10 + ((data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9])
    frames, seconds = 0, 0.0
    while i + 4 <= len(data):
        if data[i] != 0xFF or (data[i + 1] & 0xE0) != 0xE0:
            i += 1
            continue
        version = (data[i + 1] >> 3) & 0x03
        layer = (data[i + 1] >> 1) & 0x03
        bitrate = bitrates_v1_l3[(data[i + 2] >> 4) & 0x0F]
        rate_index = (data[i + 2] >> 2) & 0x03
        padding = (data[i + 2] >> 1) & 0x01
        if version not in rates or layer != 1 or bitrate is None or rate_index == 3:
            i += 1
            continue
        rate = rates[version][rate_index]
        samples = 1152 if version == 3 else 576
        length = int(samples // 8 * bitrate * 1000 // rate) + padding
        if length <= 4:
            i += 1
            continue
        frames += 1
        seconds += samples / rate
        i += length
    return round(seconds, 3) if frames >= 10 else None


def stage_media(root: Path, env: str | None = None) -> Path:
    """Copy the operator's track into media/ (gitignored). Returns the copy."""
    source = resolve_source(env)
    allowed, reason = audio_allowed(root)
    if not allowed:
        raise RuntimeError(reason)
    media = Path(root) / "media"
    media.mkdir(parents=True, exist_ok=True)
    dest = media / media_name(source)
    if not dest.is_file() or dest.stat().st_size != source.stat().st_size:
        shutil.copy2(source, dest)
    return dest


def audio_report(copy: Path, source: Path, rights: str) -> dict:
    shippable = is_shippable(rights)
    if rights == "personal_preview":
        note = ("D6: declared personal_preview -- a local preview only. "
                "verify_pptx.py's RIGHTS row fails and the build exits 1. This "
                "file must not be delivered; 7.1 row 7 is unmet.")
    elif shippable:
        note = ("declared licensed by the operator; this tool cannot verify a "
                "licence, so the declaration is the only evidence.")
    else:
        note = (f"no declaration in {RIGHTS_FILE}; audio should not have been "
                f"mounted at all.")
    return {
        "source": str(source),
        "embedded_name": Path(copy).name,
        "bytes": Path(copy).stat().st_size,
        "rights": rights,
        "shippable": shippable,
        "note": note,
    }


def mount(slide, mp3: Path, K, caps, total_slides: int,
          expected_sec: float, motion=None):
    """Put the track on slide 1, offstage, playing across the whole deck."""
    if not caps.media:
        raise RuntimeError(
            "AddMediaObject2 is reported FAIL/SKIP in COM-NOTES.md; set "
            "SIGNAL_AUDIO and re-run probe_com.py, or drop --audio to build "
            "the silent default")
    measured = mp3_duration(mp3)
    if measured is not None and abs(measured - expected_sec) > TIMELINE_TOLERANCE_SEC:
        raise AssertionError(
            f"{Path(mp3).name} is {measured:.1f}s but timing.json's 26 advTm "
            f"values describe a {expected_sec:.1f}s timeline (tolerance "
            f"+-{TIMELINE_TOLERANCE_SEC}s). Every cue after beat 1 would be "
            f"wrong. Either supply a track of the right length or re-author "
            f"timing.json's beats for this one -- do not ship the mismatch.")
    snd = slide.Shapes.AddMediaObject2(str(Path(mp3).resolve()),
                                       LinkToFile=False, SaveWithDocument=True,
                                       Left=-160, Top=-160, Width=24, Height=24)
    snd.Name = "!!audio_000"
    ps = snd.AnimationSettings.PlaySettings
    ps.PlayOnEntry = True
    ps.HideWhileNotPlaying = True        # offstage, but never Visible = False
    ps.PauseAnimation = False
    ps.StopAfterSlides = int(total_slides)
    ps.LoopUntilStopped = False
    ps.RewindMovie = False
    if motion is not None:
        motion.media_play(snd)           # effect 83, not 150

    # verify rather than assume: a silently ignored StopAfterSlides voids sync
    if int(ps.StopAfterSlides) != int(total_slides):
        raise AssertionError(
            f"PlaySettings.StopAfterSlides came back as {ps.StopAfterSlides}, "
            f"expected {total_slides}; this PowerPoint will restart the track "
            f"per slide and the deck cannot stay in sync")

    # MediaFormat.Length is authoritative and covers wav/m4a, which
    # mp3_duration() cannot read
    try:
        com_sec = float(snd.MediaFormat.Length) / 1000.0
    except Exception:
        com_sec = None
    if com_sec and abs(com_sec - expected_sec) > TIMELINE_TOLERANCE_SEC:
        raise AssertionError(
            f"PowerPoint reports the embedded track as {com_sec:.1f}s but the "
            f"timeline is {expected_sec:.1f}s (+-{TIMELINE_TOLERANCE_SEC}s). "
            f"Supply a track of the right length or re-author timing.json.")
    return snd
```

- [ ] **Step 4: Run the test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_audio_paths.py -q`
Expected: `20 passed`

- [ ] **Step 5: Prove the gate is shut by default**

This is the step that makes §0.0 item 5 true rather than aspirational. Run all three before touching audio at all:

Run: `Set-Location demos/signal-station-pptx; python -c "from pathlib import Path; from deck.audio import audio_allowed; print(audio_allowed(Path('.')))"`
Expected: `(False, '--audio requires a declaration in audio.rights.json: …')` — `audio.rights.json` ships with `rights: "none"`.

Run: `Set-Location demos/signal-station-pptx; $env:SIGNAL_AUDIO=$null; python -c "from deck.audio import resolve_source; resolve_source()"`
Expected: `RuntimeError: SIGNAL_AUDIO is not set. The default build is silent on purpose…`

Run: `Set-Location demos/signal-station-pptx; Select-String -Pattern "\.mp3|\.wav|\.m4a" -Path deck/*.py,*.py,*.json | Select-String -NotMatch "_SUFFIXES|suffix"`
Expected: no output. If any file names a track, delete the name — the whole gate rests on the repo being unable to point at one.

- [ ] **Step 6: Only if the operator opts in — stage a track and prove playback**

**Skip this step entirely for a silent build.** It requires a human to (a) set `SIGNAL_AUDIO` to a file they have the right to use and (b) edit `audio.rights.json`. If the track is from a reference work, the only honest value is `personal_preview`, and the build is then a local preview that `verify_pptx.py` will fail — that is D6 working, not a bug to route around.

Add an `audio` subcommand to `smoke.py`:

```python
def cmd_audio() -> int:
    import json
    from deck.audio import (audio_allowed, audio_report, mount, read_rights,
                            resolve_source, stage_media)
    from deck.motion import Clock, Motion
    from deck.stage import Caps, Stage
    allowed, reason = audio_allowed(ROOT)
    if not allowed:
        print(reason)
        return 3                       # the gate is shut; nothing was read
    app, K = _app()
    caps = Caps.from_notes(ROOT / "COM-NOTES.md")
    track = stage_media(ROOT)
    prs = _deck(app, K)
    for i in range(1, 4):
        slide = prs.Slides.Add(i, K.ppLayoutBlank)
        Stage(slide, K, caps).field()
        t = slide.SlideShowTransition
        t.EntryEffect = K.ppEffectFadeSmoothly
        t.Speed = K.ppTransitionSpeedSlow
        t.Duration = 1.0
        t.AdvanceOnClick = False
        t.AdvanceOnTime = True
        t.AdvanceTime = 4.0
    first = prs.Slides(1)
    # expected_sec is the *real* track length here, because this scratch deck is
    # 3 slides, not the 229.5s timeline
    from deck.audio import mp3_duration
    snd = mount(first, track, K, caps, total_slides=3,
                expected_sec=mp3_duration(track) or 0.0,
                motion=Motion(first, K, caps, Clock()))
    ps = snd.AnimationSettings.PlaySettings
    print("measured length:", mp3_duration(track))
    print("stop after slides:", ps.StopAfterSlides)
    print("offstage at:", snd.Left, snd.Top)
    path = _save(prs, K, "audio")
    prs.Close()
    reopened = app.Presentations.Open(str(path), WithWindow=False)
    ps2 = reopened.Slides(1).Shapes("!!audio_000").AnimationSettings.PlaySettings
    print("after reopen -> play on entry:", ps2.PlayOnEntry,
          "stop after:", ps2.StopAfterSlides)
    reopened.Close()
    app.Quit()
    print(json.dumps(audio_report(track, resolve_source(),
                                  read_rights(ROOT)["rights"]),
                     ensure_ascii=False, indent=2))
    return 0
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py audio`

Expected with the gate shut (the default): exit code 3 and the refusal message, having read no file.

Expected with the gate open: `stop after slides: 3`, `offstage at: -160.0 -160.0`, after reopen `play on entry: True stop after: 3`, and a report whose `"rights"` matches the declaration. If `rights` is `personal_preview`, `"shippable"` must print `false` with the D6 note — and Task 15/16 will exit 1. That is correct.

Then re-run `python probe_com.py` (its `media` row flips from `SKIP` to `OK` once `SIGNAL_AUDIO` is set), open `out/_audio-smoke.pptx`, press F5 and listen: the track must keep playing continuously across all three slides and must **not** restart on slides 2 and 3.

Run: `Set-Location demos/signal-station-pptx; git status --porcelain media`
Expected: empty output. If the audio file shows up, stop and fix `.gitignore` before anything else; §Global Constraints forbids committing it.

- [ ] **Step 7: Record the audio rows in `COM-NOTES.md`**

Under a `## Audio` heading, write the honest state in three lines: the `audio.rights.json` value in force, whether `AddMediaObject2` was exercised at all (`SKIP` is the expected answer for a silent build), and — only if Step 6 ran — continuity across 3 slides, the reopened `StopAfterSlides`, the measured track length and the embedded size.

**Accept:** `python -m pytest tests -q` reports ≥ 140 passed, 0 failed; `audio.rights.json` exists with `"rights": "none"`; the Step 5 `Select-String` finds no track name anywhere in the package; `python smoke.py audio` exits 3 with the refusal message on a default checkout; `git status --porcelain media` is empty. Step 6 is only accepted if a human opted in, and then its rights value is recorded in `COM-NOTES.md`.

---

### Task 11: `deck/scenes/common.py` + `identity.py` + `editorial.py` — the first seven slide classes

Scenes are the only place that decides what a slide *looks* like, and they may not call COM directly: a scene talks to `Stage` and `Motion` and nothing else. That is what keeps the capability branching (Task 7) and the readability clock (Task 8) from being bypassed 26 times over.

**Every role module exports the same pair:**

- `build(ctx) -> SlideBuild` — paints an A or solo slide, including its entrances.
- `morph(ctx, prev) -> SlideBuild` — adjusts an already-duplicated B slide whose timeline is cleared. It may **move, resize and recolour**, and it **must** call `reseed_ambient(ctx, prev)` to put the twinkle, the breath and a fresh meteor set back (the clear wiped them). It may add no other effects. One entrance on a persisting object here is a defect (§Step 2 law 2); the single sanctioned exception is `spike`'s type *exit* under the iris.

The density numbers live in **`AMBIENT` in `common.py` — the §Frozen density table, and the only copy in the system**. `verify_pptx.py` imports it rather than hard-coding a band, which is what stops S2/S11 from drifting away from what the scenes actually paint. `common.py` also derives `star_budget()` and `expected_shapes()` from it, so a scene cannot quietly inflate a count: the audit asserts the exact predicted number per slide.

**Files:**
- Create: `demos/signal-station-pptx/deck/scenes/common.py`
- Create: `demos/signal-station-pptx/deck/scenes/identity.py`
- Create: `demos/signal-station-pptx/deck/scenes/editorial.py`
- Create: `demos/signal-station-pptx/tests/test_scenes_contract.py`

**Interfaces:**
- Produces: `SceneContext` (dataclass: `prs`, `slide`, `spec`, `K`, `caps`, `stage`, `motion`, `stars`, `seed`, `total`, `fast`), `SlideBuild` (dataclass: `stars`, `meteor_count`, `handles: dict[str, object]`, `notes: list[str]`), `AMBIENT: dict[str, AmbientSpec]` (the frozen table, with a `fixtures` column), `B_EXTRA_FIXTURES`, `star_budget(spec, prev_stars, fast)`, `expected_shapes(spec, prev_stars, fast, with_audio)`, `ambient_layer(ctx) -> SlideBuild`, `reseed_ambient(ctx, prev, breath=None) -> (int, list[str])`, `chrome(ctx)`, `scaled(n, fast)`, and the seven builders `cold_open`, `title`, `title_lock`, `thesis`, `breath`, `concept`, `settle` — each as a `(build, morph)` pair.
- Consumes: `deck/stage.Stage`, `deck/motion.Motion`, `deck/geometry`, `deck/palette`, `deck/transitions` (the B-twin helpers).
- Consumed by: `deck/scenes/__init__.py` (Task 12).

- [ ] **Step 1: Write the failing contract test**

The contract — not the pixels — is what a unit test can hold. `tests/test_scenes_contract.py` drives every builder against a **fake Stage/Motion pair** that records calls, so the laws are checked with no Office at all.

`demos/signal-station-pptx/tests/test_scenes_contract.py`:

```python
import json
from pathlib import Path

import pytest

from deck.geometry import starfield, zone_rect
from deck.palette import TYPE
from deck.scenes.common import (
    AMBIENT, SceneContext, expected_shapes, scaled, star_budget,
)
from deck.sync import expand

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))


class FakeShape:
    """A shape with real attribute semantics -- deliberately no __getattr__.

    The earlier magic version returned a FakeShape for *any* attribute, so
    `slide.Shapes.Count` came back as a FakeShape, `range(1, Count + 1)` raised
    TypeError, and every B-twin test died inside `shapes_by_prefix` before it
    could check anything. The B-twin laws were therefore completely unverified.
    """

    def __init__(self, name="?", left=0.0, top=0.0, width=10.0, height=10.0):
        self.Name = name
        self.Left, self.Top = left, top
        self.Width, self.Height = width, height
        self.Rotation = 0.0
        self.text = ""
        self.deleted = False
        self.Adjustments = FakeAdjustments()

    def Delete(self):
        self.deleted = True


class FakeAdjustments:
    def __init__(self):
        self.items = {}

    def SetItem(self, i, value):
        self.items[i] = value


class FakeShapes:
    """A 1-based COM-style collection, which is what the helpers walk."""

    def __init__(self, shapes):
        self._shapes = shapes

    @property
    def Count(self):
        return len(self._shapes)

    def Item(self, i):
        return self._shapes[i - 1]


class FakeSlide:
    """Enough slide for transitions.shapes_by_prefix / delete_by_prefix."""

    def __init__(self, shapes=None):
        self._shapes = list(shapes or [])

    @property
    def Shapes(self):
        return FakeShapes([s for s in self._shapes if not s.deleted])

    def add(self, shape):
        self._shapes.append(shape)
        return shape


class RecordingStage:
    """Stands in for deck.stage.Stage; records what a scene asked for.

    Shapes it creates are also pushed onto the FakeSlide with their real names,
    so a later morph() finds its persisting objects by `!!` prefix exactly the
    way the production B-twin path does.
    """

    MULTI = {"glow_stack": 4, "field": 2, "vignette": 4}
    PREFIX = {"glow_stack": "glow", "rule": "rule", "oval": "trail",
              "donut": "iris_core", "ghost_latin": "ghost", "text": "title",
              "field": "field", "vignette": "vignette"}

    def __init__(self, slide):
        self.slide = slide
        self.calls = []
        self.shapes = []
        self._n = 0

    def _make(self, kind, name=None):
        self._n += 1
        shape = FakeShape(name or f"!!{kind}_{self._n:03d}",
                          left=float(self._n), top=float(self._n))
        self.shapes.append(shape)
        self.slide.add(shape)
        return shape

    def retext(self, shape, body):
        self.calls.append(("retext", (shape, body), {}))
        shape.text = body
        return shape

    def __getattr__(self, name):
        def record(*args, **kw):
            self.calls.append((name, args, kw))
            if name == "chrome":
                return [self._make("meta_index", "!!meta_index_000"),
                        self._make("meta_signal", "!!meta_signal_000")]
            if name in self.MULTI:
                kind = self.PREFIX.get(name, name)
                return [self._make(kind) for _ in range(self.MULTI[name])]
            if name in ("star", "meteor"):
                index = args[1] if len(args) > 1 else kw.get("index", 0)
                return self._make(name, f"!!{name}_{index:03d}")
            return self._make(self.PREFIX.get(name, name), kw.get("name"))
        return record


class RecordingMotion:
    AMBIENT_HELPERS = ("travel", "meteor_travel", "breath", "twinkle", "dim",
                       "spin_slow")

    def __init__(self, clock):
        self.clock = clock
        self.calls = []

    def __getattr__(self, name):
        def record(*args, **kw):
            self.calls.append((name, args, kw))
            delay = kw.get("delay", 0.0)
            duration = kw.get("duration", kw.get("dur", 0.5))
            if not isinstance(duration, (int, float)):
                duration = 0.5
            # an explicit ambient= kwarg wins -- that is how an ambient exit
            # fade declares itself, and the clock must honour it
            ambient = kw.get("ambient", name in self.AMBIENT_HELPERS)
            self.clock.mark(delay + duration, ambient=ambient,
                            hero=kw.get("hero", False))
            return FakeShape("effect")
        return record

    def cascade(self, shapes, helper, step, start=0.0, **kw):
        return [helper(s, delay=round(start + i * step, 3), **kw)
                for i, s in enumerate(shapes)]


def make_ctx(spec, fast=False, slide=None, stars=None):
    from deck.motion import Clock
    slide = slide if slide is not None else FakeSlide()
    stage = RecordingStage(slide)
    motion = RecordingMotion(Clock())
    if stars is None:
        stars = starfield(spec.idx, scaled(AMBIENT[spec.role].stars, fast))
    return SceneContext(prs=None, slide=slide, spec=spec, K=None,
                        caps=None, stage=stage, motion=motion, stars=stars,
                        seed=spec.idx, total=26, fast=fast)


def scene_for(role):
    from deck.scenes import SCENES
    return SCENES[role]


SPECS = {s.role: s for s in expand(TIMING)}
A_SPECS = {s.role: s for s in expand(TIMING) if not s.is_b}
B_SPECS = {s.role: s for s in expand(TIMING) if s.is_b}


def built_twin(role):
    """Run the A builder, then hand the SAME FakeSlide to the B builder.

    This is what makes the B-twin laws testable at all: morph() resolves its
    persisting objects by `!!` prefix on the slide the A builder painted, and it
    re-seeds the ambient layer from the star shapes it finds there.
    """
    slide = FakeSlide()
    a_ctx = make_ctx(A_SPECS[role], slide=slide)
    prev = scene_for(role).build(a_ctx)
    b_ctx = make_ctx(B_SPECS[role], slide=slide, stars=prev.stars)
    return a_ctx, prev, b_ctx


@pytest.mark.parametrize("role", ["cold_open", "title", "title_lock", "thesis",
                                  "breath", "concept", "settle"])
def test_every_a_builder_returns_a_slide_build_with_its_star_layer(role):
    spec = A_SPECS[role]
    ctx = make_ctx(spec)
    build = scene_for(role).build(ctx)
    assert len(build.stars) == AMBIENT[role].stars
    assert build.meteor_count == AMBIENT[role].meteors


@pytest.mark.parametrize("role", ["cold_open", "title", "title_lock", "thesis",
                                  "breath", "concept", "settle"])
def test_every_a_builder_paints_the_whole_stack_in_order(role):
    ctx = make_ctx(A_SPECS[role])
    scene_for(role).build(ctx)
    kinds = [c[0] for c in ctx.stage.calls]
    assert kinds.index("field") < kinds.index("vignette") < kinds.index("star")
    assert "chrome" in kinds
    assert kinds.index("star") < kinds.index("chrome")


@pytest.mark.parametrize("role", ["cold_open", "title", "title_lock", "thesis",
                                  "breath", "concept", "settle"])
def test_every_a_builder_stays_inside_the_readability_clock(role):
    spec = A_SPECS[role]
    ctx = make_ctx(spec)
    scene_for(role).build(ctx)
    assert ctx.motion.clock.check(spec.hold, spec.hold - spec.trans_dur,
                                  f"{role}") == []


@pytest.mark.parametrize("role", ["cold_open", "title", "title_lock"])
def test_kinetic_roles_declare_a_hero_and_land_it_by_1_5s(role):
    ctx = make_ctx(A_SPECS[role])
    scene_for(role).build(ctx)
    assert 0.0 < ctx.motion.clock.hero_end <= 1.5


B_ROLES = ["cold_open", "title", "title_lock", "chapter", "concept", "spike",
           "afterglow", "proof", "end"]


@pytest.mark.parametrize("role", B_ROLES)
def test_b_builders_add_no_entrance_effect_to_a_persisting_object(role):
    _, prev, ctx = built_twin(role)
    scene_for(role).morph(ctx, prev)
    entrances = {"fade", "wipe", "faded_zoom", "rise_up", "appear"}
    offenders = [c for c in ctx.motion.calls
                 if c[0] in entrances and not c[2].get("exit_", False)]
    assert offenders == [], offenders


@pytest.mark.parametrize("role", B_ROLES)
def test_b_builders_reseed_the_meteor_layer_rather_than_pairing_it(role):
    _, prev, ctx = built_twin(role)
    build = scene_for(role).morph(ctx, prev)
    assert any(n == "meteor" for n, _, _ in ctx.stage.calls)
    assert any("meteors reseed" in note for note in build.notes)


@pytest.mark.parametrize("role", B_ROLES)
def test_b_builders_reseed_the_twinkle_so_the_twin_does_not_freeze(role):
    # clear_timeline() deletes EVERY effect; without an explicit reseed the
    # twin holds a dead starfield for its whole hold
    _, prev, ctx = built_twin(role)
    build = scene_for(role).morph(ctx, prev)
    assert any(c[0] == "twinkle" for c in ctx.motion.calls), role
    assert any("twinkle reseed" in note for note in build.notes), role


@pytest.mark.parametrize("role", B_ROLES)
def test_b_builders_keep_everything_they_add_on_the_ambient_clock(role):
    _, prev, ctx = built_twin(role)
    scene_for(role).morph(ctx, prev)
    if role == "spike":
        # the only readable event allowed on a B twin: the type exiting under
        # the iris. Everything else must be ambient.
        assert ctx.motion.clock.readable_end > 0.0
        return
    assert ctx.motion.clock.readable_end == 0.0, role
    assert ctx.motion.clock.ambient_end > 0.0, role


@pytest.mark.parametrize("role", B_ROLES)
def test_b_builders_never_add_a_second_breath(role):
    _, prev, ctx = built_twin(role)
    scene_for(role).morph(ctx, prev)
    assert sum(1 for c in ctx.motion.calls if c[0] == "breath") <= 1, role


@pytest.mark.parametrize("role", ["afterglow", "end"])
def test_a_dimming_glow_does_not_also_breathe(role):
    # two ambient drivers on one object is still a law-2 failure, and it would
    # spend S9's single breath on a disc that is supposed to be fading out
    _, prev, ctx = built_twin(role)
    scene_for(role).morph(ctx, prev)
    kinds = [c[0] for c in ctx.motion.calls]
    assert "dim" in kinds
    assert "breath" not in kinds


def test_the_breath_plate_has_no_readable_entrance_beyond_its_meta_mark():
    spec = A_SPECS["breath"]
    ctx = make_ctx(spec)
    scene_for("breath").build(ctx)
    readable = [c for c in ctx.motion.calls
                if c[0] in ("fade", "wipe", "faded_zoom", "rise_up")
                and not c[2].get("exit_", False)
                and not c[2].get("ambient", False)]
    assert len(readable) <= 1


def test_the_wordless_plate_paints_no_type_above_the_meta_ceiling():
    # S13 waives "every slide has a >=18pt run" for this plate, so the plate
    # must actually be wordless -- otherwise the waiver is hiding real type
    from deck.palette import META_CEIL, TYPE
    spec = A_SPECS["breath"]
    assert spec.wordless is True
    ctx = make_ctx(spec)
    scene_for("breath").build(ctx)
    keys = [c[1][0] for c in ctx.stage.calls if c[0] == "text"]
    assert keys, "the plate still needs its meta mark"
    for key in keys:
        assert TYPE[key].size <= META_CEIL, (key, TYPE[key].size)


def test_no_other_slide_relies_on_the_wordless_waiver():
    assert [s.idx for s in expand(TIMING) if s.wordless] == [8]


def test_at_most_one_breath_per_slide_s9():
    from deck.scenes import SCENES
    for role in AMBIENT:
        if role not in SCENES:
            continue
        spec = A_SPECS.get(role) or SPECS[role]
        ctx = make_ctx(spec)
        SCENES[role].build(ctx)
        assert sum(1 for c in ctx.motion.calls if c[0] == "breath") <= 1, role


def test_a_builders_never_charge_an_ambient_effect_to_the_readable_clock():
    # the meteor-exit bug class: an ambient particle fade must not become the
    # slide's readable deadline, on any role
    from deck.scenes import SCENES
    for role, scene in SCENES.items():
        spec = A_SPECS.get(role) or SPECS[role]
        ctx = make_ctx(spec)
        scene.build(ctx)
        ambient_marks = [c for c in ctx.motion.calls
                         if c[2].get("ambient") or c[0] in ctx.motion.AMBIENT_HELPERS]
        assert ambient_marks, role
        assert ctx.motion.clock.readable_end <= 5.0, (role, ctx.motion.clock.readable_end)


def test_fast_mode_halves_the_particle_counts():
    assert scaled(56, fast=True) == 28
    assert scaled(56, fast=False) == 56
    assert scaled(3, fast=True) == 2  # never below 2, or there is no shower


def test_ambient_table_matches_the_frozen_density_table():
    assert AMBIENT["cold_open"] == (96, 2, False, 20, 11)
    assert AMBIENT["title"] == (88, 3, False, 18, 23)
    assert AMBIENT["title_lock"] == (88, 2, True, 18, 14)
    assert AMBIENT["thesis"] == (92, 1, False, 18, 10)
    assert AMBIENT["breath"] == (120, 4, True, 30, 13)
    assert AMBIENT["chapter"] == (96, 2, False, 18, 11)
    assert AMBIENT["concept"] == (92, 2, True, 16, 21)
    assert AMBIENT["spike"] == (112, 3, False, 20, 15)
    assert AMBIENT["afterglow"] == (84, 1, True, 14, 14)
    assert AMBIENT["proof"] == (80, 1, False, 14, 19)
    assert AMBIENT["settle"] == (88, 1, False, 16, 10)
    assert AMBIENT["end"] == (88, 2, True, 22, 15)


def test_every_role_in_the_slide_map_has_an_ambient_row():
    assert {s.role for s in expand(TIMING)} <= set(AMBIENT)


def test_every_star_budget_is_large_enough_for_s2():
    # S2 asks for the whole budget to be displaced; the old ">= 56" floor sat
    # above four of these, which is how the two rows contradicted each other
    for role, row in AMBIENT.items():
        assert row.stars >= 56, role
        assert row.twinkle <= row.stars, role


def test_expected_shapes_matches_the_frozen_tables_per_slide_column():
    from deck.scenes.common import expected_shapes, star_budget
    want = {1: 109, 2: 109, 3: 114, 4: 114, 5: 104, 6: 104, 7: 103, 8: 137,
            9: 109, 10: 109, 11: 115, 12: 117, 13: 115, 14: 109, 15: 115,
            16: 117, 17: 130, 18: 130, 19: 99, 20: 99, 21: 100, 22: 100,
            23: 99, 24: 99, 25: 105, 26: 105}
    prev_stars = None
    got = {}
    for spec in expand(TIMING):
        got[spec.idx] = expected_shapes(spec, prev_stars=prev_stars)
        prev_stars = [None] * star_budget(spec, prev_stars)
    assert got == want


def test_the_deck_totals_2867_shapes_and_every_slide_sits_in_the_s11_band():
    from deck.scenes.common import expected_shapes, star_budget
    prev_stars = None
    total = 0
    for spec in expand(TIMING):
        n = expected_shapes(spec, prev_stars=prev_stars)
        assert 95 <= n <= 150, (spec.idx, n)
        total += n
        prev_stars = [None] * star_budget(spec, prev_stars)
    assert total == 2867


def test_the_two_carry_slides_inherit_their_predecessors_star_count():
    from deck.scenes.common import star_budget
    prev_stars = None
    counts = {}
    for spec in expand(TIMING):
        counts[spec.idx] = star_budget(spec, prev_stars)
        prev_stars = [None] * counts[spec.idx]
    assert counts[5] == counts[4] == 88     # carry out of the title beat
    assert counts[25] == counts[24] == 88   # carry out of the settle beat


def test_the_predicted_motion_paths_land_where_s3_expects():
    specs = expand(TIMING)
    paths = sum(AMBIENT[s.role].meteors for s in specs)
    assert paths == 51
    assert all(AMBIENT[s.role].meteors >= 1 for s in specs)  # D5: all 26 slides


def test_scenes_only_use_declared_type_keys():
    import deck.scenes.editorial as editorial
    import deck.scenes.identity as identity
    for module in (identity, editorial):
        src = Path(module.__file__).read_text(encoding="utf-8")
        for key in set(__import__("re").findall(r"\.text\(\s*[\"']([a-z_]+)[\"']", src)):
            assert key in TYPE, (module.__name__, key)
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_scenes_contract.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'deck.scenes'`

- [ ] **Step 3: Implement `deck/scenes/common.py`**

```python
"""Scene plumbing: context, the ambient density table, the shared stack.

A scene never touches COM. It gets a Stage and a Motion and uses only those, so
capability fallbacks and the readability clock cannot be bypassed.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Callable, NamedTuple

from deck.geometry import Rect, meteors, starfield, zone_rect
from deck.palette import CANVAS_H, CANVAS_W


class AmbientSpec(NamedTuple):
    stars: int
    meteors: int
    breath: bool
    twinkle: int        # how many stars get a transparency pulse
    fixtures: int       # every non-particle shape, INCLUDING the shared 8


# THE FROZEN DENSITY TABLE. This is the single source of truth for S2 (how many
# stars must be displaced), S3 (how many motion paths exist), S10 (the effect
# count) and S11 (the per-slide shape count). verify_pptx.py imports this module
# rather than carrying its own copy, which is how the old S2-wants-56 /
# budget-is-44 and S11-wants-80 / scene-paints-55 contradictions arose.
#
# `fixtures` = 2 field + 4 vignette + 2 chrome + whatever the role paints.
# expected shapes = stars + meteors + fixtures.
AMBIENT: dict[str, AmbientSpec] = {
    "cold_open": AmbientSpec(96, 2, False, 20, 11),
    "title": AmbientSpec(88, 3, False, 18, 23),
    "title_lock": AmbientSpec(88, 2, True, 18, 14),
    "thesis": AmbientSpec(92, 1, False, 18, 10),
    "breath": AmbientSpec(120, 4, True, 30, 13),
    "chapter": AmbientSpec(96, 2, False, 18, 11),
    "concept": AmbientSpec(92, 2, True, 16, 21),
    "spike": AmbientSpec(112, 3, False, 20, 15),
    "afterglow": AmbientSpec(84, 1, True, 14, 14),
    "proof": AmbientSpec(80, 1, False, 14, 19),
    "settle": AmbientSpec(88, 1, False, 16, 10),
    "end": AmbientSpec(88, 2, True, 22, 15),
}

# A B twin paints two extra trail ovals; nothing else differs in count.
B_EXTRA_FIXTURES: dict[str, int] = {"concept": 2}


def scaled(n: int, fast: bool) -> int:
    """--fast halves the particle counts (D7) but never below a real shower."""
    return n if not fast else max(n // 2, 2)


def star_budget(spec, prev_stars=None, fast: bool = False) -> int:
    """How many stars this slide carries.

    A B twin and a carry slide both inherit their predecessor's field, so their
    budget is the predecessor's count, not their own row's. The table sets
    title_lock and end to 88 so the two agree for the deck's two carries.
    """
    if spec.inherits_field and prev_stars is not None:
        return len(prev_stars)
    return scaled(AMBIENT[spec.role].stars, fast)


def expected_shapes(spec, prev_stars=None, fast: bool = False,
                    with_audio: bool = False) -> int:
    """What S11 asserts the saved slide XML must contain, exactly."""
    row = AMBIENT[spec.role]
    total = (star_budget(spec, prev_stars, fast)
             + scaled(row.meteors, fast)
             + row.fixtures)
    if spec.is_b:
        total += B_EXTRA_FIXTURES.get(spec.role, 0)
    if with_audio and spec.idx == 1:
        total += 1       # the offstage media shape
    return total


@dataclass
class SlideBuild:
    stars: list = field(default_factory=list)
    meteor_count: int = 0
    handles: dict[str, Any] = field(default_factory=dict)
    notes: list[str] = field(default_factory=list)


@dataclass
class SceneContext:
    prs: Any
    slide: Any
    spec: Any
    K: Any
    caps: Any
    stage: Any
    motion: Any
    stars: list
    seed: int
    total: int = 26
    fast: bool = False

    @property
    def role(self) -> str:
        return self.spec.role

    @property
    def zone(self) -> Rect:
        return zone_rect(self.spec.role)

    @property
    def ambient(self) -> AmbientSpec:
        return AMBIENT[self.spec.role]


class Scene(NamedTuple):
    build: Callable[[SceneContext], SlideBuild]
    morph: Callable[[SceneContext, SlideBuild], SlideBuild]


# -- the shared stack ------------------------------------------------------
def ground(ctx: SceneContext) -> None:
    ctx.stage.field()
    ctx.stage.vignette()


def star_layer(ctx: SceneContext) -> list:
    shapes = [ctx.stage.star(star, i) for i, star in enumerate(ctx.stars)]
    n = scaled(ctx.ambient.twinkle, ctx.fast)
    if n:
        ctx.motion.twinkle(shapes[:n])
    return shapes


def meteor_layer(ctx: SceneContext, seed_shift: int = 0,
                 index_base: int = 0) -> int:
    """S3/D5: paint and route the shower. Count comes from AMBIENT."""
    count = scaled(ctx.ambient.meteors, ctx.fast)
    routed = meteors(seed=ctx.seed * 31 + seed_shift, count=count,
                     role=ctx.spec.role)
    for i, m in enumerate(routed):
        shape = ctx.stage.meteor(m, index_base + i)
        ctx.motion.meteor_travel(shape, m)
    return len(routed)


def chrome(ctx: SceneContext) -> None:
    ctx.stage.chrome(ctx.spec.idx, ctx.total)


def ambient_layer(ctx: SceneContext) -> SlideBuild:
    """Everything below the type, in paint order. Returns the star list so the
    B twin can drift exactly these stars."""
    ground(ctx)
    stars = star_layer(ctx)
    count = meteor_layer(ctx)
    return SlideBuild(stars=list(ctx.stars), meteor_count=count,
                      handles={"star_shapes": stars})


def reseed_meteors(ctx: SceneContext, prev: SlideBuild) -> int:
    """On a B twin the old streaks were deleted by transitions.delete_by_prefix;
    paint a fresh set with a shifted seed so the shower keeps going."""
    count = meteor_layer(ctx, seed_shift=777, index_base=500)
    return count


def reseed_ambient(ctx: SceneContext, prev: SlideBuild,
                   breath: bool | None = None) -> tuple[int, list[str]]:
    """Re-animate a B twin whose timeline was wiped. Call this, not reseed_meteors.

    `transitions.clear_timeline()` deletes EVERY effect on the duplicate -- that
    is law 2, and it is correct -- but it also deletes the twinkle and the glow
    breath. Without this helper the twin holds a frozen starfield and a dead
    glow for its whole 4-17.5s hold: the Morph lands after 2s and then nothing
    on screen moves, which is the opposite of the continuous-drift thesis.

    Everything added here is ambient, so `readable_end` stays untouched and no
    persisting object receives an entrance.

    `breath=False` suppresses the breath for a twin whose own morph already
    drives the glow (afterglow and end both dim it). Two drivers on one object
    is the law-2 failure mode even when both are ambient, and S9's one-breath
    cap would be spent on a disc that is supposed to be fading out.
    """
    want_breath = ctx.ambient.breath if breath is None else breath
    from deck.transitions import shapes_by_prefix
    notes: list[str] = []

    stars = shapes_by_prefix(ctx.slide, "!!star_")
    pulsed = scaled(ctx.ambient.twinkle, ctx.fast)
    if stars and pulsed:
        ctx.motion.twinkle(stars[:pulsed])
        notes.append(f"twinkle reseed on {min(pulsed, len(stars))} stars")

    if want_breath:
        cores = shapes_by_prefix(ctx.slide, "!!glow_")
        if cores:
            ctx.motion.breath(cores[-1])        # S9: at most one per slide
            notes.append("breath reseed on the glow core")

    count = reseed_meteors(ctx, prev)
    notes.append(f"meteors reseed ({count}, shifted seed)")
    return count, notes


# -- small shared plates ---------------------------------------------------
def centre_rect(width: float, height: float, dy: float = 0.0) -> Rect:
    return Rect((CANVAS_W - width) / 2, (CANVAS_H - height) / 2 + dy, width, height)
```

- [ ] **Step 4: Implement `deck/scenes/identity.py`** (the kinetic classes: `cold_open`, `title`, `title_lock`)

```python
"""Kinetic identity scenes. S4 lives here: the glyph assemble and the cascade."""
from __future__ import annotations

from deck.geometry import Rect, scatter_offsets, stagger
from deck.palette import CANVAS_H, CANVAS_W
from deck.scenes.common import (
    Scene, SlideBuild, ambient_layer, chrome, reseed_ambient,
)

GLYPH_SIZE = 150.0
GLYPH_GAP = 24.0


def _glyph_rects(count: int) -> list[Rect]:
    """The locked centre line the glyphs Morph into on the B twin."""
    total = count * GLYPH_SIZE + (count - 1) * GLYPH_GAP
    left = (CANVAS_W - total) / 2
    top = (CANVAS_H - GLYPH_SIZE) / 2 - 20.0
    return [Rect(left + i * (GLYPH_SIZE + GLYPH_GAP), top, GLYPH_SIZE, GLYPH_SIZE)
            for i in range(count)]


# -- cold_open (slides 1, 2) ----------------------------------------------
def cold_open_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data

    bar = ctx.stage.rule(96.0, 436.0, 180.0, 3.0, "amber")
    ctx.motion.wipe(bar, duration=0.8, delay=0.0, hero=True)

    title = ctx.stage.text("hero", data.get("title", ""),
                           Rect(96.0, 300.0, 600.0, 130.0))
    ctx.motion.fade(title, duration=0.8, delay=0.4)

    sub = ctx.stage.text("subtitle", data.get("subtitle", ""),
                         Rect(96.0, 452.0, 600.0, 36.0))
    ctx.motion.fade(sub, duration=0.6, delay=1.1)

    chrome(ctx)
    build.handles.update({"bar": bar, "title": title, "sub": sub})
    return build


def cold_open_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """Morph job: stars drift down-left (handled by build_deck), bar lengthens,
    title holds. No entrance on anything that persists."""
    from deck.transitions import extend_rule, shapes_by_prefix
    for shape in shapes_by_prefix(ctx.slide, "!!rule_"):
        extend_rule(shape, 240.0)
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["cold_open B: rule extended 180->420pt"] + ambient_notes)


# -- title (slides 3, 4) --------------------------------------------------
def title_build(ctx) -> SlideBuild:
    """S4 part 1: a 12-fragment cascade at 0.12s, then 3 scattered glyphs."""
    build = ambient_layer(ctx)
    data = ctx.spec.data
    glyphs = data.get("glyphs") or list(data.get("title", ""))[:3]

    # 12 faint fragments on the 0.12s grid -- the cascade S4 counts
    frags = []
    for i, delay in enumerate(stagger(12, 0.12)):
        x = 110.0 + (i % 6) * 128.0
        y = 150.0 + (i // 6) * 258.0
        frag = ctx.stage.oval(x, y, 3.0, "ghost", 0.55, index=900 + i, kind="frag")
        frags.append(frag)
    ctx.motion.cascade(frags, ctx.motion.fade, step=0.12, start=0.0, duration=0.4)

    # the glyphs: scattered and rotated here, locked on the B twin
    boxes = []
    offsets = scatter_offsets(seed=ctx.seed, count=len(glyphs), spread=160.0)
    for i, (glyph, rect) in enumerate(zip(glyphs, _glyph_rects(len(glyphs)))):
        dx, dy, rot = offsets[i]
        box = ctx.stage.text("hero_glyph", glyph,
                             Rect(rect.left + dx, rect.top + dy,
                                  rect.width, rect.height),
                             align="c", name=f"!!glyph_{i:03d}")
        box.Rotation = rot
        boxes.append(box)
    ctx.motion.cascade(boxes, ctx.motion.faded_zoom, step=0.33, start=0.0,
                       duration=0.7, hero=True)

    chrome(ctx)
    build.handles.update({"glyphs": boxes, "frags": frags})
    return build


def title_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """S4 part 2: the glyph assemble. Morph interpolates each glyph from its
    scattered, rotated position to the locked centre line."""
    from deck.transitions import shapes_by_prefix
    boxes = sorted(shapes_by_prefix(ctx.slide, "!!glyph_"), key=lambda s: s.Name)
    for box, rect in zip(boxes, _glyph_rects(len(boxes))):
        box.Left, box.Top = rect.left, rect.top
        box.Width, box.Height = rect.width, rect.height
        box.Rotation = 0.0
    for frag in shapes_by_prefix(ctx.slide, "!!frag_"):
        frag.Top = frag.Top - 14.0   # the fragments settle upward, no entrance
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=[f"title B: {len(boxes)} glyphs assembled to the "
                             f"locked line, rotation -> 0"] + ambient_notes)


# -- title_lock (slides 5, 6) ---------------------------------------------
def title_lock_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data

    if data.get("glow"):
        core = ctx.stage.glow_stack(CANVAS_W * 0.5, CANVAS_H * 0.42, 190.0,
                                    "amber", index=0)[-1]
        ctx.motion.breath(core)
        build.handles["glow_core"] = core

    title = ctx.stage.text("hero", data.get("title", ""),
                           Rect(160.0, 196.0, 640.0, 140.0), align="c",
                           name="!!title_000")
    ctx.motion.fade(title, duration=0.6, delay=0.0, hero=True)

    ghost = ctx.stage.ghost_latin(data.get("subtitle", "SIGNAL"),
                                 Rect(160.0, 346.0, 640.0, 48.0))
    ctx.motion.fade(ghost, duration=1.0, delay=0.3)

    chrome(ctx)
    build.handles.update({"title": title, "ghost": ghost})
    return build


def title_lock_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """morph_word glided the latin ghost in; here it glides on and the glow
    core drifts. Stars drift right 18pt (applied by build_deck)."""
    from deck.transitions import shapes_by_prefix
    for ghost in shapes_by_prefix(ctx.slide, "!!ghost_"):
        ghost.Left = ghost.Left + 46.0
    for i, disc in enumerate(shapes_by_prefix(ctx.slide, "!!glow_")):
        disc.Top = disc.Top - 12.0
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["title_lock B: ghost latin glides +46pt, glow "
                             "stack rises 12pt"] + ambient_notes)


COLD_OPEN = Scene(cold_open_build, cold_open_morph)
TITLE = Scene(title_build, title_morph)
TITLE_LOCK = Scene(title_lock_build, title_lock_morph)
```

- [ ] **Step 5: Implement `deck/scenes/editorial.py`** (the soft classes: `thesis`, `breath`, `concept`, `settle`)

```python
"""Soft editorial scenes. Low motion density, high readability."""
from __future__ import annotations

from deck.geometry import Rect, stagger
from deck.palette import CANVAS_H, CANVAS_W
from deck.scenes.common import (
    Scene, SlideBuild, ambient_layer, chrome, reseed_ambient,
)


def thesis_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data
    line = ctx.stage.text("sentence", data.get("title", ""),
                          Rect(120.0, 226.0, 720.0, 110.0), name="!!title_000")
    ctx.motion.fade(line, duration=0.8, delay=0.0, hero=True)
    meta = ctx.stage.text("meta_mark", data.get("subtitle", ""),
                          Rect(120.0, 348.0, 300.0, 24.0))
    ctx.motion.fade(meta, duration=0.4, delay=1.0)
    chrome(ctx)
    build.handles.update({"line": line, "meta": meta})
    return build


def breath_build(ctx) -> SlideBuild:
    """The wordless plate (D1's "near-wordless" count). 72 stars, 4 meteors,
    one glow breath, and a single meta mark -- nothing else readable."""
    build = ambient_layer(ctx)
    core = ctx.stage.glow_stack(CANVAS_W * 0.62, CANVAS_H * 0.40, 220.0,
                                "ghost", index=0)[-1]
    ctx.motion.breath(core, duration=9.0)
    meta = ctx.stage.text("meta_mark", ctx.spec.data.get("subtitle", "LISTEN"),
                          Rect(400.0, 476.0, 160.0, 24.0), align="c")
    ctx.motion.fade(meta, duration=0.6, delay=0.6)
    chrome(ctx)
    build.handles.update({"glow_core": core, "meta": meta})
    return build


def concept_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data

    if data.get("glow"):
        core = ctx.stage.glow_stack(CANVAS_W * 0.74, CANVAS_H * 0.44, 170.0,
                                    "amber", index=0)[-1]
        ctx.motion.breath(core)
        build.handles["glow_core"] = core

    label = ctx.stage.ghost_latin(f"{ctx.spec.beat:02d}  CONCEPT",
                                  Rect(110.0, 182.0, 420.0, 34.0))
    ctx.motion.fade(label, duration=0.4, delay=0.0)

    title = ctx.stage.text("title", data.get("title", ""),
                           Rect(110.0, 224.0, 560.0, 78.0), name="!!title_000")
    ctx.motion.fade(title, duration=0.7, delay=0.2, hero=True)

    body = ctx.stage.text("body", data.get("subtitle", ""),
                          Rect(110.0, 314.0, 520.0, 74.0))
    ctx.motion.fade(body, duration=0.5, delay=1.4)

    # the trail: pulses connected into a followable line (the beat 8 metaphor)
    trail = [ctx.stage.oval(132.0 + i * 54.0, 420.0, 5.0, "amber", 0.35,
                            index=600 + i, kind="trail")
             for i in range(6)]
    ctx.motion.cascade(trail, ctx.motion.fade, step=0.33, start=1.8,
                       duration=0.4)

    chrome(ctx)
    build.handles.update({"label": label, "title": title, "body": body,
                          "trail": trail})
    return build


def concept_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """Glow core travels, the trail extends by two ovals, stars drift."""
    from deck.transitions import shapes_by_prefix
    for disc in shapes_by_prefix(ctx.slide, "!!glow_"):
        disc.Left = disc.Left - 70.0
    existing = sorted(shapes_by_prefix(ctx.slide, "!!trail_"),
                      key=lambda s: s.Left)
    for i in range(2):
        last = existing[-1] if existing else None
        x = (last.Left + 54.0) if last is not None else 132.0
        new = ctx.stage.oval(x + 5.0, 420.0, 5.0, "amber", 0.35,
                             index=700 + i, kind="trail")
        existing.append(new)
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["concept B: glow core travels -70pt, trail +2 "
                             "ovals"] + ambient_notes)


def settle_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data
    line = ctx.stage.text("title", data.get("title", ""),
                          Rect(140.0, 240.0, 560.0, 72.0), name="!!title_000")
    ctx.motion.fade(line, duration=0.8, delay=0.0, hero=True)
    meta = ctx.stage.text("meta_mark", data.get("subtitle", ""),
                          Rect(140.0, 326.0, 420.0, 24.0))
    ctx.motion.fade(meta, duration=0.4, delay=1.0)
    chrome(ctx)
    build.handles.update({"line": line, "meta": meta})
    return build


def _hold_morph(label: str):
    """For roles whose twins hold still: reseed the ambient layer, move nothing.

    These roles (thesis, breath, settle) have no B twin in the current map, so
    this path only runs if a future beat splits one. It still has to reseed the
    ambient layer rather than only the meteors, or the twin freezes.
    """
    def morph(ctx, prev: SlideBuild) -> SlideBuild:
        count, ambient_notes = reseed_ambient(ctx, prev)
        return SlideBuild(stars=prev.stars, meteor_count=count,
                          notes=[f"{label} B: hold"] + ambient_notes)
    return morph


THESIS = Scene(thesis_build, _hold_morph("thesis"))
BREATH = Scene(breath_build, _hold_morph("breath"))
CONCEPT = Scene(concept_build, concept_morph)
SETTLE = Scene(settle_build, _hold_morph("settle"))
```

- [ ] **Step 6: Run the contract test** (it will still fail on the registry — that is Task 12)

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_scenes_contract.py -q`
Expected: FAIL — `ImportError: cannot import name 'SCENES' from 'deck.scenes'`. Everything *inside* these two modules is now written; the registry that makes the test green is Task 12's first step. Confirm the failure is only the import, not a `TypeError` inside a builder:

Run: `Set-Location demos/signal-station-pptx; python -c "import deck.scenes.editorial, deck.scenes.identity;print('both import clean')"`
Expected: `both import clean`

**Accept:** `deck/scenes/common.py`, `identity.py` and `editorial.py` all import without error; `AMBIENT` matches the §Frozen density table row for row (all five columns); `expected_shapes` reproduces that table's per-slide column and totals 2,867; every builder returns a `SlideBuild`; every `morph()` goes through `reseed_ambient`; `python -m pytest tests -q` shows the pre-existing ≥ 140 still passing and `test_scenes_contract.py` failing only on the missing `SCENES` registry.

---

### Task 12: `spike.py` + `proof.py` + the `SCENES` registry — the remaining five classes, wired

The loud half of the deck. Three effects here are the ones a viewer will actually remember, and each is capped by a declared deviation:

- **The chapter door** (slides 9, 14) — the transition is the hero event; the word lands at 1.1 s and the amber rule Wipes under it, then Morph-extends into a horizon line. S7 caps this at exactly two.
- **The iris** (slides 17, 18) — `CircleOut` in, then the `!!iris_core` donut's **aperture** is Morphed clear of the frame **corners** (550.61 pt, not the 480 pt half-width). D3 is the only licence in the deck to exceed 106 %, and it is one object, one beat, once. `IRIS_GROWTH` is 11.0 because 550.61 / `IRIS_HOLE` = 10.20; the earlier 7.2 grew the aperture to only 388.8 pt, which would have left the climax as a dark ring over the whole frame.
- **The Proof Trio** (slides 21, 22) — three cards cascade at 0.16 s over big ghost numerals, then Morph re-racks them tighter while the amber rule on card 2 extends. This is S8.

The registry closes the module: `SCENES` maps every role in the slide map to a `Scene`, and `scene_plate` is the fallback that paints the shared stack plus the beat's title so a new role can never crash a build — it just looks plain, and `build_deck.py` logs that it fell back.

**Files:**
- Create: `demos/signal-station-pptx/deck/scenes/spike.py`
- Create: `demos/signal-station-pptx/deck/scenes/proof.py`
- Create: `demos/signal-station-pptx/deck/scenes/__init__.py`

**Interfaces:**
- Produces: `CHAPTER`, `SPIKE`, `PROOF`, `AFTERGLOW`, `END` as `Scene` pairs; `deck.scenes.SCENES: dict[str, Scene]`; `deck.scenes.scene_plate: Scene`; `deck.scenes.scene_for(role) -> tuple[Scene, bool]` (the bool is "fell back").
- Consumes: everything from Task 11's `common.py`.
- Consumed by: `build_deck.py` (Task 13 looks up `scene_for(spec.role)` per slide).

- [ ] **Step 1: Implement `deck/scenes/spike.py`**

```python
"""The loud classes: chapter doors (S7) and the single iris climax (S6/D3)."""
from __future__ import annotations

from deck.geometry import Rect
from deck.palette import CANVAS_H, CANVAS_W
from deck.scenes.common import (
    Scene, SlideBuild, ambient_layer, chrome, reseed_ambient,
)

# D3, the one licence in the deck to exceed 106%. The number that matters is the
# APERTURE, not the outer ring: what the audience sees open is the hole, and the
# hole has to clear the frame CORNERS (hypot(480, 270) = 550.61pt), not the
# nearer edge. An earlier draft used IRIS_GROWTH = 7.2, which grew the hole to
# 54 * 7.2 = 388.8pt -- less than the 480pt half-width, so the iris would have
# stalled as a dark annulus over the whole frame and never revealed anything.
#
#   needed growth >= 550.61 / 54 = 10.20  ->  11.0 with margin
#   aperture radius  = 54 * 11 = 594pt    (> 550.61, corners clear)
#   outer radius     = 150 * 11 = 1650pt  (3300pt wide, inside PowerPoint's
#                                          4032pt shape limit)
IRIS_OUTER = 150.0
IRIS_HOLE = 54.0
IRIS_GROWTH = 11.0


def chapter_build(ctx) -> SlideBuild:
    """The transition is the hero event; the word lands at 1.1s."""
    build = ambient_layer(ctx)
    data = ctx.spec.data

    ghost = ctx.stage.ghost_latin(data.get("subtitle", ""),
                                  Rect(112.0, 176.0, 520.0, 38.0))
    ctx.motion.fade(ghost, duration=0.5, delay=0.0)

    word = ctx.stage.text("chapter_word", data.get("title", ""),
                          Rect(104.0, 218.0, 700.0, 160.0), name="!!title_000")
    ctx.motion.faded_zoom(word, duration=0.8, delay=0.3, hero=True)

    rule = ctx.stage.rule(112.0, 398.0, 220.0, 3.0, "amber")
    ctx.motion.wipe(rule, duration=0.6, delay=0.9)

    chrome(ctx)
    build.handles.update({"ghost": ghost, "word": word, "rule": rule})
    return build


def chapter_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """Word holds, the rule extends into a horizon line, stars drift up 16pt."""
    from deck.transitions import extend_rule, shapes_by_prefix
    for rule in shapes_by_prefix(ctx.slide, "!!rule_"):
        extend_rule(rule, 616.0)      # 220 -> 836pt, a horizon across the plate
        rule.Top = 404.0
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["chapter B: amber rule 220->836pt horizon, "
                             "word holds"] + ambient_notes)


def spike_build(ctx) -> SlideBuild:
    """Slide 17. The CircleOut transition is the entrance; the plate under it is
    inverted -- dark type on an amber core -- so the iris reads as a reveal."""
    build = ambient_layer(ctx)
    data = ctx.spec.data

    glow = ctx.stage.glow_stack(CANVAS_W * 0.5, CANVAS_H * 0.5, 260.0,
                                "amber", index=0)
    iris = ctx.stage.donut(CANVAS_W * 0.5, CANVAS_H * 0.5,
                           IRIS_OUTER, IRIS_HOLE, "field", index=0)

    word = ctx.stage.text("spike_word", data.get("title", ""),
                          Rect(180.0, 196.0, 600.0, 170.0), align="c",
                          name="!!title_000")
    ctx.motion.fade(word, duration=0.5, delay=0.6, hero=True)

    mark = ctx.stage.ghost_latin(data.get("subtitle", "IRIS"),
                                 Rect(180.0, 392.0, 600.0, 40.0))
    ctx.motion.fade(mark, duration=0.4, delay=1.0)

    chrome(ctx)
    build.handles.update({"iris": iris, "glow": glow, "word": word})
    build.notes.append(f"spike A: iris donut outer={IRIS_OUTER}pt hole={IRIS_HOLE}pt")
    return build


def spike_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """D3: the one object in the deck allowed past 106%.

    The donut's aperture is Morphed clear of the frame corners and the type
    fades under it. The fade is an *exit*, which law 2 permits -- it bans
    entrances on persisting objects, not exits -- and it is charged to the
    readable clock on purpose, because the type leaving is a readable event.
    """
    from deck.geometry import frame_corner_dist
    from deck.transitions import grow_iris, shapes_by_prefix
    aperture = IRIS_HOLE * IRIS_GROWTH
    assert aperture >= frame_corner_dist(), (
        f"iris aperture {aperture:.1f}pt does not clear the frame corners "
        f"({frame_corner_dist():.1f}pt); the climax would stall as a dark "
        f"annulus. Raise IRIS_GROWTH, not the plan's claim.")
    for iris in shapes_by_prefix(ctx.slide, "!!iris_core_"):
        grow_iris(iris, IRIS_GROWTH)
    for word in shapes_by_prefix(ctx.slide, "!!title_"):
        ctx.motion.fade(word, duration=1.2, delay=0.0, exit_=True)
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=[f"spike B: iris grown x{IRIS_GROWTH} (outer "
                             f"{IRIS_OUTER}->{IRIS_OUTER * IRIS_GROWTH:.0f}pt, "
                             f"aperture {IRIS_HOLE}->{aperture:.0f}pt vs "
                             f"{frame_corner_dist():.0f}pt corner, D3), "
                             f"type exits"] + ambient_notes)


CHAPTER = Scene(chapter_build, chapter_morph)
SPIKE = Scene(spike_build, spike_morph)
```

- [ ] **Step 2: Implement `deck/scenes/proof.py`**

```python
"""Proof Trio (S8), afterglow, and the 17.5s end lock."""
from __future__ import annotations

from deck.geometry import Rect
from deck.palette import CANVAS_H, CANVAS_W
from deck.scenes.common import (
    Scene, SlideBuild, ambient_layer, chrome, reseed_ambient,
)

CARD_W = 236.0
CARD_H = 180.0
CARD_TOP = 232.0
CARD_GAP_A = 42.0     # the first rack
CARD_GAP_B = 18.0     # the B twin re-racks tighter (S8)


def _card_rects(count: int, gap: float) -> list[Rect]:
    total = count * CARD_W + (count - 1) * gap
    left = (CANVAS_W - total) / 2
    return [Rect(left + i * (CARD_W + gap), CARD_TOP, CARD_W, CARD_H)
            for i in range(count)]


def proof_build(ctx) -> SlideBuild:
    """Three cards cascade at 0.16s over big ghost numerals."""
    build = ambient_layer(ctx)
    data = ctx.spec.data
    labels = data.get("cards") or []
    rects = _card_rects(len(labels), CARD_GAP_A)

    numerals = []
    for i, rect in enumerate(rects):
        num = ctx.stage.text("proof_numeral", str(i + 1),
                             Rect(rect.left, rect.top - 54.0, rect.width, 190.0),
                             align="c", name=f"!!numeral_{i:03d}")
        numerals.append(num)
    ctx.motion.cascade(numerals, ctx.motion.fade, step=0.16, start=0.0,
                       duration=0.4)

    cards, texts = [], []
    for i, (label, rect) in enumerate(zip(labels, rects)):
        card = ctx.stage.rule(rect.left, rect.top, rect.width, rect.height,
                              "lift", index=100 + i)
        cards.append(card)
        text = ctx.stage.text("card_label", label,
                              Rect(rect.left + 22.0, rect.top + 64.0,
                                   rect.width - 44.0, 52.0),
                              name=f"!!cardlabel_{i:03d}")
        texts.append(text)
    ctx.motion.cascade(cards, ctx.motion.rise_up, step=0.16, start=0.2,
                       duration=0.6, hero=True)
    ctx.motion.cascade(texts, ctx.motion.fade, step=0.16, start=0.3,
                       duration=0.5)

    # the amber rule on card 2 that extends on the B twin
    mid = rects[len(rects) // 2]
    accent = ctx.stage.rule(mid.left + 22.0, mid.top + 134.0, 76.0, 3.0,
                            "amber", index=200)
    ctx.motion.wipe(accent, duration=0.5, delay=0.9)

    title = ctx.stage.ghost_latin(data.get("subtitle", "PROOF"),
                                  Rect(80.0, 190.0, 300.0, 34.0))
    ctx.motion.fade(title, duration=0.4, delay=0.0)

    chrome(ctx)
    build.handles.update({"cards": cards, "numerals": numerals,
                          "accent": accent})
    return build


def proof_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """S8 second half: the rack tightens and the amber rule extends."""
    from deck.transitions import extend_rule, shapes_by_prefix
    cards = sorted(shapes_by_prefix(ctx.slide, "!!rule_1"), key=lambda s: s.Left)
    labels = sorted(shapes_by_prefix(ctx.slide, "!!cardlabel_"), key=lambda s: s.Left)
    numerals = sorted(shapes_by_prefix(ctx.slide, "!!numeral_"), key=lambda s: s.Left)
    tight = _card_rects(len(cards), CARD_GAP_B)
    for card, label, numeral, rect in zip(cards, labels, numerals, tight):
        shift = rect.left - card.Left
        card.Left = rect.left
        label.Left = label.Left + shift
        numeral.Left = numeral.Left + shift
    for accent in shapes_by_prefix(ctx.slide, "!!rule_200"):
        extend_rule(accent, 118.0)    # 76 -> 194pt
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=[f"proof B: rack gap {CARD_GAP_A}->{CARD_GAP_B}pt, "
                             f"card-2 rule 76->194pt"] + ambient_notes)


def afterglow_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data
    if data.get("glow"):
        core = ctx.stage.glow_stack(CANVAS_W * 0.5, CANVAS_H * 0.46, 210.0,
                                    "amber", index=0)[-1]
        ctx.motion.breath(core)
        build.handles["glow_core"] = core
    title = ctx.stage.text("title", data.get("title", ""),
                           Rect(120.0, 236.0, 620.0, 76.0), name="!!title_000")
    ctx.motion.fade(title, duration=0.8, delay=0.0, hero=True)
    sub = ctx.stage.text("body", data.get("subtitle", ""),
                         Rect(120.0, 324.0, 560.0, 50.0))
    ctx.motion.fade(sub, duration=0.5, delay=1.2)
    chrome(ctx)
    build.handles.update({"title": title, "sub": sub})
    return build


def afterglow_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """The glow dims by transparency and the field thins as stars drift out."""
    from deck.transitions import shapes_by_prefix
    discs = shapes_by_prefix(ctx.slide, "!!glow_")
    for disc in discs:
        ctx.motion.dim(disc, duration=3.0, delay=0.4)
        disc.Width *= 1.04
        disc.Height *= 1.04
    count, ambient_notes = reseed_ambient(ctx, prev, breath=False)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=[f"afterglow B: {len(discs)} glow discs dim over "
                             f"3.0s at 104%, breath suppressed (the glow is "
                             f"fading, not breathing)"] + ambient_notes)


def end_build(ctx) -> SlideBuild:
    build = ambient_layer(ctx)
    data = ctx.spec.data
    if data.get("glow"):
        core = ctx.stage.glow_stack(CANVAS_W * 0.5, CANVAS_H * 0.44, 240.0,
                                    "amber", index=0)[-1]
        ctx.motion.breath(core, duration=10.0)
        build.handles["glow_core"] = core
    title = ctx.stage.text("end_title", data.get("title", ""),
                           Rect(160.0, 212.0, 640.0, 110.0), align="c",
                           name="!!title_000")
    ctx.motion.fade(title, duration=1.0, delay=0.0, hero=True)
    sub = ctx.stage.ghost_latin(data.get("subtitle", ""),
                                Rect(160.0, 336.0, 640.0, 40.0))
    ctx.motion.fade(sub, duration=0.6, delay=1.4)
    dot = ctx.stage.oval(CANVAS_W * 0.5, 412.0, 6.0, "amber_hot", 0.1,
                         index=300, kind="dot")
    ctx.motion.fade(dot, duration=0.5, delay=2.0)
    chrome(ctx)
    build.handles.update({"title": title, "sub": sub, "dot": dot})
    return build


def end_morph(ctx, prev: SlideBuild) -> SlideBuild:
    """The last 17.5s: stars sink (drift (0, 26) by DRIFT['end']), the glow
    fades, the amber dot holds. Nothing enters."""
    from deck.transitions import shapes_by_prefix
    for disc in shapes_by_prefix(ctx.slide, "!!glow_"):
        ctx.motion.dim(disc, duration=6.0, delay=2.0)
    count, ambient_notes = reseed_ambient(ctx, prev, breath=False)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["end B: 17.5s slow drift, glow fades over 6.0s, "
                             "amber dot holds, breath suppressed"]
                            + ambient_notes)


PROOF = Scene(proof_build, proof_morph)
AFTERGLOW = Scene(afterglow_build, afterglow_morph)
END = Scene(end_build, end_morph)
```

- [ ] **Step 3: Implement `deck/scenes/__init__.py`** — the registry

```python
"""Scene registry. build_deck.py never imports a role module directly."""
from __future__ import annotations

from deck.scenes.common import (
    AMBIENT, B_EXTRA_FIXTURES, AmbientSpec, Scene, SceneContext, SlideBuild,
    ambient_layer, chrome, expected_shapes, reseed_ambient, reseed_meteors,
    scaled, star_budget,
)
from deck.scenes.editorial import BREATH, CONCEPT, SETTLE, THESIS
from deck.scenes.identity import COLD_OPEN, TITLE, TITLE_LOCK
from deck.scenes.proof import AFTERGLOW, END, PROOF
from deck.scenes.spike import CHAPTER, SPIKE

SCENES: dict[str, Scene] = {
    "cold_open": COLD_OPEN,
    "title": TITLE,
    "title_lock": TITLE_LOCK,
    "thesis": THESIS,
    "breath": BREATH,
    "chapter": CHAPTER,
    "concept": CONCEPT,
    "spike": SPIKE,
    "afterglow": AFTERGLOW,
    "proof": PROOF,
    "settle": SETTLE,
    "end": END,
}


def _plate_build(ctx) -> SlideBuild:
    """Fallback: the shared stack plus the beat's title. Plain, never broken."""
    from deck.geometry import Rect
    build = ambient_layer(ctx)
    data = ctx.spec.data
    title = ctx.stage.text("title", data.get("title", ""),
                           Rect(120.0, 240.0, 620.0, 76.0), name="!!title_000")
    ctx.motion.fade(title, duration=0.8, delay=0.0, hero=True)
    chrome(ctx)
    build.handles["title"] = title
    build.notes.append(f"FALLBACK plate for unregistered role {ctx.spec.role!r}")
    return build


def _plate_morph(ctx, prev: SlideBuild) -> SlideBuild:
    count, ambient_notes = reseed_ambient(ctx, prev)
    return SlideBuild(stars=prev.stars, meteor_count=count,
                      notes=["FALLBACK plate B: hold"] + ambient_notes)


scene_plate = Scene(_plate_build, _plate_morph)


def scene_for(role: str) -> tuple[Scene, bool]:
    """(scene, fell_back). build_deck.py logs every fell_back=True slide."""
    scene = SCENES.get(role)
    return (scene, False) if scene is not None else (scene_plate, True)


__all__ = ["SCENES", "Scene", "SceneContext", "SlideBuild", "AMBIENT",
           "AmbientSpec", "B_EXTRA_FIXTURES", "ambient_layer", "chrome",
           "expected_shapes", "reseed_ambient", "reseed_meteors", "scaled",
           "star_budget", "scene_for", "scene_plate"]
```

- [ ] **Step 4: Run the contract test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_scenes_contract.py -q`
Expected: `85 passed` (7 roles × 3 parametrized A tests + 3 kinetic hero tests + 9 B roles × 5 parametrized B tests + 2 dim-vs-breath + the density and wordless singles). If `test_b_builders_add_no_entrance_effect_to_a_persisting_object` fails, a `morph()` is calling an entrance helper — move that object's reveal to the A twin or make it an exit; do not relax the test, it is §Step 2 law 2. If `test_b_builders_reseed_the_twinkle_so_the_twin_does_not_freeze` fails, a `morph()` is still calling `reseed_meteors` directly instead of `reseed_ambient`.

- [ ] **Step 5: Add registry coverage to the contract test**

Append to `tests/test_scenes_contract.py`:

```python
def test_the_registry_covers_every_role_in_the_slide_map():
    from deck.scenes import SCENES
    assert {s.role for s in expand(TIMING)} == set(SCENES)


def test_an_unknown_role_falls_back_to_the_plate_rather_than_crashing():
    from deck.scenes import scene_for, scene_plate
    scene, fell_back = scene_for("no_such_role")
    assert fell_back is True and scene is scene_plate


def test_a_known_role_does_not_fall_back():
    from deck.scenes import SCENES, scene_for
    scene, fell_back = scene_for("spike")
    assert fell_back is False and scene is SCENES["spike"]


def test_the_iris_growth_is_declared_and_is_the_only_thing_past_106pct():
    from deck.scenes.spike import IRIS_GROWTH, IRIS_OUTER
    assert IRIS_GROWTH > 1.06
    assert IRIS_OUTER * IRIS_GROWTH > 960.0  # D3: past the frame, by design


def test_the_iris_APERTURE_clears_the_frame_corners_not_just_the_edges():
    # S6/D3. The audience sees the HOLE open, so the hole is what must clear the
    # frame -- and it must clear the corners (550.61pt), not the nearer edge
    # (480pt). IRIS_GROWTH = 7.2 gave a 388.8pt aperture, which would have
    # stalled the climax as a dark annulus covering the whole frame.
    from deck.geometry import frame_corner_dist
    from deck.scenes.spike import IRIS_GROWTH, IRIS_HOLE
    aperture = IRIS_HOLE * IRIS_GROWTH
    assert aperture >= frame_corner_dist(), (aperture, frame_corner_dist())
    assert aperture >= 550.61


def test_the_grown_iris_still_fits_powerpoints_shape_limit():
    from deck.scenes.spike import IRIS_GROWTH, IRIS_OUTER
    assert IRIS_OUTER * IRIS_GROWTH * 2 <= 4032.0   # 56in, PowerPoint's max


def test_the_iris_hole_ratio_survives_the_growth():
    # Donut's adjustment is a FRACTION of the ring, so scaling the shape scales
    # the hole with it -- this is why aperture growth tracks IRIS_GROWTH at all
    from deck.scenes.spike import IRIS_HOLE, IRIS_OUTER
    ratio = IRIS_HOLE / IRIS_OUTER
    assert 0.05 <= ratio <= 0.95     # the range stage.donut() clamps to
    assert ratio == pytest.approx(0.36, abs=1e-6)


def test_the_proof_rack_tightens_on_the_b_twin_s8():
    from deck.scenes.proof import CARD_GAP_A, CARD_GAP_B
    assert CARD_GAP_B < CARD_GAP_A


def test_the_spike_and_chapter_builders_run_clean_against_the_fakes():
    from deck.scenes import SCENES
    for role in ("chapter", "spike", "proof", "afterglow", "end"):
        spec = next(s for s in expand(TIMING) if s.role == role and not s.is_b)
        ctx = make_ctx(spec)
        build = SCENES[role].build(ctx)
        assert build.stars and "chrome" in [c[0] for c in ctx.stage.calls]
        assert ctx.motion.clock.check(spec.hold, spec.hold - spec.trans_dur,
                                      role) == []
```

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_scenes_contract.py -q`
Expected: `96 passed`

- [ ] **Step 6: Whole pure suite green, and no scene reaches around its Stage**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests -q`
Expected: ≥ `236 passed`, 0 failed

Run: `Set-Location demos/signal-station-pptx; Get-ChildItem deck/scenes -Recurse -Filter *.py | Select-String -Pattern "Shapes\.Add|TimeLine|AddEffect"`
Expected: no output. A scene that creates a shape or an effect directly has bypassed the capability fallbacks and the clock; move the call into `stage.py` or `motion.py`.

Run: `Set-Location demos/signal-station-pptx; Get-ChildItem deck/scenes -Recurse -Filter *.py | Select-String -Pattern "reseed_meteors\("`
Expected: matches only inside `common.py` (its definition and the one call from `reseed_ambient`). A role module that calls it directly skips the twinkle/breath reseed and ships a frozen twin.

**Accept:** `python -m pytest tests -q` reports ≥ 236 passed, 0 failed; `python -c "from deck.scenes import SCENES;print(len(SCENES), sorted(SCENES))"` prints `12` and the 12 roles; both `Select-String` checks in Step 6 come back as described; `python -c "from deck.geometry import frame_corner_dist as f; from deck.scenes.spike import IRIS_GROWTH as g, IRIS_HOLE as h; print(round(h*g,1), round(f(),1))"` prints `594.0 550.6`.

---

### Task 13: rewrite `build_deck.py` as a thin CLI

The 490-line monolith with the wrong constants becomes an orchestrator that owns **no visual decisions**. Its whole job: parse flags, open PowerPoint, walk the slide map, hand each slide to a scene, apply the transition and `advTm`, mount the audio, write the logs, save, and refuse to save a deck that broke a law.

The per-slide loop is the one piece of real logic, and the order inside it is load-bearing:

1. **A/solo slide** — `prs.Slides.Add` → `Stage` + `Motion` → `scene.build(ctx)` → `apply_transition`.
2. **B twin** — `duplicate_twin` → `clear_timeline` → `delete_by_prefix("!!meteor_")` → `drift_stars(prev.stars, *DRIFT[role])` → `retext_meta_index` → `scene.morph(ctx, prev)` (which re-seeds the ambient layer) → `apply_transition`.
3. **Carry slide** (5, 25) — `prs.Slides.Add` → `stars = drifted(prev.stars, *DRIFT[role])` → `Stage` + `Motion` → `scene.build(ctx)` → `apply_transition`. It is an A-shaped build with an inherited field: same count, same order, same `!!star_nnn` names, so Morph pairs it **by name** across the beat boundary.

After every slide, the loop asserts `slide.Shapes.Count == expected_shapes(spec)`. The frozen density table is a prediction and S11 audits it; discovering a drift here, one slide in, is far cheaper than discovering it after a 26-slide build.

The duplicate must happen **before** the next A slide is added, or the B twin lands in the wrong position; `duplicate_twin` already moves it, but building strictly in show order keeps the slide indices equal to `spec.idx`, which every log line and every audit in Task 16 relies on.

**Files:**
- Rewrite: `demos/signal-station-pptx/build_deck.py`
- Create: `demos/signal-station-pptx/tests/test_cli_args.py`

**Interfaces:**
- CLI: `--beats 1,6,11` (or `1-3`) builds a subset; `--fast` halves particle counts (D7); **`--audio` opts into the sound bed** (default: silent — there is no `--no-audio` because silence is not a mode you turn on); `--out PATH` overrides the output; `--video` also runs `CreateVideo`; `--pdf` exports the static twin (S15); `--keep-open` leaves PowerPoint up for inspection.
- Produces: `out/信号站-SIGNAL-preview.pptx`, `out/_sync-ledger.txt` (from `sync.ledger`), `out/_build-log.txt` (per-slide shape count, node count, `hero_end`, `readable_end`, `advTm`, fallback flag), and on `--pdf` / `--video` the extra artefacts.
- Produces for tests: `parse_args(argv) -> argparse.Namespace`, `parse_beats(text) -> set[int] | None`, `subset(specs, beats) -> list[SlideSpec]`, `build(args) -> int`.
- Consumes: every `deck/` module.

- [ ] **Step 1: Write the failing CLI test**

`demos/signal-station-pptx/tests/test_cli_args.py`:

```python
import json
from pathlib import Path

import pytest

from build_deck import parse_args, parse_beats, subset
from deck.sync import expand

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))


def test_defaults_build_the_whole_deck_silently():
    # 0.0 item 5: silence is the default and audio is the opt-in, not the
    # other way round. There is deliberately no --no-audio flag.
    args = parse_args([])
    assert args.beats is None and args.fast is False and args.audio is False
    assert args.out is None and args.video is False and args.pdf is False


def test_there_is_no_no_audio_flag_to_forget_to_pass():
    with pytest.raises(SystemExit):
        parse_args(["--no-audio"])


def test_audio_is_an_explicit_opt_in():
    assert parse_args(["--audio"]).audio is True


def test_beats_accepts_a_comma_list():
    assert parse_beats("1,6,11") == {1, 6, 11}


def test_beats_accepts_a_range_and_mixes_the_two():
    assert parse_beats("1-3") == {1, 2, 3}
    assert parse_beats("1-3,11,16") == {1, 2, 3, 11, 16}


def test_beats_rejects_a_beat_outside_the_map():
    for bad in ("0", "17", "4-99"):
        with pytest.raises(ValueError, match="1..16"):
            parse_beats(bad)


def test_beats_rejects_junk():
    with pytest.raises(ValueError):
        parse_beats("chapter")


def test_none_means_everything():
    assert parse_beats("") is None
    assert parse_beats(None) is None


def test_subset_keeps_whole_beats_including_both_twins():
    specs = expand(TIMING)
    picked = subset(specs, {1, 11})
    assert [s.idx for s in picked] == [1, 2, 17, 18]


def test_subset_renumbers_so_slide_indices_stay_contiguous():
    picked = subset(expand(TIMING), {6, 11})
    assert [s.idx for s in picked] == [1, 2, 3, 4]
    assert [s.beat for s in picked] == [6, 6, 11, 11]


def test_subset_of_everything_is_the_untouched_map():
    specs = expand(TIMING)
    assert subset(specs, None) == specs


def test_the_first_slide_of_a_subset_never_morphs_from_nothing():
    # a subset that starts on a B twin or a carry would Morph against the
    # previous deck's last slide, so subset() must promote it
    picked = subset(expand(TIMING), {11})
    assert picked[0].trans != "morph_obj" or picked[0].twin == "a"


def test_a_subset_that_starts_on_a_carry_slide_is_demoted_to_fade():
    # beat 3's A slide is a carry: alone in a subset it has no predecessor to
    # inherit a field from, so Morph has nothing to pair against
    picked = subset(expand(TIMING), {3})
    assert picked[0].carry is False
    assert picked[0].trans == "fade"


def test_subset_keeps_the_carry_flag_when_the_predecessor_is_present():
    picked = subset(expand(TIMING), {2, 3})
    by_beat_twin = {(s.beat, s.twin): s for s in picked}
    assert by_beat_twin[(3, "a")].carry is True
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_cli_args.py -q`
Expected: FAIL — `ImportError: cannot import name 'parse_args' from 'build_deck'` (the old monolith has no such function)

- [ ] **Step 3: Rewrite `build_deck.py`**

```python
#!/usr/bin/env python3
"""Build 信号站 SIGNAL -- a 26-slide spectacle preview, straight over COM.

This file owns no visual decisions. Geometry is in deck/geometry.py, tokens in
deck/palette.py, shapes in deck/stage.py, animation in deck/motion.py, twins in
deck/transitions.py, and the look of each slide class in deck/scenes/.

  python build_deck.py                 # the whole deck, SILENT (the default)
  python build_deck.py --beats 1,6,11  # three beats, for iteration
  python build_deck.py --fast          # half the particles (D7)
  python build_deck.py --audio         # opt in: needs SIGNAL_AUDIO + rights
  python build_deck.py --pdf --video   # also export the static twin and an mp4

There is no --no-audio flag. The default build reads no audio file and names
none; --audio is refused unless SIGNAL_AUDIO is set AND audio.rights.json
declares rights != none (0.0 item 5 / D6).
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import time
from pathlib import Path

import win32com.client as w32

from deck.audio import (audio_allowed, audio_report, mount, read_rights,
                        resolve_source, stage_media)
from deck.comconst import load_constants
from deck.geometry import drifted, starfield, zone_rect
from deck.motion import Clock, Motion
from deck.scenes import SceneContext, scaled, scene_for
from deck.scenes.common import AMBIENT, expected_shapes, star_budget
from deck.stage import Caps, Stage
from deck.sync import adv_time, deck_runtime, expand, ledger, validate
from deck.transitions import DRIFT, apply_transition, clear_timeline, \
    delete_by_prefix, drift_stars, duplicate_twin, retext_meta_index

ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "out"
DEFAULT_OUT = OUT_DIR / "信号站-SIGNAL-preview.pptx"
TIMING_PATH = ROOT / "timing.json"
NOTES_PATH = ROOT / "COM-NOTES.md"


# -- args ------------------------------------------------------------------
def parse_beats(text: str | None) -> set[int] | None:
    if not text:
        return None
    picked: set[int] = set()
    for chunk in text.split(","):
        chunk = chunk.strip()
        if not chunk:
            continue
        m = re.fullmatch(r"(\d+)(?:-(\d+))?", chunk)
        if not m:
            raise ValueError(f"cannot read beat selector {chunk!r}; "
                             f"use a list or a range, e.g. 1-3,11")
        lo = int(m.group(1))
        hi = int(m.group(2) or lo)
        if not (1 <= lo <= hi <= 16):
            raise ValueError(f"beat {chunk!r} is outside 1..16")
        picked.update(range(lo, hi + 1))
    return picked or None


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Build the 信号站 SIGNAL deck.")
    p.add_argument("--beats", default=None,
                   help="subset of beats, e.g. 1,6,11 or 1-3 (default: all 16)")
    p.add_argument("--fast", action="store_true",
                   help="halve star/meteor counts for quick iteration (D7); "
                        "a --fast build cannot pass verify_pptx.py's S11")
    p.add_argument("--audio", action="store_true",
                   help="opt in to a sound bed. Requires SIGNAL_AUDIO and a "
                        "declaration in audio.rights.json. The default build "
                        "is silent and reads no audio file (0.0 item 5 / D6)")
    p.add_argument("--out", default=None, help="output .pptx path")
    p.add_argument("--pdf", action="store_true",
                   help="also export the static twin (S15)")
    p.add_argument("--video", action="store_true", help="also render an mp4")
    p.add_argument("--keep-open", action="store_true",
                   help="leave PowerPoint running for inspection")
    return p.parse_args(argv)


def subset(specs, beats: set[int] | None):
    """Whole beats only -- a twin pair is never split. Slides are renumbered so
    indices stay contiguous and the chrome reads NN / <count>."""
    if beats is None:
        return specs
    picked = [s for s in specs if s.beat in beats]
    if not picked:
        raise ValueError(f"no slides for beats {sorted(beats)}")
    out = []
    for i, spec in enumerate(picked, start=1):
        # the first slide of a subset has no predecessor, so a Morph into it --
        # whether it is a B twin or a declared carry -- has nothing to pair
        # against and must be demoted
        promote = (i == 1 and (spec.is_b or spec.carry))
        out.append(spec.__class__(
            idx=i, beat=spec.beat,
            twin="a" if promote else spec.twin,
            role=spec.role, pattern=spec.pattern, motion=spec.motion,
            hold=spec.hold,
            trans="fade" if promote else spec.trans,
            trans_dur=spec.trans_dur if not promote else 1.0,
            beat_start=spec.beat_start, slide_start=spec.slide_start,
            is_b=False if promote else spec.is_b, data=spec.data,
            carry=False if promote else spec.carry))
    return out


# -- build -----------------------------------------------------------------
def build(args: argparse.Namespace) -> int:
    timing = json.loads(TIMING_PATH.read_text(encoding="utf-8"))
    additive = bool(timing["transition_additive"])
    specs = subset(expand(timing), parse_beats(args.beats))
    full_deck = len(specs) == 26

    if full_deck:
        problems = validate(specs, timing, additive)
        if problems:
            print("timing.json does not satisfy its own laws:", file=sys.stderr)
            for p in problems:
                print(f"  - {p}", file=sys.stderr)
            return 2

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = Path(args.out).resolve() if args.out else DEFAULT_OUT.resolve()
    caps = Caps.from_notes(NOTES_PATH) if NOTES_PATH.is_file() else Caps()
    if not NOTES_PATH.is_file():
        print("WARNING: COM-NOTES.md is missing; every capability is assumed "
              "absent. Run `python probe_com.py` first.", file=sys.stderr)

    # -- the audio gate: two keys, or silence ------------------------------
    track = None
    rights = "none"
    if args.audio:
        allowed, reason = audio_allowed(ROOT)
        if not allowed:
            print(reason, file=sys.stderr)
            return 2
        rights = read_rights(ROOT)["rights"]
        track = stage_media(ROOT)       # raises if SIGNAL_AUDIO is unset
        if rights != "licensed":
            print(f"NOTE: audio rights declared {rights!r}. This build is a "
                  f"local preview and verify_pptx.py will fail its RIGHTS row "
                  f"on purpose (D6). It must not be delivered.", file=sys.stderr)
    else:
        print("audio: silent (default). Pass --audio with SIGNAL_AUDIO and a "
              "declaration in audio.rights.json to add a sound bed.")

    app = w32.gencache.EnsureDispatch("PowerPoint.Application")
    K = load_constants(app)
    app.DisplayAlerts = K.ppAlertsNone
    prs = app.Presentations.Add(WithWindow=True)
    prs.PageSetup.SlideWidth = 960
    prs.PageSetup.SlideHeight = 540

    log: list[str] = []
    fallbacks: list[int] = []
    started = time.perf_counter()
    prev_build = None
    index_of_a = 0

    for spec in specs:
        scene, fell_back = scene_for(spec.role)
        if fell_back:
            fallbacks.append(spec.idx)
        adv = adv_time(spec, additive)
        prev_stars = prev_build.stars if prev_build is not None else None
        seed = spec.beat * 101 + 7

        if spec.is_b:
            # a duplicate twin: clear, re-place, re-seed the ambient layer,
            # and fix the chrome the duplicate inherited
            slide = duplicate_twin(prs, index_of_a)
            removed = clear_timeline(slide)
            killed = delete_by_prefix(slide, "!!meteor_")
            moved = drift_stars(slide, prev_stars, *DRIFT[spec.role])
            stage, motion = Stage(slide, K, caps), Motion(slide, K, caps, Clock())
            if not retext_meta_index(stage, slide, spec.idx, len(specs)):
                raise AssertionError(
                    f"slide {spec.idx}: no !!meta_index_ shape to re-text; the "
                    f"twin would display its A slide's number (CHROME row)")
            ctx = SceneContext(prs=prs, slide=slide, spec=spec, K=K, caps=caps,
                               stage=stage, motion=motion, stars=prev_stars,
                               seed=seed, total=len(specs), fast=args.fast)
            this_build = scene.morph(ctx, prev_build)
            detail = (f"morph(cleared={removed} meteors_killed={killed} "
                      f"drifted={moved}/{len(prev_stars)})")
        else:
            slide = prs.Slides.Add(spec.idx, K.ppLayoutBlank)
            index_of_a = spec.idx
            if spec.carry:
                # declared carry (slides 5, 25): a fresh slide that INHERITS the
                # previous field so Morph pairs it by name and the stars drift
                # across the beat boundary instead of jumping. Same count, same
                # order, same !!star_nnn names -- see the twin law.
                stars = drifted(prev_stars, *DRIFT[spec.role])
                detail = f"build(carry drift={DRIFT[spec.role]} n={len(stars)})"
            else:
                stars = starfield(seed=seed,
                                  count=star_budget(spec, None, args.fast),
                                  zone=zone_rect(spec.role))
                detail = "build"
            stage, motion = Stage(slide, K, caps), Motion(slide, K, caps, Clock())
            ctx = SceneContext(prs=prs, slide=slide, spec=spec, K=K, caps=caps,
                               stage=stage, motion=motion, stars=stars,
                               seed=seed, total=len(specs), fast=args.fast)
            this_build = scene.build(ctx)

        apply_transition(slide, spec, K, adv)
        motion.assert_timing(spec, adv)      # S13/S14, per slide, at build time

        nodes = slide.TimeLine.MainSequence.Count
        shapes = slide.Shapes.Count

        # S11 at build time: the frozen density table is a prediction, so hold
        # it to the prediction here rather than discovering a drift in the audit
        if full_deck:
            want = expected_shapes(spec, prev_stars=prev_stars, fast=args.fast)
            if shapes != want:
                raise AssertionError(
                    f"slide {spec.idx} ({spec.role}): painted {shapes} shapes, "
                    f"the frozen density table predicts {want}. Either the "
                    f"scene changed or AMBIENT did -- update the table and the "
                    f"plan's per-slide column together, never one of them")
        log.append(
            f"slide {spec.idx:>2} beat {spec.beat:>2}{spec.twin} "
            f"{spec.role:<10} {detail:<46} shapes={shapes:>4} nodes={nodes:>3} "
            f"hero={motion.clock.hero_end:>4.2f} "
            f"readable={motion.clock.readable_end:>4.2f} "
            f"ambient={motion.clock.ambient_end:>6.2f} "
            f"trans={spec.trans}:{spec.trans_dur} advTm={adv:>5.2f}"
            + ("  FALLBACK" if fell_back else ""))
        print(log[-1])
        prev_build = this_build

    if track is not None:
        mount(prs.Slides(1), track, K, caps, total_slides=len(specs),
              expected_sec=float(timing["audio_duration_sec"]),
              motion=Motion(prs.Slides(1), K, caps, Clock()))

    prs.SaveAs(str(out_path), K.ppSaveAsOpenXMLPresentation)

    if args.pdf:
        pdf = out_path.with_name("信号站-SIGNAL-static-twin.pdf")
        prs.ExportAsFixedFormat(str(pdf), K.ppFixedFormatTypePDF)
        print(f"pdf  -> {pdf}")
    if args.video:
        mp4 = out_path.with_name("信号站-SIGNAL-preview.mp4")
        prs.CreateVideo(str(mp4), True, 2, 1080, 30, 85)
        while prs.CreateVideoStatus == 1:    # ppMediaTaskStatusInProgress
            time.sleep(2.0)
        print(f"video -> {mp4} (status {prs.CreateVideoStatus})")

    elapsed = time.perf_counter() - started
    total_shapes = sum(prs.Slides(i).Shapes.Count
                       for i in range(1, prs.Slides.Count + 1))
    total_nodes = sum(prs.Slides(i).TimeLine.MainSequence.Count
                      for i in range(1, prs.Slides.Count + 1))

    (OUT_DIR / "_sync-ledger.txt").write_text(ledger(specs, additive),
                                              encoding="utf-8")
    summary = [
        f"slides      {prs.Slides.Count}",
        f"shapes      {total_shapes}",
        f"timeline    {total_nodes} main-sequence effects",
        f"runtime     {deck_runtime(specs, additive):.1f}s "
        f"(additive={additive}, track {timing['audio_duration_sec']}s)",
        f"build time  {elapsed:.1f}s",
        f"fallbacks   {fallbacks or 'none'}",
        f"fast        {args.fast}",
        f"output      {out_path}",
    ]
    if track is not None:
        summary.append("audio       " + json.dumps(
            audio_report(track, resolve_source(), rights), ensure_ascii=False))
    else:
        summary.append("audio       silent (default; no media part, no rights "
                       "claim, RIGHTS row passes trivially)")
    (OUT_DIR / "_build-log.txt").write_text(
        "\n".join(log + [""] + summary) + "\n", encoding="utf-8")
    print("\n".join([""] + summary))

    if not args.keep_open:
        prs.Close()
        app.Quit()
    return 0


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    return build(parse_args(argv))


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run the CLI test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_cli_args.py -q`
Expected: `15 passed`

- [ ] **Step 5: Confirm the help text and the arg surface without touching PowerPoint**

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --help`
Expected: usage listing `--beats`, `--fast`, `--audio`, `--out`, `--pdf`, `--video`, `--keep-open` — and **no** `--no-audio`.

Run: `Set-Location demos/signal-station-pptx; python -c "from build_deck import parse_beats,subset;from deck.sync import expand;import json;t=json.load(open('timing.json',encoding='utf-8'));print([(s.idx,s.beat,s.twin,s.trans,s.carry) for s in subset(expand(t), parse_beats('1,6,11'))])"`
Expected: six slides, renumbered 1–6, beats 1/1/6/6/11/11, slide 1 carrying `cut`, and no `carry=True` in the set.

Run: `Set-Location demos/signal-station-pptx; python -c "from build_deck import parse_args, build; import sys; sys.exit(build(parse_args(['--audio','--beats','1'])))"`
Expected: exit code 2 and the `audio.rights.json` refusal on stderr, **before** PowerPoint opens. The gate must be checked ahead of the COM work, or a refused build still costs a minute.

- [ ] **Step 6: Whole pure suite still green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests -q`
Expected: ≥ `251 passed`, 0 failed

**Accept:** `python -m pytest tests -q` reports ≥ 251 passed, 0 failed; `python build_deck.py --help` lists all seven flags and no `--no-audio`; `--audio` without a declaration exits 2 before opening PowerPoint; `build_deck.py` is under ~340 lines and contains no colour, size, coordinate or effect constant (`Select-String -Pattern "msoAnim|ppEffect|#[0-9A-Fa-f]{6}" -Path build_deck.py` returns only the `K.`-prefixed uses and the import lines).

---

### Task 14: first real build — three beats, end to end

Everything has been proven in isolation. This task runs the whole path once on a **six-slide subset** chosen to exercise every hard mechanism exactly once, so a failure is cheap to diagnose:

| Beat | Slides | What it proves |
|---|---|---|
| 1 `cold_open` | 1, 2 | the twin machine: duplicate → clear → drift → reseed, under a `cut` entry and a 2.0 s Morph |
| 6 `chapter` | 3, 4 | a signature transition (`RippleCenter`) and the rule-to-horizon Morph |
| 11 `spike` | 5, 6 | the iris: `CircleOut` in, then D3's donut growth with an exit under it |

Build it silent and fast first — the point is the mechanism, not the density — then once at full density with audio.

**Files:** none created. This task is build, inspect, fix.

- [ ] **Step 1: Build the subset, silent and fast**

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --beats 1,6,11 --fast --out out/_subset.pptx`

(No `--no-audio` is needed or accepted: silent is the default.)

Expected: `audio: silent (default)`, six log lines, then a summary. Specifically check in the log:
- slides 2, 4, 6 read `morph(cleared=N meteors_killed=M drifted=K/K)` with `K` equal to the `--fast` star count for that role (48 / 48 / 56 — half of the frozen table's 96 / 96 / 112), `M` ≥ 1, and `N` ≥ 20. **`drifted` must equal the budget on both sides of the slash**; a shortfall means `drift_stars` and the painted field disagree.
- every `hero=` value is ≤ 1.50 and every `readable=` is ≤ 5.00 — if not, the build would already have raised from `assert_timing`.
- `advTm` on slide 2 is `2.00` (hold 4.0 − morph 2.0, under `additive=true`).
- no line is marked `FALLBACK`.
- `fallbacks none` in the summary.

- [ ] **Step 2: Fix whatever the first run throws**

The failures this path produces are specific and each has one cause. Work through them in this order rather than guessing:

| Symptom | Cause | Fix |
|---|---|---|
| `AssertionError: slide N: hero signal final at X` | a scene's hero entrance has too much delay + duration | shorten in the scene module; never raise `HERO_LIMIT` |
| `AssertionError: slide N: advTm ... does not clear readable_end` | the scene's last readable event runs past the hold | shorten the entrance, or move it into the ambient layer if it is genuinely decorative |
| `drifted=0` on a B twin | `drift_stars` got a different star list than the A slide painted | the A slide's stars must come from `prev_build.stars`; confirm `ambient_layer` returns `list(ctx.stars)` |
| `AssertionError: slide N: painted X shapes, the frozen density table predicts Y` | a scene's fixture count drifted from `AMBIENT[role].fixtures` | fix whichever is wrong, then update §Frozen density table, `AMBIENT` and the plan's per-slide column **together** |
| `AssertionError: slide N: no !!meta_index_ shape to re-text` | `chrome()` did not run, or its name changed | the chrome is part of every scene's stack; check the builder calls `chrome(ctx)` |
| slide 2's corner still reads `01 / 2` | `retext_meta_index` was skipped | it runs in `build_deck`'s B branch, before `scene.morph` |
| a B twin goes visually static after the Morph | `morph()` called `reseed_meteors` instead of `reseed_ambient` | the twinkle and breath were deleted by `clear_timeline` and must be re-added |
| transitions all look like fast cuts | `Speed` written after `Duration` | Task 9 Step 3: `Speed` first. `apply_transition` reads `Duration` back and should have raised |
| `pywintypes.com_error ... -2147024809` on `Shapes(...)` | a `!!` name does not exist on the duplicate | check `name_of` indices; the B twin's meteors are deliberately at index base 500 |
| `ValueError: could not route N meteors` | a role's zone leaves no clear lane at that count | widen `_LANES` in `geometry.py`; do **not** relax `path_clears_zone` (D5) |
| `AttributeError: COM constant ...` | a constant missing from both the typelib and `VERIFIED` | re-run `probe_com.py`, add the row to `VERIFIED` |
| build hangs with a PowerPoint dialog | `DisplayAlerts` not set, or a stale PowerPoint instance | close all PowerPoint windows, re-run |

- [ ] **Step 3: Human review of the subset at full density**

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --beats 1,6,11 --out out/_subset.pptx`

Open `out/_subset.pptx`, press F5 and watch it twice. The six things that must be true:

1. **Slides 1→2 read as one shot.** Stars drift down-left with visible parallax, the amber bar lengthens, the title does not flicker or re-enter.
2. **The meteors travel.** At least one streak crosses the frame per slide, at a steady linear speed (D4), and none of them crosses the title.
3. **Slide 2→3 is the chapter door.** `RippleCenter` is unmistakable and reads as a chapter break, not a glitch.
4. **Slide 3→4 extends the rule into a horizon** while 校准 holds still.
5. **Slide 4→5 is the iris.** `CircleOut` opens onto the spike plate.
6. **Slide 5→6 is the D3 growth.** The donut's **aperture** opens past all four corners — by the end of the Morph there must be no dark ring left anywhere in frame, including the corners — and the dark type fades out underneath it. This must feel like the loudest moment of the six slides. If a dark annulus is still visible at the corners when the Morph settles, `IRIS_GROWTH` is too small; the unit test in Task 12 should have caught it.
7. **Both twins stay alive.** After each Morph settles, stars are still twinkling and fresh meteors still cross. A twin that freezes for the rest of its hold is the missing ambient reseed.

Anything that fails here is a scene-module fix (Tasks 11–12), not a plumbing fix. Note each change in `COM-NOTES.md` under `## Subset review notes`.

- [ ] **Step 4: Check the generated XML already looks the way Task 16 will demand**

A quick pre-audit, so Task 16 is not the first time the file structure is inspected. Add an `xray` subcommand to `smoke.py` (pure python — no COM):

```python
def cmd_xray() -> int:
    """Dump the per-slide XML facts Task 16 will audit, from any built pptx."""
    import re
    import zipfile
    target = Path(sys.argv[2]) if len(sys.argv) > 2 else OUT / "_subset.pptx"
    with zipfile.ZipFile(target) as z:
        names = sorted((n for n in z.namelist()
                        if re.fullmatch(r"ppt/slides/slide\d+\.xml", n)),
                       key=lambda n: int(re.findall(r"\d+", n)[0]))
        print("slides in package:", len(names))
        for name in names:
            xml = z.read(name).decode("utf-8")
            trans = re.search(r"<(?:\w+:)?transition\b[^>]*>", xml)
            child = re.search(r"<(\w+):(morph|fade|cut|circle|ripple|window)\b", xml)
            print(name,
                  "shapes", xml.count("<p:sp>") + xml.count("<p:pic>"),
                  "effects", len(re.findall(r"<p:cTn[^>]*presetID=", xml)),
                  "animMotion", xml.count("<p:animMotion"),
                  "stars", len(re.findall(r'name="!!star_', xml)),
                  "altContent", xml.count("<mc:AlternateContent"),
                  "trans", (trans.group(0)[:70] if trans else "none"),
                  "child", (child.group(2) if child else "none"))
        print("media parts:", [n for n in z.namelist()
                               if n.startswith("ppt/media/")])
    return 0
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py xray out/_subset.pptx`

Expected: 6 slides; `animMotion` ≥ 1 on every slide; `stars` matching the build log; `effects` (the `presetID`-bearing `cTn` count S10 uses) non-zero on every slide; a transition element on every slide carrying `advTm` and a `dur`; `child morph` on slides 2, 4, 6; and **no** `ppt/media/` parts, because the default build is silent.

If `advTm` is absent, PowerPoint did not accept `AdvanceOnTime` — check that `apply_transition` sets `AdvanceOnTime` **before** `AdvanceTime`. If `trans` shows no `dur`, the Speed/Duration order regressed (Task 9 Step 3).

- [ ] **Step 5: Capture the real transition XML as test fixtures**

**Task 16's parser must be tested against XML PowerPoint actually writes, not XML this plan invented.** PowerPoint does not emit a bare `<p:morph>`: a Morph is wrapped in `<mc:AlternateContent>` with an `<mc:Choice Requires="p159">` branch holding the real effect and an `<mc:Fallback>` holding a plain `<p:transition>` for older readers, and the effect element is namespace-prefixed (`p159:morph`, `p14:ripple`, `p14:window`). A regex written against a guessed shape passes its own unit test and then silently reports `transition: "none"` for all 26 slides, which would make MAP, S1, S6 and S7 meaningless.

So capture the fixtures from the subset build, plus a whole slide for the text/size/breath rows:

```python
def cmd_fixtures() -> int:
    """Cut real transition blocks out of a built deck into tests/fixtures/."""
    import re
    import zipfile
    target = Path(sys.argv[2]) if len(sys.argv) > 2 else OUT / "_subset.pptx"
    # subset 1,6,11 -> slide 2 morph, slide 3 ripple, slide 5 circle
    wanted = {"transition-morph.xml": 2, "transition-ripple.xml": 3,
              "transition-circle.xml": 5}
    fixtures = ROOT / "tests" / "fixtures"
    fixtures.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(target) as z:
        for filename, index in wanted.items():
            xml = z.read(f"ppt/slides/slide{index}.xml").decode("utf-8")
            block = re.search(
                r"<mc:AlternateContent\b.*?</mc:AlternateContent>|"
                r"<p:transition\b.*?(?:</p:transition>|/>)", xml, flags=re.S)
            if block is None:
                raise SystemExit(f"no transition block in slide {index}; "
                                 f"nothing to capture -- fix the build first")
            (fixtures / filename).write_text(block.group(0), encoding="utf-8")
            print("wrote", filename, f"({len(block.group(0))} chars)")
        # one whole slide, for the text / size / tracking / breath rows
        (fixtures / "slide-breath.xml").write_text(
            z.read("ppt/slides/slide1.xml").decode("utf-8"), encoding="utf-8")
        print("wrote slide-breath.xml")
    return 0
```

Run: `Set-Location demos/signal-station-pptx; python smoke.py fixtures out/_subset.pptx`

Then read each captured file once, by eye, and write down in `COM-NOTES.md` under `## Transition XML as written`: whether the block is wrapped in `<mc:AlternateContent>`, the namespace prefix on the effect element (`p159`, `p14`, or none), and the exact attribute names carrying the duration and the advance (`p14:dur` vs `dur`, `advTm`). **Task 16's `parse_slide_xml` is written against what is in that note**, and `tests/test_verify_rules.py` parses these files rather than string literals.

Also capture the `door_window` fixture — the subset does not include beat 9, so take it after the full build in Task 15: `python smoke.py fixtures out/信号站-SIGNAL-preview.pptx` with `wanted` extended to `{"transition-window.xml": 14}`. Task 16's fixture test must **fail**, not skip, if any of the four files is missing.

- [ ] **Step 6: Confirm the subset path and the full path disagree about nothing but size**

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --beats 1 --fast --out out/_one-beat.pptx`
Expected: two slides, chrome reading `01 / 2` and `02 / 2` — the subset renumbers, and slide 2's mark proves `retext_meta_index` ran on the duplicate. Exit code 0. Note that `--fast` halves the star counts, so the build-time `expected_shapes` assertion runs with `fast=True` and the numbers are the halved ones; this build must not be audited by `verify_pptx.py`.

- [ ] **Step 7: Record the integration result**

Append to `COM-NOTES.md` under `## Subset build`: the six log lines from Step 3, the Step 4 XML counts, the Step 5 transition-XML note, and one sentence per numbered item in Step 3 saying whether it read correctly. Task 18 quotes this as the evidence that the mechanisms work before the full build's scale is introduced.

**Accept:** `python build_deck.py --beats 1,6,11 --out out/_subset.pptx` exits 0 with no `FALLBACK` line and no assertion; the Step 4 x-ray shows 6 slides, ≥ 1 `animMotion` per slide, a `morph` child on slides 2/4/6, an `advTm` and a non-zero `dur` on all six, and no media part; `tests/fixtures/` contains three captured transition blocks plus one whole slide, all non-empty; `COM-NOTES.md` records the real wrapper and namespace prefixes; all six human checks in Step 3 pass; `python -m pytest tests -q` still reports ≥ 251 passed.

---

### Task 15: the full 26-slide build at the real density

The first time the whole thing runs. Expect minutes, not seconds (D7): 2,867 shapes and ~670 animation effects go over COM one call at a time.

This task also retires the old deck. The monolith's body is already gone (Task 13 rewrote the file in place), so what remains is to prove nothing still depends on it and that the new output replaces the old one at the same path.

**Files:** none created. One possible modification: `timing.json` holds, if the density forces a re-balance (any change there must keep `tests/test_timing_data.py` green).

- [ ] **Step 1: Dry-run the laws before spending the build time**

Run: `Set-Location demos/signal-station-pptx; python -c "
import json
from deck.sync import expand, validate, deck_runtime, ledger
t = json.load(open('timing.json', encoding='utf-8'))
s = expand(t); a = t['transition_additive']
print('problems:', validate(s, t, a) or 'none')
print('runtime:', deck_runtime(s, a))
open('out/_sync-ledger.txt','w',encoding='utf-8').write(ledger(s, a))
print(ledger(s, a))"`

Expected: `problems: none`, `runtime: 229.5`, and a 28-line ledger. A build started with a problem here will waste several minutes before failing.

- [ ] **Step 2: Full build at full density, with audio and the static twin**

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --pdf`

Expected: 26 log lines, then a summary. Hold it against the checklist bands:

| Summary field | Must be | Checklist row |
|---|---|---|
| `slides` | 26 | the slide map |
| `shapes` | ≥ 2000 | S11 |
| `timeline` | 420 ≤ n ≤ 1200 | S10 |
| `runtime` | 229.5 s | §Mode A |
| `fallbacks` | `none` | Task 12 registry |
| per-slide `shapes=` | 80 ≤ n ≤ 170 | S11 |
| per-slide `hero=` | ≤ 1.50 | S13 |
| per-slide `readable=` | ≤ 5.00 | S13 |

If a per-slide shape count is **under 80**, raise that role's `AMBIENT` star count. If it is **over 170**, lower it. Both are one-line edits in `deck/scenes/common.py`; re-run and re-check, because `AMBIENT` also feeds the contract test's expectations — update `test_ambient_table_matches_the_effect_vocabulary` and the §Effect vocabulary table in this plan in the same edit so the three never drift apart.

- [ ] **Step 3: Verify the two watch points against the real clock**

This is S16, and it is the only check that proves the deck is actually synced rather than merely arithmetically consistent.

Open `out/信号站-SIGNAL-preview.pptx`, press F5, and run it from slide 1 with a stopwatch (or watch the audio position):
- At **1:00 ± 1.5 s** the 校准 chapter door (slide 9, `RippleCenter`) must fire.
- At **2:04 ± 1.5 s** the iris (slide 17, `CircleOut`) must fire.
- At **3:49.5** the deck must end with the audio, not before and not after.

If both watch points are early or late by a similar amount, the `transition_additive` verdict from Task 5 is wrong — flip it in `timing.json`, re-run `tests/test_sync.py`, and rebuild. If only one is off, a single hold is wrong; the ledger tells you which slide drifted.

- [ ] **Step 4: Confirm the static twin reads with the motion gone (S15)**

Open `out/信号站-SIGNAL-static-twin.pdf`. It must be 26 pages, and every page must be readable as a still: no page that only makes sense mid-animation, nothing important hidden behind a mid-flight meteor, no page where the hero type is invisible because its entrance had not started. Pages 2, 8 and 26 are the near-wordless plates D1 promised — confirm they are **three** such pages, since D1's honesty cap depends on it.

- [ ] **Step 5: Spot-check the spectacle rows by eye before the machine audits them**

Watch the full show once with the checklist open and tick the human-visible half:

- S1/S2 — the Morph slides read as continuous drift, 12 of them, with near/far parallax.
- S3 — meteors on at least 10 slides, none crossing readable type.
- S4 — the title beat visibly *assembles*: scattered glyphs lock into a line.
- S5 — glow reads as a soft core on at least 6 slides, not as concentric rings.
- S6/S7 — exactly one iris and exactly two chapter doors; no two adjacent.
- S8 — the proof cards punch in and then re-rack tighter.
- S9 — nothing pulses distractingly; the breath is barely perceptible.
- S13 — every slide is readable; nothing is still arriving when the slide changes.

- [ ] **Step 6: Retire the old monolith for real**

Run: `Set-Location demos/signal-station-pptx; Get-ChildItem -Recurse -Filter *.py | Select-String -Pattern 'MSO_ANIM_|PP_TRANSITION_|0x10A7|slides"\]' ; Select-String -Pattern "build_deck" -Path ../../docs/superpowers/specs/2026-10-11-signal-station-pptx-preview-design.md`

Expected: the first `Select-String` returns nothing — every hard-coded constant name from the root-cause table is gone from the tree. The second may return spec references to `build_deck.py`, which are still correct since the path did not change; if the spec describes the old flat `slides` array in `timing.json`, note the discrepancy for Task 18's deviation ledger rather than editing the approved spec.

Run: `Set-Location demos/signal-station-pptx; git status --porcelain`
Expected: modifications to `build_deck.py`, `timing.json`, `README.md`, `.gitignore` and the new `deck/`, `tests/`, `*.md`, `*.json` files — and **nothing** under `media/` or `out/`.

- [ ] **Step 7: Rebuild once more from clean, to prove determinism**

The same seed must give the same deck, or the plan's whole testing approach is hollow.

Run: `Set-Location demos/signal-station-pptx; python build_deck.py --out out/_determinism.pptx --no-audio --pdf`
Then: `Set-Location demos/signal-station-pptx; python -c "
import re, zipfile
def fingerprint(path):
    z = zipfile.ZipFile(path); out = []
    for n in sorted(z.namelist()):
        if re.fullmatch(r'ppt/slides/slide\d+\.xml', n):
            x = z.read(n).decode('utf-8')
            out.append((n, x.count('<p:sp>'), x.count('<p:cTn'),
                        len(re.findall(r'<a:off x=\"(-?\d+)\"', x))))
    return out
a = fingerprint('out/_determinism.pptx')
import subprocess, sys
subprocess.run([sys.executable,'build_deck.py','--out','out/_determinism2.pptx','--no-audio'], check=True)
b = fingerprint('out/_determinism2.pptx')
print('identical' if a == b else 'DIFFERS')
for x, y in zip(a, b):
    if x != y: print(x, y)"`

Expected: `identical`. A difference means something in a scene is reading `random` or the clock instead of the LCG — find it and route it through `deck/geometry.LCG`.

- [ ] **Step 8: Record the full-build numbers**

Append `out/_build-log.txt`'s summary block into `COM-NOTES.md` under `## Full build`, plus the Step 3 stopwatch readings and the Step 4 PDF verdict. Task 16 audits the same file; Task 18 quotes both.

**Accept:** `python build_deck.py --pdf` exits 0 with 26 slides, `fallbacks none`, shapes ≥ 2000, timeline nodes inside 420–1200, and every per-slide `hero` ≤ 1.50 / `readable` ≤ 5.00; the two watch points land within ±1.5 s by stopwatch; the PDF has 26 readable pages including exactly 3 near-wordless plates; Step 6's constant `grep` is empty and `git status` shows nothing under `media/` or `out/`; Step 7 prints `identical`.

---

### Task 16: `verify_pptx.py` — audit the shipped file, not the builder's intentions

The build log reports what the builder *believed*. This script reports what is actually inside the `.pptx`, by unzipping it and reading the XML with no COM and no PowerPoint. That distinction matters: PowerPoint silently drops properties it does not like, and a claim that rests on the builder's own log is not evidence.

**One row per `S*` line in the spectacle checklist.** Four things about this script are load-bearing and were wrong in the first draft:

1. **`MANUAL` must not let the script exit 0.** Printing `MANUAL` and returning 0 means a deck with two unverified rows reports success, which is exactly the "a row that cannot be checked counts as failed" clause being violated by the tool that is supposed to enforce it. S15/S16 are resolved from `tests/manual-verdicts.json`; missing, incomplete or `FAIL` ⇒ exit 1.
2. **Copyright and audio-rights failures must block.** The `RIGHTS` and `BAN` rows are ordinary failing rows, so they force exit 1 like any other. There is no flag to downgrade them.
3. **The audit reads the slide XML, not `timing.json`.** `BAN` scans every slide's `<a:t>` runs and the `ppt/media/*` part names; `CHROME` reads the rendered meta index. Auditing `timing.json` only proves the input was clean.
4. **The transition parser must match what PowerPoint writes.** Morph, ripple and window transitions are wrapped in `<mc:AlternateContent>` with a namespace-prefixed effect element inside `<mc:Choice>` (`p159:morph`, `p14:ripple`, `p14:window`) and a bare `<p:transition>` in `<mc:Fallback>`. The tests parse the **real fixtures captured in Task 14 Step 5**, never invented XML.

Density comes from `deck/scenes/common.AMBIENT` — the §Frozen density table — so S2 and S11 cannot drift from what the scenes paint.

**Files:**
- Create: `demos/signal-station-pptx/verify_pptx.py`
- Create: `demos/signal-station-pptx/tests/test_verify_rules.py`

**Interfaces:**
- CLI: `python verify_pptx.py [path] [--json out.json]`. Exit `0` when every machine-checkable row passes, `1` otherwise.
- Produces: `Slide` (parsed per-slide facts), `load(path) -> list[Slide]`, one `check_s*(slides, timing) -> Row` per row, `audit(path, timing) -> list[Row]`, `Row(id, ok, detail)`.
- Reads: the built `.pptx` and `timing.json` (so the expected transition per slide comes from the same source of truth the build used).

The XML facts each row needs, and where they live in the package:

| Fact | Where | Note |
|---|---|---|
| shape count | `<p:sp>` + `<p:pic>` in `ppt/slides/slideN.xml` | `<p:pic>` catches the media shape |
| shape names | `<p:cNvPr ... name="!!star_007">` | the `!!` naming law from Task 7 |
| positions | `<a:off x=".." y="..">` in EMU | 12700 EMU per point |
| animation nodes | `<p:cTn ...>` | S10's corpus band |
| motion paths | `<p:animMotion ...>` | S3 |
| transition + advance | `<p:transition spd=".." advTm="..">` and its child element name | the child is `<p:morph option="byObject"/>`, `<p:fade/>`, `<p:circle/>`, … |
| run sizes | `<a:rPr sz="4000">` | hundredths of a point |
| tracking | `<a:rPr spc="400">` | hundredths of a point; S12 wants ≥ 400 |
| audio part | `ppt/media/*.mp3` + `<p:sndTgt>` / `ppt/slides/_rels` | S-audio row |

- [ ] **Step 1: Write the failing test**

The rules are testable without a real deck by feeding them synthetic `Slide` records. `demos/signal-station-pptx/tests/test_verify_rules.py`:

```python
import json
from pathlib import Path

import pytest

from deck.scenes.common import AMBIENT
from verify_pptx import (
    B_TWINS, EMU_PER_PT, Slide, audit, check_s1, check_s2, check_s6, check_s7,
    check_s9, check_s10, check_s11, check_s13, check_s14, parse_breaths,
    parse_slide_xml, parse_transition,
)

ROOT = Path(__file__).resolve().parents[1]
TIMING = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))


def make_slide(index=1, transition="fade", morph_option=None, shapes=100,
               ctn=20, motions=1, names=None, offsets=None, exts=None,
               texts=None, sizes=(4000,), spc=(400,), adv=6.0, grow=0,
               grow_pct=104.0, grow_dur=7.0, max_scale=0.0):
    return Slide(index=index, transition=transition, morph_option=morph_option,
                 trans_dur=1.0, adv_tm=adv, shape_count=shapes, ctn_count=ctn,
                 motion_count=motions, names=list(names or []),
                 offsets=dict(offsets or {}), exts=dict(exts or {}),
                 texts=list(texts if texts is not None
                            else [f"{index:02d} / 26"]),
                 run_sizes=list(sizes), run_spc=list(spc), grow_count=grow,
                 grow_pct=grow_pct, grow_duration=grow_dur,
                 max_scale_pct=max_scale)


def test_emu_conversion_is_the_office_constant():
    assert EMU_PER_PT == 12700


def test_s1_needs_twelve_morph_slides_and_ten_by_object():
    slides = [make_slide(i, "morph", "byObject") for i in range(1, 12)]
    slides.append(make_slide(12, "morph", "byWord"))
    slides += [make_slide(i) for i in range(13, 27)]
    assert check_s1(slides, TIMING).ok is True
    assert check_s1(slides[:-15], TIMING).ok is False


def _twin_pair(role="cold_open", a_index=1, b_index=2, near_shift=1200,
               far_shift=200, count=None):
    """An (A, B) pair whose B is slide `b_index` -- a genuine twin in the map."""
    from deck.scenes.common import AMBIENT
    count = count if count is not None else AMBIENT[role].stars
    stars = {f"!!star_{i:03d}": (i * 1000, 5000) for i in range(count)}
    exts = {k: (1000 + i * 100, 1000 + i * 100)   # bigger == nearer
            for i, k in enumerate(stars)}
    a = make_slide(a_index, names=list(stars), offsets=stars, exts=exts)
    moved = {k: (x + (far_shift if i < count // 2 else near_shift), y)
             for i, (k, (x, y)) in enumerate(stars.items())}
    b = make_slide(b_index, "morph", "byObject", names=list(moved),
                   offsets=moved, exts=exts)
    return a, b


def _full_deck(near_shift=1200, far_shift=200, short_role=None):
    """All 26 slides, with every one of the ten B twins properly displaced.

    A single pair could never make S2 pass, so the old test asserted on the
    detail string instead of on `ok` -- which is why it stayed green while the
    row itself counted nine twins against a map that has ten.
    """
    from deck.sync import expand
    specs = {s.idx: s for s in expand(TIMING)}
    slides, prev_stars = [], {}
    for i in range(1, 27):
        spec = specs[i]
        if spec.is_b or spec.carry:
            moved = {k: ((x + (far_shift if n < len(prev_stars) // 2
                               else near_shift)), y)
                     for n, (k, (x, y)) in enumerate(prev_stars.items())}
            exts = {k: (1000 + n * 100, 1000 + n * 100)
                    for n, k in enumerate(moved)}
            slides.append(make_slide(i, "morph", "byObject", names=list(moved),
                                     offsets=moved, exts=exts))
            prev_stars = moved
            continue
        count = AMBIENT[spec.role].stars
        if short_role is not None and spec.role == short_role:
            count -= 1
        stars = {f"!!star_{n:03d}": (n * 1000, 5000) for n in range(count)}
        exts = {k: (1000 + n * 100, 1000 + n * 100)
                for n, k in enumerate(stars)}
        slides.append(make_slide(i, names=list(stars), offsets=stars,
                                 exts=exts))
        prev_stars = stars
    return slides


def test_s2_passes_only_when_all_ten_b_twins_drift_their_whole_budget():
    # the old row tested a flat ">= 56", which sat ABOVE four roles' budgets
    # and below every other -- it could not express "all of them"
    row = check_s2(_full_deck(), TIMING)
    assert row.ok is True, row.detail
    assert "10 Morph-B twins" in row.detail


def test_s2_fails_when_the_map_supplies_fewer_than_ten_b_twins():
    deck = _full_deck()
    row = check_s2(deck[:12], TIMING)     # only the first few twins present
    assert row.ok is False
    assert "expected 10" in row.detail


def test_s2_fails_when_one_twins_role_is_a_star_short():
    row = check_s2(_full_deck(short_role="proof"), TIMING)
    assert row.ok is False
    assert f"/{AMBIENT['proof'].stars}" in row.detail


def test_s2_fails_when_a_single_star_is_left_behind():
    from deck.scenes.common import AMBIENT
    a, b = _twin_pair(count=AMBIENT["cold_open"].stars - 1)
    row = check_s2([a, b], TIMING)
    assert row.ok is False
    assert f"/{AMBIENT['cold_open'].stars}" in row.detail


def test_s2_fails_when_every_star_moves_the_same_distance():
    a, b = _twin_pair(near_shift=500, far_shift=500)
    row = check_s2([a, b], TIMING)
    assert row.ok is False
    assert "parallax" in row.detail


def test_s2_ignores_a_morph_that_is_not_a_b_twin():
    # slides 5 and 25 carry a Morph but are not duplicates; they belong to the
    # CARRY row, and sweeping them into S2 would measure them against the wrong
    # star budget
    specs = {s.idx: s for s in __import__("deck.sync", fromlist=["expand"]).expand(TIMING)}
    assert specs[5].carry and not specs[5].is_b
    assert specs[25].carry and not specs[25].is_b


def test_s7_fails_on_a_third_signature_transition():
    slides = [make_slide(i) for i in range(1, 27)]
    slides[8] = make_slide(9, "ripple")
    slides[13] = make_slide(14, "window")   # the real local name, not wipe_window
    assert check_s7(slides, TIMING).ok is True
    slides[20] = make_slide(21, "ripple")
    assert check_s7(slides, TIMING).ok is False


def test_s9_caps_the_breath_at_one_per_slide_106pct_and_six_seconds():
    assert check_s9([make_slide(grow=1, grow_pct=106.0, grow_dur=6.0)], TIMING).ok
    assert not check_s9([make_slide(grow=2)], TIMING).ok
    assert not check_s9([make_slide(grow=1, grow_pct=140.0)], TIMING).ok
    assert not check_s9([make_slide(grow=1, grow_dur=2.0)], TIMING).ok


def test_s10_is_the_corpus_band():
    assert check_s10([make_slide(ctn=20) for _ in range(26)], TIMING).ok  # 520
    assert not check_s10([make_slide(ctn=2) for _ in range(26)], TIMING).ok
    assert not check_s10([make_slide(ctn=60) for _ in range(26)], TIMING).ok


def test_s11_checks_both_the_per_slide_band_and_the_total():
    assert check_s11([make_slide(shapes=100) for _ in range(26)], TIMING).ok
    assert not check_s11([make_slide(shapes=40) for _ in range(26)], TIMING).ok
    assert not check_s11([make_slide(shapes=200) for _ in range(26)], TIMING).ok


def test_s13_rejects_the_dead_band_and_demands_a_readable_run():
    assert check_s13([make_slide(sizes=(4000, 1000))], TIMING).ok
    assert not check_s13([make_slide(sizes=(1500,))], TIMING).ok   # dead band
    assert not check_s13([make_slide(sizes=(1000,))], TIMING).ok   # nothing readable


def test_s14_compares_advtm_against_the_ledger():
    slides = []
    from deck.sync import adv_time, expand
    for spec in expand(TIMING):
        slides.append(make_slide(spec.idx, adv=adv_time(spec, TIMING["transition_additive"])))
    assert check_s14(slides, TIMING).ok is True
    slides[4] = make_slide(5, adv=0.2)
    assert check_s14(slides, TIMING).ok is False


FIXTURES = ROOT / "tests" / "fixtures"
FIXTURE_EXPECT = {
    "transition-morph.xml": "morph",
    "transition-ripple.xml": "ripple",
    "transition-window.xml": "window",
    "transition-circle.xml": "circle",
}


@pytest.mark.parametrize("filename,want", sorted(FIXTURE_EXPECT.items()))
def test_parse_transition_against_real_powerpoint_xml(filename, want):
    """The transition parser is tested against XML PowerPoint actually wrote.

    These files are captured by `python smoke.py fixtures` in Task 14 Step 5.
    They are REQUIRED, not optional: a skip here means the parser is unverified
    against mc:AlternateContent and namespace-prefixed effect elements, which
    is how a regex can pass its own test and then report "none" for all 26
    slides -- silently voiding MAP, S1, S6 and S7.
    """
    path = FIXTURES / filename
    assert path.is_file(), (
        f"missing {path}. Capture it with `python smoke.py fixtures "
        f"out/信号站-SIGNAL-preview.pptx` (Task 14 Step 5). Do not replace it "
        f"with hand-written XML.")
    xml = path.read_text(encoding="utf-8")
    kind, option, dur, adv = parse_transition(xml)
    assert kind == want, (kind, xml[:200])
    assert adv > 0.0, "every slide in this deck auto-advances"
    if want == "morph":
        assert option in ("byObject", "byWord", "byChar")
    if "AlternateContent" in xml:
        # the Fallback branch holds a bare <p:transition> with no effect child;
        # picking it up instead of the Choice branch is the failure mode
        assert kind != "none"


def test_parse_slide_xml_reads_a_whole_real_slide():
    path = FIXTURES / "slide-breath.xml"
    assert path.is_file(), (
        f"missing {path}; capture it with `python smoke.py fixtures` (Task 14)")
    slide = parse_slide_xml(1, path.read_text(encoding="utf-8"))
    assert slide.shape_count > 90                 # the frozen table's floor
    assert slide.names and any(n.startswith("!!star_") for n in slide.names)
    assert slide.offsets and slide.exts           # S2 needs both
    assert slide.texts                            # BAN and CHROME need text
    assert slide.motion_count >= 1                # S3
    assert slide.ctn_count >= 1                   # S10 counts presetID nodes
    assert slide.transition != "none"
    assert slide.adv_tm > 0.0


def test_a_breath_is_detected_by_semantics_not_by_animscale():
    # repeating + auto-reversing + >=6s == a breath
    breath = ('<p:cTn id="9" dur="6000" autoRev="1" repeatCount="indefinite">'
              '<p:childTnLst><p:animScale><p:to x="106000" y="106000"/>'
              '</p:animScale></p:childTnLst></p:cTn>')
    assert parse_breaths(breath) == [(106.0, 6.0)]


def test_an_entrance_zoom_is_not_counted_as_a_breath():
    # FadedZoom / RiseUp / Zoom all emit animScale. The old implementation
    # counted every one of them, so S9's "<=1 per slide" could never pass.
    entrance = ('<p:cTn id="4" dur="700" presetID="18" presetClass="entr">'
                '<p:childTnLst><p:animScale><p:from x="0" y="0"/>'
                '<p:to x="100000" y="100000"/></p:animScale></p:childTnLst>'
                '</p:cTn>')
    assert parse_breaths(entrance) == []
    slide = parse_slide_xml(3, entrance)
    assert slide.grow_count == 0
    assert check_s9([slide], TIMING).ok is True


def test_s9_still_catches_a_rogue_oversized_scale():
    rogue = ('<p:cTn id="4" dur="900"><p:childTnLst><p:animScale>'
             '<p:to x="180000" y="180000"/></p:animScale></p:childTnLst></p:cTn>')
    assert check_s9([parse_slide_xml(3, rogue)], TIMING).ok is False


def test_the_manual_rows_fail_without_a_recorded_human_verdict(monkeypatch,
                                                               tmp_path):
    import verify_pptx
    monkeypatch.setattr(verify_pptx, "MANUAL_FILE", tmp_path / "none.json")
    rows = verify_pptx.manual_rows()
    assert {r.id for r in rows} == {"S15", "S16"}
    assert all(r.ok is False for r in rows)


def test_the_manual_rows_pass_only_on_a_recorded_pass(monkeypatch, tmp_path):
    import verify_pptx
    path = tmp_path / "manual-verdicts.json"
    path.write_text(json.dumps({
        "S15": {"verdict": "PASS", "by": "operator", "date": "2026-10-11"},
        "S16": {"verdict": "FAIL", "by": "operator", "date": "2026-10-11"},
    }), encoding="utf-8")
    monkeypatch.setattr(verify_pptx, "MANUAL_FILE", path)
    rows = {r.id: r.ok for r in verify_pptx.manual_rows()}
    assert rows == {"S15": True, "S16": False}


def test_the_rights_row_passes_a_silent_deck_and_fails_a_preview(monkeypatch):
    import verify_pptx
    slides = [make_slide(1)]
    monkeypatch.setattr(verify_pptx, "read_rights",
                        lambda _root: {"rights": "none"})
    assert verify_pptx.check_rights(slides, [], TIMING).ok is True
    media = ["ppt/media/media1.mp3"]
    assert verify_pptx.check_rights(slides, media, TIMING).ok is False
    monkeypatch.setattr(verify_pptx, "read_rights",
                        lambda _root: {"rights": "personal_preview"})
    row = verify_pptx.check_rights(slides, media, TIMING)
    assert row.ok is False and "D6" in row.detail
    monkeypatch.setattr(verify_pptx, "read_rights",
                        lambda _root: {"rights": "licensed"})
    assert verify_pptx.check_rights(slides, media, TIMING).ok is True


def test_the_ban_row_reads_slide_text_and_asset_names_not_just_timing_json():
    import verify_pptx
    clean = make_slide(1)
    clean.texts = ["信号站", "01 / 26"]
    assert verify_pptx.check_no_reference_branding([clean], [], TIMING).ok is True
    dirty = make_slide(2)
    dirty.texts = ["a Nomai probe"]
    assert verify_pptx.check_no_reference_branding([dirty], [], TIMING).ok is False
    assert verify_pptx.check_no_reference_branding(
        [clean], ["ppt/media/travelers-encore.mp3"], TIMING).ok is False


def test_s13_waives_the_readable_run_only_for_the_declared_wordless_plate():
    slides = [make_slide(i, sizes=(4000, 1000)) for i in range(1, 27)]
    slides[7] = make_slide(8, sizes=(1000,))        # slide 8, meta only
    assert check_s13(slides, TIMING).ok is True
    slides[6] = make_slide(7, sizes=(1000,))        # an undeclared silent slide
    assert check_s13(slides, TIMING).ok is False
    slides[6] = make_slide(7, sizes=(4000, 1000))
    slides[7] = make_slide(8, sizes=(4000, 1000))   # a declared plate with type
    assert check_s13(slides, TIMING).ok is False


def test_s11_demands_the_frozen_tables_exact_counts():
    from deck.scenes.common import expected_shapes, star_budget
    from deck.sync import expand
    slides, prev = [], None
    for spec in expand(TIMING):
        slides.append(make_slide(spec.idx,
                                 shapes=expected_shapes(spec, prev_stars=prev)))
        prev = [None] * star_budget(spec, prev)
    assert check_s11(slides, TIMING).ok is True
    slides[3] = make_slide(4, shapes=slides[3].shape_count + 1)
    row = check_s11(slides, TIMING)
    assert row.ok is False and "table predicts" in row.detail


def test_audit_on_the_real_build_passes_every_machine_row():
    built = ROOT / "out" / "信号站-SIGNAL-preview.pptx"
    if not built.is_file():
        pytest.skip("run build_deck.py first (Task 15)")
    rows = audit(built, TIMING)
    machine = [r for r in rows if r.id not in ("S15", "S16")]
    failed = [r for r in machine if not r.ok]
    assert failed == [], [f"{r.id}: {r.detail}" for r in failed]
```

- [ ] **Step 2: Run it and watch it fail**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_verify_rules.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'verify_pptx'`

- [ ] **Step 3: Implement**

`demos/signal-station-pptx/verify_pptx.py`:

```python
#!/usr/bin/env python3
"""Audit a built deck against the spectacle checklist. No COM, no PowerPoint.

One row per S* line. MANUAL rows cannot pass on their own -- the checklist says
a row that cannot be checked counts as failed, so handover.md must carry the
human verdict for them.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import zipfile
from dataclasses import dataclass, field
from pathlib import Path

from deck.audio import RIGHTS_FILE, read_rights
from deck.palette import META_CEIL, READ_FLOOR
from deck.scenes.common import AMBIENT, expected_shapes, star_budget
from deck.sync import adv_time, expand

ROOT = Path(__file__).resolve().parent
DEFAULT_PPTX = ROOT / "out" / "信号站-SIGNAL-preview.pptx"
EMU_PER_PT = 12700

# XML LOCAL element names, as PowerPoint writes them inside mc:Choice.
SIGNATURE_XML = {"ripple", "window", "circle"}        # the three signatures
TRANS_EXPECT = {
    "cut": "cut", "fade": "fade", "morph_obj": "morph", "morph_word": "morph",
    "morph_char": "morph", "door_ripple": "ripple", "door_window": "window",
    "iris": "circle",
}

# Ten of the sixteen beats split into Morph twins (see the beat/slide map), so
# there are TEN B twins, not nine. The earlier `checked != 9` made S2 fail on a
# correct deck -- and, worse, would have passed a deck that was missing a twin.
# Kept as a named constant so the number cannot drift out of the map again.
B_TWIN_SLIDES = (2, 4, 6, 10, 12, 16, 18, 20, 22, 26)
B_TWINS = len(B_TWIN_SLIDES)
CARRY_SLIDES = (5, 25)


@dataclass
class Row:
    # There is no MANUAL state. S15/S16 resolve to True only from a recorded
    # human verdict and to False otherwise, so every row is a hard pass/fail
    # and the exit code means exactly one thing. See manual_rows().
    id: str
    ok: bool
    detail: str


@dataclass
class Slide:
    index: int
    transition: str
    morph_option: str | None
    trans_dur: float
    adv_tm: float
    shape_count: int
    ctn_count: int
    motion_count: int
    names: list[str] = field(default_factory=list)
    offsets: dict[str, tuple[int, int]] = field(default_factory=dict)
    exts: dict[str, tuple[int, int]] = field(default_factory=dict)
    texts: list[str] = field(default_factory=list)
    run_sizes: list[int] = field(default_factory=list)
    run_spc: list[int] = field(default_factory=list)
    grow_count: int = 0          # BREATHES, not animScale elements
    grow_pct: float = 0.0
    grow_duration: float = 0.0
    max_scale_pct: float = 0.0   # any animScale, for the 106% ceiling

    def stars(self) -> list[str]:
        return [n for n in self.names if n.startswith("!!star_")]

    def glows(self) -> list[str]:
        return [n for n in self.names if n.startswith("!!glow_")]


TRANSITION_EFFECTS = ("morph", "fade", "cut", "circle", "ripple", "window",
                      "wipe", "push", "cover", "dissolve")


def parse_transition(xml: str) -> tuple[str, str | None, float, float]:
    """(local effect name, option, duration, advTm) -- through AlternateContent.

    PowerPoint wraps a Morph / ripple / window transition in
    <mc:AlternateContent>, puts the real effect inside <mc:Choice Requires="…">
    as a namespace-PREFIXED element (p159:morph, p14:ripple, p14:window) and
    leaves a bare <p:transition> in <mc:Fallback> for older readers. A parser
    that looks for `<p:morph>` finds nothing on every slide, and then MAP, S1,
    S6 and S7 all silently pass on "none". So: prefer the Choice branch, and
    match the effect by LOCAL name with any prefix.
    """
    choice = re.search(r"<mc:Choice\b.*?</mc:Choice>", xml, flags=re.S)
    scope = choice.group(0) if choice else xml
    block = re.search(r"<(?:\w+:)?transition\b([^>]*)>(.*?)</(?:\w+:)?transition>",
                      scope, flags=re.S)
    if block is None:
        block = re.search(r"<(?:\w+:)?transition\b([^>]*)/>", scope)
        attrs, body = (block.group(1) if block else ""), ""
    else:
        attrs, body = block.group(1), block.group(2)
    if block is None:
        return "none", None, 0.0, 0.0

    kind = "none"
    effect = re.search(r"<(?:\w+:)?(" + "|".join(TRANSITION_EFFECTS) + r")\b",
                       body or "")
    if effect:
        kind = effect.group(1)
    opt = re.search(r'option="(\w+)"', body or "")
    option = opt.group(1) if opt else None
    # the duration attribute is namespace-prefixed in the Choice branch
    d = re.search(r'(?:\w+:)?dur="(\d+)"', attrs)
    a = re.search(r'advTm="(\d+)"', attrs)
    return (kind, option,
            int(d.group(1)) / 1000.0 if d else 0.0,
            int(a.group(1)) / 1000.0 if a else 0.0)


def _enclosing_ctn(xml: str, at: int) -> str:
    """Attributes of the nearest <p:cTn> opening tag before `at`."""
    opens = re.findall(r"<p:cTn\b([^>]*)>", xml[:at])
    return opens[-1] if opens else ""


def parse_breaths(xml: str) -> list[tuple[float, float]]:
    """[(percent, duration)] for animScale nodes that are really a BREATH.

    S9 cannot count `<p:animScale>` elements: FadedZoom, RiseUp and Zoom
    entrances all emit animScale too, so the old `xml.count("<p:animScale")`
    reported 4-13 "GrowShrinks" per slide and S9 could never pass. A breath is
    identified by its semantics instead -- it auto-reverses and it either
    repeats indefinitely or runs for at least 6s, which is precisely what
    motion.breath() writes and what no entrance writes.
    """
    out: list[tuple[float, float]] = []
    for m in re.finditer(r"<p:animScale\b", xml):
        attrs = _enclosing_ctn(xml, m.start())
        auto_rev = 'autoRev="1"' in attrs
        repeats = 'repeatCount="indefinite"' in attrs
        dur = re.search(r'\bdur="(\d+)"', attrs)
        seconds = int(dur.group(1)) / 1000.0 if dur else 0.0
        if not (auto_rev and (repeats or seconds >= 6.0)):
            continue
        tail = xml[m.start():m.start() + 600]
        to = re.search(r'<p:to x="(-?\d+)"', tail)
        out.append((float(to.group(1)) / 1000.0 if to else 0.0, seconds))
    return out


def parse_slide_xml(index: int, xml: str) -> Slide:
    names = re.findall(r'<p:cNvPr[^>]*name="([^"]*)"', xml)
    offsets: dict[str, tuple[int, int]] = {}
    exts: dict[str, tuple[int, int]] = {}
    for chunk in re.findall(r"<p:sp>.*?</p:sp>", xml, flags=re.S):
        n = re.search(r'<p:cNvPr[^>]*name="([^"]*)"', chunk)
        off = re.search(r'<a:off x="(-?\d+)" y="(-?\d+)"', chunk)
        ext = re.search(r'<a:ext cx="(\d+)" cy="(\d+)"', chunk)
        if n and off:
            offsets[n.group(1)] = (int(off.group(1)), int(off.group(2)))
        if n and ext:
            exts[n.group(1)] = (int(ext.group(1)), int(ext.group(2)))

    kind, option, dur, adv = parse_transition(xml)
    breaths = parse_breaths(xml)
    return Slide(
        index=index, transition=kind, morph_option=option, trans_dur=dur,
        adv_tm=adv,
        shape_count=xml.count("<p:sp>") + xml.count("<p:pic>"),
        # S10 counts EFFECTS: one presetID-bearing cTn per AddEffect. Raw
        # <p:cTn> runs ~3x higher (par/seq wrappers) and is not the metric.
        ctn_count=len(re.findall(r"<p:cTn[^>]*presetID=", xml)),
        motion_count=xml.count("<p:animMotion"),
        names=names, offsets=offsets, exts=exts,
        texts=[t for t in re.findall(r"<a:t>(.*?)</a:t>", xml, flags=re.S)],
        run_sizes=[int(v) for v in re.findall(r'<a:rPr[^>]*\bsz="(\d+)"', xml)],
        run_spc=[int(v) for v in re.findall(r'<a:rPr[^>]*\bspc="(-?\d+)"', xml)],
        grow_count=len(breaths),
        grow_pct=max((p for p, _ in breaths), default=0.0),
        grow_duration=max((d for _, d in breaths), default=0.0),
        max_scale_pct=max((float(v) / 1000.0 for v in
                           re.findall(r'<p:animScale\b.*?<p:to x="(\d+)"',
                                      xml, flags=re.S)), default=0.0),
    )


def load(path: Path) -> tuple[list[Slide], list[str]]:
    with zipfile.ZipFile(path) as z:
        parts = [n for n in z.namelist()
                 if re.fullmatch(r"ppt/slides/slide\d+\.xml", n)]
        parts.sort(key=lambda n: int(re.findall(r"\d+", n)[0]))
        slides = [parse_slide_xml(i, z.read(n).decode("utf-8"))
                  for i, n in enumerate(parts, start=1)]
        media = [n for n in z.namelist() if n.startswith("ppt/media/")]
    return slides, media


# -- rows ------------------------------------------------------------------
def check_s1(slides, timing) -> Row:
    morphs = [s for s in slides if s.transition == "morph"]
    by_object = [s for s in morphs if s.morph_option == "byObject"]
    ok = len(morphs) >= 12 and len(by_object) >= 10
    return Row("S1", ok, f"{len(morphs)} morph slides, {len(by_object)} byObject "
                         f"(need >=12 / >=10)")


def _drift_rows(slides, timing, want_carry: bool):
    """Pairs of (prev, slide) for genuine B twins, or for declared carries."""
    specs = {s.idx: s for s in expand(timing)}
    for prev, slide in zip(slides, slides[1:]):
        spec = specs.get(slide.index)
        if spec is None:
            continue
        if (spec.carry if want_carry else spec.is_b):
            yield prev, slide, spec


def _displacement(prev, slide, names):
    return [(n, abs(slide.offsets[n][0] - prev.offsets[n][0])
             + abs(slide.offsets[n][1] - prev.offsets[n][1]))
            for n in names if n in slide.offsets and n in prev.offsets]


def _parallax_ordered(slide, moved) -> bool:
    """Near stars (largest a:ext) must out-travel far stars (smallest)."""
    sized = [(slide.exts.get(n, (0, 0))[0], d) for n, d in moved]
    if len(sized) < 20:
        return False
    sized.sort()
    far = [d for _, d in sized[:10]]
    near = [d for _, d in sized[-10:]]
    return sum(near) / 10.0 > sum(far) / 10.0


def check_s2(slides, timing) -> Row:
    """Genuine Morph-B twins only, against the frozen table's star budget.

    Scoping this by `transition == "morph"` was wrong twice over: it swept in
    the two carry slides (which are not duplicates and are audited by CARRY)
    and it tested a flat ">= 56" floor that sat above four roles' budgets.
    """
    details, ok, checked = [], True, 0
    for prev, slide, spec in _drift_rows(slides, timing, want_carry=False):
        checked += 1
        want = AMBIENT[spec.role].stars
        moved = _displacement(prev, slide, slide.stars())
        displaced = [m for m in moved if m[1] > 0]
        if len(displaced) < want:
            ok = False
            details.append(f"slide {slide.index} ({spec.role}): "
                           f"{len(displaced)}/{want} stars displaced")
        if not _parallax_ordered(slide, displaced):
            ok = False
            details.append(f"slide {slide.index}: near stars do not out-travel "
                           f"far stars (no parallax)")
    if checked != B_TWINS:
        ok = False
        details.append(f"examined {checked} B twins, expected {B_TWINS} "
                       f"{B_TWIN_SLIDES}")
    return Row("S2", ok, "; ".join(details) or
               f"all {B_TWINS} Morph-B twins displace their whole star budget "
               f"with near-over-far parallax")


def check_carry(slides, timing) -> Row:
    """S2c: the two declared carry slides inherit and drift by name."""
    details, ok, checked = [], True, 0
    for prev, slide, spec in _drift_rows(slides, timing, want_carry=True):
        checked += 1
        want = len(prev.stars())
        moved = _displacement(prev, slide, slide.stars())
        displaced = [m for m in moved if m[1] > 0]
        if len(displaced) < want:
            ok = False
            details.append(f"slide {slide.index}: {len(displaced)}/{want} "
                           f"inherited stars displaced -- a carry slide must "
                           f"repaint its predecessor's field by name")
    if checked != len(CARRY_SLIDES):
        ok = False
        details.append(f"examined {checked} carry slides, expected "
                       f"{len(CARRY_SLIDES)} {CARRY_SLIDES}")
    return Row("CARRY", ok, "; ".join(details) or
               "both carry slides inherit and drift their predecessor's field")


def check_s3(slides, timing) -> Row:
    total = sum(s.motion_count for s in slides)
    missing = [s.index for s in slides if s.motion_count == 0]
    ok = total >= 40 and not missing
    return Row("S3", ok, f"{total} animMotion nodes (need >=40, table predicts "
                         f"51); slides with none: {missing or 'none'} (D5 says "
                         f"all 26 carry particles); clearance proven by "
                         f"tests/test_geometry.py")


def check_s4(slides, timing) -> Row:
    title = [s for s in slides if any(n.startswith("!!glyph_") for n in s.names)]
    glyphs = max((len([n for n in s.names if n.startswith("!!glyph_")])
                  for s in title), default=0)
    frags = max((len([n for n in s.names if n.startswith("!!frag_")])
                 for s in slides), default=0)
    paired = any(s.transition == "morph" for s in title)
    ok = glyphs >= 3 and frags >= 12 and paired
    return Row("S4", ok, f"{glyphs} glyph boxes, {frags} cascade fragments, "
                         f"morph-paired={paired}")


def check_s5(slides, timing) -> Row:
    stacks = [s.index for s in slides if len(s.glows()) >= 4]
    ok = len(stacks) >= 6
    return Row("S5", ok, f"{len(stacks)} slides carry a 4-layer glow stack "
                         f"(need >=6): {stacks}")


def check_s6(slides, timing) -> Row:
    irises = [s.index for s in slides if s.transition == "circle"]
    grown = [s.index for s in slides
             if any(n.startswith("!!iris_core_") for n in s.names)]
    ok = irises == [17] and len(grown) >= 2
    return Row("S6", ok, f"circle transition on {irises} (need [17]), "
                         f"iris_core present on {grown}")


def check_s7(slides, timing) -> Row:
    sig = [(s.index, s.transition) for s in slides
           if s.transition in SIGNATURE_XML]
    adjacent = any(b[0] - a[0] == 1 for a, b in zip(sig, sig[1:]))
    ok = len(sig) == 3 and not adjacent
    return Row("S7", ok, f"signatures at {sig} (need exactly 3, none adjacent)")


def check_s8(slides, timing) -> Row:
    cards = [s for s in slides
             if len([n for n in s.names if n.startswith("!!cardlabel_")]) >= 3]
    nums = [s for s in slides
            if len([n for n in s.names if n.startswith("!!numeral_")]) >= 3]
    racked = False
    for prev, slide in zip(slides, slides[1:]):
        labels = [n for n in slide.names if n.startswith("!!cardlabel_")]
        if len(labels) >= 3 and slide.transition == "morph":
            spans = []
            for source in (prev, slide):
                xs = [source.offsets[n][0] for n in labels if n in source.offsets]
                spans.append(max(xs) - min(xs) if xs else 0)
            racked = spans[1] < spans[0]
    ok = len(cards) >= 2 and len(nums) >= 2 and racked
    return Row("S8", ok, f"{len(cards)} card slides, {len(nums)} numeral slides, "
                         f"re-racked tighter={racked}")


def check_s9(slides, timing) -> Row:
    """`grow_count` is the count of semantic BREATHS (see parse_breaths), not
    of animScale elements -- entrance zooms emit those too, and counting them
    made this row unpassable."""
    bad = []
    for s in slides:
        if s.grow_count > 1:
            bad.append(f"slide {s.index}: {s.grow_count} breaths")
        if s.grow_count and s.grow_pct > 106.0:
            bad.append(f"slide {s.index}: breath {s.grow_pct:.0f}% > 106%")
        if s.grow_count and 0 < s.grow_duration < 6.0:
            bad.append(f"slide {s.index}: breath {s.grow_duration:.1f}s < 6s")
        if s.max_scale_pct > 106.0:
            bad.append(f"slide {s.index}: an animScale reaches "
                       f"{s.max_scale_pct:.0f}% (ceiling 106%; the iris is "
                       f"Morph-driven and must not appear here)")
    return Row("S9", not bad, "; ".join(bad) or
               "<=1 breath per slide, <=106%, >=6s, and no animScale over 106%")


def check_s10(slides, timing) -> Row:
    total = sum(s.ctn_count for s in slides)
    ok = 420 <= total <= 1200
    return Row("S10", ok, f"{total} animation effects (presetID-bearing p:cTn; "
                          f"band 420..1200, table predicts ~670)")


def check_s11(slides, timing) -> Row:
    """Exact predicted counts from the frozen density table, not a loose band.

    A band is what let the scenes paint 44-89 shapes while the row demanded
    80-170 and nobody noticed. The table is the prediction; this holds the file
    to it slide by slide.
    """
    specs = {s.idx: s for s in expand(timing)}
    with_audio = any(n.startswith("!!audio_") for n in (slides[0].names if slides else []))
    bad, total, prev_stars = [], 0, None
    for slide in slides:
        spec = specs.get(slide.index)
        if spec is None:
            continue
        want = expected_shapes(spec, prev_stars=prev_stars, with_audio=with_audio)
        total += slide.shape_count
        if slide.shape_count != want:
            bad.append(f"slide {slide.index} ({spec.role}): {slide.shape_count} "
                       f"shapes, table predicts {want}")
        if not 95 <= slide.shape_count <= 150:
            bad.append(f"slide {slide.index}: {slide.shape_count} outside the "
                       f"95..150 sanity band")
        prev_stars = [None] * star_budget(spec, prev_stars)
    if total < 2800:
        bad.append(f"{total} shapes total, need >=2800 (a --fast build cannot "
                   f"pass this row and must not be audited)")
    return Row("S11", not bad, "; ".join(bad) or
               f"{total} shapes, every slide exactly as the frozen density "
               f"table predicts")


def check_s12(slides, timing) -> Row:
    tracked = [s.index for s in slides if any(v >= 400 for v in s.run_spc)]
    ok = len(tracked) >= 8
    return Row("S12", ok, f"{len(tracked)} slides carry a run with spc>=400 "
                          f"(need >=8)")


def check_s13(slides, timing) -> Row:
    """Dead band everywhere; a readable run everywhere EXCEPT declared plates.

    The breath plate (slide 8) carries only a <=12pt meta mark by design, so a
    blanket "every slide has a >=18pt run" made S13 and the wordless plate
    mutually exclusive. The waiver is narrow and it cuts both ways: a declared
    wordless slide must have NO readable run, or the declaration is covering
    for real type.
    """
    wordless = {s.idx for s in expand(timing) if s.wordless}
    bad = []
    lo, hi = META_CEIL * 100 + 50, READ_FLOOR * 100
    for s in slides:
        dead = [v for v in s.run_sizes if lo < v < hi]
        if dead:
            bad.append(f"slide {s.index}: dead-band sizes {sorted(set(dead))}")
        readable = [v for v in s.run_sizes if v >= hi]
        if s.index in wordless:
            if readable:
                bad.append(f"slide {s.index} is declared wordless but carries "
                           f"runs at {sorted(set(readable))}")
        elif not readable:
            bad.append(f"slide {s.index}: no run >= {READ_FLOOR}pt and it is "
                       f"not declared wordless")
    return Row("S13", not bad, "; ".join(bad) or
               f"no run in {META_CEIL + 0.5}..{READ_FLOOR}pt; every slide has a "
               f"readable run except the {len(wordless)} declared wordless "
               f"plate(s) {sorted(wordless)}, which have none")


def check_wordless(slides, timing) -> Row:
    """D1's plate claim, measured. The old claim (slides 8, 2, 26) was false."""
    hi = READ_FLOOR * 100
    counts = {s.index: len([v for v in s.run_sizes if v >= hi]) for s in slides}
    declared = sorted(s.idx for s in expand(timing) if s.wordless)
    silent = sorted(i for i, n in counts.items() if n == 0)
    near = sorted(i for i, n in counts.items() if 0 < n <= 2)
    bad = []
    if silent != declared:
        bad.append(f"wordless plates are {silent}, declared {declared}")
    if len(near) < 3:
        bad.append(f"only {len(near)} near-wordless plates {near}, D1 claims 3")
    return Row("WORDLESS", not bad, "; ".join(bad) or
               f"1 declared wordless plate {declared} and "
               f"{len(near)} near-wordless plates {near} (D1)")


def check_chrome(slides, timing) -> Row:
    """Every slide's meta index reads its own number.

    A B twin is a duplicate, so without transitions.retext_meta_index() ten
    slides would display their A twin's "NN / 26".
    """
    total = len(slides)
    bad = [s.index for s in slides
           if not any(f"{s.index:02d} / {total}" in t for t in s.texts)]
    return Row("CHROME", not bad, f"slides showing the wrong index: "
                                  f"{bad or 'none'} (duplicated twins must be "
                                  f"re-texted)")


def check_s14(slides, timing) -> Row:
    additive = bool(timing["transition_additive"])
    expected = {s.idx: adv_time(s, additive) for s in expand(timing)}
    bad = []
    for s in slides:
        want = expected.get(s.index)
        if want is None:
            continue
        if abs(s.adv_tm - want) > 0.06:
            bad.append(f"slide {s.index}: advTm {s.adv_tm:.2f}s, ledger says "
                       f"{want:.2f}s")
    return Row("S14", not bad, "; ".join(bad) or
               "every advTm matches the sync ledger")


def check_transitions_match_timing(slides, timing) -> Row:
    bad = []
    for spec, slide in zip(expand(timing), slides):
        want = TRANS_EXPECT[spec.trans]
        if slide.transition != want:
            bad.append(f"slide {spec.idx}: {slide.transition} != {want} "
                       f"({spec.trans})")
    return Row("MAP", not bad, "; ".join(bad) or
               "every slide's transition is the one timing.json declares")


def check_audio(slides, media, timing) -> Row:
    tracks = [m for m in media if m.lower().endswith((".mp3", ".m4a", ".wav"))]
    on_first = any(n.startswith("!!audio_") for n in slides[0].names) if slides else False
    if not tracks:
        return Row("AUDIO", not on_first,
                   "silent deck: no media part (the default). "
                   + ("but a !!audio_ shape is present" if on_first else ""))
    ok = len(tracks) == 1 and on_first
    return Row("AUDIO", ok, f"{len(tracks)} media part(s), mounted on slide 1="
                            f"{on_first}")


def check_rights(slides, media, timing) -> Row:
    """0.0 item 5 / 7.1 row 7 / D6 -- and it BLOCKS.

    A media part with anything other than an explicit `licensed` declaration is
    a failing row, so the script exits 1. There is no flag to downgrade this:
    `personal_preview` is exactly the state where the deck plays locally and
    cannot be delivered, and reporting that as a pass is the dishonesty this
    row exists to prevent.
    """
    tracks = [m for m in media if m.lower().endswith((".mp3", ".m4a", ".wav"))]
    rights = read_rights(ROOT)["rights"]
    if not tracks:
        return Row("RIGHTS", True,
                   f"no media part; nothing to license (declaration on file: "
                   f"{rights!r})")
    if rights == "licensed":
        return Row("RIGHTS", True,
                   f"media part present and {RIGHTS_FILE} declares 'licensed'; "
                   f"the declaration is the only evidence -- this tool cannot "
                   f"verify a licence")
    return Row("RIGHTS", False,
               f"media part present but {RIGHTS_FILE} declares {rights!r}. "
               f"D6: this build is a local preview and is NOT deliverable "
               f"(7.1 row 7 unmet). Rebuild without --audio to ship, or supply "
               f"a track you can honestly declare 'licensed'.")


def check_no_reference_branding(slides, media, timing) -> Row:
    """Scans the rendered slide TEXT and the package assets, not just the input.

    Auditing timing.json only proves the input was clean; a banned term could
    still reach a slide through brief.json, a scene literal or an asset name.
    """
    banned = ("outer wilds", "outerwilds", "nomai", "hearthian", "mobius",
              "timber hearth", "travelers", "prahlow")
    hits = []
    for slide in slides:
        blob = " ".join(slide.texts).lower()
        hits += [f"slide {slide.index}: {b!r} in text" for b in banned if b in blob]
    for part in media:
        low = part.lower()
        hits += [f"asset {part}: {b!r}" for b in banned if b in low]
    for name in ("timing.json", "brief.json"):
        path = ROOT / name
        if path.is_file():
            blob = path.read_text(encoding="utf-8").lower()
            hits += [f"{name}: {b!r}" for b in banned if b in blob]
    return Row("BAN", not hits, "; ".join(hits) or
               "no reference branding in any slide's text, in any package "
               "asset name, or in timing.json / brief.json (0.0 item 1)")


MANUAL_FILE = ROOT / "tests" / "manual-verdicts.json"
MANUAL_PROMPTS = {
    "S15": ("open out/信号站-SIGNAL-static-twin.pdf: 26 pages, each readable "
            "with motion gone, 1 wordless plate (8) and 3 near-wordless (7, "
            "23, 24)"),
    "S16": ("run the show from slide 1: chapter door at 1:00 +-1.5s, iris at "
            "2:04 +-1.5s, ends on the last slide at 3:49.5"),
}


def manual_rows() -> list[Row]:
    """S15/S16 resolved from a recorded human verdict -- never auto-passed.

    With no verdict file these come back as FAILING rows, not as a decorative
    "MANUAL" that still lets the script exit 0. The checklist says a row that
    cannot be checked counts as failed; this is that clause, implemented.
    """
    try:
        recorded = json.loads(MANUAL_FILE.read_text(encoding="utf-8"))
    except Exception:
        recorded = {}
    rows = []
    for row_id, prompt in MANUAL_PROMPTS.items():
        verdict = str(recorded.get(row_id, {}).get("verdict", "")).upper()
        if verdict == "PASS":
            who = recorded[row_id].get("by", "unrecorded")
            when = recorded[row_id].get("date", "unrecorded")
            rows.append(Row(row_id, True,
                            f"human PASS recorded by {who} on {when}: {prompt}"))
        else:
            rows.append(Row(row_id, False,
                            f"no human PASS in {MANUAL_FILE.name} "
                            f"(found {verdict or 'nothing'}). Do the check and "
                            f"record the verdict: {prompt}"))
    return rows


def audit(path: Path, timing: dict) -> list[Row]:
    slides, media = load(Path(path))
    rows = [
        Row("COUNT", len(slides) == 26, f"{len(slides)} slides (need 26)"),
        check_s1(slides, timing), check_s2(slides, timing),
        check_s3(slides, timing), check_s4(slides, timing),
        check_s5(slides, timing), check_s6(slides, timing),
        check_s7(slides, timing), check_s8(slides, timing),
        check_s9(slides, timing), check_s10(slides, timing),
        check_s11(slides, timing), check_s12(slides, timing),
        check_s13(slides, timing), check_s14(slides, timing),
        check_carry(slides, timing),
        check_transitions_match_timing(slides, timing),
        check_chrome(slides, timing), check_wordless(slides, timing),
        check_audio(slides, media, timing),
        check_rights(slides, media, timing),
        check_no_reference_branding(slides, media, timing),
    ]
    return rows + manual_rows()


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser(description="Audit the built deck.")
    p.add_argument("path", nargs="?", default=str(DEFAULT_PPTX))
    p.add_argument("--json", default=None, help="also write the rows as JSON")
    args = p.parse_args(argv)

    timing = json.loads((ROOT / "timing.json").read_text(encoding="utf-8"))
    rows = audit(Path(args.path), timing)
    width = max(len(r.id) for r in rows)
    for r in rows:
        tag = "PASS" if r.ok else "FAIL"
        print(f"{tag:<6} {r.id:<{width}}  {r.detail}")
    failed = [r.id for r in rows if not r.ok]
    print(f"\n{len(rows) - len(failed)} pass, {len(failed)} fail")
    if args.json:
        Path(args.json).write_text(json.dumps(
            [{"id": r.id, "ok": r.ok, "detail": r.detail} for r in rows],
            ensure_ascii=False, indent=2), encoding="utf-8")
    if failed:
        # Every failure blocks, including RIGHTS, BAN and the two human rows.
        # There is no partial success and no flag that downgrades a row.
        print(f"FAILED: {', '.join(failed)}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run the rule test to green**

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests/test_verify_rules.py -q`
Expected: `13 passed` (the last one skips if no build exists yet)

- [ ] **Step 5: Audit the real deck**

Run: `Set-Location demos/signal-station-pptx; python verify_pptx.py --json out/_audit.json`

Expected: `PASS` on every machine row, `MANUAL` on S15 and S16, exit code 0. Work any `FAIL` back to its cause — each row's `detail` names the offending slides:

| Row | Likely cause when it fails |
|---|---|
| S2 | `drift_stars` ran before the stars existed, or the drift vector is too small for the EMU rounding |
| S3 | the `travel` fallback produced a stock path instead of the rewritten one — re-check Task 8 Step 5 |
| S5 | a role with `glow` in `timing.json` lost its stack; check the `data.get("glow")` branch in the scene |
| S9 | a scene called `breath` twice, or passed a percentage above the cap (the cap is enforced in `motion.py`, so this means a raw `AddEffect` slipped into a scene) |
| S11 | `AMBIENT` counts are off for a role; adjust and keep the plan table in sync |
| S12 | `Caps.tracking` is `False`, so `Font.Spacing` was never written — re-run `probe_com.py` |
| S14 | `transition_additive` disagrees with what the build used; rebuild after fixing `timing.json` |
| MAP | a transition name in `timing.json` maps to an XML element name not in `TRANS_EXPECT` |

- [ ] **Step 6: Prove the audit can fail** (a check that never fails is decoration)

Run: `Set-Location demos/signal-station-pptx; python verify_pptx.py out/_subset.pptx`
Expected: exit code 1, with `COUNT`, `S1`, `S10` and `S11` failing — the six-slide subset cannot satisfy the full-deck bands. That is the audit working.

**Accept:** `python -m pytest tests -q` reports 178 passed; `python verify_pptx.py` exits 0 with every machine row `PASS` and only S15/S16 `MANUAL`; `out/_audit.json` exists; `python verify_pptx.py out/_subset.pptx` exits 1.

---

### Task 17: `README.md` + `.gitignore` — the new CLI, the real limits, and nothing leaked

The current README documents a one-command build of a deck that no longer exists, promises nothing about limits, and its `.gitignore` only catches `out/*.pptx` — which means the PDF, the MP4, the smoke builds and the audit JSON from Tasks 7–16 are all currently untracked-but-visible, and one `git add -A` away from being committed. Close that first.

**Files:**
- Modify: `demos/signal-station-pptx/.gitignore`
- Modify: `demos/signal-station-pptx/README.md`

- [ ] **Step 1: Widen `.gitignore` before anything else**

`demos/signal-station-pptx/.gitignore`:

```gitignore
# the audio copy -- never commit an mp3 (D6, Global Constraints)
media/

# build artefacts
out/*.pptx
out/*.pdf
out/*.mp4
out/*.txt
out/*.json
out/_*

# probe / calibration output that is machine-specific
_calibration.json
_plan_heartbeat.json

__pycache__/
*.pyc
.pytest_cache/
```

`COM-NOTES.md` is deliberately **not** ignored: it is the evidence Tasks 7–10 and 18 quote, and it is small and human-readable. `_calibration.json` is ignored because its numbers are specific to one machine's scheduler.

- [ ] **Step 2: Confirm nothing new is leaking**

Run: `Set-Location demos/signal-station-pptx; git status --porcelain --untracked-files=all .`

Expected: no line under `media/`, `out/`, `__pycache__/`, and no `_calibration.json`. The listed files should be exactly: `build_deck.py`, `timing.json`, `README.md`, `.gitignore`, `brief.json`, `probe_com.py`, `calibrate_timing.py`, `verify_pptx.py`, `conftest.py`, `COM-NOTES.md`, `handover.md` (after Task 18), and the `deck/` + `tests/` trees.

Run: `Set-Location demos/signal-station-pptx; git check-ignore -v out/信号站-SIGNAL-static-twin.pdf media/travelers-encore.mp3 out/_audit.json`
Expected: three lines, each naming the `.gitignore` rule that catches it.

- [ ] **Step 3: Rewrite `README.md`**

```markdown
# 信号站 SIGNAL · 星空跟轨 PPT 预览

真实 `.pptx` 能力预览（Mode A，26 页，229.5 s）。题材自拟；星空气质；按本机音轨结构自动换片。

这不是 After Effects 的替代品。它是在 PowerPoint 自己的引擎里，把 Morph 视差、
运动路径流星、碎字拼合、分层光晕、三次签名转场和一次 iris 高潮同时做到位——
并且每一条都能被 `verify_pptx.py` 量出来。

## 需要

Windows、已安装 PowerPoint（16 及以上）、Python 3.12、`pywin32`、`pytest`。

```bash
pip install pywin32 pytest
```

## 第一次构建

```bash
cd demos/signal-station-pptx
python probe_com.py          # 探测本机 COM 能力 -> COM-NOTES.md（必须先跑）
python calibrate_timing.py   # 测 advTm 是否包含转场 -> _calibration.json
python -m pytest tests -q    # 纯 python 测试，不需要 Office
python build_deck.py --pdf   # 全量构建 + 静态 PDF 孪生
python verify_pptx.py        # 对成品做结构审计（不走 COM）
```

`probe_com.py` 的 `media` 行在干净检出时会 FAIL（还没有 mp3），这是预期的；
`build_deck.py` 跑过一次后重跑探测即可全绿。

## CLI

| 参数 | 作用 |
|---|---|
| `--beats 1,6,11` / `--beats 1-3` | 只构建这些 beat（整拍，不拆孪生页），用于快速迭代 |
| `--fast` | 星点/流星数量减半（密度换速度） |
| `--no-audio` | 不读、不嵌入 mp3，构建静音版 |
| `--out PATH` | 指定输出路径 |
| `--pdf` | 额外导出静态 PDF 孪生（26 页） |
| `--video` | 额外渲染 mp4（很慢） |
| `--keep-open` | 构建后不关闭 PowerPoint，便于检查 |

全量构建约 2,600 个形状、~700 个动画节点，耗时以分钟计。迭代请用 `--beats` + `--fast`。

## 音源

默认：`d:\KuGou\Andrew Prahlow - Travelers' encore.mp3`

覆盖：

```bash
set SIGNAL_AUDIO=D:\path\to\your.mp3
python build_deck.py
```

音轨会被复制到 `media/travelers-encore.mp3`（已 gitignore）并**嵌入**到 pptx 里，
从第 1 页起跨全部 26 页播放一次（`PlaySettings.StopAfterSlides`）。

## 放映

1. 用 PowerPoint 打开 `out/信号站-SIGNAL-preview.pptx`
2. F5 从第 1 页开始放映
3. 音频应从第 1 页起连续播放，各页按 `timing.json` 的 hold 自动换片；点击不前进

两个同步观察点：**1:00** 校准章节门（第 9 页），**2:04** iris（第 17 页）。
误差超过 ±1.5 s，先看 `timing.json` 的 `transition_additive` 是否与
`calibrate_timing.py` 的结论一致。

## 目录

| 路径 | 内容 |
|---|---|
| `timing.json` | 唯一的时间真相：16 beat、26 页孪生拆分、每页转场、观察点 |
| `brief.json` | Brief（mode / status / 禁用项 / 素材包 / 断言声明） |
| `build_deck.py` | 薄 CLI 编排，不含任何视觉决策 |
| `deck/comconst.py` | COM 常量解析（活类型库优先，verified 表兜底） |
| `deck/palette.py` | 颜色 / 字号 / 安全区（唯一写死视觉 token 的地方） |
| `deck/geometry.py` | LCG、星场、视差、流星路由、路径字符串 |
| `deck/sync.py` | beat→页展开、advTm 两种模型、校验、同步台账 |
| `deck/stage.py` | COM 形状原语（场、晕影、星、流星、光晕、字） |
| `deck/motion.py` | COM 动画原语 + 可读性时钟（hero ≤1.5s / readable ≤5s） |
| `deck/transitions.py` | 转场表、孪生复制、时间线清空、视差重置 |
| `deck/audio.py` | 音轨落地与跨页播放 |
| `deck/scenes/` | 12 个页面类的外观（identity / editorial / spike / proof） |
| `verify_pptx.py` | 解包审计：对照 spectacle checklist 的 S* 行 |
| `COM-NOTES.md` | 本机 COM 能力实测 + 诚实的「能/不能」清单 |
| `handover.md` | 交付报告、审计结果、偏差台账、观察点清单 |

## 做不到的事（别承诺）

- 没有粒子系统。「流星雨」= N 个手摆形状 + N 条运动路径，数量是构建成本，不是运行时发射器。
- 没有叠加/滤色混合模式。光晕靠半透明堆叠伪造，亮芯压亮星不会 bloom。
- 没有运动模糊、景深、动画模糊半径。拖尾是拉长的椭圆。
- 没有逐帧缓动曲线，只有 accelerate / decelerate 比例。没有弹簧与惯性。
- 没有动画遮罩。iris 只能是 `CircleOut` 转场或被 Morph 的圆环孔。
- 没有音频响应触发。同步是开环的：固定 `advTm` 对固定音轨。
- 保真度与 PowerPoint 绑定。WPS 和旧版 Office 会把 Morph 降级为交叉淡入，
  也可能忽略 `StopAfterSlides`。

完整清单见 `COM-NOTES.md`。

## 版权

音轨版权归原权利方。本仓库**不**分发 mp3。默认音源是参考作品的商业配乐，
因此构建产物**仅供本机预览，不可对外交付**（见 `handover.md` 的 D6）。
要做可交付版本，请把 `SIGNAL_AUDIO` 指向你拥有或已授权的音轨后重建。

幻灯片文案与视觉均为原创，不包含任何参考作品的名称、术语、造型或封面构图。

## 设计 / 计划

- Spec: `docs/superpowers/specs/2026-10-11-signal-station-pptx-preview-design.md`
- Guide: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md`
- Plan: `docs/superpowers/plans/2026-10-11-signal-station-pptx-deluxe.md`
```

- [ ] **Step 4: Check every command in the README actually runs**

A README that lists a wrong flag is worse than no README. Walk the commands:

Run: `Set-Location demos/signal-station-pptx; python probe_com.py --help 2>&1 | Select-Object -First 2; python build_deck.py --help; python verify_pptx.py --help; python calibrate_timing.py --help 2>&1 | Select-Object -First 2`
Expected: `build_deck.py` and `verify_pptx.py` print usage matching the README's tables exactly (flag for flag). `probe_com.py` and `calibrate_timing.py` take no flags, so they will either run or print an argparse error — that is fine as long as the README does not claim they take any.

Run: `Set-Location demos/signal-station-pptx; python -c "
import pathlib, re
readme = pathlib.Path('README.md').read_text(encoding='utf-8')
for path in re.findall(r'\| \`([a-z_/]+\.(?:py|json|md))\`', readme):
    p = pathlib.Path(path)
    print(('ok  ' if p.exists() else 'MISS'), path)"`
Expected: every row in the 目录 table prints `ok` — no `MISS`. (`handover.md` will be `MISS` until Task 18; everything else must exist now.)

- [ ] **Step 5: Confirm the README's claims match the audit**

The README's "做不到的事" list is quoted from `COM-NOTES.md`, and its two watch-point times are quoted from `timing.json`. Verify both rather than trusting the copy:

Run: `Set-Location demos/signal-station-pptx; python -c "
import json
t = json.load(open('timing.json', encoding='utf-8'))
print(t['watch_points'])
print('duration', t['audio_duration_sec'], 'additive', t['transition_additive'])"`
Expected: `chapter_lift` at 60.0 s / slide 9 and `spike` at 124.0 s / slide 17 — i.e. the README's 1:00 and 2:04.

**Accept:** `git check-ignore` catches the PDF, the mp3 and the audit JSON; `git status --porcelain --untracked-files=all .` shows nothing under `media/`, `out/` or `__pycache__/`; every path in the README's 目录 table exists except `handover.md`; `build_deck.py --help` and `verify_pptx.py --help` match the README's flag tables exactly.

---

### Task 18: `handover.md` — the ship report, with the deviations stated out loud

The last task, and the one that decides whether this work can be trusted. Its job is **not** to say the deck is good. It is to state, with numbers and sources, exactly what was built, what was measured, what was deviated from and why, and — in the case of D6 — that the artefact as built **cannot be delivered externally**.

Three rules for writing it:

1. **Every number is pasted from a file, not remembered.** `out/_build-log.txt`, `out/_audit.json`, `out/_sync-ledger.txt`, `COM-NOTES.md`, `_calibration.json`. If a number cannot be sourced, it does not go in.
2. **Every `D*` row from this plan's deviation table appears, unedited in meaning, with its cap and whether the cap held.** A deviation whose cap was exceeded is reported as exceeded.
3. **S15 and S16 carry a human verdict or they are reported as failed.** `verify_pptx.py` prints them as `MANUAL`; the checklist says an unchecked row counts as failed. There is no third option.

**Files:**
- Create: `demos/signal-station-pptx/handover.md`

- [ ] **Step 1: Collect the evidence into one place first**

Run: `Set-Location demos/signal-station-pptx; python verify_pptx.py --json out/_audit.json; python -c "
import json, pathlib
print('--- build summary ---')
log = pathlib.Path('out/_build-log.txt').read_text(encoding='utf-8').splitlines()
print('\n'.join(log[-12:]))
print('--- calibration ---')
print(pathlib.Path('_calibration.json').read_text(encoding='utf-8'))
print('--- audit rows ---')
for r in json.loads(pathlib.Path('out/_audit.json').read_text(encoding='utf-8')):
    print(('PASS' if r['ok'] else ('MANUAL' if r['ok'] is None else 'FAIL')), r['id'], '-', r['detail'][:110])"`

Expected: a summary block, the calibration verdict, and 20 audit rows. Keep this output open — the next step pastes from it.

- [ ] **Step 2: Write `handover.md`**

Fill every `<…>` from Step 1's output. Do not leave a placeholder; an unfilled bracket is a failed handover.

```markdown
# 信号站 SIGNAL · Deluxe Spectacle PPTX — 交付报告 / Handover

**构建日期：** <YYYY-MM-DD>
**产物：** `out/信号站-SIGNAL-preview.pptx`（<N> MB）、`out/信号站-SIGNAL-static-twin.pdf`（26 页）
**构建命令：** `python build_deck.py --pdf`
**对外可交付：** **否** — 见 D6。本件为本机能力预览。

## 1. 它是什么

26 页真实 `.pptx`，Mode A 自动换片，总长 **229.5 s**，锁定一条本机音轨。
16 个 beat，其中 10 个拆成 Morph 孪生页（孪生 hold 之和严格等于父 beat 的 hold，
所以音频提示点一个都没有移动）。

构建器从 490 行单体脚本重写为 `deck/` 包：纯 python 模块（`geometry` / `sync` /
`palette` / `comconst`）在没有 Office 的环境里被单元测试覆盖，COM 模块
（`stage` / `motion` / `transitions` / `audio` / `scenes/`）由薄 CLI 驱动。

## 2. 为什么原来的版本「很平」

根因不是效果不够多，而是硬编码的 COM 常量在 PowerPoint 16 上是错的：

| 原脚本常量 | 用的值 | 这个值真正代表什么 | 正确值 |
|---|---|---|---|
| `MSO_ANIM_TRIGGER_AFTER_PREVIOUS` | 1 | `msoAnimTriggerOnPageClick` | 3 |
| `MSO_ANIM_EFFECT_WHEEL` | 61 | `msoAnimEffectSpin` | 21 |
| `MSO_ANIM_EFFECT_ZOOM` | 53 | `msoAnimEffectFold` | 23 |
| `MSO_ANIM_EFFECT_FLOAT_UP` | 37 | `msoAnimEffectUnfold` | `msoAnimEffectRiseUp` = 34 |
| `MSO_ANIM_EFFECT_MEDIA_PLAY` | 150 | `msoAnimEffectMediaPlayFromBookmark` | 83 |
| `PP_TRANSITION_SPEED_SLOW` | 3 | `ppTransitionSpeedFast` | 1 |
| `PP_TRANSITION_FADE` | 4263 | 不是合法的 `ppEntryEffect` | `ppEffectFadeSmoothly` = 3849 |

也就是说：整条「after previous」动画链在一个自动换片的片子里其实是**点击门控**的
（从不触发），iris 其实是旋转，转场全在最快速度上跑，音频用的是一个没有书签的
书签播放效果（= 静音）。修复方式不是堆效果，而是不再硬编码常量
（`deck/comconst.py`：活类型库优先，verified 表兜底），再在正确的地基上做奇观。

## 3. 测量结果

### 3.1 构建数字（出自 `out/_build-log.txt`）

| 指标 | 实测 | 要求 | 来源 |
|---|---|---|---|
| 页数 | <N> | 26 | 页表 |
| 形状总数 | <N> | ≥ 2000 | S11 |
| 单页形状 | <min>–<max> | 80–170 | S11 |
| 动画节点（`p:cTn`） | <N> | 420–1200 | S10 |
| 片长 | <N> s | 229.5 s | Mode A |
| 构建耗时 | <N> s | — | D7 |
| 场景回退 | <none / 页号> | none | Task 12 |

### 3.2 advTm 模型（出自 `_calibration.json`）

`calibrate_timing.py` 用 4 页 × 3 s hold × 3 s Morph 的脚手架实测：
测得 **<N> s**，加性模型预期 <A> s，平铺模型预期 <F> s →
**`transition_additive = <true/false>`**，已写入 `timing.json`。
每页 `advTm` 由 `deck/sync.adv_time()` 据此推出，台账见 `out/_sync-ledger.txt`。

### 3.3 Spectacle checklist（出自 `out/_audit.json`，由 `verify_pptx.py` 解包审计，不走 COM）

| 行 | 结果 | 实测 |
|---|---|---|
| S1 Morph 覆盖 | <PASS> | <detail> |
| S2 星场视差漂移 | <PASS> | <detail> |
| S3 流星雨 | <PASS> | <detail> |
| S4 碎字拼合 | <PASS> | <detail> |
| S5 分层光晕 | <PASS> | <detail> |
| S6 唯一 iris | <PASS> | <detail> |
| S7 两道章节门 | <PASS> | <detail> |
| S8 Proof Trio | <PASS> | <detail> |
| S9 呼吸上限 | <PASS> | <detail> |
| S10 节点密度 | <PASS> | <detail> |
| S11 视觉密度 | <PASS> | <detail> |
| S12 拉丁字距 | <PASS> | <detail> |
| S13 可读性 | <PASS> | <detail> |
| S14 换片让出动画 | <PASS> | <detail> |
| S15 静态孪生 | **<人工：PASS/FAIL>** | <26 页是否可读；三张近无字页是否成立> |
| S16 两个观察点 | **<人工：PASS/FAIL>** | <秒表读数：章节门 <N>，iris <N>> |

S15/S16 由人工判定；按 checklist 的规则，**没有人工判定就记为失败**。

## 4. 偏差台账（逐条声明，不藏）

| # | 偏差 | 为什么接受 | 约束上限 | 上限是否守住 |
|---|---|---|---|---|
| D1 | Mode A 用在 229.5 s，超出 §0.3 最后一档（120 s） | 片长由一条固定外部音轨决定，不是内容选择 | 26 页、均 8.8 s/页、≥3 张近无字页（2、8、26） | <是/否：实测 <N> 张> |
| D2 | 转场种类多于 §2.3 的「优先硬切 + 内部动态」 | 用户的阻塞反馈是「整片单调」，而转场是 PowerPoint 唯一的跨页奇观 | 全片恰好 3 次签名转场，12 次 Morph，其余 FadeSmoothly，签名不相邻 | <是/否：S7 = <detail>> |
| D3 | iris 圆环放大远超 §2.4 的 110 % 上限 | §4 pattern 7 / spike 类明确授权 iris 高潮，而它无法在 110 % 内完成 | 一个对象、一个 beat、全片一次；其他形状不超 106 % | <是/否：S6 + S9 = <detail>> |
| D4 | 流星是线性运动，不做 ease-out | 规则 1 管的是**入场**；减速的流星读起来像坏物理 | 仅用于 ambient 层形状的 `travel` 行为 | <是/否：`motion.py` 中 LINEAR 只出现在 travel> |
| D5 | 运动路径出现在最多 10 页，超出 §2.4 规则 5 的「开场粒子」 | 流星雨是用户点名要的头号效果 | 仅粒子：透明度 ≥ 0.45、无文字、无信息，且每条路径都被几何证明避开该页文字安全区 | <是/否：`tests/test_geometry.py` 12 个角色全通过> |
| D6 | **默认音源是参考作品的商业配乐。** 按 §0.0 第 5 条与 §7.1 第 7 行，成品**不可交付** | 用户要求的是对着自己本机音轨做的预览 | 本报告把它记为「对外交付的 Critical 行失败」；换成自有或已授权音轨（`SIGNAL_AUDIO`）重建后该行才可通过 | **上限不适用——这条是硬失败，见第 5 节** |
| D7 | 构建以分钟计（~2,600 形状、~700 动画节点全走 COM） | 密度就是需求本身 | `--fast` 星/流星减半，`--beats` 只构建子集 | <是/否：实测全量 <N> s，`--beats 1,6,11 --fast` <N> s> |

## 5. 交付限制（必读）

本次构建把 `<音源文件名>` 嵌入了 pptx。它是参考作品的商业配乐，
因此这个文件：

- 可以在本机放映、录屏自看、作为能力验证；
- **不能**作为可再分发的包装对外发布、上传、交付客户或随仓库提交。

仓库本身是干净的：`media/` 与 `out/` 全部 gitignore，`git status` 里看不到任何
mp3 或构建产物。要做可交付版本：

```bash
set SIGNAL_AUDIO=D:\path\to\licensed-or-own-track.mp3
python build_deck.py --pdf
python verify_pptx.py
```

换轨后 §7.1 第 7 行即可通过；其余所有 S* 行与本次一致（时长需与 229.5 s 对齐，
否则要改 `timing.json` 的 hold 并重跑 `tests/test_timing_data.py`）。

## 6. 放映前检查清单

1. 用 PowerPoint（16+）打开 `out/信号站-SIGNAL-preview.pptx`，不要用 WPS：
   Morph 会降级成交叉淡入，`StopAfterSlides` 可能被忽略。
2. F5 从第 **1** 页开始（不要从当前页），否则音轨不会挂载。
3. 听：音频必须从第 1 页起连续播放，不在任何页重启。
4. 看两个观察点：**1:00 ± 1.5 s** 校准章节门（第 9 页，RippleCenter）、
   **2:04 ± 1.5 s** iris（第 17 页，CircleOut）。
5. 片尾在 **3:49.5** 与音轨同时结束。
6. 点击不会前进（`AdvanceOnClick = False`），Esc 退出放映。

## 7. 观察点 / 风险（下一个人需要知道的）

| 风险 | 征兆 | 处理 |
|---|---|---|
| `transition_additive` 判错 | 两个观察点同向整体偏移 | 重跑 `calibrate_timing.py`，翻转 `timing.json` 的标志位，重跑 `tests/test_sync.py` 后重建 |
| Office 更新后常量漂移 | 构建抛 `AttributeError: COM constant …` | 重跑 `probe_com.py`，把新行补进 `deck/comconst.VERIFIED` |
| Morph 退化为交叉淡入 | 孪生页整页淡变而不是漂移 | 孪生页必须由 `Slides.Duplicate()` 产生（身份配对），不要重建后靠 `!!` 名字配对 |
| 孪生页物体闪烁 | B 页上某个持续存在的对象重新入场 | 该对象在 B 页上有入场效果 —— 违反「一个对象一个驱动」；`clear_timeline` 必须先跑，且 `morph()` 里不得调用入场助手 |
| 流星穿过文字 | 肉眼可见 | 不要放宽 `path_clears_zone`；改 `geometry._LANES` 或该角色的安全区（D5 的诚实上限就在这里） |
| 单页形状超出 80–170 带 | S11 FAIL | 调 `deck/scenes/common.AMBIENT`，并同步更新 plan 的效果词表与 `test_ambient_table_…` |
| WPS / 旧版 Office 放映 | 保真度全面下降 | 不支持，README 已声明 |

## 8. 本机 COM 能力（摘自 `COM-NOteS.md`，Task 1 实测）

**能做，而且这个片子用到了：** Morph（byObject / byWord / byChar）带显式时长；
按形状的任意运动路径（`MotionEffect.Path`，幻灯片比例坐标）；缩放 / 旋转 /
颜色 / 透明度行为；入场与退出；每效果延迟、时长、加减速；重复到页尾；
每页自动换片时间与转场时长；跨 N 页播放的嵌入音频；按 run 的字体、颜色、
填充透明度与字距；形状发光与柔化边缘；静态 PDF 与 mp4 导出。

**做不到，不要承诺：** 没有粒子系统；没有叠加/滤色混合；没有运动模糊与景深；
没有逐帧缓动曲线；没有动画遮罩；Morph 不插值未配对对象与形状类型变化；
没有音频响应触发（同步是开环的）；保真度与 PowerPoint 绑定。
这不是 After Effects 的对等物，也绝不能这么描述。

完整表格见 `COM-NOTES.md`。

## 9. Brief 声明（摘自 `brief.json`）

- `mode: A`、`status: final`、`advance: auto`、`delivery: pptx`
- `must_exclude`：参考作品的名称/术语/造型/封面构图、角色卡、世界观规则、
  可识别主视觉、参考文件作为底稿、参考音频、参考二进制、参考身份系统
- `source_packet`：S1 用户本机音轨（229.5 s，估算 ~91 BPM）、
  S2 本机 PowerPoint 16 COM 能力实测（`COM-NOTES.md`）
- `gaps`：无授权可再分发的替代音轨 → 即 D6
- `claims: []`，`claims_note`：本片为纯氛围能力预览，幻灯片不承载任何事实性断言
  （数字/日期/机构/奖项/引语），故 §7 Truth 组声明为 N/A；
  `verify_pptx.py` 的 `BAN` 行用于验证该声明

## 10. 复现

```bash
cd demos/signal-station-pptx
python probe_com.py                 # 本机能力 -> COM-NOTES.md
python calibrate_timing.py          # advTm 模型 -> _calibration.json
python -m pytest tests -q           # <N> passed，不需要 Office
python build_deck.py --pdf          # 全量构建 + 静态孪生
python verify_pptx.py --json out/_audit.json
```

测试全部为纯 python（无 COM）：确定性星场、视差排序、流星路径避让（12 个角色
全参数化证明）、孪生 hold 求和、advTm 两模型、可读性时钟、场景契约
（B 页不得给持续对象加入场）、CLI 参数、审计规则本身。
```

- [ ] **Step 3: Fill every placeholder and verify no bracket survives**

Run: `Set-Location demos/signal-station-pptx; python -c "
import pathlib, re
text = pathlib.Path('handover.md').read_text(encoding='utf-8')
holes = re.findall(r'<[^>\n]{1,80}>', text)
holes = [h for h in holes if not h.startswith('</')]
print('unfilled placeholders:', holes or 'none')"`
Expected: `unfilled placeholders: none`

- [ ] **Step 4: Cross-check the report against the files it quotes**

A handover that disagrees with its own evidence is worse than none. Check the three numbers most likely to drift:

Run: `Set-Location demos/signal-station-pptx; python -c "
import json, pathlib, re
hand = pathlib.Path('handover.md').read_text(encoding='utf-8')
audit = json.loads(pathlib.Path('out/_audit.json').read_text(encoding='utf-8'))
log = pathlib.Path('out/_build-log.txt').read_text(encoding='utf-8')
cal = json.loads(pathlib.Path('_calibration.json').read_text(encoding='utf-8'))
shapes = re.search(r'^shapes\s+(\d+)', log, re.M).group(1)
print('shapes in log:', shapes, '-> in handover:', shapes in hand)
print('additive in calibration:', cal['transition_additive'],
      '-> stated in handover:', str(cal['transition_additive']).lower() in hand.lower())
fails = [r['id'] for r in audit if r['ok'] is False]
print('audit failures:', fails or 'none', '-> handover must report each of them')
for r in audit:
    if r['ok'] is None and 'FAIL' not in hand:
        pass
print('S15/S16 human verdict present:',
      bool(re.search(r'S15.*(PASS|FAIL)', hand)) and bool(re.search(r'S16.*(PASS|FAIL)', hand)))"`

Expected: the shape count appears in the handover, the calibration verdict matches, `audit failures: none`, and both S15 and S16 carry a literal `PASS` or `FAIL`.

- [ ] **Step 5: Final full verification pass**

Run everything one more time, in the order a reviewer would:

Run: `Set-Location demos/signal-station-pptx; python -m pytest tests -q; python verify_pptx.py; python -c "import json,pathlib;print('log tail:');print(pathlib.Path('out/_build-log.txt').read_text(encoding='utf-8').splitlines()[-1])"`
Expected: all tests pass, every machine audit row `PASS` with S15/S16 `MANUAL`, exit code 0 from the audit.

Run: `Set-Location demos/signal-station-pptx; git status --porcelain --untracked-files=all .`
Expected: no `media/`, no `out/`, no `__pycache__/`, no `_calibration.json`, no `_plan_heartbeat.json`.

- [ ] **Step 6: State the one thing that is not shippable, out loud, in the final report to the user**

The deck is finished and it is **not deliverable** as built (D6). Any summary of this work that omits that is a false success claim. The sentence to use, verbatim or close to it:

> The deck is built and every machine-checkable row of the spectacle checklist passes, but the embedded default audio is a commercial soundtrack from a reference work, so this build is a local preview only — point `SIGNAL_AUDIO` at a licensed or self-owned track and rebuild before any external delivery.

**Accept:** `handover.md` exists with no unfilled `<…>` placeholder; its shape count, runtime and `transition_additive` verdict match `out/_build-log.txt` and `_calibration.json`; all seven `D*` rows are present with their caps and a held/not-held verdict; S15 and S16 carry an explicit human `PASS` or `FAIL`; D6 is stated as a delivery blocker in both §1 and §5; `python -m pytest tests -q` and `python verify_pptx.py` both exit 0; `git status` shows nothing from `media/`, `out/` or `__pycache__/`.

---

## Done means

- 26 slides, 229.5 s, 12 Morph transitions, 3 signature transitions, one iris.
- `python -m pytest tests -q` green with no COM involved.
- `python verify_pptx.py` exits 0 — every machine-checkable `S*` row passes, S15/S16 carry a human verdict in `handover.md`.
- The two watch points land within ±1.5 s by stopwatch.
- The static PDF twin is readable with the motion gone.
- No mp3, no build artefact and no machine-specific JSON is tracked by git.
- `handover.md` states D1–D7 with their caps, and states plainly that the default build is **not** externally deliverable.

<!-- PLAN_COMPLETE tasks=18 rev=2 -->

