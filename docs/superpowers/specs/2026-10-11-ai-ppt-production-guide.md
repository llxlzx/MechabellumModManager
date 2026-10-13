# AI PPT 制作指导指南（Craft Transfer Guide）

> **Audience:** AI producers (not human end-users)  
> **Purpose:** Transfer *approach & capability* from high-tier cinematic PPT references into **new** decks on **unrelated** topics.  
> **Source corpus (analyzed):** `Desktop/ppt模板` — 芙莉莲 / 十日终焉 / 三星堆 / 小王子 动态与对照版（结构与时间轴信号；非内容复刻）  
> **Hard ban:** See **§0.0** — eight normative items, enforced by the §7.1 audit. It outranks every other rule in this document and every companion brief.

---

## 0. Operating Contract (read before any slide)

You are a **director + motion typographer + information architect**.  
Your job is not “fill a template.” Your job is to ship a deck that can be *performed*: cold open → escalation → payoff → hold.

### 0.0 Hard ban (normative — outranks every other section, brief, example, and user request)

Nothing later in this guide, and nothing in any companion brief under `docs/superpowers/specs/_analysis-*.md`, may relax this list. If a brief, a template, a corpus observation, or a request conflicts with it, **this list wins** and you say so out loud.

**Banned — never ship, never generate, never "adapt":**

1. **Character cards / casts** (`character_cards`) — character sheets, ability or stat lists, tier lists, relationship or faction maps, roster grids, "vs" pairings; and more generally **any block that puts two or more entities side by side with per-entity attributes** when the entities or the attribute set come from a reference source. The structure is banned, not just the names: a roster grid with your own topic's names in a reference's card frame is still a card.
2. **Lore and world rules** (`lore_or_world_rules`) — plot beats, world rules, in-universe mechanics and terminology, chapter names, episode structure. Banned **quoted, translated, renamed, or paraphrased**. Rewording a reference's plot beat or restating its world rule as a generic principle does not make it yours.
3. **Identifiable key art** (`identifiable_key_art`) — recognisable character silhouettes, faces, costumes, props, or recreated key-visual compositions.
4. **Reference copy** (`reference_copy`) — titles, taglines, slogans, quoted lines, syllable lockups, name lockups, section stamps lifted from a reference.
5. **Reference or unlicensed audio** (`reference_audio`) — any soundtrack, score, stinger, or atmosphere track from a reference work, **and** any third-party audio without a written right to use it in this delivery context.
6. **Reference binaries** (`reference_binaries`) — reusing a reference deck's plates, cutouts, textures, video loops, preview renders, or font files as shipped assets.
7. **Reference identity system** (`reference_identity_system`) — "same palette / same fonts / same chrome as X" as a requirement, **no matter where you read the values**: a companion brief, the reference `.pptx` itself, the source work's own branding, a fan wiki, a marketplace template, or a pack a user handed you "for reference". Palette, faces and chrome are derived from *your* topic (§10.0).
8. **Reference file as a base** (`reference_file_as_base`) — opening an existing deck and editing over it. **Start from a new empty deck** — not a reference file, not a vendor or marketplace template, not a template you saved off an earlier deck. If you want house furniture (masters, layouts, theme), create the empty deck first and add it yourself, so every part of the package is one you can account for.

**Allowed:** structural grammar — hierarchy, layer stacking, beat roles, timing bands, stagger logic, composition patterns, trigger models. Learn grammar, never cast lists.

**Enforcement:** §7.1 turns this list into a runnable audit. A ban failure is never fixed by rewording the claim; it is fixed by replacing the asset.

### 0.1 Success criteria (any topic)

1. **One job per slide** — if two jobs compete, split or kill one.  
2. **Readable at fullscreen in about a second** — the slide's one **hero signal** is final by the ~1–1.5 s hero deadline, before any body text; every other readable event is final by the ~5 s readable deadline. Both terms and both numbers are defined in **§2.4**, which is authoritative; this line is their headline form.  
3. **Kinetic text is planned as beats**, not pasted paragraphs.  
4. **Motion serves emphasis**, never decoration on every object.  
5. **Same craft language across slides** (type scale, accent logic, layer stack, timing feel).  
6. **Zero IP leakage** from reference corpus into finished content.

### 0.2 Output modes (pick one; do not mix casually)

| Mode | When | Slide count | Motion density | Body text |
|------|------|-------------|----------------|-----------|
| **A. PV / 短片感** | Showreel, opener, emotional pitch | see §0.3 | Very high | Minimal; fragments |
| **B. Exhibition / 叙事展陈** | Museum, case study, chapter story | see §0.3 | **Low — entrance-only** | Short blocks OK |
| **C. Briefing / 竞聘汇报** | Interview, work report | see §0.3 | Low-medium | Scannable bullets |
| **D. Hybrid** | Need both wow + info | see §0.3 | Gate motion after the open (§0.4) | Explicit switch |

**Mode B density, stated plainly:** exhibition motion is **entrance-only, with-previous, then hold** — one chord per slide, no exit, no emphasis loop, no idle float, visitor-paced advance. The measured exhibition deck sits in a 500–1000 ms band with 6 sub-200 ms spans in the entire file. "Medium-high" density belongs to Mode A; a gallery that performs is a gallery nobody reads.

Default for campus leadership / 部长竞聘: **Mode C**, optionally **D** with a 20–40s cold open.

### 0.3 Slide budget (authoritative — the only budget table in this guide)

Pick your **mode** first (§0.2 / Step 1), then the one duration row inside that mode. If any other section, brief, or habit implies a different number, **this table wins**.

**Band boundaries — every duration belongs to exactly one row.** Within a mode, the **first** band includes **both** of its bounds; every band after it **excludes** its lower bound and includes its upper bound. So in Mode A, 45 s and 60 s both sit in the first row, and the second row means *over* 60 s up to 120 s. In Mode C, 3:00 and 5:00 both sit in the 3:00–5:00 row; 5:00–8:00 means over 5:00 up to 8:00; 8:00–10:00 means over 8:00 up to 10:00. No duration falls between two rows, and none belongs to two.

**Outside the bands.** Shorter than a mode's first lower bound (a 30 s PV, a 2-minute briefing): use the first row, cut to its slide floor, and say so in the ship report. Longer than a mode's last upper bound (a 12-minute briefing): out of scope for this guide — re-scope the talk or split the deck. Never extrapolate a new row.

| Mode | Duration | Slides | Avg sec / slide | Notes |
|---|---|---|---|---|
| A | 45–60 s | 8–12 | 4–7 | Fragments only; no body blocks |
| A | 60–120 s | 10–14 | 6–9 | Top of the range only with ≥ 3 wordless plates |
| B | self-paced walkthrough (no fixed duration) | 18–28 | visitor-paced | Entrance-only motion (§0.2) |
| C | 3:00–5:00 | 10–14 | 15–25 | Scannable bullets; presenter-held |
| C | 5:00–8:00 | 12–16 | 22–32 | One extra evidence slide per added minute |
| C | 8:00–10:00 | 14–18 | 30–40 | Hard ceiling 18 |
| D | any Mode C duration | 3–5 cold open **+** the matching Mode C row | — | Cold open ≤ 40 s total and outside the C row's slide count (§0.4) |

**Overflow law:** if the content does not fit, **cut a claim**. Do not add a slide past the ceiling, and do not shrink type below the §Step 4 projection floor.

### 0.4 Mode D handoff contract (cold open → briefing)

A Mode D deck is two films glued at one seam. Build the seam deliberately or the deck reads as a style break.

- **Cold-open budget:** 20–40 s total, 3–5 slides, ≤ 1 moving asset, no body text, no bullets, no logo wall.
- **Handoff slide:** the last cold-open slide lands on a still frame that already states the deck's thesis in one line. Leave on a **hard cut**, not a transition.
- **Motion gate:** after the seam, motion drops to Mode C levels — soft, entrance-only, kinetic reserved for a title. No letter storms after the handoff.
- **Carry-through:** exactly two things cross the seam — the accent colour and one recurring mark. Field treatment, type scale and layout grid all switch to the briefing system.
- **Grounding gate:** the cold open is mood, so it carries no claims. Every fact-bearing slide lives after the seam and obeys §5.4.
- **Failure test:** if a viewer who joins at the handoff slide cannot tell the deck is now a briefing, the seam is not built.

