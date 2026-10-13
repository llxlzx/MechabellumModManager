# HOME 全文简体汉化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `D:\youxi\dl\HOME` 解包为目录加载，并用剧本感知机翻流水线把可见文本改为简体中文（R18 细校），尽快可玩。

**Architecture:** 备份整目录 → 解包 `app.asar` 到 `resources\app\` 并改名原 asar → Node 工具抽取/机翻/回写 `.ks` → 中文字体与配置 → R18 词表二次校准。工具在 `D:\youxi\dl\HOME_zh_tools\`，游戏改动仅在 `D:\youxi\dl\HOME`。

**Tech Stack:** Node.js 20+、`asar`、本地/可用的日→简中翻译后端、PowerShell 备份与验收。

**Working branch:** none (working tree only, no commits)

## Global Constraints

- 游戏根目录：`D:\youxi\dl\HOME`（直接修改，先备份）
- 备份目录：`D:\youxi\dl\HOME_backup_YYYYMMDD`
- 工具目录：`D:\youxi\dl\HOME_zh_tools\`
- 语言：简体中文；机翻优先；R18 细校
- 不翻译 Tyrano 标签/路径/变量；不重打包 8.6GB asar；第一期不重绘 UI 贴图字
- 汉化期间不打 `C2274升级补丁` xdelta
- Spec：`docs/superpowers/specs/2026-09-27-home-game-zh-localization-design.md`

## File Structure

| Path | Responsibility |
|------|----------------|
| `D:\youxi\dl\HOME_zh_tools\package.json` | 工具依赖 |
| `D:\youxi\dl\HOME_zh_tools\lib\ksText.js` | `.ks` 行级抽取/回写（保留标签） |
| `D:\youxi\dl\HOME_zh_tools\lib\translate.js` | 翻译后端抽象 + 断点续跑 |
| `D:\youxi\dl\HOME_zh_tools\scripts\01-extract.js` | 扫描并生成 `ja_zh_map.json` |
| `D:\youxi\dl\HOME_zh_tools\scripts\02-translate.js` | 批量填译文 |
| `D:\youxi\dl\HOME_zh_tools\scripts\03-apply.js` | 回写到 `resources\app` |
| `D:\youxi\dl\HOME_zh_tools\scripts\04-apply-glossary.js` | R18/专名词表二次替换 |
| `D:\youxi\dl\HOME_zh_tools\scripts\05-setup-font.js` | 复制字体并改 Config/CSS |
| `D:\youxi\dl\HOME_zh_tools\glossary_names.json` | 角色名等 |
| `D:\youxi\dl\HOME_zh_tools\glossary_r18.json` | R18 用语 |
| `D:\youxi\dl\HOME_zh_tools\test\ksText.test.js` | 抽取/回写单测 |
| `D:\youxi\dl\HOME_zh_tools\work\` | map / failed / report / ja backup |

---

### Task 1: 磁盘检查、完整备份、asar 解包与冒烟

**Files:**
- Create: `D:\youxi\dl\HOME_backup_YYYYMMDD\`（整目录复制）
- Modify: `D:\youxi\dl\HOME\resources\app.asar` → `app.asar.jp.bak`
- Create: `D:\youxi\dl\HOME\resources\app\`（解包内容）

**Interfaces:**
- Consumes: 无
- Produces: 可目录加载的游戏；原 asar 备份名 `app.asar.jp.bak`

- [ ] **Step 1: 检查空闲空间**

```powershell
Get-PSDrive D | Select-Object Used,Free
```

Expected: `Free` ≥ 20GB（21474836480 字节）。若不足则停止并告知用户清理磁盘。

- [ ] **Step 2: 完整备份**

```powershell
$date = Get-Date -Format 'yyyyMMdd'
$src = 'D:\youxi\dl\HOME'
$dst = "D:\youxi\dl\HOME_backup_$date"
robocopy $src $dst /E /COPY:DAT /R:2 /W:3 /NFL /NDL /NP
if ($LASTEXITCODE -ge 8) { throw "robocopy failed: $LASTEXITCODE" }
```

Expected: 备份目录存在且含 `HOME.exe`、`resources\app.asar`。

- [ ] **Step 3: 解包 asar**

```powershell
cd D:\youxi\dl\HOME\resources
npx --yes asar extract .\app.asar .\app
```

Expected: `app\data\scenario\` 下有大量 `.ks`；耗时可能很长。

- [ ] **Step 4: 切换为目录加载**

```powershell
Rename-Item -LiteralPath 'D:\youxi\dl\HOME\resources\app.asar' -NewName 'app.asar.jp.bak'
```

Expected: 仅存在 `app.asar.jp.bak` 与 `app\`，无 `app.asar`。

- [ ] **Step 5: 冒烟启动**

启动 `D:\youxi\dl\HOME\HOME.exe`，确认能到标题画面。失败则：

```powershell
Remove-Item -Recurse -Force 'D:\youxi\dl\HOME\resources\app' -ErrorAction SilentlyContinue
Rename-Item 'D:\youxi\dl\HOME\resources\app.asar.jp.bak' 'app.asar'
```

---

### Task 2: KS 文本库 + 单测

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\package.json`
- Create: `D:\youxi\dl\HOME_zh_tools\lib\ksText.js`
- Create: `D:\youxi\dl\HOME_zh_tools\test\ksText.test.js`

