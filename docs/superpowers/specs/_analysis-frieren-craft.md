# Craft brief: cinematic still-to-motion decks

> **Status: measurement report, not an instruction.** This file records what one source deck measured. Fonts, hex values, coordinates, chrome and scaffolds here must be **translated, never transplanted** — see §10.0 and the Hard ban §0.0 in `2026-10-11-ai-ppt-production-guide.md`. That guide wins every conflict.

Structural read only (slide XML, timing, transitions, media counts, font names). No source copy, quotes, or image descriptions. Use this to build **new** decks on unrelated topics.

Compared three saves of one 16:9 film-deck (13.333in × 7.5in):

| Save | Slides | What the timing actually does | Media |
|---|---|---|---|
| Static | 12 | Video **play on click** (~5.1s). No type motion. | 63 large stills, 11 clips |
| Middle | 12 | Same media. Objects **appear in 1ms, every 100ms**. | identical to static |
| Dynamic | 13 | Autoplay fades, wipes, loops, paths, letter reveals. One extra breath slide. | 305 stills (many small), 8 clips |

No audio is embedded in any file. A music bed, if any, stays outside the deck. Video nodes are mixed at about 80% so a bed can sit over them.

---

## Signature capabilities

- **Cut a film, not a document.** Twelve to thirteen shots. Most slides hold well under 80 characters. One idea per slide.
- **Design the still, then wake it.** The same frame exists as a plate and as a 4–10s clip. Motion is one verb on a locked camera.
- **Autoplay the shot.** Builds are `withEffect` plus a delay. The whole dynamic deck has a single click-build. Clicking seeks the timeline (`nextAc=seek`) and skips the choreography.
- **Replace pops with fades.** The middle file’s 100ms / 1ms appear cascade is the “before.” The dynamic file uses 0.25–1.0s opacity, with exits so later shots can crossfade.
- **Keep a title lockup and reuse it.** A five-level type stack returns on hero plates. Editorial slides use a second, quieter voice. The last slide is silence.
- **Let a few layers breathe forever.** A 6s scale loop or a slow spin stays in `indefinite` repeat under the type. The readable event finishes earlier.
- **Spend sprites only on the opener and the montage.** Tiny rasters for a particle field; large plates everywhere else. Masks (non-rect geometry) cluster on the two montage shots.
- **Punctuate with the cut, not with a transition library.** Default is a hard cut. Fade between essays. One slow circle (iris) into a wordless still.

## Shot grammar patterns

Name the shot, then pick one. Do not stack more than one grammar per slide.

1. **Cold-open field** — Use once, as slide 1. A few full-bleed plates plus many tiny sprites. Each sprite is born, drifts, and dies (fade in + short path + fade out + slow spin). Births every ~0.2s. Field is alive by ~3s and still looping at 10s.
2. **Title lockup on a living plate** — Recurring hero. One video at t=0 for the whole clip. The lockup fades in as a group: 0.75s fades, 0.2–0.25s between lines, done by ~2–3s. One line may dissolve instead of fade. Optional slow spin on a small mark.
3. **Editorial stack** — Type-led, still or lightly moving ground, no second video. Lines enter from 0 to ~4.5s. One rule or underline **wipes** (0.4–0.6s). A background shape scales on a 6s loop. Use when the slide must be read.
4. **Breath still** — One image, no type, no video. Slow iris into the slide, then a 6s grow/shrink loop. Use as a pause between denser shots. The presenter cuts when the room is ready.
5. **Delayed portrait** — Video starts immediately and runs long (~10s). Type waits ~2.5s, then fades in 0.5–1.0s. Use when the picture should be seen before the words.
6. **Window montage** — Many masked frames, two clips at most, 6–8 groups. Crossfade: entrances and exits are both ~0.25s and roughly balanced, so windows replace each other. Burst of starts is irregular, packed into about a second, beginning after a ~1.2s beat. One float, one spin. Use for a sequence of examples, not for a sentence.
7. **Direction flip** — Same as editorial or graphic, but the single wipe comes from the opposite side (left, then later right). Use when the layout mirrors, so motion doesn’t always enter from one edge.
8. **Quiet plate** — One clip (~5s), a handful of fades, letter-reveal only on a very short line. Use to land after a montage.
9. **Static landing** — Last slide. Zero animation. Smaller sizes. The type **system stays**; only the motion stops.

