# AI PPT Production Guide — Dual-Check Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every Critical and Important finding from the Stage-1 dual verification of `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md`, so the guide is a self-sufficient, enforceable production instruction whose rules cannot be overridden by the companion measurement briefs.

**Architecture:** All substantive fixes land in the **main guide**; the six `_analysis-*.md` companions stay measurement reports and receive only a one-line status header (Task 4). Three structural spines carry most of the fix: a normative **§0.0 Hard ban** that outranks everything, a **§10.0 precedence + translation law** that demotes briefs to observations, and a **§7 rebuilt as a labelled gate** (`[CRITICAL]` / `[RECOMMENDED]`) with three runnable audits (§7.1 ban/IP-leakage, §7.2 unsupported-claims, §7.3 delivery QA). Remaining tasks resolve contradictions (slide budget, video rule, ambient vs perform test, Mode B density) by making one section authoritative and pointing the others at it.

**Tech Stack:** Markdown only. No code, no build, no tests to run. Verification is `rg` (ripgrep) pattern checks plus a read-through, run from the repo root.

**Working branch:** none (working-tree only)

## Global Constraints

- **Edit only these files.** Primary: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md`. Secondary (Task 4 only, one line each): `_analysis-frieren-craft.md`, `_analysis-tendays-craft.md`, `_analysis-sanxingdui-craft.md`, `_analysis-littleprince-craft.md`, `_analysis-local-metrics.md`, `_analysis-timing-recipes.md` — all under `docs/superpowers/specs/`.
- **No commits.** This plan contains no Commit steps. Work stays in the working tree and is reviewed as a working-tree diff. No `git add`, `git commit`, `git push`, no PR.
- **No new product scope.** Only the dual findings listed per task. Do not invent new modes, new corpus claims, or new measured numbers.
- **Mode C / 竞聘 stays the priority path.** Where a rule needs a default, the default serves Mode C.
- **Companion briefs stay measurement reports.** Never move a fix into a brief to avoid editing the guide.
- **Preserve house style:** headings `## N.` / `### N.N`, two-space line breaks at the end of list items where the file already uses them, `—` em dashes, mixed CJK/Latin prose, tables with `|---|` separators.
- **Line numbers in this plan are as of the pre-edit file** (455 lines). Every task also gives a unique anchor string; after earlier tasks shift lines, **anchor on the string, not the number**.
- **Run tasks in order.** Tasks 1, 2, 3, 6, 7, 10, 11, 14 each edit §7 or the Step-0 JSON at distinct anchors; out-of-order execution risks clobbering. Step-0 JSON writers in order: Task 1 (`must_exclude`), Task 5 (`delivery` → `source_packet`), Task 10 (`viewing`, after `mode`), Task 11 (`advance` / `advance_notes`, after `delivery`). Task 15 deliberately overwrites the §8 and §11 text that Tasks 1 and 2 wrote — see its supersession note.

---

## File Map

| File | Change | Responsible for |
|---|---|---|
| `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` | Modify (Tasks 1–3, 5–16) | The whole fix: ban, grounding, precedence, contradictions, gates |
| `docs/superpowers/specs/_analysis-tendays-craft.md` | Modify, 1 line (Task 4) | Status header: measurement report, not instruction |
| `docs/superpowers/specs/_analysis-sanxingdui-craft.md` | Modify, 1 line (Task 4) | Same |
| `docs/superpowers/specs/_analysis-littleprince-craft.md` | Modify, 1 line (Task 4) | Same |
| `docs/superpowers/specs/_analysis-frieren-craft.md` | Modify, 1 line (Task 4) | Same |
| `docs/superpowers/specs/_analysis-local-metrics.md` | Modify, 1 line (Task 4) | Status header: observations, not targets |
| `docs/superpowers/specs/_analysis-timing-recipes.md` | Modify, 1 line (Task 4) | Same |

Nothing is created. Nothing is deleted.

---

## Finding → Task Map

| Finding | Task |
|---|---|
| **A-Crit-1** §10 briefs can override ban (precedence + translation + tendays row + §7 ethics item) | 3 (with 4) |
| **B-Crit-1** No factual grounding for 竞聘 (source_packet / ledger / `[NEEDS INPUT]` / audit) | 5, 6 |
| **B-Crit-2** Hard ban incomplete in Step 0 / §8 / §7; Critical labelling missing | 1, 2 |
| A-Imp video rule vs PV briefs | 8 |
| A-Imp slide budget conflicts | 7 |
| A-Imp Mode C body ≥18 pt | 10 |
| A-Imp advance/trigger model, Mode C presenter-held | 11 |
| A-Imp ease-out / no bounce / scale ≤105% | 9 |
| A-Imp expand §8 prompt seed | 15 |
| A-Imp testable ban procedure | 2 |
| A-Imp Mode B density = entrance-only | 7 |
| B-Imp metrics as observations, with exceptions | 12 |
| B-Imp ambient vs perform-test contradiction | 9 |
| B-Imp auto-advance ≥ Morph + margin | 9 |
| B-Imp Mode C schema expansion | 13 |
| B-Imp Mode D cold-open budget/handoff | 7 |
| B-Imp delivery QA | 14 |
| Minor: OST wording | 1 |
| Minor: static-twin naming | 15 |
| Minor: contrast proxy | 10 |
| Minor: corpus count — 7 files / 4 storyboards / 3 static saves | 4, 12, 15 |
| Minor: "ship report" defined once | 2 (consumed by 8, 14) |
| Minor: hero ≤ 1.5 s vs readable deadline (~5 s) | 9 (with 8) |
| Minor: `advance` / `delivery` enum alignment | 11, 14 |
| Minor: ban → audit coverage matrix for all 8 items | 2, 16 |

### Round-2 dual findings (added after the second dual check)

| Finding | Task |
|---|---|
| **R2-B-Crit-1** §7.1 incomplete in substance: no paraphrase detection (item 2), no entity-structure check (item 1), identity checks covered only companion briefs (item 7), matrix overstated items 1 / 2 / 7 | 2 (§7.1 intro + rows 1, 3–5, new rows 9–10, matrix), 1, 3, 16 |
| **R2-B-Crit-2** §7.1 row 8 allowed "your own template", contradicting §0.0 "start from an empty deck" | 1, 2 |
| R2-Imp `reference_file_as_base` tag-count expectation unsatisfiable — row 8 never wrote the literal tag (both legs reported this) | 2 (tags in every §7.1 Check cell), 1, 16 |
| R2-Imp §0.3 duration bands left 45 s / 3:00 / 5:00 belonging to no row | 7, 16 |
| R2-Minor hero-deadline grep is case-sensitive; Step 8 bullet has no §2.4 pointer; §0.1 "1 second" vs §2.4 "~1–1.5 s" | 9 |
| R2-Minor §2.1 `.mp4` range is 0–23 and the per-file counts were wrong | 8 |
| R2-Minor §5.1 says a 3-slide cold open while §0.4 says 3–5 | 7 |
| R2-Minor Mode C spine arithmetic lands at 9, below the §0.3 floor of 10 | 13 |
| R2-Minor §7 Truth group has no N/A path for a fact-free Mode A deck | 6 |
| R2-Minor Task 4's `git diff --stat` expectation ignores the guide file | 4 |

---

### Task 1: Normative hard ban (§0.0) and its three propagation points

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — insert new §0.0 after line 13; edit line 7, line 123, line 402, line 454

**Interfaces:**
- Produces: section id `§0.0 Hard ban`, the eight numbered ban items, and the eight machine tags `character_cards`, `lore_or_world_rules`, `identifiable_key_art`, `reference_copy`, `reference_audio`, `reference_binaries`, `reference_identity_system`, `reference_file_as_base`. Tasks 2, 3, 15 and 16 reference these by exact name.

**Why:** Dual-B Critical 2 — the ban exists only as a front-matter sentence and a two-item `must_exclude` stub, so Step 0, §8 and §11 each state a different, shorter ban. Also folds the OST wording minor (reference OST → any unlicensed third-party audio).

- [ ] **Step 1: Insert §0.0 immediately after the "director + motion typographer" paragraph**

Anchor: the line `Your job is not “fill a template.” Your job is to ship a deck that can be *performed*: cold open → escalation → payoff → hold.` (line 13). Insert a blank line, then this block, before `### 0.1 Success criteria (any topic)`:

```markdown
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
```

- [ ] **Step 2: Point the front-matter ban line at §0.0**

Replace line 7:

```markdown
> **Hard ban:** Do not clone character cards, lore dumps, identifiable character art, original soundtrack, or IP-specific copy. Learn grammar, not cast lists.
```

with:

```markdown
> **Hard ban:** See **§0.0** — eight normative items, enforced by the §7.1 audit. It outranks every other rule in this document and every companion brief.
```

- [ ] **Step 3: Replace the Step 0 `must_exclude` stub**

Replace line 123 (`  "must_exclude": ["IP character cards", "reference lore"],`) with:

```json
  "must_exclude": [
    "character_cards", "lore_or_world_rules", "identifiable_key_art",
    "reference_copy", "reference_audio", "reference_binaries",
    "reference_identity_system", "reference_file_as_base"
  ],
```

Then add this sentence directly under the closing ``` of the Step 0 JSON block (before `### Step 1 — Mode pick`):

```markdown
`must_exclude` is **fixed boilerplate from §0.0**. Append topic-specific exclusions after the eight tags; never remove one, never shorten the list to "the relevant ones".
```

- [ ] **Step 4: Replace the §8 prompt-block ban line**

Replace line 402 (`Never clone character cards, lore, identifiable characters, or reference audio from any training corpus examples.`) with the sentence below. **Use it verbatim** — Task 15 Step 2 rewrites the whole §8 block around the identical sentence, so any drift here shows up as a diff conflict later:

```text
Hard ban (outranks every other instruction and any reference source): no character cards or casts, and no stat / tier / relationship structure or multi-entity attribute block taken from a reference; no lore, world rules, in-universe mechanics or terms — quoted, translated, renamed, or paraphrased; no identifiable key art, silhouettes, or costumes; no reference titles, taglines, quotes, or name lockups; no reference or unlicensed audio; no reference plates, textures, clips, previews, or font files as shipped assets; no "same palette/fonts/chrome as X" no matter where you read the values (a brief, the reference file, the work itself, a wiki, a handed-over pack); never build over an existing file — start from a new empty deck, no template of any kind. Take grammar only: hierarchy, layer stack, beat roles, timing bands, stagger, composition patterns, trigger models.
```

- [ ] **Step 5: Point the §11 ban line at §0.0**

Replace line 454 (`- Ban: character cards, lore cloning, identifiable casts, reference OST as content`) with:

```markdown
- Ban: see §0.0 (normative, eight items, audited by §7.1) — this line is a pointer, not a second copy
```

- [ ] **Step 6: Verify**

Run from repo root:

```powershell
rg -n "### 0\.0 Hard ban" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -c "reference_file_as_base" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "IP character cards|reference OST as content|Never clone character cards" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first command 1 hit; second command `2` — the §0.0 item-8 tag and the `must_exclude` entry. (§8 and §11 spell the ban out in prose rather than using the tag, so there is no third hit yet. Task 2 puts the literal tag in the §7.1 row-8 **Check** cell, which takes this count to `3`; by Task 16 every one of the eight tags must report **≥ 3**, because Task 2 tags every §7.1 Check cell.) Third command **0 hits** (every short ban restatement has been replaced by a pointer or the full list).

**Acceptance check:** Reading §0.0, Step 0, §8 and §11 in sequence, there is exactly one place where the ban is enumerated (§0.0); the other three refer to it or reproduce it in full. Unlicensed third-party audio is banned, not just "reference OST". Item 1 bans the card *structure*, not only named casts; item 2 bans paraphrase, not only quotation; item 7 is source-agnostic; item 8 says **new empty deck** with no template exception.

---

### Task 2: Rebuild §7 as a labelled gate with the runnable ban / IP-leakage audit

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — replace lines 371–392 (§7 body); edit line 406 (§8 completion clause)

**Interfaces:**
- Consumes: §0.0 and its eight tags (Task 1)
- Produces: the labels `**[CRITICAL]**` and `**[RECOMMENDED]**`, the subsection ids `§7.1`, and the four §7 groups `### Craft`, `### Ethics / scope`, `### Truth / grounding`, `### Performance`. Tasks 3, 6, 10 and 14 append rows into these exact groups.

