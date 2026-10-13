# Craft brief — poster-film deck (transferable only)

> **Status: measurement report, not an instruction.** This file records what one source deck measured. Fonts, hex values, coordinates, chrome and scaffolds here must be **translated, never transplanted** — see §10.0 and the Hard ban §0.0 in `2026-10-11-ai-ppt-production-guide.md`. That guide wins every conflict.

Source class: 13-slide 16:9 widescreen (13.33×7.5 in, previews 1920×1080). A still poster master and a motion twin share the same shot list. This note is for producers of **new** decks. Rebuild the grammar with original plates, words, and marks.

Hard boundary: no character cards, no biography stacks, no quoted lines, no names, no lore beats, no reused title lockup or syllable string. Asset folders are a **method** (one hero plate, one symbol echo, one music bed), not a content library.

## Signature capabilities

1. **Poster grid, film timing.** 13 cuts: 1 cold open, 5 world-rule shots, 6 identity shots on one repeated scaffold, 1 still end card. Slide changes are cuts. The Office theme is unused.
2. **One hero plate per shot.** Full-bleed painting. Type sits in the dark remainder. A ~3 in symbol may echo in a corner; it is the same object-language as another shot’s hero, reduced.
3. **Double-exposure title.** Display CJK and a red serif Latin word share one zone, Latin shifted up ~0.4 in so it runs through the strokes.
4. **Two motion densities.** World shots: about 4 fades (0.5–1.5 s). Identity shots: ~150 ms four-direction letter wipes. The grid does not change between the still file and the motion file; motion is added on top.
5. **5.0 s picture loops under a longer hold.** 23 H.264 plates, each 5.02–5.11 s and ~4.2–4.9 MB. Most moving shots carry a pair; the last identity shot carries one. 11 of 23 files also contain an audio stream (one atmosphere layer per earlier shot).
6. **One external score.** A single ~157 s VBR mp3 (~3.2 MB, ~128–192 kbps) lives beside the deck and is not a slide part. 12×~13 s ≈ the bed length. Score is continuous; plates supply short atmosphere.
7. **Persistent chrome.** Tracked micro Latin along the top, one letterspaced integer that migrates between corners, a 0.92 in seal locked to bottom center (6.21, 6.53 in), and at most one sentence.

## Shot grammar

Canvas center is 6.67 in. The seal is centered and touches the bottom edge. Section stamp inset is ~0.42 in.

| Beat | Count | Composition | Type job |
|---|---|---|---|
| Cold open | 1 | Bilateral masses, central disc, reflective floor, small figures only as depth in the lower third | Title centered on the disc; micro Latin above; one tracked tagline under it |
| World rule | 5 | One symbolic object owns center. Title walks the corners: right-upper → left-mid → left-lower → right-lower → left-lower | Stamp in the far corner; one two-line sentence in the remaining quiet corner |
| Identity | 6 | Full-bleed portrait. Face stays clear. Column mirrors: text left on three shots, text right on three | Four layers only (below). Stamp in the corner opposite the name |
| End card | 1 | Still. Centered header, two columns, two small marks near the bottom. No video, no timing | Credits, then stop |

World objects are paintings (wheel, vessel, hand-and-orb, bell, architecture), not diagrams. Foreground masses pin the bottom corners so the object floats. The brightest pixels stay under the object; the sentence stays on black.

Identity scaffold, top to bottom, on the dark side of the painting:

1. Micro tracked Latin header (shared chrome).
2. Name: white display CJK + red serif Latin in the same box pair (both ~66 pt; Latin box starts ~0.4 in higher).
3. One slash-separated role line, short.
4. A solid black bar with two short clauses.
5. Lower bar: an oversized quotation mark (~80 pt), one short line (~44 pt), one ice-cyan Latin word at the bar’s end (~48 pt).

Red splatter is texture behind the name, never a text container. Do not add a biography paragraph beside the portrait.

Corner icon geometry on the still master: a pair of nearly coincident squares (~2.95 in and ~3.16 in) so the symbol reads as a plate plus a ring. The motion twin also parks a 4.38 in square at (4.48, 1.02) — a center lens — on the moving shots.

