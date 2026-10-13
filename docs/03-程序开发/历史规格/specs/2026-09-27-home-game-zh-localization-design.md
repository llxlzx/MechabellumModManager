# HOME 全文简体汉化设计

**日期：** 2026-09-27  
**目标游戏路径：** `D:\youxi\dl\HOME`  
**引擎：** TyranoScript / TyranoBuilder，Electron 打包（`HOME.exe` + `resources\app.asar`）

## 目标与约束

- **范围：** 尽量全文汉化（主线、支线、H、结局、系统可见文案）。
- **语言：** 简体中文。
- **质量：** 机翻优先、偏直译、尽快可玩；R18 内容单独词表 + 细校。
- **安装：** 直接改 `D:\youxi\dl\HOME`；动手前完整备份。
- **加载方式（推荐）：** 解包 `app.asar` → `resources\app\`，原包改名为 `app.asar.jp.bak`，Electron 读目录以便迭代。
- **工具位置：** `D:\youxi\dl\HOME_zh_tools\`（与游戏目录并列，不混入本仓库业务代码）。
- **非目标（第一期）：** UI 日文贴图字重绘、语音字幕、官方 xdelta 升级补丁联调、繁体、可开关双语插件。

## 架构（方案 1）

```
备份整目录
  → 解包 app.asar 到 resources/app/
  → 改名 app.asar → app.asar.jp.bak
  → 抽取 .ks/.html 可见文本 → ja_zh_map.json
  → 批量机翻（断点续跑）
  → R18 词表二次校准
  → 回写剧本 + 中文字体/配置
  → 启动验收
```

回滚：恢复整目录备份，或把 `app.asar.jp.bak` 改回 `app.asar` 并删除/移走 `resources\app\`。

## 备份与解包

1. 确认 `D:` 空闲 **≥20GB**。
2. 复制 `D:\youxi\dl\HOME` → `D:\youxi\dl\HOME_backup_YYYYMMDD`（备份校验完成前不改原目录）。
3. `npx asar extract resources\app.asar resources\app`。
4. `app.asar` → `app.asar.jp.bak`。
5. 启动 `HOME.exe` 冒烟（能进标题即通过）。
6. 汉化期间不打 `C2274升级补丁` 内 xdelta（针对日文 asar）。

## 文本流水线

### 扫描

- 主：`resources\app\data\scenario\**\*.ks`（约 656 个）。
- 次：主题/菜单相关 `.ks`、`.html`（存读档、设置文案）。

### 抽取（剧本感知）

翻译：对白、旁白、选项等可见文本。  
不翻译：`[...]` 标签、`;` 注释、`*` 标签名、变量名、资源路径、脚本逻辑。

产物：

- `HOME_zh_tools\work\ja_zh_map.json` — 日文→简中总表  
- `HOME_zh_tools\work\failed.json` — 失败条目  
- `HOME_zh_tools\work\report.md` — 统计  
- `HOME_zh_tools\work\scenario_ja_backup\` — 回写前日文剧本备份  

### 机翻

- 简体、偏直译；已有译文跳过（断点续跑）。
- 实现：Node 脚本 + 可替换的翻译后端（默认免费/可用的批量接口；失败入 `failed.json` 可重试）。

### 回写

按原文精确替换；写后做结构校验（标签/路径未被破坏）。异常文件跳过并记入报告。

## R18 细校

1. **范围：** `H_*.ks`、`*serihu*`、`*biyaku*`、`*suimin*`、`*3P*` 及对应 `EV_*` 等。
2. **词表：** `HOME_zh_tools\glossary_r18.json` + `glossary_names.json`（角色名等全书统一）。
3. **原则：** 不删信息；粗俗度贴近原文；称呼一致。
4. **流程：** 全量机翻 → R18 二次套词表 → 每条角色线抽检若干段 → 修词表再全量套用。

## 字体与显示

1. 加入支持简体的字体（优先 Windows `msyh.ttc` / 思源黑体等），放入 `resources\app` 下主题可引用路径。
2. 改 `Config.tjs` 与主题 CSS，默认中文字体，避免 □。
3. 严重截断时小幅调字号/行距。
4. 第一期不重绘日文 UI 贴图按钮。

## 分期落地（推荐顺序）

| 期 | 内容 | 验收 |
|----|------|------|
| P0 | 备份、解包、冒烟、中文字体 | 日文也能正常进游戏且中文不乱码（测一句即可） |
| P1 | 全量 `.ks` 机翻回写 | 开局/菜单可见中文对白 |
| P2 | R18 词表细校 | 抽样 H 剧本无标签损坏、关键用语一致 |
| P3 | 菜单 HTML/残留日文扫尾 | 存读档界面主要文案中文 |
| P4 | 人工润色（可选，非阻塞） | 按需 |

## 风险

- **磁盘：** 备份+解包约需 20GB+。  
- **asar 体积 ~8.6GB：** 解包耗时长；用目录加载避免反复重打包。  
- **误译标签：** 必须剧本感知抽取，禁止整文件盲翻。  
- **官方补丁：** 与目录版冲突；汉化完成前不打 xdelta。

## 验收标准

- 能启动、存读档正常。  
- 对白/选项主要为简体中文。  
- R18 抽样无乱码、无明显标签损坏。  
- 可用备份或 `app.asar.jp.bak` 完整回滚。