**Interfaces:**
- Consumes: 无
- Produces:
  - `extractTexts(source: string): string[]` — 去重前的可见文本列表（可含重复，调用方可 dedupe）
  - `applyMap(source: string, map: Record<string,string>): string` — 只替换文本段
  - `validateStructure(before: string, after: string): { ok: boolean, reason?: string }`

- [ ] **Step 1: 初始化 package.json**

```json
{
  "name": "home-zh-tools",
  "private": true,
  "type": "module",
  "scripts": {
    "test": "node --test test/**/*.test.js",
    "extract": "node scripts/01-extract.js",
    "translate": "node scripts/02-translate.js",
    "apply": "node scripts/03-apply.js",
    "glossary": "node scripts/04-apply-glossary.js",
    "font": "node scripts/05-setup-font.js"
  }
}
```

- [ ] **Step 2: 写失败单测**

```js
import test from 'node:test';
import assert from 'node:assert/strict';
import { extractTexts, applyMap, validateStructure } from '../lib/ksText.js';

test('extracts dialogue but not tags', () => {
  const src = `;[cm]\n[cm]\nこんにちは\n[p]\n*start\n選択肢A\n`;
  const texts = extractTexts(src);
  assert.ok(texts.includes('こんにちは'));
  assert.ok(texts.includes('選択肢A'));
  assert.ok(!texts.some(t => t.includes('[cm]')));
  assert.ok(!texts.includes('*start'));
});

test('applyMap preserves tags', () => {
  const src = `[bg storage="a.png"]\nこんにちは\n[p]\n`;
  const out = applyMap(src, { 'こんにちは': '你好' });
  assert.equal(out, `[bg storage="a.png"]\n你好\n[p]\n`);
  assert.equal(validateStructure(src, out).ok, true);
});
```

- [ ] **Step 3: 跑测确认失败**

```powershell
cd D:\youxi\dl\HOME_zh_tools
node --test test/ksText.test.js
```

Expected: FAIL（模块不存在）。

- [ ] **Step 4: 实现 ksText.js**

规则：
- 逐行处理；`trim` 后以 `;` 开头 → 跳过整行（不抽取）。
- 以 `*` 开头的标签行 → 跳过。
- 整行匹配 `^\s*\[.*\]\s*$` → 跳过（纯标签行）。
- 其他行：去掉行内完整 `[...]` 标签后，剩余非空文本即抽取对象；回写时按「最长原文优先」替换剩余文本部分，标签原样拼回。
- `validateStructure`：前后 `[` / `]` 计数一致；`storage="..."` 等路径属性字符串集合一致。

- [ ] **Step 5: 跑测确认通过**

```powershell
node --test test/ksText.test.js
```

Expected: PASS。

---

### Task 3: 抽取脚本生成 ja_zh_map.json

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\scripts\01-extract.js`
- Create: `D:\youxi\dl\HOME_zh_tools\work\ja_zh_map.json`
- Create: `D:\youxi\dl\HOME_zh_tools\work\report.md`

**Interfaces:**
- Consumes: `extractTexts` from `lib/ksText.js`
- Produces: `work/ja_zh_map.json` 形如 `{ "こんにちは": { "zh": "", "files": ["data/scenario/a.ks"], "r18": false } }`

- [ ] **Step 1: 实现 01-extract.js**

- 根：`D:\youxi\dl\HOME\resources\app`
- Glob：`data/scenario/**/*.ks`，以及 `data/others/plugin/theme_kopanda_09_2/**/*.{ks,html}`
- `r18: true` 若相对路径匹配 `/H_|serihu|biyaku|suimin|3P/i`
- 已存在的 `zh` 非空则保留（重跑不丢译文）

- [ ] **Step 2: 运行抽取**

```powershell
cd D:\youxi\dl\HOME_zh_tools
node scripts/01-extract.js
```

Expected: `work/ja_zh_map.json` 键数量 > 1000；`report.md` 含文件数与唯一句数。

---

### Task 4: 批量机翻（断点续跑）

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\lib\translate.js`
- Create: `D:\youxi\dl\HOME_zh_tools\scripts\02-translate.js`

**Interfaces:**
- Consumes: `ja_zh_map.json`
- Produces: 填好 `zh` 的 map；`work/failed.json`

- [ ] **Step 1: translate.js**

```js
// translateBatch(items: string[], opts) => Promise<(string|null)[]>
// 默认后端：可工作的日→zh-CN HTTP/库；单条失败返回 null
// 限速：约 100–200ms/条或按 API 批量，避免封禁
```

- [ ] **Step 2: 02-translate.js**

- 只处理 `zh === ''`
- 每 N 条写盘一次 map（断点续跑）
- 失败写入 `failed.json`

- [ ] **Step 3: 先小样翻译 50 条验证**