**Why:** Dual-B Critical 2 — §8 says "do not claim completion if any Critical item fails" but nothing in §7 is labelled Critical, so the gate is unenforceable. Dual-A Important — the ban needs a *procedure*, not an assertion.

- [ ] **Step 1: Replace the whole §7 body (lines 371–392) with the labelled gate**

Replace from `## 7. Final ship checklist (AI must answer YES)` through the last Performance bullet (`- [ ] File size / media count justified (video only where it pays off)`) with:

```markdown
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

### Ethics / scope

- [ ] **[CRITICAL]** §7.1 ban & IP-leakage audit run; all **ten** rows pass; output recorded in the ship report
- [ ] **[CRITICAL]** No reference character cards or casts, and no stat / tier / relationship structure or multi-entity attribute block from a reference; no lore, world rules or in-universe terms; no identifiable casts; no reference copy — **including paraphrased, translated or renamed versions** (§0.0 items 1–4, verified by §7.1 rows 1, 9, 10)
- [ ] **[CRITICAL]** No reference or unlicensed audio; every track has a stated right to use (§0.0 item 5)
- [ ] **[CRITICAL]** Asset rights OK for the stated delivery context (§0.0 item 6)
- [ ] **[CRITICAL]** Deck was started from a **new empty** deck — no reference file and no template of any kind was opened and edited over (§0.0 item 8, verified by §7.1 row 8)

### Truth / grounding

*(filled in by Task 6 — leave this heading in place)*

### Performance

- [ ] **[CRITICAL]** Fullscreen playtest once, uninterrupted, at the delivery aspect ratio (§Step 8)
- [ ] **[CRITICAL]** Reduced-motion path exists if delivery includes `web`
- [ ] **[RECOMMENDED]** File size / media count justified (video only where it pays off)

### 7.1 Ban & IP-leakage audit (run before any ship claim)

This is a procedure, not a promise. Run every row and paste the output into the ship report. "I didn't copy anything" is not a result.

**"Reference source" means all of these, equally.** A companion `_analysis-*` brief; a reference `.pptx` or its unpacked parts; the source work itself (episodes, chapters, art books, soundtracks, official key visuals); a fan wiki or fandom pack; a vendor or marketplace template; any asset pack or deck a user handed you "for reference". Every pass condition below applies to **all** of them. Closing only the companion-brief route is not closure — the brief is the route we happened to write down, not the only one a producer uses.

Each row's **Check** cell names the §0.0 tags it closes, so the coverage matrix below is verifiable by grep rather than by assertion.

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
```

- [ ] **Step 2: Make the §8 completion clause point at the labels**

Replace line 406 (`End with the ship checklist; do not claim completion if any Critical item fails.`) with:

```text
Finish by running the §7 checklist and the §7.1/§7.2/§7.3 audits. Items tagged [CRITICAL] are release gates: if any is unchecked, unverified, or unverifiable, do not say the deck is done, finished, ready, or shipped — report which Critical item failed and why. An item you could not verify counts as failed.
```

- [ ] **Step 3: Verify**

```powershell
rg -c "\[CRITICAL\]" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "### 7\.1 Ban & IP-leakage audit" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "### Truth / grounding" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "^\| 8 \| File origin" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "^\| 9 \| Paraphrase|^\| 10 \| Entity-structure" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "your own template" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -c "reference_file_as_base" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first ≥ 12; second 1 hit; third 1 hit; fourth 1 hit; fifth **2 hits** (rows 9 and 10 both present, and row 10 is the last row); sixth **0 hits** — the template exception is gone; seventh `3` — §0.0 item 8, `must_exclude`, and the row-8 **Check** cell. That third occurrence is what makes Task 16 Step 3's tag count satisfiable; before this task it was 2 and the expectation was unreachable.

**Acceptance check:** Every §7 bullet carries exactly one of the two labels. §7.1 has **ten** rows, each with a *how to run it* column and a pass condition that names an artefact, not a feeling. Every row's Check cell carries the §0.0 tags it closes. Rows 9 and 10 exist and audit *substance* and *structure* rather than strings, so a paraphrased world rule or a re-skinned character card fails the audit instead of sliding through rows 1–8. Rows 3–5 fail a brief-sourced face/hex/scaffold **and** a reference-deck-sourced, wiki-sourced or handed-pack-sourced one. Row 8 accepts only a new empty deck. §8 no longer references an undefined "Critical item".

The §7 Ethics group after this task covers §0.0 items 1–6 and 8; **item 7 (`reference_identity_system`) gets its Ethics row in Task 3 Step 5.** Do not add a second one here.

**Ban → audit coverage produced by this task** (Task 16 Step 8 re-checks it). Every §0.0 item lands in at least one row that produces an artefact. The **Honest limit** column exists because the previous version of this matrix claimed coverage the rows could not deliver — do not widen a cell without widening the row it names:

| §0.0 item | Tag | §7.1 row(s), primary in bold | Honest limit |
|---|---|---|---|
| 1 | `character_cards` | **10** (entity-structure sweep), 9 (card substance, paraphrased), 5 (card-derived scaffold) | Row 1 catches only a *named* cast; row 6 catches a card's **art**, not its structure. Structure closure is row 10 and nothing else |
| 2 | `lore_or_world_rules` | **9** (paraphrase sweep), 1 (named / quoted terms) | Row 1 alone passes a fully reworded plot beat or world rule — that is exactly why row 9 exists. Do not cite row 1 as coverage for item 2 on its own |
| 3 | `identifiable_key_art` | **2** (asset provenance), **6** (silhouette at 25%) | — |
| 4 | `reference_copy` | **1** | — |
| 5 | `reference_audio` | **7** | — |
| 6 | `reference_binaries` | **2**, 3 (font files), 8 (orphan media) | — |
| 7 | `reference_identity_system` | **3**, **4**, **5** | Covered for *every* reference source only because rows 3–5 name the reference deck, the work's own branding, a wiki and a handed-over pack alongside the briefs. If any of those three rows is narrowed back to "a companion brief", item 7 is open again |
| 8 | `reference_file_as_base` | **8** | Row 8 is assertion-proof only via its four package inspections; the stated origin alone proves nothing |

---

### Task 3: §10 precedence + translation law; demote the companion briefs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — insert §10.0 after line 424; edit the §10 table header (line 426) and the tendays row (line 429); rewrite §10.1 law 6 (line 443) and add law 9; append one row to §7 `### Ethics / scope`

**Interfaces:**
- Consumes: §0.0 (Task 1), §7 group headings and label syntax (Task 2)
- Produces: section id `§10.0`, the four-level precedence list, the translation table. Task 4 and Task 16 cite `§10.0` by name.

**Why:** Dual-A Critical 1 — §10 currently says only "§0 bans still win if anything conflicts", while the briefs hand the producer hard-coded font quartets, hex palettes, chrome kits in inch coordinates, and a six-slide identity scaffold. A producer following the §10 "Steal" column lands directly in a ban violation.

- [ ] **Step 1: Insert §10.0 between the §10 intro sentence and the brief table**

Anchor: line 424, `These are **capability briefs**, not content to copy. §0 bans still win if anything conflicts.` Replace that whole line with:

```markdown
These files are **measurement reports** of specific source decks. They record what one deck did. They are not instructions, and no value inside them may be pasted into your deck.

### 10.0 How to read a companion brief (precedence + translation law)

**Precedence, highest first:**

1. **§0.0 Hard ban**
2. This guide's rules (§0–§9)
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
```

- [ ] **Step 2: Retitle the table's third column**

Replace the header rows at lines 426–427:

```markdown
| File | Read when | Steal |
|------|-----------|-------|
```

with:

```markdown
| File | Read when | Transferable grammar (translate per §10.0 — never transplant) |
|------|-----------|-------|
```

- [ ] **Step 3: Reword the tendays row (line 429)**

Replace:

```markdown
| `_analysis-tendays-craft.md` | Mode A poster-film | Still twin→motion twin; double-exposure title; **two motion densities**; type docks on dark remainder; external score optional |
```

with:

```markdown
| `_analysis-tendays-craft.md` | Mode A poster-film | Static twin→motion twin discipline; the two-motion-densities idea (soft world vs kinetic identity); type docking in the dark remainder; one continuous external bed instead of per-slide cues. **Do not take:** its font list, its black/blood/cyan palette, its chrome kit (micro header, migrating integer, bottom seal), or its six-slide identity scaffold — those are §0.0 items 6–7 unless independently derived (§10.0) |
```

- [ ] **Step 4: Rewrite §10.1 law 6 and append law 9**

Replace line 443:

```markdown
6. **Identity / character-card scaffolds from fandom packs are NOT transferable to 竞聘** — map that energy to Proof Trio / evidence slides instead.
```

with:

```markdown
6. **Identity / character-card scaffolds from fandom packs are NOT transferable** — not to 竞聘, not to any deck. What transfers is the information structure beneath them (entity → role → one claim → one proof); what does not transfer is the column, the black bar, the quote plate, the corner stamp, or the mirrored left/right rhythm. In Mode C, that energy becomes a Proof Trio or an evidence slide (§5.2).
```

Then append after law 8:

```markdown
9. **Briefs are measurements; this guide is the instruction.** When a brief's value looks attractive — a face, a hex, a coordinate, a chrome element, a scaffold — translate it per §10.0. Transplanting it is a Critical failure (§7.1), no matter how well it reads.
```

- [ ] **Step 5: Append the brief-derivation ethics row to §7 `### Ethics / scope`**

Add as the last bullet of that group (created in Task 2):

```markdown
- [ ] **[CRITICAL]** No font family, hex palette, chrome kit, or layout scaffold taken from **any reference source** — a companion brief, the reference `.pptx`, the source work's own branding, a fan wiki, a marketplace template, or a pack a user handed you — without an independent topic-derived reason (§10.0 translation law; §7.1 rows 3–5, which test every one of those routes, not only the brief)
```

- [ ] **Step 6: Verify**

```powershell
rg -n "### 10\.0 How to read a companion brief" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "never transplant" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "NOT transferable to" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -c "any reference source" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second ≥ 1 hit; third **0 hits** (law 6 is no longer scoped to 竞聘 only); fourth ≥ 3 (the §7.1 intro definition, §7.1 pass conditions, this Ethics row).

**Acceptance check:** A producer reading §10 top-to-bottom meets the precedence list before the brief table, and the tendays row names what must *not* be taken. §7 now has a Critical row that fails a deck built on reference-derived fonts/palette/scaffold **however the producer got hold of them** — this row and §7.1 rows 3–5 must stay source-agnostic together, since narrowing either one re-opens §0.0 item 7.

---

### Task 4: One-line status header on all six companion briefs

**Files:**
- Modify: `docs/superpowers/specs/_analysis-frieren-craft.md` — insert after line 1
- Modify: `docs/superpowers/specs/_analysis-tendays-craft.md` — insert after line 1
- Modify: `docs/superpowers/specs/_analysis-sanxingdui-craft.md` — insert after line 1
- Modify: `docs/superpowers/specs/_analysis-littleprince-craft.md` — insert after line 1
- Modify: `docs/superpowers/specs/_analysis-local-metrics.md` — insert after line 1
- Modify: `docs/superpowers/specs/_analysis-timing-recipes.md` — insert after line 1

**Interfaces:**
- Consumes: `§0.0` and `§10.0` section ids (Tasks 1, 3)
- Produces: nothing other tasks depend on; Task 16 greps for the header string.

**Why:** Dual-A Critical 1 support. The briefs keep their measured content (they are useful *as* measurements); the header stops a producer who opens a brief directly from reading it as a spec. This is the only companion edit in the plan — do not restructure brief bodies.

- [ ] **Step 1: Add the craft-brief header to the four craft files**

In each of `_analysis-frieren-craft.md`, `_analysis-tendays-craft.md`, `_analysis-sanxingdui-craft.md`, `_analysis-littleprince-craft.md`, insert directly after the `# ...` H1 line (keeping one blank line on each side):

