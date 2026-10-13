# Exhibition craft — museum dynamic deck (transferable)

> **Status: measurement report, not an instruction.** This file records what one source deck measured. Fonts, hex values, coordinates, chrome and scaffolds here must be **translated, never transplanted** — see §10.0 and the Hard ban §0.0 in `2026-10-11-ai-ppt-production-guide.md`. That guide wins every conflict.

Source measured: 23-slide 16:9 deck (`12192000×6858000` EMU), one embedded film, a separate metal-relief texture folder, one display-font pack. Use this to build **new** cultural / exhibition decks. Do not clone motifs, reuse the plates, or paste artifact essays.

Exhibition and anime-PV share motion literacy (stagger = reading order, ease-out = arrival, fade/wipe = hierarchy, hold when the beat is done). They spend it on opposite goals. A PV performs. A gallery **lights a room and then gets out of the way**. This deck’s timing XML makes that concrete: median span **500 ms**, p75 **750 ms**, **321/364** spans sit in 500–1000 ms, only **6** under 200 ms. No motion paths. No rotation as a design move. One video. The visitor advances the room.

## Signature capabilities

1. **Route before objects.** Threshold film → four-beat poetic lockup → where-you-are → time spine → three chapter doors → a repeated room module → exit plinth. Chapters are a path through the building, not a table of contents dumped as bullets.
2. **One claim per room.** Interior slides are a vitrine, a material essay, or a belief meditation. The frame stays; the object or idea changes. Metadata is chips (era, grade, pager), not the story.
3. **Metal is the wall, not the subject.** Texture, duotone, and overflow crops put every photo in the same bronze room. The picture in the case stays the only figure.
4. **Monument type, quiet sentence.** One display face for the threshold word, a song/serif for titles, a light gothic at body size for the single sentence, a second voice only for the label.
5. **Entrance-only lighting.** Each slide is one chord (`withEffect`), then a hold. Morph (~2 s, by object) is the door between chapters. Body slides cut. Dwell is manual.

## Shot grammar

Read the deck as rooms, not as a cut timeline.

| Room | What the frame does | What it refuses |
| --- | --- | --- |
| Threshold | Full-bleed film, almost no type. Slow 1.5 s transition. | Titles on top of the moving image |
| Lockup | Stacked full-bleed grounds. Glyphs center. Institution name is a small plate that wipes in after the line. | A logo slam or a trailer title card |
| Preface | Left half is field; a column holds place, date, one institutional idea. | A map infographic |
| Wayfinding | One address, one distance. A label, not a chapter. | Multi-column facts |
| Time spine | A full-width rule near mid-height (~5% tall). Events hang off it, one line each. | A vertical essay of dates |
| Three doors | Index of the route: numeral, poetic title, one-line promise, plus “order of visit.” | Equal cards for every subtopic |
| Chapter monument | Four (or so) huge characters, split, plus a short thesis and a chapter index. | A sentence set in the display face |
| Landing | Sibling chapters stay visible. Pinyin/latin is a quiet eyebrow. One paragraph. A room tag (floor / hall), not a UI nav bar. | Hiding where you are |
| Vitrine | Stable scaffold: crumbs at top (~4% / 14% / 23% x, ~2% y), three sibling pills at the bottom (~36% / 46% / 56% x, ~77% y), object, claim, chips, 1/2/3 pager. | A new layout per object |
| Material / belief | Two-character craft or idea title, one sentence, image bleeding off the right or bottom (often from ~39% x, taller than the slide). | A grid of specimens |
| Exit | Same metal ground. Thanks plus institution. A plinth band along the bottom (~y 82%, full width). | A credit roll |

Framing habits to reuse on new subjects:

- **Two to three full-bleed fields** (ground, relief, veil), then one cased picture. This file stacks more; new decks should stop at the veil.
- **Overflow crop.** Plates often start at negative origin and run 150–240% of the slide, so the frame cuts a detail. The wall has no edge.
- **Crumbs never leave.** Once inside, the three chapter names remain. Orientation is part of the shot, not a menu slide you revisit.
- **Geometry is rect.** Hundreds of rectangles, a few ellipses, rare plaques. Ornament comes from texture and type, not from shape effects.

## Timing

All figures from this deck’s animation XML. Delays are on the entrance node; durations are the inner spans.