---

## 1. What to steal (capabilities) vs refuse (content)

### 1.1 Steal these capabilities

- Narrative beat design (cold open / chapter / spike / settle)  
- Extreme hierarchy (display type vs whisper metadata)  
- Full-bleed cinematic field + negative-space text docking  
- Layer stack thinking (bg → mid → subject → type → grain/accent)  
- Kinetic typography (glyph/word fragmentation, staggered reveals)  
- Dual-language locking (primary + subordinate language as graphic, not translation dump)  
- Timing discipline (short hits + held rests)  
- **Static twin → motion twin** workflow — compose every slide as a finished still first, then add timing to that exact frame. The static twin stays a shippable file, not a draft (§Step 4, §7.3 row 4)  
- Video as *assist* on key emotional beats, not wallpaper on every slide  

### 1.2 Refuse these

- Specific character sheets / ability lists / relationship maps from references  
- Copy-pasting slogans, titles, or world rules from references  
- Recreating identifiable character silhouettes or key art  
- Using reference audio tracks  
- “Same palette as X IP” as a requirement (derive palette from *your* topic mood)

This list is the **§0.0 hard ban** in prose, kept here so §1 reads as a pair. §0.0 is the normative text and the §7.1 audit tests that version; if the two ever differ, **§0.0 wins**.

---

## 2. Evidence-backed craft signals (from corpus metrics)

**How to read this section:** these are **observations of seven source files**, not targets and not a scoring rubric. They tell you the range high-tier decks lived in; they do not tell you your deck is wrong for sitting outside it. Use them to sanity-check a plan ("25 dense bullet slides for a 60 s PV" is outside every observed range), never to tune a deck toward a number.

**Sample:** **7 saved files covering 4 storyboards**, 4 properties, one genre cluster (CJK cinematic PPT). No Western corporate decks, no academic decks, no live keynotes, no 竞聘 decks. Three of the seven are **static saves** of a storyboard that also has an animated save, so the effective sample is **four storyboards** — and one of those four (the exhibition deck) is the only non-PV deck in the set.

### 2.1 Deck shape (observed ranges, not targets)

| Signal | PV packs (芙莉莲动态 / 十日终焉动画 / 小王子) | Exhibition (三星堆动态) |
|--------|-----------------------------------------------|-------------------------|
| Slides | ~12–13 | ~23 |
| Widescreen | 13.33×7.5 in (16:9) | same |
| Timing coverage | Nearly all slides have timing trees | Nearly all |
| Anim node density | Hundreds–1400+ / deck | ~350 / deck |
| Text fragmentation (short tokens ≤2 chars) | Often 0.85–0.96 | ~0.69 (still fragmented, less extreme) |
| Avg pictures / slide | ~5–10 | ~11 |
| Video (both kinds counted together) | 1–23 `.mp4` files across this column's three saves (小王子动态 1 / 芙莉莲动态 8 / 十日终焉动画 23); across all seven saves in the corpus the count runs **0–23**. Mostly **living plates**, not edited assists (see Step 5) | 1 file |

**AI rule:** If Mode A and you plan 25 slides of dense bullets, you already failed. Cut to beats.

**Exceptions inside the sample itself — no single row is a law:**

- **Anim-node density spans 196 → 1374** across the **four animated saves** (196 / 359 / 594 / 1374). The other three files have zero. The 1374-node deck gets there by fragmenting six near-identical slides, not by being six times better.
- **Fragmentation ratio spans 0.62 → 0.96.** The 0.69 exhibition deck is the most *readable* of the set. A high ratio is a kinetic-title production method, not a quality score.
- **Pictures per slide spans 4.4 → 11.1.** The 11.1 figure counts stacked full-bleed fields, and that deck's own brief calls five stacked plates an anti-pattern.
- **Timing medians span 200 ms → 1000 ms**, measured on the four animated saves only. There is no correct median; the median follows the mode.
- **Three of the seven files carry zero animation nodes** — the static saves. Two of them still report timing coverage (7 of 12 and 11 of 12 slides) while carrying no animation nodes at all; the third reports no timing trees at all. So "nearly all slides have timing trees" describes the **animated saves** only, and even where a timing tree exists it may be empty. Never infer motion from timing coverage.

**Never report a corpus number as a property of your own deck** ("fragmentation 0.9, matching the reference"). That is a cargo-cult metric, and it is also an IP-proximity smell (§7.1).

### 2.2 Timing recipes (observed duration buckets)

Normalize your motion language to these bands:

| Band | Typical use | Notes from corpus |
|------|-------------|-------------------|
| **80–200ms** | Micro snaps, set visibility, flickers | Dominant in ultra-kinetic pack (十日终焉动画 median ≈200ms) |
| **200–500ms** | Stagger steps between fragments | Common bridge |
| **500–1000ms** | Standard enter / fade / scale settle | Dominant in 三星堆 (median ≈500ms); common in 芙莉莲动态 (median ≈750ms) |
| **1–2s** | Phrase hold, morph-ish travel, title lock | Dominant in 小王子 (median ≈1000ms, many 1–2s) |
| **>2s** | Emotional hold, video assist, end card | Rare; earn it |

**Parallelism:** Heavy use of `par` (parallel) + `set` + `anim`/`animEffect`/`animScale`.  
**AI rule:** Build timelines as nested groups: *parallel layer reveals* inside *sequential story beats*.

### 2.3 Transition vocabulary (use sparingly)

- Soft **fade** between chapters  
- Occasional **circle/iris** for “portal / reveal / performance spike”  
- Prefer **hard cut + internal motion** over fancy every-slide transitions  
- Morph-like continuity is a *special tool* (小王子-heavy); only when shapes truly correspond  

### 2.4 Motion physics (applies to every mode)

Consistent across all four source packs. These are rules, not ranges.

1. **Ease-out, always.** Entrances arrive and settle: `decel=100%`, or `accel=30% / decel=70%`. Linear entrances read as cheap; entrances that accelerate out read as broken.
2. **No bounce, no overshoot.** Nothing passes its rest position and comes back.
3. **Scale ceiling ~105%.** An ambient zoom goes to about 105% over ~6 s, and at most one object per slide does it. **110% is the hard ceiling** for any scale move; treat 120%+ as out of system. Honest provenance: this is read off the scale effects the four craft briefs measured, not an exhaustive scan of every `animScale` node in the corpus — it is the house rule, not a proven absence.
4. **No 1 ms appear cascades.** "Appear" is not an entrance. Minimum 250 ms, normally 500–1000 ms (§2.2). Sole exception: deliberate micro-flicker décor at ≤ 6 pt and low contrast.
5. **No motion paths or spins as a design move.** Exceptions: one slow ambient rotation (≥ 6 s, indefinite) sitting under the type, and opener particles.
6. **Stagger follows reading order.** Even steps, **80–500 ms** overall: editorial lines and title lockups sit at 100–500 ms, and kinetic glyph cascades sit at the fast end, 80–250 ms (§Step 5). An irregular burst inside ~1 s only for a montage. A strict 100 ms grid across a whole deck is the measured failure mode.
7. **Nothing animates once the hold starts** — except a declared ambient layer, defined next.

**Ambient layer vs readable event (this resolves the apparent §Step 8 contradiction)**

- An **ambient layer** is slow, low-contrast and carries no information: a 6 s 105% scale, a 20 s rotation, a particle field, a living plate breathing. It may continue indefinitely.
- A **readable event** is anything that carries meaning: type, a rule, a chart, a label, a chip, a number.
- The **hero signal** is the one readable event §0.1 says must land in a second — usually the title line, the number, or the anchor subject. Exactly one per slide.

Rules:

- **Hero deadline, ~1–1.5 s.** The hero signal is at final position and full opacity by then. This is the rule §0.1 point 2 is stating in craft terms.
- **Readable deadline, ~5 s.** Every *other* readable event is finished and still by the end of the first plate cycle (~5 s) **or** by the start of the slide's hold, whichever comes first. A title lockup that completes at 2–3 s and a last editorial line that lands by ~4.5 s are **on time**, not late — that is the measured budget in the craft briefs, and the Step 5 concept recipe sits inside it.
- Nothing readable may still be moving after the readable deadline. Montage timelines that technically run 13–14 s because exits are long are fine **only** if the hero landed at 1.5 s and the rest landed by 5 s.
- Ambient layers may keep moving underneath, at low contrast, never crossing the type.
- §Step 8 fails a slide for *motion still running when the viewer needs to read*. That means a **readable event** past its deadline. A plate breathing under a landed sentence is correct, not a failure — fix §Step 8's reading, not the plate.
- **Test:** freeze the slide twice — at 1.5 s the hero must be final; at the readable deadline every word must be at final position and full opacity.

---

## 3. Master pipeline (AI must follow in order)

```
0 Brief lock → 1 Mode pick → 2 Beat sheet → 3 Visual system
→ 4 Still compose (static twin) → 5 Motion score → 6 Asset brief
→ 7 Build → 8 Perform test → 9 Ship checklist
```

### Step 0 — Brief lock (required fields)

Produce a JSON object before designing:

```json
{
  "topic": "",
  "audience": "",
  "duration_sec": 0,
  "mode": "A|B|C|D",
  "status": "draft|final",
  "viewing": "projected|close_range",
  "promise": "one sentence: what the viewer should believe after",
  "must_include": [],
  "must_exclude": [
    "character_cards", "lore_or_world_rules", "identifiable_key_art",
    "reference_copy", "reference_audio", "reference_binaries",
    "reference_identity_system", "reference_file_as_base"
  ],
  "delivery": "pptx|web|both",
  "advance": "presenter|auto|web_keys",
  "advance_notes": "if auto: where each slide's hold comes from",
  "source_packet": {
    "provided": true,
    "items": [
      { "id": "S1", "what": "what this source is, e.g. 2025 社团活动台账", "from": "user|public_record|interview|own_notes", "date": "YYYY-MM" }
    ],
    "gaps": ["what you asked the owner for and did not receive"]
  }
}
```

`must_exclude` is **fixed boilerplate from §0.0**. Append topic-specific exclusions after the eight tags; never remove one, never shorten the list to "the relevant ones". `viewing` defaults to `projected` and may only be `close_range` when `mode` is `B` and the viewer stands in front of the screen (kiosk, wall panel). It is the sole unlock for the 14 pt body exception in §Step 4; Mode C may never set it.

`status` is the deck's **delivery state**, and it is the only thing in this guide that licenses an open `[NEEDS INPUT]` token. `final` means the deck is handed over as finished and will be presented as-is; `draft` means the owner still has to complete it before it is shown. **`status` defaults to `final`** — `draft` is a deliberate declaration made with the owner, recorded in the Brief and restated in the ship report, not a label you reach for at ship time because a token is still open. (Not to be confused with the §5.4 ledger row's own `status`, which describes one claim, not the deck.)

**No-invention rule (every fact-bearing deck; Mode C above all):**

Every number, date, name, title, award, scale, rank, outcome and quotation on a slide must trace to a `source_packet` item id. You may invent **framing, metaphor, structure, visual language and phrasing**. You may not invent **facts**.

`source_packet.provided: false` is a legitimate state — it means you must ask before you design. Do not proceed to Step 2 with an empty packet and a fact-bearing brief.

When a slide needs a fact you do not have, put the literal token on the slide and in the beat sheet:

```
[NEEDS INPUT: 2025 会员留存率 — from S?]
```

Token rules:

- It stays **visible** in the deliverable. Never silently fill it with a plausible number.
- Never replace it with a hedge. 显著提升 / 多次 / 广受好评 / 大幅 standing in for a missing number is the same failure wearing a coat.
- Never replace it with a rounder, safer invention ("约 30%"). Unknown is unknown.
- Count them. The ship report states the count and lists each one.
- A deck may ship with open tokens **only** when the Brief carries `status: draft`. A deck with `status: final` and one or more open tokens is a Critical failure (§7.2 row 2).

### Step 1 — Mode pick

Map duration → mode → slide budget using the **§0.3 authoritative table**. Do not restate budgets here; a second copy is how the three numbers in this guide drifted apart in the first place.

Record the chosen row in the Brief as `mode` + `duration_sec`. If the content will not fit the row's ceiling, apply the §0.3 overflow law — cut a claim, do not add slides.

### Step 2 — Beat sheet (storyboard)

Each beat = one slide. Schema:

```json
{
  "i": 1,
  "role": "cold_open|title|chapter|concept|proof|spike|gallery|settle|end",
  "job": "single imperative",
  "hero": "what eye hits first",
  "type_plan": ["fragments in reveal order"],
  "motion": "none|soft|kinetic|spike",
  "hold_sec": 3.5,
  "advance": "click|auto",
  "advance_sec": 0,
  "trigger": "on_click|with_previous|after_previous",
  "notes": "layer intent only"
}
```

**Advance & trigger model — declare it once in the Brief, then per beat:**

| Model | Who advances | Entrance triggers | Use |
|---|---|---|---|
| `presenter` | A human clicks; the slide waits indefinitely | One chord per slide: first effect `on_click`, everything else `with_previous`. Ambient may loop. | **Mode C default — always.** Mode B galleries. |
| `auto` | A timer (`advance_sec`) | All `with_previous` / `after_previous`. No click-gated reveal anywhere. | Mode A films, kiosk loops, exported video |
| `web_keys` | Keyboard or click on a web stage | A state machine per beat; honour `prefers-reduced-motion` | Web pseudo-PPT |

**The two `advance` fields are not the same enum.** The Brief's `advance` (`presenter | auto | web_keys`) names the deck's **model**, declared once. A beat's `advance` (`click | auto`) names that one slide's **mechanism**, and it is derived from the model, never chosen against it: Brief `presenter` → every beat `click`; Brief `web_keys` → every beat `click` (the key press *is* the click); Brief `auto` → every beat `auto` with `advance_sec > 0`. A beat that disagrees with the Brief's model is a Brief bug (law 3, law 6).

**Laws**

1. **Mode C is presenter-held.** Never auto-advance a briefing: the speaker's pace is the deck's pace, and a timed slide turns a question into a cut. If an unattended copy is also needed, export a **second** file set to `auto` with recomputed holds — do not ship one file trying to be both.
2. **One driver per object.** An object is revealed by Morph **or** by an entrance effect, never both. Parking an object offstage *and* giving it an entrance means it never appears — a measured corpus bug.
3. **Click-gated type in an `auto` deck is a bug.** If `advance: auto`, no effect may carry `delay: indefinite`.
4. **Auto holds obey §10.1 law 8** — longest motion + 0.5 s, then reading time on top.
5. **A click must not destroy the shot.** In `auto` decks, state in the Brief whether a click advances or seeks; a click that silently skips the choreography is the measured failure.
6. **Enum alignment — the Brief's three enums are not independent.**
   - `delivery` has exactly three values: `pptx | web | both`. There is no `video` value; a motion-twin MP4 is an **export artefact** of an `auto` build, checked by §7.3 row 4, not a delivery mode.
   - `advance: web_keys` requires `delivery` to include `web` (`web` or `both`).
   - `advance: presenter` is the only legal value when `mode` is `C` (law 1).
   - `viewing: close_range` is only legal when `mode` is `B` (§Step 4 projection floor).
   - Any combination outside these is a Brief bug, not a style choice. Fix the Brief before Step 2.

**Canonical PV arc (adapt, don’t copy themes):**

1. Black / noise / tuning bar (atmosphere)  
2. Title lock (display type)  
3. Thesis line (one sentence max)  
4–5. Chapter or concept spikes (metaphor image + short label)  
6. Proof / structure (only place denser info is allowed)  
7. Emotional or craft spike (iris / scale / video assist)  
8. Settle cards (3 points max if briefing)  
9. End hold  

**Canonical briefing arc (Mode C):** use the **§5.2 information architecture** — seven core-spine blocks, plus the duration-gated optional blocks. Do not restate the arc here; §5.2 is the only Mode C spine in this guide.

### Step 3 — Visual system (derive, don’t clone)