```powershell
node scripts/02-translate.js --limit 50
```

Expected: 50 条 `zh` 非空；抽查无把 `[bg` 之类翻进去。

- [ ] **Step 4: 全量翻译**

```powershell
node scripts/02-translate.js
```

Expected: 未翻译比例 < 5% 或仅剩 `failed.json` 可重试项。

---

### Task 5: 中文字体与 Config

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\scripts\05-setup-font.js`
- Modify: `D:\youxi\dl\HOME\resources\app\data\system\Config.tjs`（或实际字体配置处）
- Modify: 主题 CSS（如 `theme_kopanda_09_2\ts09.css`）
- Copy font into `resources\app\data\others\font\`（或引擎约定目录）

**Interfaces:**
- Consumes: Windows 字体，优先 `C:\Windows\Fonts\msyh.ttc`
- Produces: 游戏默认中文字体

- [ ] **Step 1: 探测 Config 中字体字段**

在 `resources\app` 内搜索 `face` / `font` / `.ttf` / `ヒラギノ` 等，确认写入点。

- [ ] **Step 2: 实现并运行 05-setup-font.js**

复制 `msyh.ttc`（或下载 Noto Sans SC），改配置/CSS 为该字体族。

- [ ] **Step 3: 启动游戏确认无 □□□**

用临时一句中文或先跑一小文件 apply 验证。

---

### Task 6: 回写全量剧本

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\scripts\03-apply.js`
- Create: `D:\youxi\dl\HOME_zh_tools\work\scenario_ja_backup\`

**Interfaces:**
- Consumes: `applyMap`, `validateStructure`, `ja_zh_map.json`
- Produces: 已汉化的 `resources\app\data\scenario\**\*.ks`

- [ ] **Step 1: 实现 03-apply.js**

- 回写前把原文件拷到 `work/scenario_ja_backup/` 相对路径
- `validateStructure` 失败则不覆盖，记入 report
- 仅替换 map 中 `zh` 非空的键

- [ ] **Step 2: 先 apply 单个文件冒烟**

```powershell
node scripts/03-apply.js --only data/scenario/first.ks
```

启动游戏确认开场中文。

- [ ] **Step 3: 全量 apply**

```powershell
node scripts/03-apply.js
```

Expected: 绝大多数文件成功；失败列表可接受地短。

---

### Task 7: R18 / 专名词表细校

**Files:**
- Create: `D:\youxi\dl\HOME_zh_tools\glossary_names.json`
- Create: `D:\youxi\dl\HOME_zh_tools\glossary_r18.json`
- Create: `D:\youxi\dl\HOME_zh_tools\scripts\04-apply-glossary.js`

**Interfaces:**
- Consumes: 已汉化 `.ks`；词表为 `{ "源中文或日文": "目标中文" }` 有序列表
- Produces: R18 文件二次替换后的文本

- [ ] **Step 1: 写入专名表（至少）**

```json
{
  "凪": "凪",
  "凛子": "凛子",
  "蕾": "蕾",
  "ハヤト": "隼人",
  "はやと": "隼人"
}
```

（以剧本实际用名为准，抽取后校正。）

- [ ] **Step 2: 写入 R18 词表**

根据机翻抽检结果，把明显错译/不一致的身体/行为用语做成替换表；只作用于 `r18` 路径文件。

- [ ] **Step 3: 运行 glossary 并抽检**

```powershell
node scripts/04-apply-glossary.js
```

打开各一条 `H_nagi_*` / `H_rinko_*` / `H_tubomi_*` 对照日文备份，修词表后可重跑。

---

### Task 8: 菜单 HTML 扫尾与总验收

**Files:**
- Modify: `resources\app\data\others\plugin\theme_kopanda_09_2\html\*.html` 等残留日文
- Create: `D:\youxi\dl\HOME_zh_tools\work\report.md`（更新最终统计）

**Interfaces:**
- Consumes: 同上流水线（可对 html 跑 extract/translate/apply 或手工高频句）
- Produces: 可玩的全文简中版

- [ ] **Step 1: 对主题 html 跑抽取/翻译/回写（或纳入 Task 3–6 的 glob）**

- [ ] **Step 2: 验收清单**

- 标题 → 新游戏/继续
- 存读档
- 主线一段中文
- R18 一段无乱码/无标签损坏
- 回滚说明写在 `HOME_zh_tools\README.md`

- [ ] **Step 3: 写 README 回滚步骤**

```markdown
# 回滚
1. 删 D:\youxi\dl\HOME\resources\app
2. 将 app.asar.jp.bak 改回 app.asar
# 或整目录用 HOME_backup_YYYYMMDD 覆盖
```

---

## Spec Coverage Check

| Spec 要求 | Task |
|-----------|------|
| 备份+解包+目录加载 | Task 1 |
| 剧本感知抽取/回写 | Task 2–3, 6 |
| 机翻断点续跑 | Task 4 |
| 中文字体 | Task 5 |
| R18 细校 | Task 7 |
| 菜单扫尾/验收/回滚 | Task 8 |
| 不打官方 xdelta | Task 1 约束 / README |
