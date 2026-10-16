# 反馈渠道的目录标准 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `MechabellumMods` 的目录校验接受旧条目没有 `feedback`，但拒绝空值、`http`、`file` 和写坏的 `mailto`；投稿模板和创作标准把这条写成必填。

**Architecture:** `scripts/validate_catalog.py` 增加 `check_feedback`。缺键跳过。键存在时用与管理器相同的隐藏规则。文档只改模板和「先问完再写」，不改已有 `catalog.json` 条目。

**Tech Stack:** Python 3、`unittest`。

**Working branch:** `feat/feedback-catalog-standard`（仓库 `D:\gongzuo\MechabellumMods`。不要在 `master` 上提交。）

## Global Constraints

- 旧条目没有 `feedback` 键时，整份目录仍然通过。
- 键存在时，去掉空白后必须非空，并且不能是管理器会隐藏的渠道：非 `https`/`mailto` 的绝对地址、带用户信息的 `https`、写坏的 `mailto:`。
- `http://example.com`、`file:`、`javascript:` 失败。`author@example.com` 和 `https://example.com/form` 通过。
- 不给现有模组补 `feedback`，不提高 `minManagerVersion`。
- 不把 `https://discord.gg/CQDkCfDTA` 写进任何条目。
- 标准写明：这是玩家把使用问题送回作者的渠道；会打开的只有不带用户信息的 `https`；中文和英文玩家都要能用。GODOG 的 Discord 邀请不能充数。

---

### Task 1: 校验脚本

**Files:**
- Modify: `D:\gongzuo\MechabellumMods\scripts\validate_catalog.py`
- Test: `D:\gongzuo\MechabellumMods\scripts\test_validate_catalog.py`

**Interfaces:**
- Consumes: 现有 `mod()` 夹具和 `run_main`
- Produces: `check_feedback(mod: dict, errors: list[str])`，由 `main` 在每个条目上调用

- [ ] **Step 1: 建分支并写失败测试**

在 `D:\gongzuo\MechabellumMods` 执行 `git checkout -b feat/feedback-catalog-standard`。

给 `run_main` 的 demo 条目分别不带 `feedback`、带 `author@example.com`、带 `"  "`、带 `http://example.com`、带 `file:///C:/x`。断言：缺键和合法邮箱的退出码是 0；空白、`http`、`file` 的退出码不是 0，stderr 含 `feedback`。

- [ ] **Step 2: 运行，确认失败**

Run: `python scripts/test_validate_catalog.py`

Expected: FAIL，缺键以外的断言失败，或函数不存在导致错误文本里没有 `feedback`。

- [ ] **Step 3: 实现 `check_feedback`**

`"feedback" not in mod` 时返回。值不是字符串，或 `strip` 后为空：记 `id=...: feedback must be a non-empty string`。再用 `urllib.parse.urlsplit`。有协议且不是 `https` 也不是 `mailto`：失败。`https` 且 netloc 含 `@` 或没有主机：失败。`mailto` 时取 path（`urlsplit` 把邮箱放在 path）里 `?` 之前的部分，必须符合一个 `@` 且其后有 `.`、无空白无逗号。普通说明（没有协议）通过。

- [ ] **Step 4: 再跑测试**

Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add scripts/validate_catalog.py scripts/test_validate_catalog.py
git commit -m "feat: reject unsafe feedback values without forcing old entries"
```

### Task 2: 投稿模板和创作标准

**Files:**
- Modify: `D:\gongzuo\MechabellumMods\README.md` 三处「【联系方式 / Contact】（可选）」
- Modify: `D:\gongzuo\MechabellumMods\docs\submit.html` 对应三处
- Modify: `D:\gongzuo\MechabellumMods\docs\ai-mod-standard.md`「先问完再写」里的身份条

**Interfaces:**
- Consumes: 管理器计划 Task 5 的那句中文模板
- Produces: 三份文档使用同一句必填说明

- [ ] **Step 1: 替换模板行**

三处都换成：

`【反馈渠道 / Feedback】（必填。作者自己提供，玩家要能据此联系上作者。会打开的只有不带用户信息的 https。这是把使用问题送回作者的渠道，中文和英文玩家都要能用。）`

`ai-mod-standard.md` 的身份条改为：作者名写入 `author`。反馈渠道写入 `feedback`，不写进简介。缺了就停下来问作者。不得留空，不得用 `https://discord.gg/CQDkCfDTA` 充数。作者自己会看的 `https` 链接、邮箱，或一段玩家能照做的说明都可以。必要性写在同一条。

「问齐但不由脚本退回」的名单加上 `feedback`。

- [ ] **Step 2: 搜索残留**

Run: `rg "联系方式 / Contact】（可选）" README.md docs/submit.html docs/ai-mod-standard.md`

Expected: 无匹配。

- [ ] **Step 3: Commit**

```bash
git add README.md docs/submit.html docs/ai-mod-standard.md
git commit -m "docs: require an author-provided feedback channel"
```