```json
{
  "bg": "#0E0E10_or_topic_base",
  "paper": "#F5F5F7",
  "accent_1": "single saturated emotional color",
  "accent_2": "optional cool counter for depth",
  "type_display": "serif OR heavy display for titles",
  "type_body": "clean gothic/sans for reading",
  "type_meta": "tracked small caps / letterspaced latin",
  "decor_motifs": ["thin rules", "corner index", "one recurring mark"],
  "grain": "0–5% optional",
  "forbidden": ["rainbow accents", "equal emphasis everywhere"]
}
```

**Rules:**

- One accent does 80% of emotional pointing.  
- Dark field + light type OR light field + ink type; pick one system.  
- Recurring micro-mark (dot / rule / monogram) = continuity, not logo spam.

### Step 4 — Still compose first (static twin)

Before animation, each slide must work as a poster:

**Layer stack (bottom → top)**

1. **Field** — full-bleed atmosphere (photo/paint/gradient/texture)  
2. **Anchor subject** — one metaphor object/region  
3. **Vignette / shade** — protect type contrast  
4. **Type lockup** — title / sub / meta  
5. **UI chrome** — chapter label, index, tiny data-as-decor  
6. **Grain / overlay** — optional unify layer  

**Composition patterns (name them in notes):**

| Pattern | Layout | Use |
|---------|--------|-----|
| **Poster Center** | Hero center, title over/through | Cold open, end |
| **Dock Left / Art Right** | Text dock in negative space | Concept explain |
| **Dock Right / Art Left** | Same mirrored | Variation |
| **Diagonal Axis** | Subject off-center, title opposite corner | Tension |
| **Chapter Gate** | Huge type, minimal art motion | Section break |
| **Proof Grid** | 3 cards max | Briefing evidence |
| **Glyph Rain** | Fragmented letters assembling | Kinetic title only |

**Type hierarchy (default scale, 13.333×7.5 in canvas; CSS `clamp` equivalents for web)**

- Display: 64–120 pt — at most one poster glyph per chapter may go 140–170 pt  
- Section: 28–40 pt  
- Body: **18–24 pt** — 18 is a floor, not a target  
- Meta: 10–12 pt tracked — a *mark*, never a sentence. It sits below the 18 pt floor precisely because it is **not** meant to be read from the room (a page index, a tracked latin ghost, a rule label); the moment a viewer needs its content, it is body text and owes the floor  

**Projection floor (any room with a projector or a shared screen — and Mode C always)**

- Anything that must be read: **≥ 18 pt**. No exception for "it's only a footnote" — if it must be read, it is body text.
- If the content will not fit at 18 pt, **cut words or split the slide**. Shrinking type to fit is a Critical failure (§7).
- **Mode C has no exception.** A 竞聘 / 汇报 deck is read across a room off a projector or a shared screen, so 18 pt is its unconditional floor. "It's a web deck, they'll be on a laptop" does **not** unlock 14 pt in Mode C — a briefing you built for a laptop will be projected anyway, and you will not be in the room to stop it.
- The **only** exception in this guide: **Mode B** at close range — a kiosk or wall panel a visitor stands in front of — may go to 14 pt, and only when the Brief declares `"viewing": "close_range"`. Modes A, C and D never do; `viewing: close_range` is illegal outside Mode B.
- **Contrast proxy** (use when you cannot measure a ratio): the type must still read when you (a) convert the frame to greyscale and (b) blur it until individual glyphs smear. If it disappears into the plate in either test, add a vignette or a shade plate (layer 3 above) — do not add a stroke, an outline, or a glow.

**Body text cap**

- Mode A: ≤ 18 Chinese characters on screen at once (prefer less)  
- Mode B: ≤ 70 characters in one block  
- Mode C: ≤ 5 bullets × ≤ 16 characters each (or 3 tighter bullets)

### Step 5 — Motion score

Assign each slide a motion class:

| Class | Enter | Hold | Exit | Max animated objects |
|-------|-------|------|------|----------------------|
| none | cut | static | cut | 0 |
| soft | 300–700ms fade/up | rest | fade | 3–5 |
| kinetic | staggered fragments 80–250ms steps | short rest | hard cut | 8–20 fragments |
| spike | iris/scale/video 400–1200ms | punch hold | hard cut | 1 hero event |

**Sequencing recipe (kinetic title):**

1. `set` hide all fragments  
2. parallel groups of 2–4 fragments every 80–160ms  
3. accent underline / blot / rule at +200ms after last glyph  
4. hold 1.2–2.0s  
5. cut or fade to next beat  

**Sequencing recipe (concept slide):**

1. Field already present (or 400ms fade)  
2. Subject scale 95%→100% or subtle rise 500–800ms  
3. Section label fade 300ms  
4. Title lock 500–800ms  
5. Body fade last 400ms  
6. Hold for reading (Mode B/C: 4–7s)

*Clock check (§2.4): run end-to-end this recipe lands at ~2.1–2.7 s — well inside the ~5 s readable deadline. The hero still owes the ~1–1.5 s hero deadline, so lead with whichever of step 2 (subject) or step 4 (title) is the hero and start it at or near t=0; the non-hero steps fill the remaining budget. If the title is the hero, run steps 3–4 before step 2.*

**Living plate vs video assist** (this supersedes any "≤ 1 video per N slides" rule stated elsewhere):

Two different things both get called "video". Budget them separately or the briefs will look like they contradict each other.

| Kind | What it is | Mode A | Mode B | Mode C | Mode D |
|---|---|---|---|---|---|
| **Living plate** | The slide's own still, woken up. Locked camera, one motion verb (drift / light shift / slow rise), 4–6 s, loops or holds under the type. No cuts, no subject change, no audio of its own. | up to **one per slide** (two only inside a montage) | ≤ 2 in the deck, threshold only | 0 by default | cold open only |
| **Video assist** | Edited footage carrying its own narrative: cuts, camera moves, a subject doing something, or sound. | ≤ 1 per 4–5 slides **and** ≤ 2 per deck | 1 threshold film, 15–25 s, no type on top | 0 unless it is a product demo, then ≤ 1 | ≤ 1, inside the cold open |

**Living-plate rules**

1. Compose the still first, then animate **that exact frame** (§Step 4). A plate that does not match its still is a new shot, not a plate.
2. Standard length 4–6 s. It may loop or hold under a longer dwell. The slide's **hero signal** lands inside the first ~1–1.5 s; every remaining readable event lands by the **readable deadline** — the end of the first plate cycle, ~5 s (§2.4). Once landed, type stays landed while the plate keeps breathing.
3. **One motion verb per plate.** Two verbs makes it a video assist — budget it as one.
4. No per-plate audio. If a bed exists it is one continuous external track, with plates mixed under it.
5. Weight is a real constraint: plate-per-slide decks run past 200 MB. Set a total media cap in the Brief and state the final size in the ship report (defined in §7 "How to read this checklist"; the check is §7.3 row 5).

### Step 6 — Asset brief (abstract)

Ask for assets by **role**, never by “copy that character”:

```json
{
  "field": "mood + lighting + space type",
  "subject": "metaphor object (not a named character)",
  "texture": "optional material overlay (metal/paper/grain)",
  "type_special": "display font file if required",
  "motion_plates": "optional mp4 loops for spike slides"
}
```

### Step 7 — Build constraints by delivery

**If `pptx`:**

- Widescreen 16:9  
- Prefer Morph only when shapes match; else timing + fade/scale  
- Keep a **static twin** file for editing safety  
- Embed fonts only if licensing allows; else provide install list  

**If `web` (pseudo-PPT):**

- Fullscreen stage, keyboard + click advance, autoplay optional  
- CSS/JS state machine per beat  
- Honor `prefers-reduced-motion` (fade-only fallback)  
- No external image CDN required for core demo  

### Step 8 — Perform test (mandatory)

Play once without pausing. Fail if:

- Any slide needs explanation to know its job  
- Text unreadability against field  
- A **readable event** still moving past its deadline (§2.4): the hero not final by the ~1–1.5 s hero deadline, or any other word, rule, chart, label, chip or number not final and still by the ~5 s readable deadline or the start of the hold, whichever comes first. A declared low-contrast **ambient layer** still breathing underneath is **not** a failure — see §2.4  
- Two accents fighting  
- More than one “most important” element  

