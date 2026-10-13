# Local metrics (craft signals only)

> **Status: measurement report, not an instruction.** These are observations of **seven saved files covering four storyboards** from one genre cluster — not targets, not a scoring rubric. Do not tune a deck toward a number here. See §2 and §10.0 in `2026-10-11-ai-ppt-production-guide.md`, which wins every conflict.

## 三星堆文件包\三星堆博物馆-动态.pptx
- size_mb=248.2 slides=23 timing_slides=22 anim_nodes≈359
- media=83 {'.png': 62, '.wdp': 15, '.jpg': 2, '.jpeg': 3, '.mp4': 1}
- transitions: fade≈4 circle≈0
- top anim density slides: s5:42nodes/15pic/45sp, s14:35nodes/10pic/22sp, s2:29nodes/20pic/15sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.69
- avg_pictures_per_slide≈11.1

## 十日终焉文件包\十日终焉 - 动画.pptx
- size_mb=250.2 slides=13 timing_slides=12 anim_nodes≈1374
- media=240 {'.png': 215, '.wdp': 1, '.jpg': 1, '.mp4': 23}
- transitions: fade≈0 circle≈0
- top anim density slides: s7:175nodes/5pic/39sp, s8:175nodes/5pic/39sp, s9:175nodes/5pic/39sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.88
- avg_pictures_per_slide≈5.2

## 十日终焉文件包\十日终焉.pptx
- size_mb=236.2 slides=13 timing_slides=0 anim_nodes≈0
- media=28 {'.png': 24, '.wdp': 3, '.jpg': 1}
- transitions: fade≈0 circle≈0
- top anim density slides: s1:0nodes/8pic/9sp, s2:0nodes/6pic/11sp, s3:0nodes/4pic/11sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.62
- avg_pictures_per_slide≈4.4

## 小王子-动态.pptx
- size_mb=114.9 slides=12 timing_slides=9 anim_nodes≈196
- media=55 {'.wdp': 9, '.png': 43, '.jpg': 2, '.mp4': 1}
- transitions: fade≈10 circle≈0
- top anim density slides: s4:43nodes/7pic/28sp, s3:35nodes/14pic/13sp, s5:28nodes/8pic/27sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.87
- avg_pictures_per_slide≈8.0

## 芙莉莲文件包\葬送的芙莉莲.pptx
- size_mb=203.4 slides=12 timing_slides=7 anim_nodes≈0
- media=80 {'.png': 63, '.wdp': 5, '.jpg': 1, '.mp4': 11}
- transitions: fade≈0 circle≈0
- top anim density slides: s1:0nodes/20pic/17sp, s2:0nodes/4pic/8sp, s3:0nodes/12pic/5sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.76
- avg_pictures_per_slide≈6.5

## 芙莉莲文件包\葬送的芙莉莲2.pptx
- size_mb=203.5 slides=12 timing_slides=11 anim_nodes≈0
- media=80 {'.png': 63, '.jpg': 1, '.wdp': 5, '.mp4': 11}
- transitions: fade≈0 circle≈0
- top anim density slides: s1:0nodes/20pic/17sp, s2:0nodes/4pic/8sp, s3:0nodes/12pic/5sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.76
- avg_pictures_per_slide≈6.5

## 芙莉莲文件包\葬送的芙莉莲_动态(1).pptx
- size_mb=198.4 slides=13 timing_slides=12 anim_nodes≈594
- media=319 {'.png': 305, '.wdp': 5, '.jpg': 1, '.mp4': 8}
- transitions: fade≈5 circle≈2
- top anim density slides: s1:134nodes/22pic/15sp, s12:117nodes/33pic/20sp, s6:94nodes/31pic/10sp
- text_fragmentation_ratio(short<=2 / all_counted)≈0.96
- avg_pictures_per_slide≈9.4

## Cross-pack craft hypotheses (for AI guide)
1. Dynamic tier = high timing coverage + media density; static twin = same storyboard fewer nodes.
2. Exhibition pack (Sanxingdui) spreads more slides (chapter pacing); PV packs stay ~12-13 beats.
3. Text often split into glyph/word fragments for kinetic type — AI should plan text as beats not paragraphs.
4. Video (.mp4) used as motion assist on key emotional beats, not every slide.
5. WDP/PNG stacks imply layered composites; AI should think layers: bg / mid / subject / type / grain.
6. Widescreen 13.33x7.5; design full-bleed first.