```markdown
> **Status: measurement report, not an instruction.** This file records what one source deck measured. Fonts, hex values, coordinates, chrome and scaffolds here must be **translated, never transplanted** — see §10.0 and the Hard ban §0.0 in `2026-10-11-ai-ppt-production-guide.md`. That guide wins every conflict.
```

- [ ] **Step 2: Add the metrics header to the two data files**

In `_analysis-local-metrics.md` and `_analysis-timing-recipes.md`, insert directly after the H1 line:

```markdown
> **Status: measurement report, not an instruction.** These are observations of **seven saved files covering four storyboards** from one genre cluster — not targets, not a scoring rubric. Do not tune a deck toward a number here. See §2 and §10.0 in `2026-10-11-ai-ppt-production-guide.md`, which wins every conflict.
```

- [ ] **Step 3: Verify**

```powershell
rg -c -g "_analysis-*.md" "Status: measurement report, not an instruction" docs/superpowers/specs
```

Expected: all six companion files listed, each with count `1`.

> **Windows note:** `rg` takes a *path* as its last argument, not a glob. `rg "pattern" docs/superpowers/specs/_analysis-*.md` works in PowerShell only by accident of expansion and fails outright when no file matches or when the shell is `cmd`. Use `-g "_analysis-*.md" <dir>` as above, or list the six paths explicitly. The same correction applies to Task 16 Step 1.

**Acceptance check:** Each brief's header is line 3 (after H1 + blank line); no other line in any brief changed. `git diff --stat` shows these **six companion files at +2 lines each** (header + blank). The guide file also appears in the same diff — Tasks 1–3 modified it — so the total file count at this point is **7, not 6**; what this task owns is the six briefs. Check the six brief rows, not the row count.

---

### Task 5: Step 0 `source_packet` and the `[NEEDS INPUT]` no-invention rule

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — edit the Step 0 JSON (around line 124) and add prose after it

**Interfaces:**
- Consumes: the Step 0 JSON block, already edited by Task 1 Step 3
- Produces: the Brief field `source_packet` with keys `provided` (bool), `items[]` (each `{id, what, from, date}`), `gaps[]`; and the literal on-slide token format `[NEEDS INPUT: <what> — from S?]`. Task 6 consumes `source_packet.items[].id` as the ledger's `source` value.

**Why:** Dual-B Critical 1 — the guide tells an AI to produce a 竞聘 deck about a real person with zero mechanism for where the facts come from. Without this, a well-followed guide produces a beautifully designed fabrication.

- [ ] **Step 1: Add `source_packet` to the Brief JSON**

Anchor: line 124, `  "delivery": "pptx|web|both"`. Replace it with:

```json
  "delivery": "pptx|web|both",
  "source_packet": {
    "provided": true,
    "items": [
      { "id": "S1", "what": "what this source is, e.g. 2025 社团活动台账", "from": "user|public_record|interview|own_notes", "date": "YYYY-MM" }
    ],
    "gaps": ["what you asked the owner for and did not receive"]
  }
```

(Note the comma now needed after `"pptx|web|both"`.)

- [ ] **Step 2: Add the no-invention rule after the Step 0 JSON block**

Insert after the `must_exclude` boilerplate sentence added in Task 1 Step 3, before `### Step 1 — Mode pick`. (The outer fence below is four backticks because the content itself contains a fenced block — insert the inner three-backtick fence verbatim into the guide.)

````markdown
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
- A deck may ship with open tokens **only** when the Brief marks it as a draft for the owner to complete. A deck presented as final with open tokens is a Critical failure (§7.2).
````

- [ ] **Step 3: Verify**

```powershell
rg -n "source_packet" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "NEEDS INPUT" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: `source_packet` ≥ 3 hits (JSON key, prose, `provided: false` note); `NEEDS INPUT` ≥ 3 hits.

**Acceptance check:** The Step 0 JSON is still valid JSON-with-placeholders (trailing commas correct). The no-invention rule names the hedge failure mode explicitly, not just the fabrication failure mode.

---

### Task 6: Claim ledger (§5.4) and the unsupported-claims audit (§7.2)

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — add §5.4 after §5.3 (after line 353); add §7.2 after §7.1; append two rows to §7 `### Truth / grounding`

**Interfaces:**
- Consumes: `source_packet.items[].id` (Task 5), §7 group headings (Task 2)
- Produces: the ledger object `claims[]` with keys `id`, `slide`, `text_on_slide`, `type` (`fact|number|outcome|attribution|opinion`), `source`, `verifiable_by`, `status` (`supported|needs_input|opinion`). Task 14's manifest row names "claim ledger"; Task 16 greps `§5.4`.

**Why:** Dual-B Critical 1 (second half). `source_packet` says where facts come from; the ledger is what forces each *on-slide sentence* to point at one, and §7.2 is what makes it checkable.

- [ ] **Step 1: Add §5.4 after the §5.3 anti-defaults list**

Insert after the last §5.3 bullet (`- No animation longer than reading time`), before the `---` that closes §5. (Outer fence is four backticks; insert the inner three-backtick JSON fence verbatim into the guide.)

````markdown
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
````

- [ ] **Step 2: Add §7.2 after the whole of §7.1**

Anchor: the **last** paragraph of §7.1 — the one beginning `**There is no "my own template" exception to row 8.**`. §7.1 ends with four paragraphs after its table (fail handling, the rows 9–10 note, the row-8 assertion note, the template note); §7.2 goes after all four, not between them.

```markdown
### 7.2 Unsupported-claims audit (Mode C and any fact-bearing deck)

| # | Check | Pass condition |
|---|---|---|
| 1 | Ledger coverage | Every on-slide claim is a §5.4 row; every `fact / number / outcome / attribution` row carries a `source` id |
| 2 | Open tokens | `[NEEDS INPUT]` count stated in the ship report; **zero** if the deck is presented as final |
| 3 | Hedge sweep | Search the deck copy for 显著 / 大幅 / 多次 / 广受好评 / 屡获 / 第一 / 最 / 唯一 / 行业领先. Each hit is either backed by a ledger row or cut |
| 4 | Attribution | Every 我主导 / 我负责 / 我创立-class phrase matches what its source actually supports |
| 5 | Number basis | Every number on a slide has period, denominator and method recorded in the ledger (not necessarily on the slide) |
| 6 | Invented specifics | No name, organisation, date, award or quantity appears anywhere in the deck that is absent from `source_packet` |

**Fail handling:** one failed row = one Critical failure in §7. The fix is to obtain the source or downgrade the claim — never to soften the wording until it stops sounding checkable.
```

- [ ] **Step 3: Fill the §7 `### Truth / grounding` group**

Replace the placeholder line `*(filled in by Task 6 — leave this heading in place)*` with:

```markdown
**Scope — when this group is `N/A`.** These rows apply to every **fact-bearing** deck: Mode C always, plus any Mode A / B / D deck that puts a number, date, name, title, award, outcome or quotation on a slide. A deck with **zero** fact-bearing claims — a pure mood film, an abstract concept piece — marks this group `N/A` **and states that in the ship report with the reason** ("no slide carries a fact"). That is itself a checkable claim: if §7.1 row 1's entity list or §7.1 row 9's per-slide origin lines turn up a single factual assertion, the group is not `N/A` and every row below applies. `N/A` is a **declared result**, not a skipped row — leaving these rows blank and silent still counts as failed.

- [ ] **[CRITICAL]** `source_packet` present; §7.2 audit run; all six rows pass
- [ ] **[CRITICAL]** Every fact-bearing on-slide claim has a §5.4 ledger row with a `source` id
- [ ] **[CRITICAL]** `[NEEDS INPUT]` count stated; zero open tokens if the deck is presented as final
- [ ] **[RECOMMENDED]** Each claim's `verifiable_by` would survive one follow-up question in the room
```

- [ ] **Step 4: Verify**

```powershell
rg -n "### 5\.4 Claim ledger" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "### 7\.2 Unsupported-claims audit" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "filled in by Task 6" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "when this group is" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second 1 hit; third **0 hits**; fourth 1 hit.

**Acceptance check:** §5.4's `source` values and Task 5's `source_packet.items[].id` use the same `S1`-style ids; §7.2 row 1 ties them together; the §7 Truth group no longer contains the placeholder. A fact-free Mode A mood film has a stated way through this group — a declared `N/A` with a reason, cross-checked against §7.1 rows 1 and 9 — so the Critical rows do not force a fabricated ledger onto a deck that carries no facts, and do not let a fact-bearing deck skip them either.

---

### Task 7: One authoritative slide budget (§0.3), Mode B density fix, Mode D handoff (§0.4)

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — edit the §0.2 table (lines 26–31); insert §0.3 and §0.4 after the Mode C default line (line 33), before line 37; replace the Step 1 budget list (lines 130–135); edit the §5.1 cold-open bullet (line 336)

**Interfaces:**
- Consumes: nothing
- Produces: section ids `§0.3` (budget) and `§0.4` (Mode D handoff). §7 Craft row 1 (Task 2) already cites `§0.3`; Task 13 cites `§0.3`.

**Why:** Dual-A Important — three sections give three different slide budgets (§0.2 Mode A 10–14 vs Step 1 "45–60 s PV: 8–12"; §0.2 Mode C 12–18 vs Step 1 "3–5 min: 10–14"). Dual-A Important — §0.2 labels Mode B "Medium-high" motion while the exhibition brief measures entrance-only. Dual-B Important — Mode D has no cold-open budget and no handoff contract.

- [ ] **Step 1: Point the §0.2 table at §0.3 and fix Mode B density**

Replace the §0.2 table (lines 26–31) with:

```markdown
| Mode | When | Slide count | Motion density | Body text |
|------|------|-------------|----------------|-----------|
| **A. PV / 短片感** | Showreel, opener, emotional pitch | see §0.3 | Very high | Minimal; fragments |
| **B. Exhibition / 叙事展陈** | Museum, case study, chapter story | see §0.3 | **Low — entrance-only** | Short blocks OK |
| **C. Briefing / 竞聘汇报** | Interview, work report | see §0.3 | Low-medium | Scannable bullets |
| **D. Hybrid** | Need both wow + info | see §0.3 | Gate motion after the open (§0.4) | Explicit switch |

**Mode B density, stated plainly:** exhibition motion is **entrance-only, with-previous, then hold** — one chord per slide, no exit, no emphasis loop, no idle float, visitor-paced advance. The measured exhibition deck sits in a 500–1000 ms band with 6 sub-200 ms spans in the entire file. "Medium-high" density belongs to Mode A; a gallery that performs is a gallery nobody reads.
```

- [ ] **Step 2: Insert §0.3 and §0.4 before `## 1. What to steal`**

After the Mode C default line (line 33, `Default for campus leadership / 部长竞聘: **Mode C**, optionally **D** with a 20–40s cold open.`) and before the `---` preceding §1, insert:

```markdown
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
```

- [ ] **Step 3: Replace the Step 1 duplicate budget list**

Replace lines 130–135:

```markdown
Map duration → slide budget:

- 45–60s PV: 8–12 slides  
- 3–5 min briefing: 10–14 slides  
- 8–10 min briefing: 12–18 slides  
- Exhibition walkthrough: 18–28 slides  
```

with:

```markdown
Map duration → mode → slide budget using the **§0.3 authoritative table**. Do not restate budgets here; a second copy is how the three numbers in this guide drifted apart in the first place.

Record the chosen row in the Brief as `mode` + `duration_sec`. If the content will not fit the row's ceiling, apply the §0.3 overflow law — cut a claim, do not add slides.
```

- [ ] **Step 4: Align the §5.1 cold-open line with §0.4**

§5.1 says a **3-slide** cold open while §0.2 and §0.4 both say **3–5**. One of the two numbers has to go, and §0.4 is the section that owns the contract.

Anchor on the string, not the number — Step 2 of this task inserted §0.3 and §0.4 above it, so the line is no longer at 336. Replace:

```markdown
- Keep PV tricks in optional 3-slide cold open if Mode D  
```

with:

```markdown
- Keep PV tricks inside the optional Mode D cold open — budget, handoff and motion gate in **§0.4** (20–40 s, 3–5 slides). No second number here  
```

- [ ] **Step 5: Verify**

```powershell
rg -n "### 0\.3 Slide budget|### 0\.4 Mode D handoff" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Map duration" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Medium-high" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Band boundaries" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "3-slide cold open" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 2 hits; second 1 hit (the new pointer sentence, with no bullet list under it); third 1 hit (only inside the explanatory "Medium-high density belongs to Mode A" sentence, not in the table); fourth 1 hit; fifth **0 hits**.

**Acceptance check:** Exactly one table in the file contains slide-count ranges. Mode B's density cell reads entrance-only. §0.4 gives a number for the cold open (20–40 s / 3–5 slides) and a named failure test, and §5.1 now points at it instead of naming a rival 3-slide figure. Walk the boundaries by hand: 45 s, 60 s, 61 s, 2:59, 3:00, 5:00, 5:01, 8:00, 8:01 and 10:00 each land in exactly one row, and 30 s and 12:00 each have a stated handling.

---

### Task 8: Living plate vs video assist (replaces the Mode A video rule)

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — replace line 263 (Step 5 video assist rule); edit the §2.1 Video row (line 75)

**Interfaces:**
- Consumes: §0.3 modes (Task 7)
- Produces: the two named asset kinds **living plate** and **video assist**. Task 9 and Task 14 refer to "living plate" by that name.

**Why:** Dual-A Important — Step 5 says "≤ 1 video per 4–5 slides in Mode A", but the frieren brief measures one living plate per slide and the tendays brief measures 23 clips across 13 slides. A producer following both at once is stuck. The two briefs are not describing the same thing as "video", and the guide never separates them.

- [ ] **Step 1: Replace the Step 5 video assist rule (line 263)**

Replace:

```markdown
**Video assist rule:** ≤ 1 video per 4–5 slides in Mode A; Mode C default 0 unless demo.
```

with:

```markdown
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
```

- [ ] **Step 2: Clarify the §2.1 Video row (line 75)**

Replace:

```markdown
| Video | Concentrated (key beats) | Sparse (≈1) |
```

with:

```markdown
| Video (both kinds counted together) | 1–23 `.mp4` files across this column's three saves (小王子动态 1 / 芙莉莲动态 8 / 十日终焉动画 23); across all seven saves in the corpus the count runs **0–23**. Mostly **living plates**, not edited assists (see Step 5) | 1 file |
```

**Note for the implementer (context, not text to paste).** These are `.mp4` file counts from `_analysis-local-metrics.md`. All seven saves: 十日终焉动画 **23**, 葬送的芙莉莲 **11**, 葬送的芙莉莲2 **11**, 芙莉莲动态 **8**, 小王子动态 **1**, 三星堆动态 **1**, 十日终焉静态 **0** — so the honest full-corpus range is **0–23**, and the table row quotes only the three saves named in its own column header. Two things to keep straight: the two 11-file saves carry **zero** anim nodes, so a video count says nothing about motion (§2 How-to-read, Task 12); and an earlier draft of this row claimed "8–23 files per deck", which is wrong at both ends — it excludes the two 1-file saves and the 0-file save and misses the 11s entirely. Keep the qualifier; it is the difference between an observation and an invented budget.

- [ ] **Step 3: Verify**

```powershell
rg -n "Living plate vs video assist" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "1 video per 4" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second 1 hit only — inside the new table's *video assist* row (the old standalone rule is gone).

**Acceptance check:** Mode A can legitimately carry one living plate per slide and still obey the video-assist cap. The §2.1 corpus row no longer implies 23 edited videos in a 13-slide deck, and every figure in it matches `_analysis-local-metrics.md` exactly — the column's three saves at 1 / 8 / 23, and the full-corpus range stated as 0–23 rather than a tidier interval that excludes real files.

---

### Task 9: §2.4 Motion physics; ambient vs perform test; auto-advance margin

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — insert §2.4 after §2.3 (after line 99); append a clock note to the Step 5 concept recipe (after line 261); rewrite §10.1 law 8; edit §0.1 point 2 (line 18); edit the Step 8 motion bullet (line 301)

**Interfaces:**
- Consumes: §2.2 bands, §Step 8 perform test, "living plate" (Task 8)
- Produces: section id `§2.4`, the terms **ambient layer**, **readable event** and **hero signal**, the two deadlines (**hero deadline** ~1–1.5 s, **readable deadline** ~5 s), and the formula `advance_sec ≥ longest_effect_end + 0.5 s`. Task 8's living-plate rule 2 and Task 15's §8 prompt seed both restate the two deadlines; Task 11 law 4 cites §10.1 law 8.

**Why:** Dual-A Important — ease-out, no-bounce and the 105% scale ceiling are measured in every pack but stated in no guide rule. Dual-B Important — §8 fails a slide for "motion still running when viewer needs to read", which would fail every correctly built ambient loop; the two concepts need separating. Dual-B Important — §10.1 law 8 says hold ≥ longest Morph with no margin and no definition of "longest".

- [ ] **Step 1: Insert §2.4 after the §2.3 transition bullets, before the `---` closing §2**

```markdown
### 2.4 Motion physics (applies to every mode)

Consistent across all four source packs. These are rules, not ranges.

1. **Ease-out, always.** Entrances arrive and settle: `decel=100%`, or `accel=30% / decel=70%`. Linear entrances read as cheap; entrances that accelerate out read as broken.
2. **No bounce, no overshoot.** Nothing passes its rest position and comes back.
3. **Scale ceiling ~105%.** An ambient zoom goes to about 105% over ~6 s, and at most one object per slide does it. **110% is the hard ceiling** for any scale move; treat 120%+ as out of system. Honest provenance: this is read off the scale effects the four craft briefs measured, not an exhaustive scan of every `animScale` node in the corpus — it is the house rule, not a proven absence.
4. **No 1 ms appear cascades.** "Appear" is not an entrance. Minimum 250 ms, normally 500–1000 ms (§2.2). Sole exception: deliberate micro-flicker décor at ≤ 6 pt and low contrast.
5. **No motion paths or spins as a design move.** Exceptions: one slow ambient rotation (≥ 6 s, indefinite) sitting under the type, and opener particles.
6. **Stagger follows reading order.** Even steps 100–500 ms for titles and editorial; an irregular burst inside ~1 s only for a montage. A strict 100 ms grid across a whole deck is the measured failure mode.
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
```

- [ ] **Step 2: Annotate the Step 5 concept-slide recipe with its cumulative clock**

Without this, the recipe (400 ms field + 500–800 ms subject + 300 ms label + 500–800 ms title + 400 ms body ≈ 2.1–2.7 s end-to-end) looks like it violates a "1.5 s" rule. It does not — only the *hero* owes 1.5 s. Make that explicit where the recipe lives.

Anchor: the concept-slide recipe's last line, `6. Hold for reading (Mode B/C: 4–7s)`. Append directly after it:

```markdown
*Clock check (§2.4): run end-to-end this recipe lands at ~2.1–2.7 s — well inside the ~5 s readable deadline. The hero still owes the ~1–1.5 s hero deadline, so lead with whichever of step 2 (subject) or step 4 (title) is the hero and start it at or near t=0; the non-hero steps fill the remaining budget. If the title is the hero, run steps 3–4 before step 2.*
```

- [ ] **Step 3: Rewrite §10.1 law 8**

Replace:

```markdown
8. **If auto-advancing, hold ≥ longest Morph/transition on that slide.**
```

with:

```markdown
8. **Auto-advance must clear the slide's longest motion plus a safety margin, then add reading time.** `advance_sec ≥ longest_effect_end + 0.5 s`, where `longest_effect_end` is the maximum over: the Morph/transition duration, every entrance's `delay + duration`, and any video-assist length. Example: a 2000 ms Morph plus a type cascade starting at +300 ms and running 1000 ms gives `max(2.0, 1.3) + 0.5 = 2.5 s` before anything else — then add reading time on top (Mode B/C: 4–7 s). The corpus contains a 500 ms auto-advance against a 2000 ms Morph; that clips the move. Do not copy it.
```

- [ ] **Step 4: Reconcile §0.1 point 2 with the §2.4 hero deadline**

§0.1 says "Readable at fullscreen in 1 second"; §2.4 sets the hero deadline at ~1–1.5 s. Two numbers for one rule reads as a contradiction, and the headline sentence is the one a producer remembers.

Line 18 is still accurate here — every earlier insertion landed below it — but anchor on the string anyway. Replace:

```markdown
2. **Readable at fullscreen in 1 second** — hero signal lands before body text.  
```

with:

```markdown
2. **Readable at fullscreen in about a second** — the slide's one **hero signal** is final by the ~1–1.5 s hero deadline, before any body text; every other readable event is final by the ~5 s readable deadline. Both terms and both numbers are defined in **§2.4**, which is authoritative; this line is their headline form.  
```

- [ ] **Step 5: Point the Step 8 perform-test bullet at §2.4**

Left as it is, this bullet fails every correctly built ambient loop — the exact contradiction §2.4 was written to resolve. §2.4 tells the reader to "fix §Step 8's reading"; this step is where that happens.

Anchor on the string: Tasks 1, 5, 7 and 10 all inserted text above it, so the line is well past 301 by now. Replace:

```markdown
- Motion still running when viewer needs to read  
```

with:

```markdown
- A **readable event** still moving past its deadline (§2.4): the hero not final by the ~1–1.5 s hero deadline, or any other word, rule, chart, label, chip or number not final and still by the ~5 s readable deadline or the start of the hold, whichever comes first. A declared low-contrast **ambient layer** still breathing underneath is **not** a failure — see §2.4  
```

- [ ] **Step 6: Verify**