### Step 9 — Ship checklist (AI self-gate)

See §7.

---

## 4. Shot grammar catalog (callable patterns)

Use pattern names in your storyboard `notes` so motion and layout stay aligned.

1. **Cold Grain** — near-black + noise/texture + thin energy bar; 1.5–3s  
2. **Title Fracture** — word/glyph assemble; accent settle; no body  
3. **Thesis Whisper** — one line, high tracking or soft fade; max 1 supporting meta  
4. **Chapter Hammer** — oversized chapter word; English ghost type behind; 2–4s hold  
5. **Metaphor Field** — one symbol carries meaning (clock, path, bridge, spark…); text docks in safe shade  
6. **Proof Trio** — three equal cards; stagger 120–200ms; Mode C default  
7. **Iris Spike** — circle expand or center blast; Mode A climax only  
8. **Gallery Drift** — multiple plates; only one focal at a time  
9. **Data-as-Decor** — a number/index as typography ornament, not a spreadsheet  
10. **End Lock** — minimal mark + short farewell; hold; no new info  

---

## 5. Mode C specialization — 竞聘 / 汇报 (priority path)

When topic is leadership election / work report:

### 5.1 Tone

- Credible, designed, modern — **not** dark-fantasy PV skin by default  
- Motion: soft + selective kinetic on title only  
- Keep PV tricks inside the optional Mode D cold open — budget, handoff and motion gate in **§0.4** (20–40 s, 3–5 slides). No second number here  

### 5.2 Information architecture (scale by duration)

**Core spine — every Mode C deck, any length. Nothing here is droppable:**

1. **Identity** — name, role sought, one-line positioning  
2. **Promise** — the single sentence you want repeated after you leave the room  
3. **Pillars** — three capabilities, named as nouns, not adjectives  
4. **Evidence** — one slide per case: situation → action → result, every claim carrying a §5.4 ledger id  
5. **Plan / platform** — three concrete moves, each with a first step and a date. Not aspirations. A 竞聘 deck without a plan is a résumé read aloud  
6. **Ask** — what you are running for, stated plainly, plus the resource ask if the role needs budget, people or authority  
7. **Close** — the promise again, one mark, stop  

**Optional blocks, added by duration (budget from §0.3):**

| Block | Add when | What it is | Slides |
|---|---|---|---|
| **Diagnosis** | over 5:00, **and** the race is contested or the record has a visible gap | What is actually wrong or unfinished today: 2–3 specific observations, each sourced. Credibility is usually won here — naming the real problem outranks listing achievements. | 1 |
| **Timeline** | over 5:00, **and** the record is cumulative | A horizontal spine: 3–5 dated points, one line each. Shows trajectory instead of an unordered pile of wins. | 1 |
| **KPI / metrics** | over 8:00, **and** the role owns a number | 3 numbers maximum, each with period, denominator and method in the ledger. Never a dashboard. | 1 |
| **Risk / honesty** | over 8:00 | One thing you are not good at yet, plus the mitigation. Costs 40 s, buys the room. | 1 |

**Duration gate (binding, one rule):** the duration in each "Add when" cell is a **hard gate**, and the condition beside it is a filter applied inside that gate. A block may be added only when **both** halves are true. The gate is never opened by the condition alone: at **3:00–5:00 no optional block may be added at all**, however contested the race, however cumulative the record. A contested race, a cumulative record or a number-owning role changes *which* optional block you add first once the gate is open — never *whether* a 3:00–5:00 deck gets one. At 3:00–5:00 the diagnosis is one line inside **Promise**, not a slide.

**Ordering law:** Diagnosis goes **before** Pillars (problem → why me), never after Evidence. Timeline and KPI live inside the Evidence run. Risk goes immediately before the Plan.

**Dropping law:** drop **optional** blocks before compressing the Evidence run; never drop a core-spine block. Three strong evidence slides beat six one-line blocks.

**At 3:00–5:00 you run the core spine and nothing else.** The number you must hit is the §0.3 **total slide floor of 10** — there is no separate Evidence quota. Do the arithmetic: six single-slide blocks plus an *n*-slide Evidence run lands at `6 + n`, so the default route to the floor is an Evidence run of **4**. With only three cases worth showing, keep the Evidence run at 3 and split Pillars one capability per slide: `5 + 3 + 3 = 11`, inside the row's 10–14. Both routes are legal. What is not legal is reaching the floor by borrowing an optional block from a longer duration row, or by shipping 9 slides.

### 5.3 Anti-defaults for this mode

- No character-card layouts  
- No unreadable textured body text  
- No 12-line paragraphs  
- No animation longer than reading time  
- No body text below 18 pt — ever, in this mode. If it does not fit, cut it, never shrink it  

### 5.4 Claim ledger (build before designing any evidence slide)

A 竞聘 deck is an argument about a real person's real record. Build the ledger first, then design slides from it — not the other way round. One row per on-slide claim.

```json
{
  "claims": [
    {
      "id": "C1",
      "slide": 5,
      "text_on_slide": "the exact words that will appear",
      "type": "fact|number|outcome|attribution|opinion",
      "source": "S1",
      "verifiable_by": "how a skeptical listener could check this",
      "status": "supported|needs_input|opinion"
    }
  ]
}
```

**Ledger laws**

1. Every claim of type `fact | number | outcome | attribution` needs a `source` id from `source_packet`. No id → `status: needs_input` → the slide shows `[NEEDS INPUT]`.
2. `type: opinion` is allowed only when the slide frames it as a position ("我的判断是…"), never as a result.
3. **Attribution precision.** 我主导 / 我参与 / 团队完成，我负责 X are three different claims. Pick the one the source supports, never the strongest one the source permits.
4. **Numbers carry their basis** — period, denominator, and measurement method. "提升 30%" with no baseline is `needs_input`, not a number.
5. **The ask is a commitment, not a claim.** What you will do if elected needs feasibility, not a source; mark those rows `type: opinion`.
6. A claim that cannot survive one follow-up question in the room should be cut, not softened.

---

## 6. Anti-patterns (reject in review)

1. **Paragraph poster** — long body centered on busy art  
2. **Equal noise** — every shape animated  
3. **Template tourism** — switching styles every slide  
4. **IP costume** — wearing reference franchise aesthetics without topic fit  
5. **Card encyclopedia** — multi-entity sheets with stats (especially from fandom)  
6. **Transition candy** — fancy transitions masking weak hierarchy  
7. **Unread kinetic** — fragments moving while body text must be read  
8. **Accent rainbow** — more than two strong accent colors  
9. **Logo confetti** — recurring marks on every corner competing with title  
10. **Asset first, story never** — pretty plates with no beat role  

---

## 7. Final ship checklist (AI self-gate)

**How to read this checklist**

- **[CRITICAL]** — a release gate. If any Critical item is unchecked, unverified, or unverifiable, the deck is **not shippable** and you must not report the work as complete, done, finished, or ready. Name the failing item instead.
- **[RECOMMENDED]** — craft quality. Shipping without it is allowed; log a one-line reason.

Every Critical item is answered with **evidence** — a slide number, a count, a filename, a ledger row, a command you ran. "Yes" is not an answer. An item you could not check counts as **failed**, not as passed.

**What "the ship report" means (defined here, used everywhere else in this guide):** the ship report is this checklist answered item by item with its evidence, plus the raw output of the §7.1, §7.2 and §7.3 audits. It is not a separate deliverable — it is a section of the `handover.md` manifest (§7.3 row 6), written at ship time. Every other section that says "state it in the ship report" means: put it in that section. If there is no handover file, there is no ship report, and the Critical rows that cite it fail.

### Craft

- [ ] **[CRITICAL]** Mode chosen and held consistently; slide count inside the §0.3 authoritative budget
- [ ] **[CRITICAL]** Beat sheet exists; every slide has exactly one `job`
- [ ] **[CRITICAL]** Static twin readable with motion disabled — export or play with animation off and read every slide
- [ ] **[RECOMMENDED]** Visual system tokens defined and applied on runs, not left to the theme
- [ ] **[RECOMMENDED]** Timing inside the §2.2 bands; spike slides ≤ 2–3 per short deck
- [ ] **[RECOMMENDED]** Fragmentation intentional where kinetic; body text not shredded into unread dust
- [ ] **[CRITICAL]** Projection floor held: nothing that must be read is below 18 pt, and the contrast proxy passes on every text slide. The sole exception is `mode: B` **with** `viewing: close_range` in the Brief — Mode C never qualifies