- **Who moves:** entrance only. `fill=hold`. No exit, no emphasis loop, no path.
- **How it is triggered:** one chord per slide (`withEffect` / with previous). The only click is the opening film’s media call (~**21.6 s**). Do not autoplay the gallery (`advTm` is 0; the visitor holds).
- **How long:** live in **500–750 ms**. Fade often **500 ms**. A soft scale-settle, when used, is **750 ms**. A name-plate wipe can run **~1250 ms** but only after the line has already appeared.
- **Stagger:** glyphs **100 ms** apart (a short word finishes inside a second). Stacked labels and catalog doors **250 ms** apart. A wall of doors may cascade ~3 s; that is the long end. Do not go faster to “feel dynamic.”
- **Ease:** fade nodes are almost all ease-out — `decel=100%` (dominant) or `accel=30%` + `decel=70%`. Motion arrives and rests. No overshoot bounce.
- **Doors vs rooms:** chapter thresholds (lockup, later monuments, exit) use **Morph by object, 2000 ms**, with a fade fallback. Opening film uses a **1500 ms** slow transition. Interior slides have **no transition** (a cut into the next case).
- **Shared with PV literacy:** stagger follows reading order; the last thing to move is the smallest label; nothing keeps animating after the hold.
- **Not shared:** PV packs in the same study cluster pile up sub-200 ms hits, paths, and rotation. This deck almost never leaves the 500–1000 ms band, and it never flies an object across the room.

## Type / color abstract

**Pack name only:** `字灵梨花香` (the display pack beside the deck). One job: the split threshold word. Do not set body, captions, or UI in it.

Role system (swap in licensed faces with the same jobs; do not treat the embedded commercial faces as the only legal choice):

| Role | Size (pt) | Behavior |
| --- | --- | --- |
| Monument | 115–166 | Characters separated. Chapter door only. |
| Poetic lockup | ~80–96 | The show’s one line, then stop. |
| Chapter / section title | 36–72 | Song or serif. Short. |
| Object or idea name | ~48 | The label on the case. |
| Body sentence | **14** | Light gothic. One sentence. |
| Chips, crumbs, pager | 10.5–14 | Tracked, quiet. Latin/pinyin is an eyebrow, not karaoke. |
| Label voice | ~14–20 | A second, more handwritten or kai-like face, only for the “story” chip — never for paragraphs. |

**Palette, painted not themed.** The file still carries a default Office theme (blue accent). The show ignores it. Paint these roles explicitly:

- Ground / lacquer: `#192A22`, `#130C04`, `#211205`, `#2A4639`
- Patina: `#163A21`, `#225A33`, `#477A56`, `#619E75`, `#C2E49C`
- Gilt: `#FFF5CB`, `#FDF3C9`, `#F7D490`
- Cinnabar, rare: `#811500`, `#FF8B00` — one spark per chapter door, not a second palette
- Paper white only for type that must read: `#FFFFFF` / `#FEFFFD`

**Texture strategy (metal / relief atmosphere).** The folder is ten copper-plate line plates (about 1.5–2.9k, plus two ~6.7k heroes). Measured samples sit in near-black bronze (`#1A0E05` down to `#000000`) with thin gilt lines (`#C09D5A`–`#FFCE86`), not flat gold fills. Floral and cloud line-work is high-frequency edge energy; centers are medallions or quiet field.

Use the strategy, invent new plates for the subject you actually have:

- Relief is a **wall material**: full-bleed or edge-cropped underlay.
- Knock it back to the deck’s own veil range: alpha about **5–20%** common (`alphaModFix` 5%, 9%, 20%), up to ~35–50% only for a quiet ground with no competing photo.
- Bind real photos with duotone into the same metal: **black → gilt cream** (`#FFF5CB`, tinted), or a **shaded green → white**. Do not leave museum photos in untreated full color on this ground.
- Shadows are soft and often the ground pigment itself (`#192A22` at ~74%), or black at 40–62% with a small blur (~4–9 pt) for contact and a large blur (~60 pt) for atmosphere. Not a hard drop shadow.
- The figurative drawings in this folder (masks, beasts, suns) are **not** a sticker set. A new deck draws non-literal tooling for its own subject. If a real object photo exists, it occupies the case; the plate never does.

## Animation recipes

All of these are entrances, with previous, then hold. Times are the ones to copy.

