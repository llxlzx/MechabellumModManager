# Timing recipe signals (EMU/ms from PPT timing XML)

> **Status: measurement report, not an instruction.** These are observations of **seven saved files covering four storyboards** from one genre cluster — not targets, not a scoring rubric. Do not tune a deck toward a number here. See §2 and §10.0 in `2026-10-11-ai-ppt-production-guide.md`, which wins every conflict.

## 十日终焉文件包\十日终焉 - 动画.pptx
node_types_top=[('anim', 1123), ('set', 857), ('par', 773), ('animEffect', 161), ('animScale', 66), ('animRot', 24), ('cmd', 23), ('video', 23), ('seq', 12)]
adv_signals={'auto_tm': 12}
dur_samples n=1487 median_ms≈200 p25≈150 p75≈1000
dur_buckets={'>2s': 41, '<200ms': 678, '1-2s': 335, '500-1000': 215, '200-500': 218}

## 芙莉莲文件包\葬送的芙莉莲_动态(1).pptx
node_types_top=[('par', 385), ('anim', 362), ('set', 359), ('animEffect', 156), ('animScale', 44), ('animMotion', 24), ('seq', 12), ('cmd', 11), ('video', 10), ('animRot', 8)]
adv_signals={}
dur_samples n=650 median_ms≈750 p25≈300 p75≈1021
dur_buckets={'500-1000': 210, '1-2s': 175, '>2s': 85, '200-500': 164, '<200ms': 16}

## 三星堆文件包\三星堆博物馆-动态.pptx
node_types_top=[('anim', 330), ('par', 231), ('set', 161), ('animEffect', 29), ('seq', 23), ('cmd', 2), ('video', 1)]
adv_signals={'auto_tm': 1}
dur_samples n=364 median_ms≈500 p25≈500 p75≈750
dur_buckets={'1-2s': 21, '>2s': 4, '500-1000': 321, '<200ms': 6, '200-500': 12}

## 小王子-动态.pptx
node_types_top=[('par', 114), ('anim', 89), ('set', 76), ('animScale', 63), ('animEffect', 42), ('seq', 11), ('cmd', 4), ('video', 2), ('animRot', 2)]
adv_signals={'auto_tm': 2}
dur_samples n=208 median_ms≈1000 p25≈1000 p75≈2000
dur_buckets={'>2s': 54, '1-2s': 127, '500-1000': 17, '<200ms': 4, '200-500': 6}