### Ethics / scope

- [ ] **[CRITICAL]** §7.1 ban & IP-leakage audit run; all **ten** rows pass; output recorded in the ship report
- [ ] **[CRITICAL]** No reference character cards or casts, and no stat / tier / relationship structure or multi-entity attribute block from a reference; no lore, world rules or in-universe terms; no identifiable casts; no reference copy — **including paraphrased, translated or renamed versions** (§0.0 items 1–4, verified by §7.1 rows 1, 9, 10)
- [ ] **[CRITICAL]** No reference or unlicensed audio; every track has a stated right to use (§0.0 item 5)
- [ ] **[CRITICAL]** Asset rights OK for the stated delivery context (§0.0 item 6)
- [ ] **[CRITICAL]** Deck was started from a **new empty** deck — no reference file and no template of any kind was opened and edited over (§0.0 item 8, verified by §7.1 row 8)
- [ ] **[CRITICAL]** No font family, hex palette, chrome kit, or layout scaffold taken from **any reference source** — a companion brief, the reference `.pptx`, the source work's own branding, a fan wiki, a marketplace template, or a pack a user handed you — without an independent topic-derived reason (§10.0 translation law; §7.1 rows 3–5, which test every one of those routes, not only the brief)

### Truth / grounding

**Scope — when this group is `N/A`.** These rows apply to every **fact-bearing** deck: Mode C always, plus any Mode A / B / D deck that puts a number, date, name, title, award, outcome or quotation on a slide. A deck with **zero** fact-bearing claims — a pure mood film, an abstract concept piece — marks this group `N/A` **and states that in the ship report with the reason** ("no slide carries a fact"). That is itself a checkable claim: if §7.1 row 1's entity list or §7.1 row 9's per-slide origin lines turn up a single factual assertion, the group is not `N/A` and every row below applies. `N/A` is a **declared result**, not a skipped row — leaving these rows blank and silent still counts as failed.

- [ ] **[CRITICAL]** `source_packet` present; §7.2 audit run; all six rows pass
- [ ] **[CRITICAL]** Every fact-bearing on-slide claim has a §5.4 ledger row with a `source` id
- [ ] **[CRITICAL]** Brief `status` stated; `[NEEDS INPUT]` count stated; **zero** open tokens when `status: final`
- [ ] **[RECOMMENDED]** Each claim's `verifiable_by` would survive one follow-up question in the room

### Performance

- [ ] **[CRITICAL]** Fullscreen playtest once, uninterrupted, at the delivery aspect ratio (§Step 8)
- [ ] **[CRITICAL]** Reduced-motion path exists if delivery includes `web`
- [ ] **[RECOMMENDED]** File size / media count justified (video only where it pays off)
- [ ] **[CRITICAL]** §7.3 delivery QA run on the handover file; `handover.md` manifest exists

### 7.1 Ban & IP-leakage audit (run before any ship claim)

This is a procedure, not a promise. Run every row and paste the output into the ship report. "I didn't copy anything" is not a result.

**"Reference source" means all of these, equally.** A companion `_analysis-*` brief; a reference `.pptx` or its unpacked parts; the source work itself (episodes, chapters, art books, soundtracks, official key visuals); a fan wiki or fandom pack; a vendor or marketplace template; any asset pack or deck a user handed you "for reference". Every pass condition below applies to **all** of them. Closing only the companion-brief route is not closure — the brief is the route we happened to write down, not the only one a producer uses.

Each row's **Check** cell names the §0.0 tags it closes, so tag coverage is verifiable by grep rather than by assertion.

| # | Check | How to run it | Pass condition |
|---|---|---|---|
| 1 | Named entities & strings (`reference_copy`, `lore_or_world_rules`) | List every proper noun, title, tagline, section stamp, quoted line and distinctive phrase in the deck. For each, state its origin: topic research / user-supplied / invented. An entry that is a rename, translation or transliteration of a reference's name, title or in-universe term **keeps the reference as its origin**. This row reads *strings*; paraphrased **substance** — plot beats, world rules, mechanics — is row 9's job, and row 9 runs even when this row comes back clean. | Zero entries whose origin is a reference source, and zero entries that are a reference string wearing a new spelling |
| 2 | Asset provenance (`identifiable_key_art`, `reference_binaries`) | List every image, video, texture and audio file with its origin (generated / licensed / user-supplied) and its right to ship. | Zero files from a reference source or its preview renders; every file has a stated right |
| 3 | Font provenance (`reference_binaries`, `reference_identity_system`) | List every font family set on a run, plus its licence basis for this delivery **and the topic reason you chose it**. | Zero faces chosen because a reference source used them — a brief naming them, the reference deck's own runs, the work's branding, a wiki's typography page and a handed-over pack all fail identically (§10.0). Every face has a licence basis and a topic reason |
| 4 | Palette derivation (`reference_identity_system`) | State the topic mood sentence the palette came from, then each role token's hex. | No token justified as "same as \<reference\>". No hex taken from any reference source — a brief's measured palette, an eyedropper on a reference frame, a wiki's colour table, or a franchise style guide all fail identically |
| 5 | Scaffold derivation (`reference_identity_system`, `character_cards`) | For every repeated layout scaffold, name the information role it serves **in this deck** and the §5.2 block (Mode C) or beat role it belongs to. | No scaffold exists only because a reference source used it — whether a brief described it, you saw it in the reference deck, or you lifted it from the work's own materials (identity column, vitrine crumbs, migrating integer, corner seal, card frame) |
| 6 | Silhouette check (`identifiable_key_art`) | View the deck at 25% zoom with text hidden. | No frame reads as a recognisable character, costume, prop or key visual |
| 7 | Audio (`reference_audio`) | List every audio track and its licence. | Zero reference OST; every track licensed for this delivery context |
| 8 | File origin (`reference_file_as_base`) | State the file the deck was started from. Then inspect the shipped file: (a) list every slide **master** and **layout** and name who authored each; (b) open `ppt/media/` and compare its contents against your asset list from row 2; (c) check `docProps/app.xml` (`Template`, `TitlesOfParts`) and `docProps/core.xml` (`creator`, `lastModifiedBy`, `revision`) for another deck's identifiers; (d) check `ppt/theme/` for a theme name you did not author and `ppt/fonts/` for embedded font files you did not license. | Origin is a **new empty deck** — nothing else passes. Every master and layout is yours and named by you. `docProps/app.xml`'s `Template` names the blank default and no vendor, marketplace or personal template. Zero media files in `ppt/media/` that are absent from your row-2 asset list. No other deck's name, author, template or theme survives in the package |
| 9 | Paraphrase & borrowed-substance sweep (`lore_or_world_rules`, `character_cards`) | Read your beat sheet against whatever reference material you actually looked at, judging **content, not wording**. Write one line per slide: what claim, sequence or mechanic is on this slide, and where did it come from? Flag specifically — a reference's plot-beat order retold about your topic; an in-universe rule restated as a generic principle ("魔法要付代价" → "能力要付代价"); its chapter / episode structure reused as your section structure; its central conflict, twist or arc reused as your narrative spine; in-universe terminology swapped for a near-synonym; a character's arc reused as your "growth story". | Every slide's substance traces to topic research, a `source_packet` item, or invention. Zero slides whose substance is a reference's plot beat, world rule, mechanic, arc or episode structure **in any wording**. "I wrote it in my own words" is not a pass condition — the per-slide origin line is, and a missing line is a failed row |
| 10 | Entity-structure sweep (`character_cards`) | List every slide and every repeated block that puts **two or more entities side by side with per-entity attributes**: name + portrait + role line, stat / score / tier / rating rows, roster grids, tier lists, relationship or faction maps, "vs" pairings, character org charts. For each, state (a) which §5.2 block or beat role it serves and (b) where the entities **and the attribute set** came from. | Zero roster grids, stat rows, tier lists or relationship maps whose entities **or attribute set** come from a reference source — including ones re-skinned with your own topic's entities, because the attribute set is the card. Zero multi-entity attribute blocks with no §5.2 block or beat role to serve. This row is §6 anti-pattern 5 ("Card encyclopedia") made runnable |

