# 星辰之子 · 旅者安可版（高清）实施计划

> 规格：`docs/superpowers/specs/2026-10-12-stardust-encore-hd-design.md`。不做 git 提交，所有改动留在工作区。
> 命令都在 `demos/outer-wilds-real-pptx/` 下运行，PowerShell 先设置 `$env:PYTHONIOENCODING="utf-8"`。

分层坐标、抠图阈值要依赖修复后的底图，只能边看边定，所以各任务给出的是接口、测试和验收标准，没有预先写好的完整代码。

### 任务 1：`tools/still_restore.py`（TDD）
- 接口：
  - `Pick(src, t, crop=None, gamma=1.0)`
  - `read_frames(pick, n=9) -> list[np.ndarray]`：裁切后缩放到 1920×1080，float 0..1
  - `align_to(ref, img) -> (warped, err)`：DIS 光流
  - `fuse(frames, max_err=…) -> np.ndarray`：中值融合
  - `unsharp(img, radius=1.2, amount=0.6)`
  - `restore(pick) -> np.ndarray`
  - `PICKS: dict[int, Pick]`
  - CLI：`python -m tools.still_restore [pages]` 写出 `plates/il_p{i}.png`
- 测试 `tests/test_still_restore.py`：
  - 带噪的合成帧融合后噪声标准差至少降到一半；
  - 整体平移 3 px 的帧能对齐（融合后边缘仍然锐利，不重影）；
  - 锐化后拉普拉斯方差上升；
  - `PICKS` 覆盖 8 个过渡页，第 9 页取 accolades 12.35。
- 验收：8 张底图拼成对比图（修复前单帧对比修复后）；锐度要高于单帧，且没有重影。

### 任务 2：分层 `build_il{2,3,5,7,9,11,13,16}`
- 在 `tools/split_layers.py` 中新增，输出到 `layers/il{i}/`；CLI 增加 `il2` 等选项。
- 测试：每个 builder 的输出文件都存在，尺寸 1920×1080（或方形裁切），主体 alpha 非空，且主体区域在预期框内（`tests/test_split_layers.py` 追加一个基于真实底图的冒烟测试，底图缺失时跳过）。
- 验收：每页拼出「底图 / bg_clean / 主体在棋盘格上」对比图，确认没有明显残影或边缘破洞。

### 任务 3：动态背景配置
- `living_bg.encore_specs()` 增加 2、3、5、7、9、11、13、16 页（基底为 `layers/il{i}/bg_clean.png`），保留现有 9 页的配置。
- 测试：`encore_specs` 覆盖全部 17 页，时长等于各页的 span。

### 任务 4：分镜 `_i*` 重写
- `deck/encore.py` 中 8 个过渡页按规格的「逐页动画」重写，第 9 页 motif 改为「挪麦雕像」。
- 测试：
  - 全部 17 页 `validate` 通过；
  - 过渡页没有文字；
  - 11 页太阳层位于铁塔层之前（先加入，所以在下层），且太阳有向下的入场位移；
  - 11 页的光爆峰值 t≈CLIMAX−start；
  - 5 页行星使用闭合的 orbit_path；
  - 16 页有 3 层光核脉动。

### 任务 5：版本与导出
- `Edition.export_height`（v1 720 / encore 1080），`export_video` 使用该值；encore 的 `default_out` 改为高清文件名。
- 测试：`tests/test_editions.py` 更新。

### 任务 6：渲染、构建、核对、交付
1. 生成底图、分层，渲染 8 个过渡页的背景（`python -m tools.living_bg --edition encore 2 3 5 7 9 11 13 16`）。
2. `python build_layered.py --edition encore --video`。
3. 锐度对比（`_tmp/blur_diag.py` 指向新文件），同步测量（`measure_sync2.py`），逐页拼图检查。
4. 全部测试；把拼图、MP4 复制到 assets 并用 cursor 打开，启动 PPTX，用中文汇报。