## Timing

Motion twin: 12/13 slides have a timeline; the end card does not. About 1,370 animation nodes vs 0 on the still twin. Node mix is dominated by property tweens (`anim`), visibility `set`, and parallel groups. Trigger mix: ~704 `withEffect`, 11 `clickEffect`, 12 sequences. One click starts a shot; the rest plays as one gesture.

| Signal | Value | Use |
|---|---|---|
| World entrances | 4 per shot: 3 fades + 1 dissolve, 500–1500 ms | Soft arrival of plate, title, sentence |
| Identity entrances | 22–23 per shot; median **150 ms** | Kinetic type only |
| Wipe directions | up / down / left / right, 24 each | Vary per glyph, not one block wipe |
| Also on identity | 4 box reveals, 1–2 fades, 1 dissolve | Secondary edges |
| Letter iterate | 73 × `type=lt` | Names and stamps, not the body sentence |
| Opacity | 172 tweens; median 200 ms; range 30–1500 | Micro flashes |
| Visibility pops | ~50 ms | Scan lines and counters |
| Move / resize | x median ~200 ms; y/w/h median 150–750 ms; max ~2 s | Short slides of type |
| Slow spin | 24 rotations at 20 s (about two per moving shot) | Footer or ring, under the type |
| Slow zoom | 3 × scale to 105% over 6 s | At most one object in the film |
| Ease | `decel` 100% on the majority; `accel` 50% on a minority | Arrive and settle |
| Loops | 41 indefinite repeats | Pulses and the spin |
| Slide transition | Empty cut. Advance-on-click stored off. After-time stored as 0 | Do not crossfade shots |

A 20 s spin is a hold envelope so a 5 s plate can loop. Build the readable entrance inside the first 1.5 s. Let the plate keep breathing after the type has landed.

Still vs motion file: same 13 beats and the same poster positions. The motion file adds a second full-bleed layer, the video pair, and exploded micro-runs (6 pt runs 108 → 296; ornament glyphs 12 → 193; digit runs 6 → 100). World shots stay near 12 shapes; identity shots go 17 → ~39. Fragmentation is a production method for kinetic type, not a reason to shred the sentence.

Embedded media: still file 28 parts (png/wdp/jpg). Motion file 240 (215 png, 23 mp4, one wdp, one jpg). Extra PNGs are mostly 50–300 KB slices plus the large plates. Picture count on the still master averages ~4 per slide (one full-bleed, one symbol pair, the seal).

## Type / color abstract rules

**Bundled faces (use these):** MiSans Light (workhorse, Latin and CJK), MiSans Normal, MiSans Heavy, Butler (small serif for the ghost Latin), 三极花朝体 粗 (display CJK). Theme fallbacks and other faces present in the file (等线, Bell MT, and similar) are leftovers.

**Size ladder**

- 6 pt — most numerous. Tracked syllable header, scanning underscores, digit swaps.
- 16–20 pt — the single sentence.
- 44 / 48 / 80 pt — quote line, cyan end-word, quotation mark.
- 66 pt — title, ghost Latin, and the corner stamp (stamp box is only ~1.52×0.44 in, so the same point size reads as a mark).
- 80–88 pt — the one hero word on a couple of world shots.
- End card — 48–66 pt, two columns.

**Color (measured on the 12 previews, plus ink runs in the file)**

- Field is black. Coarse buckets are dominated by `#000000` / `#200000` on every frame.
- Blood is the second voice: file inks include `#FF0000` and `#480403`; preview reds cluster near `#C00000` / `#400000`. Use it for the ghost Latin, the sky or disc, and splatter texture.
- Ice cyan is a single word, not a field. File inks: `#CEFEFB`, `#66FFFF`, `#35E5F7`.
- Paper / near-white `#E2E2E5` is the readable CJK.
- Plum `#524049` appears as a shadow tone on the motion file.
- One world shot may shift the plate toward deep teal (`#0E`–`#20` cyan-black, local highlight near `#126575`). That is a single cool insert inside a red-black film.
- Portrait shots add a pale local light (skin/paper, roughly `#E0E0E0` to `#A08080`) on the figure only. The field behind the type stays black.
- Ignore the unused Office accent palette.