**Fail handling:** one failed row = one Critical failure above. Fix the asset. Do not reword the claim, and do not downgrade the row to Recommended.

**Rows 9 and 10 are where rewording gets caught.** Rows 1–8 inspect artefacts — strings, files, hexes, faces, package parts — and a producer who paraphrases a reference's lore or re-skins its character card passes every one of them. Row 9 audits *substance* and row 10 audits *structure*, which is why neither can be skipped on the grounds that row 1 was clean.

**Row 8 is not optional and is not satisfied by assertion.** "I started from scratch" is not a result; the master/layout list, the `ppt/media/` inventory and the docProps values are. If the deck *was* built over an existing file, there is no repair — rebuild it in a new empty deck and re-run this row. Orphan media (files in the package that no slide references and your asset list does not contain) is the standard fingerprint of an edited-over deck.

**There is no "my own template" exception to row 8.** §0.0 item 8 says *start from a new empty deck*, and this row enforces exactly that. A template — vendor, marketplace, or one you saved off an earlier deck — arrives carrying masters, layouts, a theme and sometimes media from wherever *it* came from, and you cannot audit parts you did not add. If you want house furniture, create the empty deck first, add the masters, layouts and theme yourself, and account for each of them here and in rows 2–3.

### 7.2 Unsupported-claims audit (Mode C and any fact-bearing deck)

| # | Check | Pass condition |
|---|---|---|
| 1 | Ledger coverage | Every on-slide claim is a §5.4 row; every `fact / number / outcome / attribution` row carries a `source` id |
| 2 | Open tokens | The Brief's `status` and the `[NEEDS INPUT]` count are both stated in the ship report. If `status: final`, the count is **zero**. If `status: draft`, every open token is listed with what it needs and from whom — and the row still fails if `status` was flipped to `draft` to get past this check rather than agreed with the owner at Brief lock (§Step 0) |
| 3 | Hedge sweep | Search the deck copy for 显著 / 大幅 / 多次 / 广受好评 / 屡获 / 第一 / 最 / 唯一 / 行业领先. Each hit is either backed by a ledger row or cut |
| 4 | Attribution | Every 我主导 / 我负责 / 我创立-class phrase matches what its source actually supports |
| 5 | Number basis | Every number on a slide has period, denominator and method recorded in the ledger (not necessarily on the slide) |
| 6 | Invented specifics | No name, organisation, date, award or quantity appears anywhere in the deck that is absent from `source_packet` |

**Fail handling:** one failed row = one Critical failure in §7. The fix is to obtain the source or downgrade the claim — never to soften the wording until it stops sounding checkable.

### 7.3 Delivery QA (run on the actual file you are handing over)

| # | Check | Pass condition |
|---|---|---|
| 1 | Format & ratio | `.pptx` (OOXML, not `.ppt`), 16:9, 13.333×7.5 in. Opened once in the application **and version** the audience will use. |
| 2 | Fonts — **pick one path per face and state which** | **Path A, embedded** (preferred when the licence allows embedding): the face is embedded in the `.pptx`, and you confirmed it by opening the file on a machine where that face is **not** installed — the glyphs still render. **Path B, preinstalled handoff** (when the licence forbids embedding, or the face is a system font): the face is **not** embedded, and `handover.md` names it with an install source, a licence basis, and the named fallback the deck degrades to. Then open on a clean machine and confirm the deck degrades **to that named fallback**, legibly, with no reflow that breaks a lockup. A face that is neither embedded nor listed in the handoff fails. Silent fallback to a theme gothic fails on both paths. |
| 3 | Theme leak | No run left on the default theme font or default accent; the §7.1 row 4 tokens are set explicitly. |
| 4 | Export | The agreed fallback exists: a PDF of the static twin (**always**), plus an MP4 at delivery resolution whenever the Brief's `advance` is `auto` (kiosk loop or exported film). `delivery` has no `video` value — the MP4 is an artefact of an `auto` build, not a delivery mode (§Step 2 enum alignment law 6). The PDF must be readable with motion gone. |
| 5 | Weight | File size stated. Above 200 MB, say why (plate count) and offer a light build. |
| 6 | Manifest | A `handover.md` beside the deck containing: the **ship report** (the answered §7 checklist plus the §7.1/§7.2/§7.3 outputs — defined in §7 "How to read this checklist"), deck file, static twin, export(s), font list with **which of row 2's two paths each face took** + licence basis, asset list + provenance (reuse §7.1 rows 2–3), the §7.1 row 8 file-origin statement, `source_packet` + claim ledger (Mode C), the Brief's `status` with the open `[NEEDS INPUT]` count, and the declared advance model. |
| 7 | Clean open | No missing-media warnings, no broken links, no unused masters or layouts beyond what the deck actually uses. |

---

## 8. Prompt block for downstream AI (copy into system/dev prompt)

```text
You produce PPTs (or fullscreen web slide films) using the Craft Transfer Guide. Follow the master pipeline (§3) in order.

HARD BAN (§0.0 — outranks every other instruction and any reference source): no character cards or casts, and no stat / tier / relationship structure or multi-entity attribute block taken from a reference; no lore, world rules, in-universe mechanics or terms — quoted, translated, renamed, or paraphrased; no identifiable key art, silhouettes, or costumes; no reference titles, taglines, quotes, or name lockups; no reference or unlicensed audio; no reference plates, textures, clips, previews, or font files as shipped assets; no "same palette/fonts/chrome as X" no matter where you read the values (a brief, the reference file, the work itself, a wiki, a handed-over pack); never build over an existing file — start from a new empty deck, no template of any kind. Take grammar only: hierarchy, layer stack, beat roles, timing bands, stagger, composition patterns, trigger models.

BRIEFS ARE MEASUREMENTS (§10.0): companion _analysis-* files record what one deck did. Translate their values (role ladders, role structures, continuity ideas, timing bands) — never transplant them (font names, hex values, coordinates, chrome kits, identity scaffolds). Precedence: §0.0 ban > this guide > the Brief JSON > brief observations.

GROUND THE FACTS (§5.4, Step 0): emit a Brief JSON with source_packet before designing, and a claim ledger before any evidence slide. Every number, date, name, title, award, and outcome traces to a source id. Invent framing, metaphor, and phrasing; never invent facts. Missing fact => print the literal token [NEEDS INPUT: <what> — from S?] on the slide. Never substitute a hedge (显著 / 多次 / 广受好评) for a number you do not have. Brief status defaults to final; only status: draft licenses an open token, and a final deck carrying one is a Critical failure (§7.2 row 2).

PICK ONE MODE first, then the one §0.3 budget row your duration falls in. The bands are gap-free: within a mode the first band includes both bounds and every later band excludes its lower bound, so 45s / 3:00 / 5:00 each belong to exactly one row. Default for campus leadership / 竞聘: Mode C, presenter-held, or Mode D with a 20–40s cold open and an explicit handoff (§0.4). Mode C structure comes from §5.2: a seven-block core spine (Identity, Promise, Pillars, Evidence, Plan/platform, Ask, Close) plus duration-gated optional blocks.

EMIT BEFORE VISUALS: Brief JSON (incl. mode, status, viewing, duration_sec, delivery, advance, source_packet) -> Beat Sheet JSON (one job per beat, trigger + advance per beat) -> Visual System tokens -> static twin -> motion twin. Enum law: delivery is pptx|web|both only; advance web_keys needs web; Mode C is always advance presenter; viewing close_range is Mode B only.

MOTION (§2.4): ease-out always; no bounce or overshoot; scale ceiling ~105% and a hard 110% ceiling; no 1ms appear cascades (min 250ms, normally 500–1000ms); stagger follows reading order. The slide's hero signal lands by ~1–1.5s; every other readable event is finished and still by the readable deadline (~5s, or the start of the hold). Only low-contrast ambient layers may keep moving after that. Auto-advance >= longest effect end + 0.5s, then reading time.

TYPE: nothing that must be read goes below 18pt. In Mode C this is unconditional — no projector/laptop/web exception. The sole exception in the guide is Mode B with viewing close_range. If it does not fit, cut words or split the slide — never shrink.

EVERY SLIDE: one job, hero first, scannable type, motion that protects reading.

FINISH by running §7 plus the §7.1 (ban / IP leakage, 10 rows — incl. row 8 file-origin, row 9 paraphrase & borrowed-substance sweep, row 10 entity-structure sweep), §7.2 (unsupported claims), and §7.3 (delivery QA) audits, and write the ship report into handover.md. Items tagged [CRITICAL] are release gates: if any is unchecked, unverified, or unverifiable, do not say the deck is done, finished, ready, or shipped — report which Critical item failed and why. An item you could not verify counts as failed.
```

