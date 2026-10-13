# Bocchi-style PPT Capability Demo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a self-contained 7-slide fullscreen web demo with Bocchi-the-Rock–inspired band aesthetics to show animation ceiling for the user.

**Architecture:** Static `index.html` + `styles.css` + `app.js`. A tiny pure `createDeckController` state machine drives slide index, autoplay, and reduced-motion; CSS classes handle enter/exit. No build step, no external assets.

**Tech Stack:** HTML5, CSS3 (keyframes, clip-path), vanilla JS (ES modules optional — prefer IIFE/single script for file://), Node for unit-testing the controller only.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Deliver under `demos/bocchi-ppt-demo/` only; do not change Mod Manager app code
- No official art, screenshots, or music
- Colors: bg `#0E0E10`, pink `#FF4FA3`, cyan `#3DE0FF`, yellow `#FFE566`, purple `#B8A0FF`, text `#F5F5F7`
- 7 slides (indices 0–6); autoplay ~45–60s total; Space/click/arrows; pause; F fullscreen
- Honor `prefers-reduced-motion: reduce` with fade-only transitions
- Offline, zero image dependencies
- Spec: `docs/superpowers/specs/2026-10-10-bocchi-ppt-capability-demo-design.md`

## File Structure

| File | Responsibility |
|------|----------------|
| `demos/bocchi-ppt-demo/deck-controller.js` | Pure state machine (testable) |
| `demos/bocchi-ppt-demo/deck-controller.test.js` | Node assert tests for controller |
| `demos/bocchi-ppt-demo/index.html` | Slide markup + chrome |
| `demos/bocchi-ppt-demo/styles.css` | Visual system + transitions |
| `demos/bocchi-ppt-demo/app.js` | Wire controller to DOM, timers, keys |
| `demos/bocchi-ppt-demo/README.md` | How to open + shortcuts |

---

### Task 1: Deck controller + tests

**Files:**
- Create: `demos/bocchi-ppt-demo/deck-controller.js`
- Create: `demos/bocchi-ppt-demo/deck-controller.test.js`

**Interfaces:**
- Consumes: none
- Produces:
  - `createDeckController({ slideCount, onChange })` → `{ getIndex, next, prev, goTo, play, pause, isPlaying, dispose }`
  - `onChange(index, meta)` called on every navigation; `meta` is `{ reason: 'next'|'prev'|'goto'|'auto'|'init' }`
  - Autoplay: while playing, advance every `durationsMs[index]` (array length = slideCount); after last slide, pause and stay on last

- [ ] **Step 1: Write the failing test**

```js
// demos/bocchi-ppt-demo/deck-controller.test.js
const assert = require('assert');
const { createDeckController } = require('./deck-controller.js');

function testInitial() {
  const events = [];
  const c = createDeckController({
    slideCount: 7,
    durationsMs: [100, 100, 100, 100, 100, 100, 100],
    onChange: (i, meta) => events.push([i, meta.reason]),
  });
  assert.strictEqual(c.getIndex(), 0);
  assert.strictEqual(c.isPlaying(), false);
  c.dispose();
}

function testNextPrev() {
  const c = createDeckController({ slideCount: 3, durationsMs: [9999, 9999, 9999], onChange() {} });
  c.next();
  assert.strictEqual(c.getIndex(), 1);
  c.prev();
  assert.strictEqual(c.getIndex(), 0);
  c.prev();
  assert.strictEqual(c.getIndex(), 0);
  c.goTo(2);
  assert.strictEqual(c.getIndex(), 2);
  c.next();
  assert.strictEqual(c.getIndex(), 2);
  c.dispose();
}

function testAutoplayAdvancesThenStops() {
  return new Promise((resolve, reject) => {
    const seen = [];
    const c = createDeckController({
      slideCount: 3,
      durationsMs: [30, 30, 30],
      onChange: (i) => seen.push(i),
    });
    c.play();
    setTimeout(() => {
      try {
        assert.ok(seen.includes(1) && seen.includes(2), 'expected autoplay to reach last slide');
        assert.strictEqual(c.getIndex(), 2);
        assert.strictEqual(c.isPlaying(), false);
        c.dispose();
        resolve();
      } catch (e) {
        c.dispose();
        reject(e);
      }
    }, 200);
  });
}

async function main() {
  testInitial();
  testNextPrev();
  await testAutoplayAdvancesThenStops();
  console.log('ok');
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `node demos/bocchi-ppt-demo/deck-controller.test.js`  
Expected: FAIL (cannot find module or `createDeckController` missing)

- [ ] **Step 3: Write minimal implementation**

```js
// demos/bocchi-ppt-demo/deck-controller.js
function createDeckController({ slideCount, durationsMs, onChange }) {
  if (!slideCount || slideCount < 1) throw new Error('slideCount required');
  const durs = durationsMs || Array(slideCount).fill(7000);
  let index = 0;
  let playing = false;
  let timer = null;

  function emit(reason) {
    if (typeof onChange === 'function') onChange(index, { reason });
  }

  function clearTimer() {
    if (timer != null) {
      clearTimeout(timer);
      timer = null;
    }
  }

  function schedule() {
    clearTimer();
    if (!playing) return;
    if (index >= slideCount - 1) {
      playing = false;
      return;
    }
    const wait = durs[index] != null ? durs[index] : 7000;
    timer = setTimeout(() => {
      index += 1;
      emit('auto');
      if (index >= slideCount - 1) {
        playing = false;
        clearTimer();
      } else {
        schedule();
      }
    }, wait);
  }

  return {
    getIndex: () => index,
    isPlaying: () => playing,
    next() {
      if (index < slideCount - 1) {
        index += 1;
        emit('next');
        if (playing) schedule();
      }
    },
    prev() {
      if (index > 0) {
        index -= 1;
        emit('prev');
        if (playing) schedule();
      }
    },
    goTo(i) {
      const n = Math.max(0, Math.min(slideCount - 1, i | 0));
      if (n !== index) {
        index = n;
        emit('goto');
        if (playing) schedule();
      }
    },
    play() {
      if (playing) return;
      playing = true;
      schedule();
    },
    pause() {
      playing = false;
      clearTimer();
    },
    dispose() {
      playing = false;
      clearTimer();
    },
  };
}

module.exports = { createDeckController };
```

- [ ] **Step 4: Run test to verify it passes**

Run: `node demos/bocchi-ppt-demo/deck-controller.test.js`  
Expected: prints `ok` and exit code 0

---

### Task 2: HTML shell + CSS visual system + slides 0–4

**Files:**
- Create: `demos/bocchi-ppt-demo/index.html`
- Create: `demos/bocchi-ppt-demo/styles.css`

**Interfaces:**
- Consumes: none yet (static markup)
- Produces: DOM contract for Task 3:
  - `#deck` root; each slide `.slide[data-slide="0"…"6"]`
  - Active slide has class `is-active`; previous exit may use `is-exit`
  - `#noise` overlay; `#chrome` with `#slide-num`, `#btn-play`, `#btn-fs`
  - Slide 4 contains `.iris` element for circular reveal

- [ ] **Step 1: Create `index.html` with 7 slides and chrome**

Use this structure (copy verbatim as baseline):

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>能力试水 · 乐队风 PPT Demo</title>
  <link rel="stylesheet" href="styles.css" />
</head>
<body>
  <div id="noise" aria-hidden="true"></div>
  <main id="deck" tabindex="0">
    <section class="slide is-active" data-slide="0">
      <div class="tune-bar"></div>
      <p class="eyebrow">DEMO · CAPABILITY</p>
    </section>
    <section class="slide" data-slide="1">
      <h1 class="title-glitch" data-text="一拍进场">一拍进场</h1>
      <p class="sub">孤独摇滚气质 · 网页伪 PPT 试水</p>
    </section>
    <section class="slide" data-slide="2">
      <div class="quad">
        <div class="cell c-pink"><span>躁</span></div>
        <div class="cell c-yellow"><span>亮</span></div>
        <div class="cell c-cyan"><span>稳</span></div>
        <div class="cell c-purple"><span>燃</span></div>
      </div>
    </section>
    <section class="slide" data-slide="3">
      <ul class="lyrics">
        <li>不是官方画面</li>
        <li>是节奏、色块与卡点</li>
        <li>用来确认动画上限</li>
      </ul>
      <div class="wave" aria-hidden="true"></div>
    </section>
    <section class="slide slide-burst" data-slide="4">
      <div class="iris"></div>
      <p class="burst-label">LIVE</p>
    </section>
    <section class="slide" data-slide="5">
      <h2 class="card-title">也能承载信息</h2>
      <div class="cards">
        <article class="card">01 · 结构清晰</article>
        <article class="card">02 · 重点可扫读</article>
        <article class="card">03 · 动画不抢内容</article>
      </div>
    </section>
    <section class="slide" data-slide="6">
      <p class="end-mark">Demo / 能力试水</p>
      <p class="end-sub">空格翻页 · F 全屏</p>
    </section>
  </main>
  <footer id="chrome">
    <span id="slide-num">01 / 07</span>
    <button type="button" id="btn-play">播放</button>
    <button type="button" id="btn-fs">全屏</button>
  </footer>
  <script src="deck-controller.js"></script>
  <script src="app.js"></script>
</body>
</html>
```

Note: `deck-controller.js` uses `module.exports`; for browser, Task 3 will add a tiny `window.createDeckController` export at the bottom of `deck-controller.js` without breaking Node:

```js
if (typeof module !== 'undefined' && module.exports) {
  module.exports = { createDeckController };
}
if (typeof window !== 'undefined') {
  window.createDeckController = createDeckController;
}
```

Apply that export dual-path in Task 1 implementation if not already present before Task 3 wires `app.js`.

- [ ] **Step 2: Create `styles.css` covering tokens, noise, slides 0–4 motion**

Must include:
- CSS variables for the six colors above
- `#noise` with repeating SVG/data-URI or CSS noise via filter + high-frequency gradient; `pointer-events: none`
- `.slide` fullscreen absolute stack; only `.is-active` visible/interactive
- Slide 0: `.tune-bar` horizontal sweep keyframes
- Slide 1: `.title-glitch` pink/cyan text-shadow offset; short shake then settle when `.is-active`
- Slide 2: `.cell` staggered `animation-delay` pop with `cubic-bezier(0.34, 1.56, 0.64, 1)`
- Slide 3: `.lyrics li` cascade in; `.wave` as repeating linear-gradient bar animation
- Slide 4: `.iris` uses `clip-path: circle()` from ~0% to 150% when active; background hard pink/cyan split
- `@media (prefers-reduced-motion: reduce)` — disable shake/sweep; use opacity only

- [ ] **Step 3: Manual visual check slides 0–4**

Run: open `demos/bocchi-ppt-demo/index.html` in browser (or `npx --yes serve demos/bocchi-ppt-demo`)  
Expected: can temporarily add `is-active` by hand in DevTools on slides 0–4 and see distinct looks; page does not need working buttons yet

---

### Task 3: `app.js` wiring + slides 5–6 polish + README

**Files:**
- Create: `demos/bocchi-ppt-demo/app.js`
- Create: `demos/bocchi-ppt-demo/README.md`
- Modify: `demos/bocchi-ppt-demo/styles.css` (cards + end slide + chrome)
- Modify: `demos/bocchi-ppt-demo/deck-controller.js` (ensure browser global export)

**Interfaces:**
- Consumes: `window.createDeckController` from Task 1
- Produces: runnable demo meeting spec acceptance criteria

- [ ] **Step 1: Implement `app.js`**

```js
(function () {
  const slides = Array.from(document.querySelectorAll('.slide'));
  const slideCount = slides.length;
  const slideNum = document.getElementById('slide-num');
  const btnPlay = document.getElementById('btn-play');
  const btnFs = document.getElementById('btn-fs');
  const deck = document.getElementById('deck');

  const durationsMs = [4000, 7000, 7000, 8000, 6000, 8000, 5000];
  let prev = 0;

  function render(index) {
    slides.forEach((el, i) => {
      el.classList.toggle('is-active', i === index);
      el.classList.toggle('is-exit', i === prev && i !== index);
    });
    prev = index;
    const n = String(index + 1).padStart(2, '0');
    slideNum.textContent = n + ' / ' + String(slideCount).padStart(2, '0');
    btnPlay.textContent = controller.isPlaying() ? '暂停' : '播放';
  }

  const controller = window.createDeckController({
    slideCount,
    durationsMs,
    onChange(index) {
      render(index);
    },
  });

  render(0);

  function togglePlay() {
    if (controller.isPlaying()) controller.pause();
    else controller.play();
    btnPlay.textContent = controller.isPlaying() ? '暂停' : '播放';
  }

  btnPlay.addEventListener('click', (e) => {
    e.stopPropagation();
    togglePlay();
  });
  btnFs.addEventListener('click', (e) => {
    e.stopPropagation();
    if (!document.fullscreenElement) document.documentElement.requestFullscreen().catch(() => {});
    else document.exitFullscreen().catch(() => {});
  });

  document.addEventListener('keydown', (e) => {
    if (e.code === 'Space') {
      e.preventDefault();
      controller.next();
    } else if (e.code === 'ArrowRight') controller.next();
    else if (e.code === 'ArrowLeft') controller.prev();
    else if (e.code === 'KeyF') btnFs.click();
    else if (e.code === 'KeyP') togglePlay();
  });

  deck.addEventListener('click', (e) => {
    if (e.target.closest('#chrome')) return;
    controller.next();
  });

  // Auto-start play after short beat so slide 0 animates first
  setTimeout(() => {
    controller.play();
    btnPlay.textContent = '暂停';
  }, 600);
})();
```

- [ ] **Step 2: Finish CSS for slides 5–6 and chrome**

- `.cards .card` slide-in from right with stagger when parent slide `.is-active`
- `.end-mark` large centered type; subtle scale-in
- `#chrome` fixed bottom-right, compact, high contrast, does not block full-bleed feel

- [ ] **Step 3: Write README**

```markdown
# 乐队风 PPT 能力试水

浏览器打开 `index.html`。若脚本受限，在本目录执行：

```bash
npx --yes serve .
```

## 快捷键

- 空格 / 点击：下一页
- ← →：上下页
- P：播放/暂停
- F：全屏

这是动画上限试水，不是竞聘正稿。
```

- [ ] **Step 4: Acceptance self-check**

Run: `node demos/bocchi-ppt-demo/deck-controller.test.js` → `ok`  
Run: open demo, fullscreen, watch once without touching  
Expected vs spec:
1. Slides 0–2 feel like a short film cold open  
2. Slide 4 iris burst hits hard  
3. Slide 5 remains readable  
4. No network image requests (DevTools Network)

---

## Spec coverage (self-review)

| Spec item | Task |
|-----------|------|
| 7 slides storyboard | Task 2–3 |
| Visual tokens / noise / glitch / quad / lyrics / iris | Task 2 |
| Content cards + end | Task 3 |
| Autoplay + keys + chrome | Task 3 |
| reduced-motion | Task 2 CSS |
| Offline no images | Task 2–3 |
| Isolated under demos/ | all tasks |
| Controller testability | Task 1 |