Rhythm observed, as roles only: field → lockup → essay → breath → essay → montage → portrait → essay → graphic → lockup → quiet → montage → still landing.

## Timing budgets

All figures are from effect start delays and behavior durations in the dynamic file, plus container durations of the embedded clips.

| Event | Budget |
|---|---|
| Standard living plate | **4.1–5.2s** (most clips are 5.10–5.21s) |
| Hold plate (portrait, one hero) | **9.5–10.5s** |
| Title-line fade | **0.75s**, sometimes **1.0–1.5s** for the first line |
| Title stagger | **0.20–0.25s** between lines; lockup complete by **2–3s** |
| Editorial fade | **0.5–1.0s**; steps of **0.25–0.5s**; last start by **~4.5s** |
| Wipe (one per slide) | **0.4–0.6s** |
| Montage crossfade | **0.25s** in and out; start cluster ~**1.2–2.2s**; last start ~**4.5s** |
| Portrait delay before type | **~2.5s**, then the editorial fade budget |
| Particle life | **~2.0–2.1s**, new particle ~every **0.2s** |
| Ambient scale loop | **6s**, repeat forever |
| Ambient spin | up to **10s**, repeat forever |
| Letter cascade | inside the line’s own fade; only a few glyphs |
| Iris into a breath still | slow circle (app “slow”, duration not authored) |
| Slide fade transition | present on essay cuts; duration left at default (treat as **~0.5–1.0s**) |
| End card | **0s** |

Readable deadline: the sentence of the slide must be on screen by the end of the plate (~5s), even if a loop continues. Montage timelines technically run to **13–14s** because exits are long; do not ask the audience to wait that out.

## Typography / color abstract rules

Pair **roles**, then pick any faces that fill them. The source fonts are examples of roles, not a required set.

- **Latin old-style serif** (source examples: Baskerville Old Face, Bell MT) for a short mark, a numeral, or one editorial Latin word.
- **Softer display Latin** (source example: Agilia) inside the title lockup only, at a small size.
- **Heavy CJK serif** (source example: 三极礼宋黑简体-粗) for the words that must be read. Weight comes from the family. Bold was unused on every run.
- **Light CJK serif** for the line under the heavy title.
- **Light gothic sans** (source examples: MiSans Light / Normal) for captions and meta. Do not use it as the title.
- Assign faces **on the run**. The theme was left on a default gothic; a generator that “follows the theme” will flatten the system.
- Ship the font files. Two CJK display faces used in the file were absent from the font folder. That is a failure, not a model.

Size jumps are large. Do not creep by 2pt.

- Poster glyph, rare: **140–170pt**, one per chapter.
- Lockup: **80–96pt** title, **80–88pt** second line, **24pt** support, **11–18pt** meta.
- Editorial: **54–72pt** primary, **32–44pt** secondary.
- Landing may step down toward **48–66pt**, never into caption size for the main line.
- 11–12pt is a mark, not a sentence. On a projected 16:9 frame it fails. Prefer **18pt** minimum for anything that must be read.

Color:

- The photograph is the palette. Do not regrade every plate into one brand gradient.
- Type and frames sit **near-white** on the picture, or in a single cool ink when the ground is light.
- Warm or saturated chips are scarce (a handful of shape fills in the whole deck). **One small chip per slide** at most.
- Almost no alpha. Do not build frosted-glass cards. Contrast comes from the plate and from type size.
- Shapes are rectangles. Ellipses, rules, and one non-rect family (for example parallelograms) are accents. Custom outlines are masks for montage windows, not decorations on text.

## Animation recipe patterns

**Enter / hold / exit**