```powershell
rg -n "### 2\.4 Motion physics" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -ni "readable deadline" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -ni "hero deadline" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Clock check" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "longest_effect_end" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Readable at fullscreen in 1 second|^- Motion still running when viewer" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second ≥ 3 hits; third **≥ 4 hits** — the §2.4 definition, the §0.1 headline (Step 4), the Step 5 clock check (Step 2), and the Step 8 bullet (Step 5); fourth 1 hit; fifth ≥ 2 hits (formula + example); sixth **0 hits**.

> **Use `-i` on the two deadline greps.** §2.4 writes the definitions as capitalised bold labels (`**Hero deadline, ~1–1.5 s.**`) while every later use is lower-case mid-sentence. A case-sensitive `rg "hero deadline"` therefore finds fewer hits than the guide actually contains and makes a correct file look like a failed task — that is a tooling bug in the check, not a defect in the guide.

**Acceptance check:** §2.4 states ease-out, no bounce, and the 105%/110% ceiling as numbered rules. The ambient / readable-event / hero distinction is defined before it is used. **No rule anywhere in the guide now demands that all readable type land by 1.5 s** — the hero owes 1.5 s, everything else owes the ~5 s readable deadline, and §0.1 point 2, the Step 5 concept recipe, the living-plate rule (Task 8), and the craft briefs' 2–3 s lockup budget all agree with that. **Both of the places that previously stated the rule in their own numbers now defer to §2.4**: §0.1 no longer says a flat "1 second", and the Step 8 perform test no longer fails a breathing ambient layer. §10.1 law 8 gives a computable floor, not a comparison.

---

### Task 10: Projection readability floor and contrast proxy

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — replace the Step 4 type hierarchy block (lines 222–227); add one field to the Step 0 JSON; append one bullet to §5.3; append one row to §7 `### Craft`

**Interfaces:**
- Consumes: §7 group headings and labels (Task 2), the Step 0 JSON (Tasks 1, 5)
- Produces: the phrase **projection floor**, the value **18 pt**, and the Brief field `viewing` (`projected|close_range`, default `projected`). Task 7's overflow law already points at "the §Step 4 projection floor"; Task 16 greps `close_range`.
- Ordering note: this task inserts `viewing` directly after `"mode"`, so it does **not** disturb Task 11's `"delivery"` anchor.

**Why:** Dual-A Important — the guide's body range is 14–18 pt, but a 竞聘 deck is shown on a projector in a lit room, and the frieren brief's own measurement says 11–12 pt fails on a projected 16:9 frame and 18 pt is the floor. Folds the contrast-proxy minor.

- [ ] **Step 1: Replace the Step 4 type hierarchy block (lines 222–227, from `**Type hierarchy (default scale)**` through `- Meta: 10–12 tracked`)**

Replace:

```markdown
**Type hierarchy (default scale)**

- Display: 64–120 pt feel (or CSS `clamp` equivalent)  
- Section: 28–40  
- Body: 14–18  
- Meta: 10–12 tracked  
```

with:

```markdown
**Type hierarchy (default scale, 13.333×7.5 in canvas; CSS `clamp` equivalents for web)**

- Display: 64–120 pt — at most one poster glyph per chapter may go 140–170 pt  
- Section: 28–40 pt  
- Body: **18–24 pt** — 18 is a floor, not a target  
- Meta: 10–12 pt tracked — a *mark*, never a sentence  

**Projection floor (any room with a projector or a shared screen — and Mode C always)**

- Anything that must be read: **≥ 18 pt**. No exception for "it's only a footnote" — if it must be read, it is body text.
- If the content will not fit at 18 pt, **cut words or split the slide**. Shrinking type to fit is a Critical failure (§7).
- **Mode C has no exception.** A 竞聘 / 汇报 deck is read across a room off a projector or a shared screen, so 18 pt is its unconditional floor. "It's a web deck, they'll be on a laptop" does **not** unlock 14 pt in Mode C — a briefing you built for a laptop will be projected anyway, and you will not be in the room to stop it.
- The **only** exception in this guide: **Mode B** at close range — a kiosk or wall panel a visitor stands in front of — may go to 14 pt, and only when the Brief declares `"viewing": "close_range"`. Modes A, C and D never do; `viewing: close_range` is illegal outside Mode B.
- **Contrast proxy** (use when you cannot measure a ratio): the type must still read when you (a) convert the frame to greyscale and (b) blur it until individual glyphs smear. If it disappears into the plate in either test, add a vignette or a shade plate (layer 3 above) — do not add a stroke, an outline, or a glow.
```

- [ ] **Step 2: Add the `viewing` field to the Step 0 JSON**

This is the only way a reader can declare the Mode B close-range exception, and the only way §7 can audit it. Anchor: the `  "mode": "A|B|C|D",` line. Insert immediately after it:

```json
  "viewing": "projected|close_range",
```

Then add this sentence to the `must_exclude` boilerplate paragraph added in Task 1 Step 3:

```markdown
`viewing` defaults to `projected` and may only be `close_range` when `mode` is `B` and the viewer stands in front of the screen (kiosk, wall panel). It is the sole unlock for the 14 pt body exception in §Step 4; Mode C may never set it.
```

- [ ] **Step 3: Append to §5.3 anti-defaults**

Add after `- No animation longer than reading time`:

```markdown
- No body text below 18 pt — ever, in this mode. If it does not fit, cut it, never shrink it  
```

- [ ] **Step 4: Append to §7 `### Craft`**

```markdown
- [ ] **[CRITICAL]** Projection floor held: nothing that must be read is below 18 pt, and the contrast proxy passes on every text slide. The sole exception is `mode: B` **with** `viewing: close_range` in the Brief — Mode C never qualifies
```

- [ ] **Step 5: Verify**

```powershell
rg -n "Projection floor" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Contrast proxy" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "close_range" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "^- Body: 14" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "laptop" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first ≥ 2 hits; second 1 hit; third ≥ 4 hits (JSON enum, the `viewing` paragraph, the §Step 4 exception, the §7 Craft row); fourth **0 hits**; fifth 1 hit only — the sentence in §Step 4 that *denies* the laptop exception, never one that grants it.

**Acceptance check:** **≥ 18 pt is unconditional for Mode C.** Grep the guide for every mention of 14 pt and confirm each one is either the deleted old scale (gone) or the Mode B + `close_range` exception. There is no "web", "laptop" or "screen-read" escape hatch anywhere. The contrast proxy is a two-step action, not an adjective.

---

### Task 11: Advance & trigger model in the Brief and Beat schemas; Mode C presenter-held

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — add two fields to the Step 0 JSON; add three fields to the Step 2 beat schema; insert the trigger-model block after the beat schema

**Interfaces:**
- Consumes: Step 0 JSON (Tasks 1, 5), §10.1 law 8 (Task 9), §2.4 (Task 9)
- Produces: Brief fields `advance` (`presenter|auto|web_keys`) and `advance_notes`; beat fields `advance`, `advance_sec`, `trigger` (`on_click|with_previous|after_previous`). Task 14's manifest row cites "the intended advance model".

**Why:** Dual-A Important — the briefs measure three incompatible trigger models (click-gated type, autoplay with-previous chords, visitor-paced advance) and the guide never asks the producer to pick one, so the failure modes (click-gated type in an auto export; auto-advance clipping a Morph) are unpreventable. Mode C must be presenter-held.

- [ ] **Step 1: Add the advance fields to the Step 0 JSON**

Anchor: the `"delivery": "pptx|web|both",` line (as edited by Task 5). Insert immediately after it, before `"source_packet"`:

```json
  "advance": "presenter|auto|web_keys",
  "advance_notes": "if auto: where each slide's hold comes from",
```

- [ ] **Step 2: Add the advance fields to the Step 2 beat schema**

Anchor: the beat schema's `  "hold_sec": 3.5,` line. Insert immediately after it:

```json
  "advance": "click|auto",
  "advance_sec": 0,
  "trigger": "on_click|with_previous|after_previous",
```

- [ ] **Step 3: Insert the trigger model block after the beat schema's closing fence, before `**Canonical PV arc`**

```markdown
**Advance & trigger model — declare it once in the Brief, then per beat:**

| Model | Who advances | Entrance triggers | Use |
|---|---|---|---|
| `presenter` | A human clicks; the slide waits indefinitely | One chord per slide: first effect `on_click`, everything else `with_previous`. Ambient may loop. | **Mode C default — always.** Mode B galleries. |
| `auto` | A timer (`advance_sec`) | All `with_previous` / `after_previous`. No click-gated reveal anywhere. | Mode A films, kiosk loops, exported video |
| `web_keys` | Keyboard or click on a web stage | A state machine per beat; honour `prefers-reduced-motion` | Web pseudo-PPT |

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
```

- [ ] **Step 4: Verify**

```powershell
rg -n "Advance & trigger model" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Mode C is presenter-held" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "presenter\|auto\|web_keys" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Enum alignment" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second 1 hit; third ≥ 1 hit (the Brief field); fourth 1 hit.

**Acceptance check:** The Brief declares the model; each beat declares its trigger; law 1 makes Mode C presenter-held without an escape hatch other than a separate exported file. Law 6 ties `delivery`, `advance`, `viewing` and `mode` together, and no section anywhere in the guide references a `delivery` value that is not in the enum. The beat schema is still valid JSON-with-placeholders.

---

### Task 12: Reframe §2 corpus metrics as observations, with the sample's own exceptions

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — replace line 63; retitle §2.1 (line 65); append an exceptions block after the §2.1 table

**Interfaces:**
- Consumes: Task 4's metrics-file header wording (keep them consistent)
- Produces: nothing other tasks depend on

**Why:** Dual-B Important — §2 calls the numbers "production budgets", which turns six observed files into targets. The sample is four storyboards from one genre cluster, and it contradicts itself on nearly every axis.

- [ ] **Step 1: Replace line 63**

Replace `Use these as **production budgets**, not trivia.` with:

```markdown
**How to read this section:** these are **observations of seven source files**, not targets and not a scoring rubric. They tell you the range high-tier decks lived in; they do not tell you your deck is wrong for sitting outside it. Use them to sanity-check a plan ("25 dense bullet slides for a 60 s PV" is outside every observed range), never to tune a deck toward a number.

**Sample:** **7 saved files covering 4 storyboards**, 4 properties, one genre cluster (CJK cinematic PPT). No Western corporate decks, no academic decks, no live keynotes, no 竞聘 decks. Three of the seven are **static saves** of a storyboard that also has an animated save, so the effective sample is **four storyboards** — and one of those four (the exhibition deck) is the only non-PV deck in the set.
```

- [ ] **Step 2: Retitle §2.1 (line 65)**

Replace `### 2.1 Deck shape` with `### 2.1 Deck shape (observed ranges, not targets)`.

- [ ] **Step 3: Append the exceptions block after the §2.1 "AI rule" line**

Insert after `**AI rule:** If Mode A and you plan 25 slides of dense bullets, you already failed. Cut to beats.`:

```markdown
**Exceptions inside the sample itself — no single row is a law:**

- **Anim-node density spans 196 → 1374** across the **four animated saves** (196 / 359 / 594 / 1374). The other three files have zero. The 1374-node deck gets there by fragmenting six near-identical slides, not by being six times better.
- **Fragmentation ratio spans 0.62 → 0.96.** The 0.69 exhibition deck is the most *readable* of the set. A high ratio is a kinetic-title production method, not a quality score.
- **Pictures per slide spans 4.4 → 11.1.** The 11.1 figure counts stacked full-bleed fields, and that deck's own brief calls five stacked plates an anti-pattern.
- **Timing medians span 200 ms → 1000 ms**, measured on the four animated saves only. There is no correct median; the median follows the mode.
- **Three of the seven files carry zero animation nodes** — the static saves. Two of them still report timing coverage (7 of 12 and 11 of 12 slides) while carrying no animation nodes at all; the third reports no timing trees at all. So "nearly all slides have timing trees" describes the **animated saves** only, and even where a timing tree exists it may be empty. Never infer motion from timing coverage.

**Never report a corpus number as a property of your own deck** ("fragmentation 0.9, matching the reference"). That is a cargo-cult metric, and it is also an IP-proximity smell (§7.1).
```

- [ ] **Step 4: Verify**

