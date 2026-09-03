# 设计：多语言、Cloudflare 投稿中转、分类举报、署名声明

日期：2026-09-03  
状态：已批准  
目标版本：v1.0.5

## 决策摘要

- 多语言：zh-CN / en / ru / ja / de；默认跟随系统，否则回退 zh-CN；机翻 + `docs/i18n` 人工翻译预留
- 投稿：管理器 → Cloudflare Worker + R2 → GitHub Issue（待审）；正式 catalog 仅维护者合并
- 举报：确认框 + 类别（作弊 / 病毒 / 与游戏无关 / 其他）；「其他」必填说明；提交到 Worker → Issue
- 安全定位：不免杀毒担保；免责声明 + 人工审核 + 举报下架；仅做扩展名/大小/哈希/Melon 特征等轻量校验
- 署名：院长大人；QQ 319323959；感谢测试者/玩家/Mod 作者；非盈利声明

## 多语言

- `LocalizationService` + `Resources/Strings.*.resx`
- 设置下拉切换，写入 `AppConfig.UiLanguage`（`system` | `zh-CN` | …）
- `docs/i18n/README.md`、`docs/i18n/source-zh-CN.tsv`

## Cloudflare Worker

仓库内 `relay/`（Wrangler）：

- `POST /v1/submissions` — 元数据 + 文件 → R2 `pending/…` + Issue `[submission]`
- `POST /v1/reports` — category + notes + modId → Issue `[report]`
- 密钥仅环境变量：`GITHUB_TOKEN`、`R2_*`；速率限制与体积上限
- 管理器配置 `RelayBaseUrl`（可默认占位，部署后填写）

## UI

- 设置：语言、署名/感谢/非盈利声明
- Mod 浏览 / 库：举报按钮 → 对话框
- 投稿入口：选 dll、填信息、上传（Relay 未配置时提示不可用）

## 非目标

- 内置杀毒引擎 / 「已担保无毒」宣传
- 未审核自动进 catalog
- 赞助/支付 UI