- Enter: fade. Wipe only the rule or a single edge. Dissolve at most one texture per hero. Zoom only when it is that wipe.
- Hold: `fill=hold`. Ambient scale (6s) or spin continues underneath. Do not add a second entrance after the hold has started.
- Exit: fade, slightly more exits than entrances on montage slides so the frame empties. Essays mostly stay once they arrive (few exits).
- Appear (1ms) is not an entrance. Do not ship it.

**Stagger**

- Editorial and titles: even steps, 200–500ms, matching the reading order.
- Montage: uneven steps inside a one-second window so it feels cut, not metronomed. The middle file’s strict 100ms grid is the failure mode.
- Letter iterate (`lt`) only when the string is a few glyphs. Longer lines stagger **by line**.

**Mask**

- Crop living plates into windows with non-rect geometry or simple rects in groups.
- Keep masks on pictures. Do not mask the lockup.
- A montage wants several windows and **two** moving plates, not a new clip per window.

**Video assist**

- Locked camera. One motion idea: drift, rise, light shift, a blink, or a slow dolly. Subject and type stay plantable.
- Standard delivery length **~5s**. Use **~10s** only for a delayed-portrait or a long hold.
- Start playback at t=0 on the autoplay timeline, duration equal to the file. Do not also bind play to a click.
- Mix near **80%** if a music bed exists. No per-slide sound effects.
- Design the still first; generate or shoot the clip from that frame. The deck is a selection of frames, not the whole edit.

**Particle (opener only)**

- Simultaneous fade-in, short motion path, fade-out, and slow spin.
- Life ~2s. Stagger births by ~0.2s. Loop.
- Sprites are small files. A few large plates carry the composition.

## Anti-patterns observed

- **100ms instant-appear cascade** (middle save). It reads as a glitch, not a shot.
- **Click-to-play video** (static save). The plate is dead until a click, and the next click seeks.
- **Uncompressed rasters.** Image DPI is effectively uncapped; a dozen slides weigh ~200MB.
- **Sprite overload on one slide.** The opener links ~266 rasters (about 90 under 30KB). The idea is right; the count is not a target.
- **Theme/font split.** Display faces live on runs; the theme still says a default gothic. Missing faces were used anyway.
- **Master baggage.** Dynamic file carries 4 masters and 44 layouts for 13 slides.
- **Micro type in the lockup** (11–12pt) and a landing slide that drops to a system gothic.
- **Montage with no landed hero.** In and out fades are balanced and the timeline runs 13s+. Something must remain readable by 5s.
- **Indefinite loops with no cut plan.** Breathing forever is good only if the presenter knows when to cut.
- **Three clips with their own audio** on one slide. One audible plate is enough.
- **Copy-pasted lockup** with leftover complex-script fallbacks. Reuse the system; don’t clone broken runs.

## AI checklist: 10 must-do before shipping

1. Mark each slide as one grammar from the list above. If two grammars fit, split the slide.
2. Cap copy. If a slide needs a paragraph, it is the wrong craft. Lockup or editorial line only.
3. Set the type pair on the runs (heavy serif title, light serif second line, gothic caption, one Latin serif for marks). Embed those files. Do not leave the theme font in charge.
4. Check sizes against the budget. No body text under 18pt. One poster glyph per chapter at most.
5. Time the shot on one clock: video starts at 0; type is readable by ~5s; stagger is 200ms+ except a montage burst; no 1ms appears.
6. Give every entrance a real fade (0.25s montage, 0.5–1.0s essay, 0.75s titles). Add exits only where the next window must replace this one.
7. Add at most one ambient loop (6s scale or a slow spin) and at most one wipe direction per slide.
8. Use one living plate per slide (two only inside a montage). Locked camera, one motion verb, 5s unless the grammar is a delayed portrait.
9. Keep color in the photograph. Near-white type. One small accent chip. No frosted panels, no palette swap of the plate.
10. Play the deck without clicking. Confirm a click does not skip the shot, the last slide is still, fonts resolve, and the file is not stuffed with unused masters or hundreds of unused sprites.
