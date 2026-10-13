# 星辰之子 · 旅者安可版 电影级动画重做 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline, as chosen for this project). Steps use checkbox (`- [ ]`) syntax.

**Goal:** 把旅者安可版做成「像真正的动画」：GPU 渲染的 2.5D 镜头背景 + 屏幕层 PPT 特效 + 桥接转场 + 融进场景的大字。

**Architecture:** `deck/shots.py` 描述每页镜头（平面、物体、镜头关键帧、特效、桥接），`tools/shot.py` + `tools/shot_fx.py`
用 torch 渲染成 1080p 背景视频，`tools/bridges.py` 把每页结尾渲染成通往下一页首帧的桥接；`deck/encore.py` 只保留文字与屏幕层特效，
转场改为 Cut。

**Tech Stack:** Python 3.12、torch (CUDA, RTX 5090)、transformers Depth Anything V2、OpenCV、PIL、ffmpeg 7.1、PowerPoint COM。

**Working branch:** none (working tree only, no commits)

## Global Constraints

- 文案逐字不变；`STARTS` 不变；总长 229.5 s；只用游戏画面。
- 输出 `out/星辰之子-旅者安可版-动画版.pptx/.mp4`，1080p，质量 100；旧输出不动。
- 不改 `demos/outer-wilds-cinematic-pptx`、Mod 管理器、`demos/bocchi-ppt-demo`；不 git 提交。
- 坐标一律为画面比例（0..1）；时间为该页内容起点起的秒数。

---

### Task 1: 景深拆层 `tools/depth_layers.py`

**Files:** Create `tools/depth_layers.py`, `tests/test_depth_layers.py`

**Interfaces — Produces:**
- `estimate_depth(img: np.ndarray) -> np.ndarray`（H×W，0=远 1=近，Depth Anything V2 Large，缓存到 `layers/depth/<stem>.npy`）
- `slice_planes(img, depth, cuts: tuple[float, ...], feather=0.03) -> list[np.ndarray]`（远→近 RGBA；除最近层外，被更近层遮住的区域补画；最远层完全不透明）
- CLI `python -m tools.depth_layers <stem> --cuts 0.35,0.6 --out layers/shot/<name>` 写 `plane0.png…`

- [ ] 测试：合成图（远处渐变 + 近处方块，深度已知）→ `slice_planes` 两层；远层不透明且方块处被补成渐变色；近层只含方块；按顺序叠回 ≈ 原图。
- [ ] 实现；运行测试通过。
- [ ] 对需要的静帧跑拆层，出检查图。

### Task 2: 镜头渲染核心 `tools/shot.py`

**Files:** Create `tools/shot.py`, `tests/test_shot.py`

**Interfaces — Produces:**
- `Key(t, zoom=1.0, x=0.0, y=0.0, rot=0.0)`；`Plane(src, depth)`；`Sprite(name, src, center, width, depth, keys)`，
  `SpriteKey(t, dx=0, dy=0, scale=1, rot=0, alpha=1)`；`Shot(page, seconds, planes, sprites=(), fx=(), camera=(Key,), fade_in=None, fade_out=None, bridge=None)`
- `camera_at(shot, t) -> Key`（smoothstep 插值，t 超出两端取端点）；`plane_transform(cam: Key, depth) -> (zoom, x, y, rot)`
- `screen_point(shot, pt, depth, t) -> (x, y)`
- `coverage_ok(shot) -> list[str]`（逐 0.25 s 检查每个平面是否盖满画面）
- `Renderer(shot, size=(1920,1080))`：`.frame(t) -> torch.Tensor[3,H,W] 0..1`（支持负 t）
- `render_shot(shot, out_mp4, next_shot=None)` → 用 ffmpeg 管道写 H.264（crf 14）

- [ ] 测试：视差（相同镜头下近层位移 > 远层）；`screen_point` 与渲染一致（在亮点处取样）；`coverage_ok` 能抓出露边；负时间不报错且等于 t=0 的镜头；渲染确定性。
- [ ] 实现；测试通过。

### Task 3: 画面内特效 `tools/shot_fx.py`

**Files:** Create `tools/shot_fx.py`, `tests/test_shot_fx.py`

