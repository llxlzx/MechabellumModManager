# 设计：独立 Mod 仓库 + 管理器「Mod 浏览」

日期：2026-09-03  
状态：已批准（用户同意）

## 决策

- Mod 托管：**独立仓库** `llxlzx/MechabellumMods`（若名不可用则同账号下等价名）
- 点击 Mod：**仅加入本地库**，不自动勾选方案、不自动应用
- 元数据：`catalog.json`（名称、作者、版本、更新时间、功能说明、相对文件路径、类型）
- 初代内容：来自 `Mods.zip` 的 7 个 DLL + 频道帖整理的中文说明；作者显示「巴巴」

## 仓库布局

```
MechabellumMods/
  README.md
  catalog.json
  mods/<id>/<file>.dll
```

管理器默认拉取：
`https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json`

文件下载：
`https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{file}`

## 管理器行为

- 「Mod 浏览」页：列表显示 name / author / version / updatedAt / summary
- 「刷新目录」「加入本地库」（已存在同哈希则提示已安装）
- 使用现有 `ImportDll` 导入下载的临时文件
- 网络失败提示需可访问 GitHub / 代理
- 高风险名仍走 RiskHeuristic，加入前可确认（与库导入一致）

## 非目标（v1）

- QQ 频道自动抓取
- 百度网盘下载
- 自动启用/应用方案
- 图片预览（可后续加）

## 仓库创建（人工）

管理器侧已写死 `llxlzx/MechabellumMods`。若仓库尚不存在，请在 GitHub 账号下手动新建空仓库（同名），再上传 `catalog.json` 与 `mods/`。本环境无法代开浏览器完成创建。