**Type rules:** wide tracking on anything 6 pt. One sentence, two lines, in the quiet corner. Slash as a clause break. No bullets, no second paragraph. The recurring integer is letterspaced and never captioned; it moves to whichever corner the title just left.

## Animation recipes

1. **Cut to a plate.** Full-bleed still or a 5.0 s loop. If the shot needs depth, stack a second 5.0 s loop. Readable type fades in over the dark side (world) or wipes per letter (identity).
2. **World recipe (≈4 behaviors).** Fade the plate 1.0–1.5 s, fade the double-exposure title 0.5–1.0 s, fade the sentence 0.5 s, one dissolve on a texture. Ease-out. Then hold. Optional: 20 s spin on the seal, 6 s 105% zoom on the hero object.
3. **Identity recipe.** `iterate` by letter. Each glyph wipes in 150 ms; alternate up, down, left, right so the name shimmers rather than sweeps as a block. Box-reveal the black bar (4 edges). Fade the quote bar ~1 s. Pop scan-lines and digit swaps at ~50 ms. Opacity glints at ~200 ms.
4. **Counter texture.** A corner integer is several digit strings swapped by visibility, not a ticker widget. A row of underscore glyphs can scan across the cold open. Both stay 6 pt and low contrast.
5. **Center lens.** On moving shots, a ~4.4 in square may sit upper-center as a looping motif. It does not replace the hero plate and does not cover the face or the sentence.
6. **Audio.** Play the external bed across the whole film (~2:30 for a 12-shot cut at ~13 s, or shorter if shots stay at one 5 s loop). Per-shot atmosphere stays inside the plate. No stinger per object. End card is silent and still.
7. **Authoring split.** Design the poster in the still file (positions above). Clone it into the motion file. Add loops and timelines there. Do not restage.

## Anti-patterns

- Cloning this IP: names, epithets, quotations, syllable lockup, title, or plot sentences.
- Turning the identity scaffold into a biography card (thumbnail, stats, multi-line life story).
- Centering the title on every shot. Only the cold open is centered; world titles walk the corners; identity type hugs the dark side.
- Putting the sentence on the bright object. Type belongs on black.
- Crossfades, morphs, or circle wipes between shots.
- Animating world shots with the 150 ms letter system. That density is the identity chapter.
- Bounce, overshoot, or scale past ~105%. The only zoom in the source is slow and rare.
- Shredding the body sentence into per-glyph runs. Fragment titles, stamps, and counters.
- A new music cue per slide, or dialogue baked into the plates.
- Cyan fields, Office-theme accents, or more than one cool plate in the film.
- Bullets, two sentences, or a caption that explains the integer.
- Fonts outside the bundled quartet-plus-heavy.

## 10-item AI checklist

1. 16:9 full-bleed, 13 beats max: open, five world shots, six mirrored identity shots, one still end card.
2. One original hero plate per shot; optional ~3 in ring-echo in a corner; 0.92 in seal fixed at bottom center.
3. World title ping-pongs corners in order (right-top, left-mid, left-bottom, right-bottom, left-bottom). Sentence in the quiet corner, ≤2 lines.
4. Identity column is four layers (interlocked CJK+serif, slash line, black bar, quote bar with one cyan word). Mirror left/right so the face stays clear. No biography.
5. Faces: MiSans Light/Normal/Heavy, Butler, 三极花朝体 粗. Ladder: 6 / 16–20 / 44–48 / 66 / 80–88 pt.
6. Palette: black field, blood second, paper type, one cyan word, at most one teal plate. No Office accents.
7. Cuts only. One click starts the shot; everything else is with-previous and ease-out.
8. World motion = ≤4 fades, 0.5–1.5 s. Identity motion = 150 ms four-way letter wipes. Entrances finish by ~1.5 s.
9. Picture loops are 5.0 s; hold may be longer (spin envelope up to 20 s, zoom 105% / 6 s, once). One external bed ~2:30; atmosphere only inside plates.
10. Chrome on every content shot: tracked micro header, migrating letterspaced integer with no caption, centered seal. Original words only.