```powershell
rg -n "production budgets" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Exceptions inside the sample itself" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "observed ranges, not targets" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "six source files|6 decks|Two of the six" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first **0 hits**; second 1 hit; third 1 hit; fourth **0 hits**.

**Acceptance check:** §2 states its sample size and its blind spots before any number. Every count traces to `_analysis-local-metrics.md`, which lists **seven** `.pptx` files: 三星堆动态 (23 slides / 359 nodes), 十日终焉动画 (13 / 1374), 十日终焉静态 (13 / **0**), 小王子动态 (12 / 196), 芙莉莲 (12 / **0**), 芙莉莲2 (12 / **0**), 芙莉莲动态 (13 / 594) — four storyboards, three static saves, four animated saves. The exceptions list names five concrete internal contradictions, each tied to a real figure in `_analysis-local-metrics.md` / `_analysis-timing-recipes.md`. **No "six files" or "two of six" wording survives anywhere in the guide or the brief headers (Task 4).**

---

### Task 13: Expand the Mode C information architecture with duration-gated blocks

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — replace §5.2 (lines 338–345); replace the Step 2 "Canonical briefing arc (Mode C)" block (lines 165–172)

**Interfaces:**
- Consumes: §0.3 budget rows (Task 7), §5.4 ledger (Task 6)
- Produces: the **seven-block core spine** (Identity, Promise, Pillars, Evidence, Plan / platform, Ask, Close) and the four optional block names **Diagnosis**, **Timeline**, **KPI / metrics**, **Risk / honesty**

**Why:** Dual-B Important — the current six-item spine is the same for a 3-minute and a 10-minute 竞聘, and it has no place for the diagnosis, timeline, or metrics that decide a contested race. Dual-A Important — the first draft of this task made **Plan / platform** optional while §5.2 and the Step 2 arc both treated it as mandatory, so the fix introduced the contradiction it was meant to remove. Plan stays in the core spine; only Diagnosis, Timeline, KPI and Risk are duration-gated, and Step 2 stops carrying a rival arc.

- [ ] **Step 1: Replace §5.2 (lines 338–345)**

Replace:

```markdown
### 5.2 Information architecture

1. Identity (name / role sought)  
2. Promise (one line)  
3. Three pillars (skills)  
4. Evidence (1 slide each: situation → action → result)  
5. Platform (3 concrete moves)  
6. Close  
```

with:

```markdown
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
| **Diagnosis** | 5:00+, or any contested race | What is actually wrong or unfinished today: 2–3 specific observations, each sourced. Credibility is usually won here — naming the real problem outranks listing achievements. | 1 |
| **Timeline** | 5:00+, or when the record is cumulative | A horizontal spine: 3–5 dated points, one line each. Shows trajectory instead of an unordered pile of wins. | 1 |
| **KPI / metrics** | 8:00+, or when the role owns a number | 3 numbers maximum, each with period, denominator and method in the ledger. Never a dashboard. | 1 |
| **Risk / honesty** | 8:00+; optional at any length | One thing you are not good at yet, plus the mitigation. Costs 40 s, buys the room. | 1 |

**Ordering law:** Diagnosis goes **before** Pillars (problem → why me), never after Evidence. Timeline and KPI live inside the Evidence run. Risk goes immediately before the Plan.

**Dropping law:** drop **optional** blocks before compressing the Evidence run; never drop a core-spine block. Three strong evidence slides beat six one-line blocks. At 3:00–5:00 you run the core spine and nothing else — which means an Evidence run of **at least 4 slides**. Do the arithmetic: Evidence is itself one of the seven blocks, so six single-slide blocks plus an *n*-slide evidence run lands at `6 + n`, and the §0.3 floor for that row is 10. With only three cases to show, reach the floor by splitting Pillars one capability per slide — never by borrowing a 5:00+ optional block.
```

- [ ] **Step 2: Point the Step 2 "Canonical briefing arc" at §5.2**

The Step 2 beat-sheet section carries a second, shorter Mode C arc. Leaving both is how §5.2 and Step 2 drifted apart in the first place.

Replace:

```markdown
**Canonical briefing arc (Mode C):**

1. Title + role  
2. Why me / promise  
3. Capability pillars (3)  
4–N. Evidence slides (one case each)  
N+1. Plan if elected (3 moves)  
N+2. Close  
```

with:

```markdown
**Canonical briefing arc (Mode C):** use the **§5.2 information architecture** — seven core-spine blocks, plus the duration-gated optional blocks. Do not restate the arc here; §5.2 is the only Mode C spine in this guide.
```

- [ ] **Step 3: Verify**

```powershell
rg -n "Optional blocks, added by duration" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Ordering law|Dropping law" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Plan / platform" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Plan if elected|Capability pillars \(3\)" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "at least 4 slides" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second 2 hits; third 1 hit — inside the **core spine**, not the optional table; fourth **0 hits**; fifth 1 hit.

**Acceptance check:** **Plan / platform appears exactly once in the guide, as core-spine item 5.** It is not also listed as optional, and no section says it may be dropped — the contradiction between "mandatory in §5.2" and "optional ≥ 5 min" is gone. The Step 2 canonical briefing arc is a pointer, not a second spine.

**The spine arithmetic agrees with §0.3, stated honestly.** Evidence is one of the seven blocks, so the core spine costs `6 + n` slides for an *n*-slide evidence run. A 3-slide run gives **9 — one below the §0.3 3:00–5:00 floor of 10**, which is why the Dropping law sets the minimum evidence run at 4 (10 slides, exactly the floor) and names splitting Pillars as the alternative route to it. A 4-slide run plus split Pillars reaches 12, still inside the row's 10–14 ceiling. No reading of §5.2 now produces a legal deck that undershoots §0.3.

---

### Task 14: §7.3 Delivery QA and its checklist row

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — add §7.3 after §7.2; append one row to §7 `### Performance`

**Interfaces:**
- Consumes: §7.1 rows 2–3 (Task 2), §5.4 ledger and `source_packet` (Tasks 5–6), the advance model (Task 11), the living-plate weight cap (Task 8)
- Produces: the handover artefact name `handover.md`

**Why:** Dual-B Important — the guide stops at "build it" and never checks the file actually handed over: format, font resolution on a second machine, a motion-free export, weight, and a manifest. Keep it lean; this is seven rows, not a QA programme.

- [ ] **Step 1: Add §7.3 after the §7.2 fail-handling line**

```markdown
### 7.3 Delivery QA (run on the actual file you are handing over)

| # | Check | Pass condition |
|---|---|---|
| 1 | Format & ratio | `.pptx` (OOXML, not `.ppt`), 16:9, 13.333×7.5 in. Opened once in the application **and version** the audience will use. |
| 2 | Fonts — **pick one path per face and state which** | **Path A, embedded** (preferred when the licence allows embedding): the face is embedded in the `.pptx`, and you confirmed it by opening the file on a machine where that face is **not** installed — the glyphs still render. **Path B, preinstalled handoff** (when the licence forbids embedding, or the face is a system font): the face is **not** embedded, and `handover.md` names it with an install source, a licence basis, and the named fallback the deck degrades to. Then open on a clean machine and confirm the deck degrades **to that named fallback**, legibly, with no reflow that breaks a lockup. A face that is neither embedded nor listed in the handoff fails. Silent fallback to a theme gothic fails on both paths. |
| 3 | Theme leak | No run left on the default theme font or default accent; the §7.1 row 4 tokens are set explicitly. |
| 4 | Export | The agreed fallback exists: a PDF of the static twin (**always**), plus an MP4 at delivery resolution whenever the Brief's `advance` is `auto` (kiosk loop or exported film). `delivery` has no `video` value — the MP4 is an artefact of an `auto` build, not a delivery mode (§Step 2 enum alignment law 6). The PDF must be readable with motion gone. |
| 5 | Weight | File size stated. Above 200 MB, say why (plate count) and offer a light build. |
| 6 | Manifest | A `handover.md` beside the deck containing: the **ship report** (the answered §7 checklist plus the §7.1/§7.2/§7.3 outputs — defined in §7 "How to read this checklist"), deck file, static twin, export(s), font list with **which of row 2's two paths each face took** + licence basis, asset list + provenance (reuse §7.1 rows 2–3), the §7.1 row 8 file-origin statement, `source_packet` + claim ledger (Mode C), open `[NEEDS INPUT]` count, and the declared advance model. |
| 7 | Clean open | No missing-media warnings, no broken links, no unused masters or layouts beyond what the deck actually uses. |
```

- [ ] **Step 2: Append to §7 `### Performance`**

```markdown
- [ ] **[CRITICAL]** §7.3 delivery QA run on the handover file; `handover.md` manifest exists
```

- [ ] **Step 3: Verify**

```powershell
rg -n "### 7\.3 Delivery QA" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "handover.md" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "Path A, embedded|Path B, preinstalled handoff" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "delivery.{0,20}includes video|delivery includes video" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first 1 hit; second ≥ 3 hits (the §7 ship-report definition, §7.3 row 6, the §7 Performance row); third 2 hits; fourth **0 hits**.

**Acceptance check:** §7.3 is exactly seven rows. Row 2 gives a font face **two** distinct pass paths — embedded-and-verified-on-a-clean-machine, or not-embedded-but-documented-with-a-named-fallback-and-verified-degradation — and a face that fits neither fails; the old single rule silently failed every licence that forbids embedding. Row 4 cites `advance: auto` rather than a non-existent `delivery` value. The manifest row reuses §7.1's asset/font lists rather than defining a second inventory, and names the ship report as its first contents.

---

### Task 15: Terminology consistency, §8 prompt seed expansion, version bump

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md` — line 48 (§1.1), §10.1 law 1 (line 438), the §8 prompt-block body (lines 399–406 — everything between the opening text fence at line 398 and the closing fence at line 407, fences kept), §11 (lines 451–454)

**Interfaces:**
- Consumes: every section id created by Tasks 1–14
- Produces: the canonical term pair **static twin / motion twin**; the final §8 prompt seed

**Why:** Dual-A Important — the §8 prompt seed is the artefact a downstream AI actually receives, and it currently omits the ban detail, the budget table, grounding, the trigger model, and the labelled gate. Minor — the same workflow is called "static twin → dynamic twin" (§1.1), "static twin" (Step 4), "still twin→motion twin" (§10 table, fixed in Task 3) and "still twin" (§10.1 law 1).

> **Supersession note — read before starting.** Step 2 below replaces the **entire** §8 `text` block, including the two lines Task 1 Step 4 and Task 2 Step 2 wrote into it. Step 3 replaces the **entire** §11 list, including the ban pointer Task 1 Step 5 wrote. That is intentional, not duplicated work: the earlier edits keep the guide internally consistent if the plan stops partway, and the text here is the final authority. **Run the earlier steps anyway, do not re-apply them after this task, and if the earlier text is missing when you get here, finish the earlier task first** — its absence means a prior step did not land.

- [ ] **Step 1: Canonicalise the twin terminology**

Replace line 48:

```markdown
- Static twin → dynamic twin workflow (compose stills first, then animate)  
```

with:

```markdown
- **Static twin → motion twin** workflow — compose every slide as a finished still first, then add timing to that exact frame. The static twin stays a shippable file, not a draft (§Step 4, §7.3 row 4)  
```

Replace §10.1 law 1:

```markdown
1. **Compose the still twin first; motion is a second pass on the same bones.**
```

with:

```markdown
1. **Compose the static twin first; the motion twin is a second pass on the same bones** (terminology per §1.1 — "static twin / motion twin" everywhere in this guide).
```

