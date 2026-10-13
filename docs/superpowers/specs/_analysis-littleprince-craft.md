# Transferable craft — illustrated Morph deck

> **Status: measurement report, not an instruction.** This file records what one source deck measured. Fonts, hex values, coordinates, chrome and scaffolds here must be **translated, never transplanted** — see §10.0 and the Hard ban §0.0 in `2026-10-11-ai-ppt-production-guide.md`. That guide wins every conflict.

Source: `小王子-动态.pptx` (12 slides, 13.333×7.5 in). Craft only. Do not reuse its wording, characters, or plot as a script. Abstract themes (power, vanity, appetite, and their swaps) are layout roles, not lines to copy.

Canvas is widescreen. Real transitions are Morph `byObject` (`p159:morph`, `p14:dur` 2000 ms, one slide 2250 ms, `spd="slow"`), wrapped in `mc:AlternateContent` with a slow fade fallback. Slides 1 and 4 are hard cuts. Media: cutout PNGs, a few HD-photo crops, one short video. Embedded voices: 仿宋 (display), Gigi (latin gloss), 等线 / 等线 Light (theme fallback only).

## Signature capabilities

1. **Selective Morph identity.** An object travels only if its name matches across the pair. Rename it and Morph fades it in or out instead of flying it. Shared-name counts: collage settle 11/12, vertical relay 11/28 and 6/13, locked triptych 35/39, stage exit 31/36, late plates 1. Prefix survivors (`PA-` in this file). Never duplicate a name on one slide.
2. **One-viewport relays.** Matched plates move about one frame: previous line exits up, next plate is parked just below (or the whole rig exits left while a full-bleed plate enters from the right). Geometry that must stay put is left on the same coordinates.
3. **In-place word Morph.** On the triptych, panel geometry is frozen and only the noun inside a matched box changes, so the word itself interpolates. Both languages have to change together or the gloss stays on the old concept.
4. **One cubic silhouette, instanced.** Slides 4–6 are ~10 MB each because one path (~460 moves, ~5,200 cubics) is pasted six times, then rotated. The craft is the repeated needle, not unique drawings. Thin and taller than the frame (about 0.17×8.9 in).
5. **Glyphs are objects.** A line is many text boxes, not a paragraph. Each box has its own fade, 10% rise, scale ease-out, and stagger. Spatial fragmentation and temporal stagger are the same idea.
6. **Two-beat shot.** Morph (2 s) moves scenery and retires the previous line. A click then writes the new line. Entrance effects hide those glyphs until the click, so do not also expect Morph to reveal them.
7. **Living stills.** Gold 4-point stars and 0.1 in dots twinkle on irregular offsets. A few cutouts drift 2% of the slide and rotate 4° over 2 s. Nothing spins, nothing uses a motion path.

## Shot grammar

Read as shot types, not as a storyboard.

- **Bleed collage.** No type, no timing. Cutouts larger than the frame, some rotated, a few with shadow. Hard cut.
- **Settle.** Same names, positions pull inward. Morph 2 s. Type and a video twin appear only after the click.
- **Vertical relay.** Previous cluster leaves upward by about one frame. Next line is offstage below on the way in, then writes on click at 100 ms per glyph.
- **Hard cut into a triptych.** Three full-height trapezoids fanned about ±23° around 180°. One two-character abstract noun per panel, gloss tucked under it, both rotated to the panel. Stars and dots already in the field.
- **Word swap.** Same rig, nouns replaced in matched boxes. Pictures pop or drop. Twinkles continue.
- **Stage left.** Named rig translated off the left edge; a full-bleed plate that was waiting past the right edge lands at x=0. No click animation. This slide is a transition, not a reading slide.
- **Glyph column.** One character per box, size stepping down, x jitter, y walking down the frame. After the cascade, two or three tall shapes wipe left (0.5 s).
- **Phrase stack.** Longer thought breaks into short boxes (about 1–8 characters), tracking tightened, one size accent inside the line. Stagger widens.
- **Breath plate.** Full-bleed only, one shared name so a single plate travels, then a 0.5 s auto-advance.
- **Title reprise.** Same three-glyph lockup plus script-face subtitle, shifted off center. Bookend of the opening lockup.

## Timing

- Morph length is `p14:dur`, not the word “slow”: **2000 ms** default, **2250 ms** on one relay. Fade fallback uses `spd="slow"` only.
- Type is **click-gated** (`delay="indefinite"` on the main sequence), then `withEffect` groups.
- **Title cadence 200 ms** (gloss at 0, then each glyph). **Column cadence 100 ms**, first glyph often at 300 ms so it clears the morph. **Phrase cadence 300–600 ms**, last box as late as 2000 ms. Each glyph then animates for **1000 ms**.
- Twinkle delays are **irregular** (113, 150, 174, 275, 396, 518, 736 ms), durations **1500 or 2000 ms**, auto-reverse.
- Cutout pops stagger roughly **0 / 200 / 300 / 500 ms** (scale 900 ms). Photo drops stagger **0 / 400 / 800 ms**.
- Side drift starts at **69 ms**, move 500 ms, rotation 2000 ms.
- Video: `playFrom(0.0)` for **5410 ms** on the same click as the first beat, plus a `togglePause` call. Poster still shares the video rect.
- Auto-advance **500 ms** on two plates, one of them with click disabled. That is shorter than the 2000 ms morph, so the move is clipped.

## Type rules