**R1 — Glyph dawn (title line).** Each character fades (preset fade, 100% ease-out) and eases in. Start together as a chord, delay **+100 ms** per character. Opacity ~500 ms. Optional height/width ease on the same span. After the last glyph, wait until ~**1250 ms**, then wipe the institution name from the left (~1250 ms). Nothing else moves.

**R2 — Rise into the case (default body move).** `y` from `#ppt_y + 5% of slide height` to rest, **500 ms**, linear keys under an ease-out parent, plus opacity **500 ms**. That is a breath (~0.4 inch on this 7.5 inch slide), not a fly-in from offscreen. Use on the sentence, the chips, the crumbs.

**R3 — Left engrave (rules and names only).** Wipe from the left (wipe preset, subtype from the left). **500 ms** on a timeline rule or a thin label; longer only for the lockup’s name plate (R1). Do not wipe photographs.

**R4 — Focus settle (hero image, optional, once per room).** Scale from about **2.1×** to **1.04×** at 45% of the span, then to **1.0** by **750 ms**, with a fade. It finds focus. It does not punch past 1.0 and bounce. Skip it on labels.

**R5 — Door cascade (index only).** Pairs of title + card, **+250 ms** each, each pair using R2. Stop by ~3 s. This is the longest interior motion in the file.

**R6 — Chapter door (transition, not an effect).** Morph by object, **2000 ms**, only when the monument layout and the next room share objects (ground, crumbs, giant type). Interior cuts. Do not morph every slide or the building never arrives.

**R7 — Threshold film.** One full-bleed video, type absent, ~15–25 s, advanced by click, then a 1.5 s slow fade into the lockup. It is a lobby, not a soundtrack under later slides.

## Anti-patterns

- **Encyclopedia slides.** This file’s object rooms sometimes grow into long interpretive paragraphs, and one later room repeats another theme’s sentence. Keep the vitrine; cut to one claim. Chips may hold era and grade. Paragraphs may not hold the catalog.
- **PV timing on a gallery.** Sub-200 ms slams, motion paths, spins, exit-then-enter on every element, beat-synced autoplay, impact zoom with overshoot. The shared literacy is stagger and ease-out, not impact.
- **Morph as a universal transition.** Here it is the chapter door. On body slides it makes the case feel unmoored.
- **Click-by-click bullet builds.** This deck does not use them. One chord, then dwell.
- **Display face as the body face.** The pear-blossom pack is the monument. Body is a light gothic at 14 pt.
- **Texture as a second artifact.** Full-opacity figurative relief, centered, fighting the real photo. Also: cloning this folder’s masks and beasts onto an unrelated topic.
- **Untreated photos and the Office theme.** Blue accents and full-color snapshots break the metal room. Duotone or don’t use the picture.
- **A new composition every slide.** The repeated crumbs, pills, and vitrine are the craft. Novelty belongs in the object, not the scaffold.
- **Five full-bleed plates.** The file is heavy with stacked fields. Two or three layers (ground, relief, veil) plus one case photo.
- **Type on the film, karaoke pinyin, equal-weight centered everything.** Latin eyebrows are indexes. The film is empty. Hierarchy is the shot.
- **Motion that continues.** No idle float, no pulsing glow, no exit. When the light is up, stop.

## 10-item AI checklist

1. Write the route in three chapters before any object, each chapter a threshold word plus one promise.
2. Open on a dark threshold with no type; the first words are the poetic lockup, then the institution name (R1).
3. Give every interior slide one claim. Object rooms reuse one vitrine and change only the object, the claim, and the chips.
4. Keep the three chapter crumbs on every interior slide, plus a quiet room tag where a floor or hall matters.
5. Paint ground, patina, gilt, and one cinnabar spark. Duotone photographs into black → gilt or green → white. Ignore default theme blues.
6. Use metal relief as wall only: gilt line on near-black, 5–20% opacity or cropped off-frame. Invent new tooling. Do not paste this pack’s figures.
7. Animate entrance-only, with previous: 100 ms glyph stagger or 250 ms stack stagger; fade 500 ms ease-out; rise 5% over 500 ms; left wipe only for rules and the name.
8. Morph ~2 s only on chapter doors. Cut inside the chapter. Do not auto-advance.
9. Set the monument face on the threshold word only; song/serif on titles; light gothic at 14 pt for the sentence. Pack name for the monument job: `字灵梨花香` or an equivalent display face you have rights to.
10. End the chord and hold. No second beat, no exit, no credit roll. The last slide is an exit plinth on the same ground.