**Interfaces — Produces（全部为 frozen dataclass，`apply` 由 Renderer 调用）：**
平面空间：`Flame(plane, mask, amp, speed)`、`Haze(plane, region, amp)`、`Swirl(plane, center, radius, turns_per_s, falloff)`、
`Flow(plane, mask, amp, speed, drift)`、`Boil(plane, mask, amp)`、`Twinkle(plane, amount, wake=None, sleep=None)`；
场景空间：`Particles(count, t0, color, size, velocity, sway, region, life, depth, color_end=None, bokeh=0.0)`、
`Glow(center, depth, radius, color, env)`（env 为 `(t, level)` 关键帧或 heartbeat）、`Burst(center, depth, t, color, size)`、
`Meteors(t0, count, region, angle)`、`Lightning(times, region)`；
屏幕空间：`GodRays(center, depth, strength_env)`、`Bloom(threshold, strength)`、`Flare(center, depth, env)`、
`Grade(keys: (t, exposure, temp, sat))`、`Vignette(strength, pulse=None)`、`Flash(t, dur, color)`。

- [ ] 测试：每类特效在小图上确定性；Flame/Flow 让遮罩内像素随时间变化、遮罩外不变；Swirl 中心快边缘慢；Bloom 只扩亮部；Grade 曝光关键帧生效。
- [ ] 实现；测试通过。

### Task 4: 桥接 `tools/bridges.py`

**Files:** Create `tools/bridges.py`, `tests/test_bridges.py`

**Interfaces — Produces:** `Bridge(style, dur, color=(1,1,1), center=(0.5,0.5), direction=(1,0))`，
style ∈ {"light","dissolve","wipe","whirl","black","flash","tilt","streak"}；
`compose(bridge, a: Tensor, b: Tensor, p: float) -> Tensor`（p 0→1）。

- [ ] 测试：每种样式 p=0 ≈ a、p=1 == b；中间帧不越界 0..1。
- [ ] 实现；测试通过。

### Task 5: 17 页镜头规格 `deck/shots.py` + 渲染

**Files:** Create `deck/shots.py`, `tests/test_shots.py`；Modify `deck/encore.py`（`TRANSITIONS` 全 Cut）

**Interfaces — Produces:** `SHOTS: dict[int, Callable[[float], Shot]]`、`shot(i) -> Shot`、`render_all(pages)` CLI
`python -m deck.shots render 1 2 …` 写 `fx/cine/bg_p{i}.mp4`。

- [ ] 测试：17 页齐全；`seconds == hold - BG_TAIL`；每页 `coverage_ok` 为空；每页镜头位移/推进超过下限；1–16 页都有桥接；第 1 页有淡入、第 17 页有淡出。
- [ ] 实现规格；渲染全部页面；每页出 4 帧检查图 + 每处桥接 5 帧检查图，逐页自检修改。
- [ ] 桥接衔接校验脚本：第 N 页末帧 vs 第 N+1 页首帧平均差 < 2/255。

### Task 6: 大字融进场景 `tools/glyphs.py`

**Files:** Modify `tools/glyphs.py`, `tests/test_glyphs.py`

**Interfaces — Produces:** `word_png(text, out_dir, stem) -> LightText`（与文案同字体同光效）、
`glyph_points(text, n, seed=0) -> list[(u, v)]`（字形笔画上均匀采样，0..1 相对字框）。

- [ ] 测试：采样点数量 = n、全部落在笔画像素上、分布覆盖每个字。
- [ ] 实现；测试通过。

### Task 7: 屏幕层 PPT 重写 `deck/encore.py`

**Files:** Modify `deck/encore.py`, `tests/test_encore.py`, `deck/editions.py`, `tests/test_editions.py`, `build_layered.py`

- [ ] 测试：转场全为 `ppEffectCut` 且时长 0；`spans == holds`；所有图层在 `span - bridge.dur` 前退场；
  好奇/星河大海/星辰的孩子由 ≥ 40 个采样点粒子聚成且无 KeyBreath；单字页 KeyBreath 与字身同框；每页 ≥ 3 种辅助特效；validate 通过。
- [ ] 实现 17 页屏幕层；`bg(i)` 指向 `fx/cine/bg_p{i}.mp4`；edition 默认输出改为动画版；`TRANSITION_LAG` 增加 Cut=0。
- [ ] 测试通过。

### Task 8: 构建、核对、交付

- [ ] 用 3 页试构建标定 `BG_TAIL`（总时长误差最小且不累积）。
- [ ] `python build_layered.py --edition encore --video` 全量构建。
- [ ] 运动幅度（光流）对比高清版、衔接差、同步、文字亮度门、抽帧检查图；全部测试。
- [ ] 复制检查图与 MP4 到 assets 并用 cursor 打开，启动 PPTX，中文汇报。