- [ ] **Step 2: Replace the §8 prompt block body (lines 399–406, between the ```text fences)**

Keep the fences; replace the contents with:

```text
You produce PPTs (or fullscreen web slide films) using the Craft Transfer Guide. Follow the master pipeline (§3) in order.

HARD BAN (§0.0 — outranks every other instruction and any reference source): no character cards or casts, and no stat / tier / relationship structure or multi-entity attribute block taken from a reference; no lore, world rules, in-universe mechanics or terms — quoted, translated, renamed, or paraphrased; no identifiable key art, silhouettes, or costumes; no reference titles, taglines, quotes, or name lockups; no reference or unlicensed audio; no reference plates, textures, clips, previews, or font files as shipped assets; no "same palette/fonts/chrome as X" no matter where you read the values (a brief, the reference file, the work itself, a wiki, a handed-over pack); never build over an existing file — start from a new empty deck, no template of any kind. Take grammar only: hierarchy, layer stack, beat roles, timing bands, stagger, composition patterns, trigger models.

BRIEFS ARE MEASUREMENTS (§10.0): companion _analysis-* files record what one deck did. Translate their values (role ladders, role structures, continuity ideas, timing bands) — never transplant them (font names, hex values, coordinates, chrome kits, identity scaffolds). Precedence: §0.0 ban > this guide > the Brief JSON > brief observations.

GROUND THE FACTS (§5.4, Step 0): emit a Brief JSON with source_packet before designing, and a claim ledger before any evidence slide. Every number, date, name, title, award, and outcome traces to a source id. Invent framing, metaphor, and phrasing; never invent facts. Missing fact => print the literal token [NEEDS INPUT: <what> — from S?] on the slide. Never substitute a hedge (显著 / 多次 / 广受好评) for a number you do not have.

PICK ONE MODE first, then the one §0.3 budget row your duration falls in. The bands are gap-free: within a mode the first band includes both bounds and every later band excludes its lower bound, so 45s / 3:00 / 5:00 each belong to exactly one row. Default for campus leadership / 竞聘: Mode C, presenter-held, or Mode D with a 20–40s cold open and an explicit handoff (§0.4). Mode C structure comes from §5.2: a seven-block core spine (Identity, Promise, Pillars, Evidence, Plan/platform, Ask, Close) plus duration-gated optional blocks.

EMIT BEFORE VISUALS: Brief JSON (incl. mode, viewing, duration_sec, delivery, advance, source_packet) -> Beat Sheet JSON (one job per beat, trigger + advance per beat) -> Visual System tokens -> static twin -> motion twin. Enum law: delivery is pptx|web|both only; advance web_keys needs web; Mode C is always advance presenter; viewing close_range is Mode B only.

MOTION (§2.4): ease-out always; no bounce or overshoot; scale ceiling ~105% and a hard 110% ceiling; no 1ms appear cascades (min 250ms, normally 500–1000ms); stagger follows reading order. The slide's hero signal lands by ~1–1.5s; every other readable event is finished and still by the readable deadline (~5s, or the start of the hold). Only low-contrast ambient layers may keep moving after that. Auto-advance >= longest effect end + 0.5s, then reading time.

TYPE: nothing that must be read goes below 18pt. In Mode C this is unconditional — no projector/laptop/web exception. The sole exception in the guide is Mode B with viewing close_range. If it does not fit, cut words or split the slide — never shrink.

EVERY SLIDE: one job, hero first, scannable type, motion that protects reading.

FINISH by running §7 plus the §7.1 (ban / IP leakage, 10 rows — incl. row 8 file-origin, row 9 paraphrase & borrowed-substance sweep, row 10 entity-structure sweep), §7.2 (unsupported claims), and §7.3 (delivery QA) audits, and write the ship report into handover.md. Items tagged [CRITICAL] are release gates: if any is unchecked, unverified, or unverifiable, do not say the deck is done, finished, ready, or shipped — report which Critical item failed and why. An item you could not verify counts as failed.
```

- [ ] **Step 3: Update §11 document control**

Replace lines 451–454 (the four existing bullets) with these five:

```markdown
- Version: 2026-10-11 (v1.2 — dual-verification fixes: normative §0.0 hard ban, all eight items audited by the ten §7.1 rows incl. the paraphrase and entity-structure sweeps; §10.0 brief precedence + translation law; §5.4 claim ledger and source_packet grounding; §7 labelled gate with the §7.1/§7.2/§7.3 audits and a defined ship report; §0.3 authoritative slide budget with gap-free duration bands; §0.4 Mode D handoff; §2.4 motion physics with hero and readable deadlines; living plate vs video assist; unconditional 18 pt projection floor)
- Type: AI production guide (actionable, self-sufficient — companion briefs are measurements only)
- Corpus: `Desktop/ppt模板` structural analysis only; **7 files / 4 storyboards / 3 static saves**, one genre cluster (see §2)
- Ban: see §0.0 (normative, eight items, audited by §7.1) — this line is a pointer, not a second copy
- Terminology: **static twin / motion twin** (never "dynamic twin" or "still twin")
```

- [ ] **Step 4: Verify**

```powershell
rg -n "dynamic twin|still twin" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -n "v1\.2" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
rg -c "HARD BAN|BRIEFS ARE MEASUREMENTS|GROUND THE FACTS|PICK ONE MODE|EMIT BEFORE VISUALS|MOTION \(|TYPE: nothing|EVERY SLIDE|FINISH by running" docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md
```

Expected: first **0 hits** except inside the §11 terminology line (which names them as forbidden — confirm by eye that every hit is that one line); second 1 hit; third **9** (one per §8 paragraph lead).

**Acceptance check:** The §8 block, pasted alone into a fresh model's system prompt, carries all nine leads — ban, precedence law, grounding rule, mode + §0.3 budget + §5.2 spine, emit order + enum law, motion physics with both deadlines, the unconditional 18 pt floor, the per-slide rule, and the labelled gate plus the ship report. Nothing in the guide still says "dynamic twin" or "still twin" as the workflow name.

---

### Task 16: Final verification sweep — confirm every Critical is closed

**Files:**
- Read only: all seven files touched by this plan. **Make no edits in this task** except to fix a check that fails.

**Interfaces:**
- Consumes: everything produced by Tasks 1–15

- [ ] **Step 1: Run the section-presence and label checks**

From the repo root. **`rg`'s last argument is a path, not a glob** — use `-g` for the companion files (see Task 4 Step 3).

```powershell
$g = "docs/superpowers/specs/2026-10-11-ai-ppt-production-guide.md"
$specs = "docs/superpowers/specs"

rg -n "### 0\.0 Hard ban" $g
rg -n "### 0\.3 Slide budget" $g
rg -n "### 0\.4 Mode D handoff" $g
rg -n "### 2\.4 Motion physics" $g
rg -n "### 5\.4 Claim ledger" $g
rg -n "### 7\.1 Ban & IP-leakage audit" $g
rg -n "### 7\.2 Unsupported-claims audit" $g
rg -n "### 7\.3 Delivery QA" $g
rg -n "### 10\.0 How to read a companion brief" $g

rg -c "\[CRITICAL\]" $g
rg -c -g "_analysis-*.md" "Status: measurement report, not an instruction" $specs
```

Expected: nine section headings, 1 hit each; `[CRITICAL]` count ≥ 15; six companion files each with count 1.

- [ ] **Step 2: Check every §7 bullet is labelled**

A checklist bullet with no label is a bullet the §8 completion clause cannot gate. Compare two counts rather than eyeballing:

```powershell
rg -c "^- \[ \]" $g
rg -c "^- \[ \] \*\*\[(CRITICAL|RECOMMENDED)\]\*\*" $g
```

Expected: **the two numbers are equal.** If they differ, list the offenders and label them:

```powershell
rg -n "^- \[ \]" $g | rg -v "\[CRITICAL\]|\[RECOMMENDED\]"
```

Expected: no output. (This is the one place in Task 16 where you *do* edit: add the missing label, choosing `[CRITICAL]` only if the item is a genuine release gate.)

- [ ] **Step 3: Check all eight §0.0 tags reach Step 0 `must_exclude`**

```powershell
$tags = @(
  "character_cards","lore_or_world_rules","identifiable_key_art","reference_copy",
  "reference_audio","reference_binaries","reference_identity_system","reference_file_as_base"
)
foreach ($t in $tags) { "{0,-28} {1}" -f $t, (rg -c $t $g) }

rg -n -A 6 '"must_exclude"' $g
```

Expected: every tag reports **≥ 3** — once as a §0.0 item, once in `must_exclude`, and at least once in a §7.1 row's **Check** cell, because Task 2 tags every row with the items it closes. `character_cards` and `reference_identity_system` report more (three rows each). The `must_exclude` array printed by the second command contains **all eight** tags and nothing shortened.

A tag reporting exactly **2** means a §7.1 Check cell lost its tag, which makes the Step 8 coverage matrix unverifiable by grep — fix the Check cell, not this expectation. This is the check that failed the round-2 dual on both legs: the earlier plan expected `reference_file_as_base` ≥ 3 while row 8 never wrote the literal tag anywhere, so a correctly implemented guide reported 2 and the task failed on its own arithmetic.

- [ ] **Step 4: Validate the two JSON blocks**

Both are JSON-with-placeholders, so a parser will not help — check them structurally by eye against this field list.

**Step 0 Brief JSON — 12 fields, in this order:**

```powershell
rg -n '"topic"|"audience"|"duration_sec"|"mode"|"viewing"|"promise"|"must_include"|"must_exclude"|"delivery"|"advance"|"advance_notes"|"source_packet"' $g
```

Expected: 12 field lines inside one block, in the order listed. Then confirm by reading: braces and brackets balance; every field except the last carries a trailing comma and the last does not; `must_exclude` is an array of eight quoted strings; `source_packet` is an object with `provided`, `items` (array of objects with `id`/`what`/`from`/`date`) and `gaps`; no comment or prose leaked inside the fences.

**Step 2 Beat schema — 11 fields:**

```powershell
rg -n '"i"|"role"|"job"|"hero"|"type_plan"|"motion"|"hold_sec"|"advance"|"advance_sec"|"trigger"|"notes"' $g
```

Expected: `advance`, `advance_sec`, `trigger` sit directly after `hold_sec`; `notes` is last and carries no trailing comma.

**§5.4 claim ledger JSON:** `claims` is an array of one example object with `id`, `slide`, `text_on_slide`, `type`, `source`, `verifiable_by`, `status`; the inner three-backtick fence opened and closed exactly once inside the four-backtick block.

- [ ] **Step 5: Check the §8 prompt seed covers everything**

```powershell
rg -c "HARD BAN|BRIEFS ARE MEASUREMENTS|GROUND THE FACTS|PICK ONE MODE|EMIT BEFORE VISUALS|MOTION \(|TYPE: nothing|EVERY SLIDE|FINISH by running" $g
```

Expected: **9**. Then read the block once and confirm each lead is substantive, not a pointer: the ban is spelled out in full (a downstream model may never see §0.0), the precedence order is stated, the grounding rule names the `[NEEDS INPUT]` token and the hedge failure, the mode paragraph names §0.3 and the §5.2 spine, the emit paragraph carries the enum law, motion carries **both** deadlines, type states the Mode C floor as unconditional, and the finish paragraph names all three audits and the ship report.

- [ ] **Step 6: Check the grounding fields are wired end to end**

```powershell
rg -c "source_packet" $g
rg -n "NEEDS INPUT" $g
rg -n "verifiable_by|text_on_slide|needs_input" $g
rg -n "handover.md" $g
rg -n "ship report" $g
```

Expected: `source_packet` ≥ 5 (Brief field, no-invention prose, `provided: false` note, §7.2 row 6, §7.3 row 6); `NEEDS INPUT` ≥ 4; the ledger keys each ≥ 1; `handover.md` ≥ 3; `ship report` ≥ 4 — and **exactly one** of those is a definition (§7 "How to read this checklist"), every other being a use.

- [ ] **Step 7: Run the regression checks (strings that must be gone)**

All patterns below are **ASCII-only on purpose** — a CJK literal inside a PowerShell `rg` argument depends on console code page and can silently match nothing, which would make a failed regression look like a pass.

```powershell
rg -n "IP character cards|reference OST as content|Never clone character cards|production budgets|s PV: 8|^- Body: 14|Medium-high \||NOT transferable to|filled in by Task|six source files|6 decks|Two of the six|Plan if elected|delivery includes video|your own template|3-slide cold open|Readable at fullscreen in 1 second|^- Motion still running when viewer|eight\*\* rows" $g
```

Expected: **0 hits**. Any hit means an earlier task's replacement did not land — fix it in place before continuing. What each pattern guards: the three short ban restatements (Task 1), the metrics-as-budgets framing (Task 12), the duplicate Step 1 budget list (Task 7), the 14 pt body scale (Task 10), the Mode B "Medium-high" density cell (Task 7), the 竞聘-scoped law 6 (Task 3), the §7 Truth placeholder (Task 6), the three stale corpus counts (Tasks 4/12/15), the rival Step 2 briefing arc (Task 13), the non-existent `delivery` video value (Task 14), the §7.1 row-8 "your own template" exception (Tasks 1–2), the rival §5.1 3-slide cold open (Task 7), the §0.1 flat "1 second" and the unqualified Step 8 motion bullet (Task 9), and the stale "all **eight** rows pass" count in the §7 Ethics group now that §7.1 has ten rows (Task 2).

- [ ] **Step 8: Walk the ban → audit coverage matrix (all eight §0.0 items)**

This is the check that failed review twice: first §7.1 claimed full IP closure while one ban item had no runnable row, then it claimed coverage for items 1, 2 and 7 that its rows could not actually deliver. **Every §0.0 item must land in at least one §7.1 row that produces an artefact, and the artefact must be capable of catching the item's realistic failure mode — not merely adjacent to it.** Open §0.0 and §7.1 side by side and fill this in from what the file actually says. Do not copy the expected column on faith; the previous two versions of this matrix were wrong precisely because someone did.

| §0.0 item | Tag | §7.1 row(s) | The artefact that proves it | The failure mode it must catch |
|---|---|---|---|---|
| 1 | `character_cards` | **10**, 9, 5 | The multi-entity attribute inventory with per-block origin (10); the per-slide substance origin lines (9); the scaffold role list (5) | A card re-skinned with your own topic's entities but a reference's attribute set. **Rows 1 and 6 do not catch this** — row 1 reads strings, row 6 reads art |
| 2 | `lore_or_world_rules` | **9**, 1 | The per-slide substance origin lines (9); the named-entity origin list (1) | A reference world rule restated as a generic principle, or its plot-beat order retold about your topic. **Row 1 alone passes this** — row 9 is the closing artefact |
| 3 | `identifiable_key_art` | 2, 6 | The asset provenance list; the 25% zoom pass | A recognisable silhouette or recreated key visual, generated rather than copied |
| 4 | `reference_copy` | 1 | The named-entity origin list, including renames and translations | A tagline or section stamp carried over, including reworded |
| 5 | `reference_audio` | 7 | The track + licence list | Any unlicensed bed, not only a reference OST |
| 6 | `reference_binaries` | 2, 3, 8 | The asset list; the font + licence list; the `ppt/media/` inventory | A reference plate, preview render or font file shipped inside the package |
| 7 | `reference_identity_system` | 3, 4, 5 | The font licence basis **plus topic reason**; the palette derivation sentence + hexes; the scaffold role list | "Same fonts/palette/chrome as X" **sourced from anywhere** — a brief, the reference deck, the work's branding, a wiki, a handed-over pack. Confirm rows 3–5 name all of those routes; if any one of them says only "a companion brief", item 7 is open |
| 8 | `reference_file_as_base` | **8** | The stated origin file, the master/layout authorship list, the `ppt/media/` diff against row 2, and the `docProps` / theme / embedded-font inspection | Building over an existing deck — reference **or template**. Confirm the pass condition accepts only a new empty deck and carries no "your own template" exception |

If any item has an empty row cell, **§7.1 is not full IP closure and Task 16 fails** — add the missing row to §7.1 before continuing, and update the §7 Ethics group's row count to match. If a cell is filled but its row cannot produce the artefact in the fourth column or catch the failure mode in the fifth, that is the same failure wearing a filled cell: widen the row, do not widen the claim.

- [ ] **Step 9: Walk the Critical closure table by reading, not grepping**

Confirm each row by opening the named section and reading it:

| Leg | Critical finding | Closed by | Confirm by reading |
|---|---|---|---|
| A-1 | §10 companion checklists can override the ban | Tasks 3, 4 | §10.0 precedence list puts §0.0 first and brief observations last; the translation table has a "may NOT take" column covering fonts, hex, chrome, scaffolds, coordinates, assets |
| A-1a | tendays steal row encourages transplanting | Task 3 Step 3 | The tendays row carries an explicit **Do not take** clause |
| A-1b | §7 lacks an ethics item against brief-derived identity | Task 3 Step 5 | §7 Ethics has a `[CRITICAL]` row naming fonts, palette, chrome kit and scaffold |
| A-1c | Companion briefs read as instructions | Task 4 | All six briefs open with the status header |
| B-1 | No factual grounding for 竞聘 | Tasks 5, 6 | Step 0 has `source_packet`; the no-invention rule and `[NEEDS INPUT]` token are defined; §5.4 ledger exists; §7.2 audits it; §7 Truth group has three `[CRITICAL]` rows |
| B-2a | Hard ban incomplete in Step 0 / §8 / §11 | Task 1 | §0.0 enumerates eight items; Step 0 `must_exclude` carries all eight tags; §8 and §11 restate or point, with no shorter third version anywhere |
| B-2b | No IP-leakage audit | Task 2 | §7.1 has **ten** rows, each with a runnable method and a pass condition naming an artefact; Step 8's matrix has no empty cell |
| B-2c | Critical items not labelled | Task 2 | Every §7 bullet carries `[CRITICAL]` or `[RECOMMENDED]` (Step 2 proved it by count); §8's completion clause defines what a Critical failure forbids saying |
| R2-B-1 | §7.1 was incomplete in substance: no paraphrase detection, no entity-structure check, identity checks closed only the companion-brief route, matrix overstated items 1 / 2 / 7 | Tasks 2, 3 | §7.1 row 9 audits per-slide **substance** and names the paraphrase forms explicitly (plot-beat order, world rule as generic principle, episode structure as section structure, arc as spine, near-synonym terminology); row 10 inventories every **multi-entity attribute block** and fails one re-skinned with your own entities; the §7.1 intro defines "reference source" as brief **+** reference file **+** the work **+** wiki **+** template **+** handed-over pack, and rows 3–5 and the Task 3 ethics row all apply to every one of them; Step 8's matrix carries an **Honest limit** / failure-mode column and no longer credits row 1 with items 1–2 or the brief route with item 7 |
| R2-B-2 | §7.1 row 8 allowed "your own template" against §0.0's "start from an empty deck" | Tasks 1, 2 | §0.0 item 8 reads **new empty deck** and names vendor, marketplace and personal templates as banned origins; §7.1 row 8's pass condition accepts only a new empty deck and adds the `docProps` `Template` check; a dedicated paragraph states there is no "my own template" exception and gives the legitimate route (create empty, add your own furniture, account for it in rows 2–3); Step 7 greps `your own template` and expects **0 hits** |

- [ ] **Step 10: Walk the Important closure list**

Confirm by reading: §0.3 is the only slide-budget table, its duration bands do not overlap, **and they leave no duration unassigned at a boundary** — first band inclusive on both bounds, later bands lower-exclusive, with stated handling above and below the mode's range, so 45 s / 3:00 / 5:00 each belong to exactly one row (A, B); §0.2 Mode B density says entrance-only (A); §0.4 gives the Mode D cold-open budget and handoff, and §5.1 points at it instead of naming a rival 3-slide figure (B, A); Step 5 separates living plate from video assist, and §2.1's video row matches `_analysis-local-metrics.md` with a 0–23 full-corpus range (A); §2.4 states ease-out / no bounce / ≤105% with the 110% hard ceiling honestly qualified (A), the ambient vs readable-event rule (B), and the hero (~1–1.5 s) vs readable (~5 s) deadlines that reconcile Step 5's 2.1–2.7 s recipe with the briefs' 2–3 s lockup — with §0.1 point 2 and the Step 8 perform-test bullet both deferring to §2.4 rather than restating their own numbers (A); §10.1 law 8 gives `longest_effect_end + 0.5 s` (B); Step 4 sets the **unconditional** 18 pt Mode C projection floor, with `viewing: close_range` + Mode B as the only exception and no laptop/web escape (B), plus the contrast proxy (A); Step 0 + Step 2 + the trigger table give the advance model with Mode C presenter-held and the enum alignment law (A, B); §2 frames metrics as observations of 7 files / 4 storyboards / 3 static saves, with exceptions (B, A); §5.2 keeps Plan/platform in the core spine with only Diagnosis / Timeline / KPI / Risk duration-gated, Step 2's briefing arc points at it, and the Dropping law's `6 + n` arithmetic reaches the §0.3 floor of 10 instead of landing at 9 (B, A); §7.3 delivery QA exists with **two** font pass paths (B); §7 defines "ship report" exactly once and §7.2 / §7.3 / Task 8's weight rule all use that definition (A); the §7 Truth group states when it is `N/A` and how that is itself checked, so a fact-free Mode A deck has a declared path through it (A); §8 prompt seed covers ban + precedence + grounding + budget + spine + enums + motion + type + gate (A); §7.1 makes the ban testable across all eight items with **ten** rows, including substance (row 9) and structure (row 10) (A, B).

- [ ] **Step 11: Read the guide end to end once**

Check for: broken internal references (a `§x.y` that does not exist), duplicate section numbers, orphaned headings, table rows with the wrong column count, and unclosed code fences. Pay particular attention to the two four-backtick blocks (Task 5 Step 2, Task 6 Step 1) — a mismatched inner fence silently swallows the rest of a section. Fix anything found; make no other changes.

- [ ] **Step 12: Report**

State: files changed (expect 7), the `[CRITICAL]` count, the labelled-bullet count equality from Step 2, the eight-tag counts from Step 3 (every tag ≥ 3), the ban→audit matrix from Step 8 with no empty cell **and no cell claiming more than its row can run**, and one line per Critical confirming closure — including the two round-2 rows (R2-B-1 substance/structure/source coverage, R2-B-2 the empty-deck requirement). If any check failed and you could not fix it within this task, say which — do not report the plan as complete.

**Acceptance check:** All nine new sections present; all regression strings absent; every §7 bullet labelled, proven by equal counts; all eight ban tags present in `must_exclude` and each reporting ≥ 3 because every §7.1 Check cell carries its tags; **all eight ban items mapped to at least one §7.1 row that can actually catch the item's failure mode**, with §7.1 at **ten** rows and row 8 accepting only a new empty deck; both JSON blocks structurally valid; the §8 seed carrying all nine leads; both closure tables walked by reading; and the guide reads start-to-finish without a dangling reference.

---

## Notes for the implementer

- **This is a documentation change.** There is no build, no test suite, and nothing to run but `rg`. The "tests" are the per-task verify steps; run them, do not assume them.
- **Replacements are replacements.** Where a step says *replace*, the old text must be gone. Several verify steps check for exactly that; a duplicated rule is the failure mode this whole plan exists to fix.
- **Keep the guide's voice.** Short declaratives, named failure modes, numbers with units. Do not add hedges, and do not add "consider" or "you may want to" to a rule.
- **Do not edit any companion brief beyond the single header line in Task 4**, and do not move a guide fix into a brief.
- **No commits.** Leave everything in the working tree.