- One thought per slide. No paragraphs, no bullets, no notes.
- Display is **仿宋**, ink `#1D0B09`. Latin gloss is **Gigi**, centered, scheme background (light), no shadow. Theme 等线 is leftover on empty shapes; do not set voice in it.
- **Mode A — one glyph, one box.** Title lockup ~166 pt. Body staircase roughly 138 → 115 → 96 → 88 → 80 → 72 → 66 pt, not a single style. Boxes are oversized (title box ~3.1×2.9 in) and overlap; the letter sits in the box, it is not a tight em-square.
- **Baseline is broken on purpose.** Box y is hand-offset so the line feels set, not typed.
- **Mode B — short phrase.** 44–96 pt, `spc="-300"` (−3 pt tracking). One run inside the box jumps size (the accent word). Still shadowed.
- **Gloss is a caption, not a column.** Script face at 24–40 pt, under or above the CJK, center-aligned. On panels, gloss rotates with the noun.
- **Shadow recipe** (display and gold points only): outer shadow, blur 14 pt, distance 3 pt, direction 45°, same ink at 96% alpha, `rotWithShape="0"`. It separates type from photography. Do not shadow the light gloss.
- Panel labels: two-character abstract noun at 54 pt, gloss at 24 pt, center-aligned, rotation matching the panel (~−32°, 0°, +24°).

## Animation recipes

All of these are one click, then delays. Scale percentages in the file are thousandths (`100000` = 100%). `ppt_y` is a fraction of slide height.

1. **Glyph rise.** Set visibility. Fade 1000 ms. `ppt_y` from `#ppt_y-0.1` to `#ppt_y` (rise ~0.75 in). Scale to 100% with decel 100%. Stagger 100 or 200 ms.
2. **Gloss fall.** Same stack, but `ppt_y` from `#ppt_y+0.1` (drops in). Move decel 50%, scale decel 100%. Start with or one beat before the first glyph.
3. **Twinkle.** Opacity 0→1, auto-reverse, accel 30% / decel 70%, 1.5–2.0 s. Simultaneous scale to 120%, auto-reverse, same duration. Apply to a subset of stars and dots. Offsets must not be a round grid.
4. **Pop cutout.** `ppt_w` and `ppt_h` keyframes `0 → 0.8 → 1` in 900 ms. Opacity 0→1 in 50 ms so the scale reads immediately. Stagger ~200 ms.
5. **Drop settle.** `ppt_y` keyframes `+1.2h → +0.25h → 0` in 1000 ms. Opacity 300 ms. Stagger 400 ms.
6. **Side drift.** `ppt_x` by +2% of the slide, 500 ms, decel 100%. Opacity 500 ms. Scale to 100%. Optional `animRot by="240000"` (4°) over 2000 ms. No motion paths anywhere in this deck.
7. **Wipe accent.** `wipe(left)`, 500 ms, after the glyph cascade (about 1000 and 1200 ms). Use on two or three tall shapes, not on type.
8. **Morph scene.** `byObject`, 2000 ms. Match names to move; rename to fade. Move matched objects by about one viewport, or leave them pixel-locked when only the string should change. Keep a fade fallback for hosts that cannot morph.
9. **Video twin.** Still and video on the same rectangle. `playFrom(0.0)` ~5.4 s, fired with the first type beat, not with the morph.

## Anti-patterns

- Auto-advance of 500 ms against a 2000 ms morph. The relay never finishes. If the slide must advance itself, set the hold longer than the morph.
- Stale gloss. The noun box was rewritten; the matched latin box was not. Morph will not correct a string you forgot to edit.
- Duplicate names on one slide (two boxes sharing one `PA-` name). `byObject` then pairs the wrong object.
- Parking a glyph offstage and also giving it an entrance. The entrance hides it, so the morph never shows that glyph. Choose one driver: Morph for scenery and exits, entrance-stagger for the line being read.
- Six full copies of a 5,000-cubic path. Repeat the silhouette, but do not duplicate the path data if the file has to stay light.
- Treating Morph as the only readable state. Unmatched objects fade; a hard cut (slides 1 and 4) is how a new world starts. Information that matters must still read as a still.
- Click-gated type after a 2 s morph looks unfinished in an auto-running export. That beat is intentional only when a person is advancing.
- Light Gigi on a photograph with no shadow. It only works as a quiet caption on a dark area. Do not use it for the primary noun.
- Letting 等线 18 pt default formatting leak onto decorative shapes. Voice is the 仿宋 / Gigi override.

## 10-item AI checklist

1. Morph `byObject` at ~2 s, with a fade fallback, and any auto-advance longer than that morph?
2. Does every traveling object have one stable unique name, and every object that should fade have a new name?
3. If only a word changes, is the box geometry identical, and did both the noun and the gloss change?
4. Is the line split into boxes (one glyph, or a short phrase), with a size staircase and a broken baseline?
5. Is the CJK entrance rise + fade + scale ease-out, staggered at 100 ms (column) or 200 ms (title)?
6. Does the latin gloss drop the opposite way, in a script face, smaller, uns shadowed?
7. Are sparkles an irregular auto-reverse twinkle (opacity plus 120% scale), not a synchronized blink?
8. Do cutouts bleed past the frame, and do new ones pop (50 ms opacity, 900 ms scale) or drop-settle, never along a diagonal motion path?
9. Is there one abstract noun per panel, rotated with the panel, and no sentence on that slide?
10. Does the frame still read if Morph and the click cascade are turned off? If not, the composition is not finished.