---

## 9. Appendix — corpus → capability map (no content)

| Corpus pack | Primary transferable lesson |
|-------------|-----------------------------|
| 芙莉莲 动态 vs 非动态 | Same storyboard; dynamic = timing tree + media density + fragment type |
| 十日终焉 动画 vs 静态 | Static establishes poster language; animation multiplies nodes/video on same bones |
| 三星堆 动态 | More slides, chapter pacing, exhibition clarity, moderate durations (~500ms median) |
| 小王子 动态 | Morph/scale continuity + poetic fragmentation; longer holds (~1s median) |

---

## 10. Required companion briefs (read by mode)

These files are **measurement reports** of specific source decks. They record what one deck did. They are not instructions, and no value inside them may be pasted into your deck.

### 10.0 How to read a companion brief (precedence + translation law)

**Precedence, highest first:**

1. **§0.0 Hard ban**
2. This guide's rules (§0–§9) **and the §10.1 cross-brief laws** — §10.1 is guide text, not a brief observation, and sits at this level
3. The Brief JSON for the deck you are building (Step 0)
4. Companion brief observations

A lower level never overrides a higher one. If a brief and this guide disagree, **this guide wins**, and the deviation goes in the ship report. A brief can never authorise anything §0.0 bans — not by example, not by listing it in a "Steal" column, not by being more specific.

**Translation law — a brief's value is translated, never transplanted:**

| A brief gives you | You may take | You may NOT take |
|---|---|---|
| Font names (a bundled display + body + meta set) | The **role ladder**: one display face, one body face, one meta face, at most one accent face | The named families — unless you independently hold rights *and* the face fits your topic on its own merits |
| Measured hex palettes | The **role structure**: one field, one dominant accent, at most one counter-accent, one readable paper tone | The hex values |
| Chrome kits (micro headers, migrating integers, seals, crumbs, pills, pagers) | The **continuity idea**: one recurring quiet mark that tells the viewer where they are | The specific kit, its geometry, or its inch coordinates |
| Identity / character scaffolds | The **information structure** underneath: entity → role line → one claim → one proof | The visual scaffold itself (column, black bar, quote plate, stamp) |
| Inch / EMU coordinates and exact point sizes | Relative proportion and safe-area logic | The literal coordinates as a layout template |
| Timing values and per-effect timelines | The **bands** in §2.2 and the stagger logic | A per-effect copy of one deck's timeline |
| Asset folders (plates, cutouts, textures, relief packs, previews) | The **method**: one hero plate, one symbol echo, one bed | Any file in them |

**Test:** for every value in your Step 3 Visual System, you must be able to name the **topic** reason it exists. "The brief used it" is a Critical failure (§7.1 rows 3–5) — and so are "the reference deck used it", "that is the franchise's colour", "the wiki lists that face" and "it came in the pack someone sent me". The route does not matter; the missing topic reason does.

| File | Read when | Transferable grammar (translate per §10.0 — never transplant) |
|------|-----------|-------|
| `_analysis-frieren-craft.md` | Mode A / D cold open | Still→wake; living plate + ambient loop; breath still; montage windows; readable deadline before loop ends |
| `_analysis-tendays-craft.md` | Mode A poster-film | Static twin→motion twin discipline; the two-motion-densities idea (soft world vs kinetic identity); type docking in the dark remainder; one continuous external bed instead of per-slide cues. **Do not take:** its font list, its black/blood/cyan palette, its chrome kit (micro header, migrating integer, bottom seal), or its six-slide identity scaffold — those are §0.0 items 6–7 unless independently derived (§10.0) |
| `_analysis-sanxingdui-craft.md` | Mode B exhibition | Route/rooms; entrance-only motion; Morph as **chapter door only**; texture as wall; visitor-paced advance |
| `_analysis-littleprince-craft.md` | Morph-heavy decks | `byObject` naming; two-beat (Morph then type); glyphs-as-objects; auto-advance **>** Morph dur |
| `_analysis-local-metrics.md` | Any | Observed ranges & fragmentation ratios — **not** budgets; §0.3 is the only budget table |
| `_analysis-timing-recipes.md` | Any | Duration buckets by pack class |
| `_pptx-craft-raw.json` | Debugging structure | Per-file slide/media/anim counts |

### 10.1 Cross-brief laws (non-negotiable)

1. **Compose the static twin first; the motion twin is a second pass on the same bones** (terminology per §1.1 — "static twin / motion twin" everywhere in this guide).  
2. **Readable deadline:** audience must finish the slide’s sentence while ambient loops may continue underneath.  
3. **Two densities max per film:** soft world/editorial vs kinetic identity/title — do not run 150ms letter storms on every slide.  
4. **Type lives in protected shade** (dark remainder / vignette), not on the brightest subject pixels.  
5. **Morph is special:** chapter doors (exhibition) or named-object relays (illustrated Morph) — never a default between all slides.  
6. **Identity / character-card scaffolds from fandom packs are NOT transferable** — not to 竞聘, not to any deck. What transfers is the information structure beneath them (entity → role → one claim → one proof); what does not transfer is the column, the black bar, the quote plate, the corner stamp, or the mirrored left/right rhythm. In Mode C, that energy becomes a Proof Trio or an evidence slide (§5.2).  
7. **No 1ms appear cascades** as “animation”; replace with 250–1000ms fades (unless intentionally micro-flicker décor at ≤6pt).  
8. **Auto-advance must clear the slide's longest motion plus a safety margin, then add reading time.** `advance_sec ≥ longest_effect_end + 0.5 s`, where `longest_effect_end` is the maximum over: the Morph/transition duration, every entrance's `delay + duration`, and any video-assist length. Example: a 2000 ms Morph plus a type cascade starting at +300 ms and running 1000 ms gives `max(2.0, 1.3) + 0.5 = 2.5 s` before anything else — then add reading time on top (Mode B/C: 4–7 s). The corpus contains a 500 ms auto-advance against a 2000 ms Morph; that clips the move. Do not copy it.
9. **Briefs are measurements; this guide is the instruction.** When a brief's value looks attractive — a face, a hex, a coordinate, a chrome element, a scaffold — translate it per §10.0. Transplanting it is a Critical failure (§7.1), no matter how well it reads.

---

## 11. Document control

- Version: 2026-10-11 (v1.2 — dual-verification fixes: normative §0.0 hard ban, all eight items audited by the ten §7.1 rows incl. the paraphrase and entity-structure sweeps; §10.0 brief precedence + translation law; §5.4 claim ledger and source_packet grounding; §7 labelled gate with the §7.1/§7.2/§7.3 audits and a defined ship report; §0.3 authoritative slide budget with gap-free duration bands; §0.4 Mode D handoff; §2.4 motion physics with hero and readable deadlines; living plate vs video assist; unconditional 18 pt projection floor; §5.2 optional-block duration gate and the 3:00–5:00 total-slide-floor rule; Brief `status: draft|final` gating open `[NEEDS INPUT]` tokens through §7 / §7.2 row 2 / §7.3 row 6)
- Type: AI production guide (actionable, self-sufficient — companion briefs are measurements only)
- Corpus: `Desktop/ppt模板` structural analysis only; **7 files / 4 storyboards / 3 static saves**, one genre cluster (see §2)
- Ban: see §0.0 (normative, eight items, audited by §7.1) — this line is a pointer, not a second copy
- Terminology: **static twin / motion twin** (never "dynamic twin" or "still twin")  
