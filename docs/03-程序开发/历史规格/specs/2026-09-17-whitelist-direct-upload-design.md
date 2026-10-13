# 白名单直传 + 邮件双通道 Mod 发布设计

**日期:** 2026-09-17  
**状态:** 已定稿（实现中）  
**范围:** 管理器内白名单作者一键发布到 `MechabellumMods`；普通作者仍走邮件投稿

---

## 1. 目标与非目标

### 目标

- **双通道并行**
  - **通道 A（默认）:** 现有「投稿 Mod」邮件流程，不变。
  - **通道 B（白名单）:** 少数受邀作者在管理器内一键上传/更新，经服务端写入 `llxlzx/MechabellumMods`。
- **对作者简单:** 填邀请码 → 选文件/填元数据 → 上传；成功即表示已进入 GitHub 目录真相源。
- **对你可控、便宜:** Cloudflare Worker 最小实现；GitHub token 与邀请码表只在服务端；CF 免费档覆盖小流量。
- **不破坏现有玩家链路:** 目录契约（`sha256` / `size` / 大文件 `originUrl`）与下载顺序（镜像 → `originUrl` → raw）保持不变。

### 非目标（本设计不做）

- 全员开放上传 / 公开注册。
- 用嵌套 git 仓库 + 本地 copy 脚本替代发布（那是本机同步思路，不是社区发布）。
- MVP 内强制全量 COS 镜像同步（见 §7；可后续增量补）。
- 自动病毒扫描、人工审每一笔白名单上传（信任模型见 §8）。
- 替换邮件通道或删除 `SubmitGuideDialog`。

---

## 2. 四条不变量（逻辑补丁，必须满足）

1. **一次直传 = 一次完整发布事务**  
   计算哈希 →（如需）上传 GitHub Release 并得到可下载 URL → 更新仓库内文件（小体积）与 `catalog.json`（含 `sha256`/`size`，大文件含 `originUrl`）。任一步失败则整单失败，不留下「目录已改但文件没有」或「缺 sha256」的半成品。

2. **`catalog.json` 更新必须防并发覆盖**  
   使用 GitHub Contents API 的 SHA 条件更新；冲突则有限次重试（重新 GET → 合并本条目 → PUT）。禁止裸「读改写」无重试。

3. **无主人的已有条目默认拒绝直传**  
   仅「新 id」或「该邀请码已是主人」或「维护者已认领给该邀请码」允许写入。邮件上架、尚未认领的条目，白名单作者不能更新。

4. **对外语义:** 直传成功 = **已发布到 GitHub `MechabellumMods`**；国内 COS 热缓存可能滞后。UI/文案必须区分，避免作者误判失败。

---

## 3. 整体架构

```
普通作者 ──► 投稿邮件 ──► 维护者审核 ──► 手动写入 MechabellumMods
              （现状，保持）

白名单作者 ──► 管理器「直传」──► Cloudflare Worker
                                   │
                                   ├─ 校验邀请码 ∈ 白名单且未吊销
                                   ├─ 校验归属（新 id / 同码主人 / 已认领）
                                   ├─ 完整发布事务（不变量 1）
                                   └─ 持久化 ownership 映射
                                          │
玩家 ──► 刷新目录 ──► 下载（镜像 → originUrl → raw）
```

| 组件 | 职责 |
|------|------|
| 管理器 UI | 邮件投稿入口保留；直传入口（邀请码、元数据、选 DLL/预览、进度与结果文案） |
| Cloudflare Worker | 鉴权、归属、发布事务、ownership 存储 |
| GitHub `MechabellumMods` | 目录与文件真相源；CI `validate_catalog` 仍跑 |
| GitHub Release `mods-current` | 大文件（及与现网一致的二进制托管策略） |
| COS 镜像 | 玩家加速；MVP 不阻塞直传成功 |

仓库写权限凭证（GitHub token）**仅**存在于 Worker secrets，不进管理器安装包、不进邀请码本身。

---

## 4. 邀请码、归属与认领

### 4.1 邀请码

- 由你离线生成（高熵随机串），写入 Worker 侧白名单配置（KV / 加密 JSON / secrets 列表，实现选定一种即可）。
- 每条记录建议字段：`inviteId`（稳定内部 id）、`codeHash`（只存哈希，不存明文）、`label`（备注，如作者名）、`enabled`、`createdAt`、`revokedAt?`。
- 作者在管理器本地保存的是**明文邀请码**（类似许可证）；传输仅走 HTTPS。
- **吊销:** `enabled=false` 后立即不能新传；已绑定 mod 的后续更新策略默认 **冻结**（该码无法再更新；需你改认领或重新启用）。

### 4.2 归属（ownership）

- 映射：`modId → inviteId`（注意存 `inviteId` 而非码明文，便于轮换码时迁移）。
- **新 mod:** 直传成功且 catalog 中原不存在该 `id` → 绑定当前 `inviteId` 为该 mod 主人。
- **更新:** 仅当 `ownership[modId] == 当前 inviteId` 时允许覆盖文件与该条目元数据。
- **权限边界（已选方案）:** 白名单可创建任意**新** `id`；不可更新他人已绑定条目；不可更新无主人条目（除非认领）。

### 4.3 认领（维护者操作）

- 对邮件通道已存在、尚无主人的 `modId`，你在服务端配置中写入 `modId → inviteId`。
- 认领后，该邀请码可走直传更新。
- 不提供作者自助「抢占」；禁止「谁先更新谁得手」。

### 4.4 邀请码轮换

- 支持：同一 `inviteId` 作废旧码、签发新码（只改 `codeHash`）。
- 归属仍挂在 `inviteId` 上，作者换码后不丢名下 mod。

### 4.5 mod id 规则

- `id` 与目录文件夹名一致（沿用现有仓库约定）；字符集与现网 `validate_catalog` 一致。
- **冲突:** 目标 `id` 已在 catalog 中且当前码不是主人 → 拒绝，明确错误码。
- 邮件正在处理、尚未进 catalog 的同名 id：以 catalog 为准；进 catalog 前直传可「占坑」——运营上你应避免与邮件并行开同一 id（文档约定即可，系统不解析邮件）。

---

## 5. 管理器（客户端）行为

### 5.1 入口

- **投稿 Mod:** 现有 `SubmitGuideDialog` / 邮件模板，逻辑不变。
- **直传:** 主栏「投稿 Mod」旁常驻按钮「白名单直传」（`DirectUpload`）。
  - 打开同一对话框：无邀请码时先填码并写入 `AppConfig`；有码则可新建/更新。
  - 更新时可拉取 `/v1/mods/owned` 列出本码已绑定 id，也可手输 id（最终以服务端裁定为准）。

### 5.2 本地存储

- 邀请码存 `%AppData%\MechabellumModManager\config.json` 字段 `AuthorInviteCode`（与现有 `AppConfig` 一致，明文 JSON；传输仅 HTTPS）。
- Worker 基础 URL：编译期默认常量 + 可选 `AppConfig.DirectUploadApiBaseUrl` 覆盖（便于你换部署域）；**不得**嵌入 GitHub token。

### 5.3 上传表单（MVP 字段）

| 字段 | 新建 | 更新 | 说明 |
|------|------|------|------|
| `id` | 必填 | 必填 | 与仓库约定一致 |
| 显示名 / summary | 必填 | 可选改 | 写入 catalog 条目 |
| 类型 / category | 按现网 | 可选改 | 与现 catalog 字段对齐 |
| DLL 文件 | 必填 | 必填（换包） | 主二进制 |
| preview.png | 可选 | 可选 | 有则覆盖 |
| 版本号 | 建议必填 | 建议必填 | 展示用；**更新判定仍以 sha256 为主** |

locales 等高级字段：MVP 可只写默认语言字段；后续再开编辑器。

### 5.4 交互与文案

- 进度：校验 → 上传 → 服务端处理 → 完成。
- 成功文案必须包含：已写入 GitHub；玩家刷新目录可见；**若使用国内镜像，缓存可能延迟**（不变量 4）。
- 失败：展示服务端错误码对应短句（邀请码无效、无权限、id 冲突、文件过大、并发冲突请重试等）。

### 5.5 不做什么

- 客户端不直接调 GitHub API 写仓库。
- 客户端不实现「pull 嵌套仓库再 copy 到上级」作为发布手段。

---

## 6. Worker API（服务端）

### 6.1 建议端点（MVP）

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/v1/auth/check` | 校验邀请码是否有效（可选，供 UI 预检） |
| `POST` | `/v1/mods/publish` | 新建或更新（完整事务） |
| `GET` | `/v1/mods/owned` | 返回该码名下 `modId` 列表（需邀请码） |

维护者认领 / 吊销：**不**做进公开 API；改 Worker 配置或私有管理脚本即可（MVP）。

### 6.2 `POST /v1/mods/publish`

**请求（逻辑字段，传输可用 multipart）:**

- `inviteCode`
- `id`, `name`, `summary`, `version`, `category`/`type`（与 catalog 对齐的字段子集）
- `dll` 文件字节
- `preview` 可选

**处理步骤（顺序固定）:**

1. 校验邀请码 → 解析 `inviteId`；无效/吊销 → `401`。
2. 计算 DLL（及 preview）`sha256` 与 `size`。
3. GET 当前 `catalog.json`（及 blob SHA）。
4. **授权判定:**
   - catalog **无**该 `id` → 允许新建；成功提交后写入 ownership。
   - catalog **有**该 `id` → 仅当 ownership 中已是该 `inviteId`（含事先认领）→ 允许更新；否则 `403`。
5. 按体积策略放置二进制（见 §6.3）。
6. 合并 catalog 条目（写齐 `sha256`/`size`/`originUrl?`/路径字段）。
7. 条件更新 `catalog.json`（及小文件路径）；SHA 冲突 → 有限重试整段 3–7。
8. 持久化 ownership（新建时）。
9. 返回成功：`modId`、`sha256`、`catalogRevision`（或 commit SHA）、`mirrorNote`。

任一步失败：尽量不提交 catalog；若 Release 资产已上传但 catalog 未改，视为可接受的孤儿资产（下次同名覆盖或定期清理），**不得**提交缺哈希的 catalog。

### 6.3 体积与存放策略

与现网一致：

- **GitHub 单文件硬限 100MB**；超过则必须走 Release 资产，`catalog.originUrl` 指向 Release；git 内可不放该 DLL。
- 未超限：可放 `mods/<id>/...`，`originUrl` 按现网惯例（可与 `publish-mods-release` 策略对齐：为统一下载路径也可一律填 Release——实现时与当前仓库脚本行为对齐，避免两套玩家路径）。
- 资产命名：路径斜杠 → `__` 等现网约定，保持不变。

具体阈值（例如「一律 Release」vs「小文件仅 git」）实现阶段与 `MechabellumMods` 现脚本对齐，本设计只要求：**玩家三候选链路不断、CI 能过**。

### 6.4 限流与体量

- 按邀请码限流（例如每小时 N 次 publish）。
- Worker / 平台请求体大小限制需在实现时核对；超限时改为「预签名直传中间对象再由 Worker 收尾」——列为 **实现阶段风险**，MVP 若只服务小 DLL 作者可先文档声明上限。

---

## 7. 镜像（COS）策略

### MVP

- 直传成功**不**要求已同步 COS。
- 玩家端：冷门自动回落 GitHub/`originUrl`；热门子集可能短暂旧目录——可接受并在文案中说明。
- 你仍可用现有 `sync-mirror` 按需同步。

### 后续（非阻塞本设计）

- Worker 成功后对「本单涉及的 catalog + 文件对象」做增量推送 COS（先文件后 catalog，沿用镜像文档顺序）。
- 或定时同步。

---

## 8. 安全与信任

- 白名单 = 信任边界；不防止恶意 DLL，只防止陌生人上传与改他人条目。
- 邀请码泄露 ≈ 该身份被盗用：立即吊销 + 必要时改 ownership。
- 传输 HTTPS；服务端只存 code 哈希。
- 不在客户端或日志中打印完整邀请码 / GitHub token。
- 与已取消的「全员中继」差异：默认关闭、强归属、可吊销；发码过滥会退化——靠运营控制。

---

## 9. 错误码（客户端可映射）

| 码 | 含义 |
|----|------|
| `invalid_invite` | 码无效或已吊销 |
| `forbidden_not_owner` | 无主人或非主人 |
| `id_conflict` | 新建 id 已存在且非主人 |
| `validation_failed` | 元数据/文件不符合约定 |
| `payload_too_large` | 超过平台或策略上限 |
| `catalog_conflict` | 并发冲突，请重试 |
| `upstream_github` | GitHub 侧失败 |
| `internal` | 其他 |

---

## 10. 测试要点

### 服务端 / 契约

- 新 id + 有效码 → catalog 有条目且含 sha256；ownership 已写。
- 同码更新 → sha256 变，玩家侧可检出更新。
- 他码更新同一 id → 403。
- 无主人旧条目未认领 → 403；认领后同码可更新。
- 吊销后 publish → 401。
- 并发两次 publish 不同 id → 最终 catalog 两条目都在（无丢失）。
- 缺 sha256 的 catalog 不得被提交（可用 staging 仓或契约测试）。

### 客户端

- 无码只见邮件通道；有码可见直传。
- 成功/镜像延迟文案可见。
- 错误码映射正确。

### 回归

- 邮件投稿入口与文案未回归损坏。
- 玩家浏览/下载/更新（哈希）路径未改坏。

---

## 11. 里程碑

| 阶段 | 内容 |
|------|------|
| M1 | Worker：邀请码校验 + ownership + 小文件完整事务 + catalog 条件更新 |
| M2 | 管理器：邀请码设置 + 直传 UI + 错误/成功文案 |
| M3 | 大文件 Release + `originUrl` 对齐现网；体量上限策略 |
| M4 | （可选）增量推 COS；管理端认领工具改善 |

---

## 12. 已拍板决策摘要

| 项 | 决策 |
|----|------|
| 通道 | 邮件保留 + 白名单直传 |
| 直传形态 | 管理器 → Worker → `MechabellumMods` |
| 鉴权 | 邀请码；凭证在服务端 |
| 托管 | Cloudflare Worker（最小） |
| 权限 | 可建任意新 mod；不可改他人 |
| 归属 | 首传成功绑定 `inviteId`；维护者可认领旧 mod |
| 旧 mod | 未认领不可直传更新 |
| 镜像 | MVP 不阻塞；文案说明滞后 |

---

## 13. 已锁定的实现细节（原开放项）

| 项 | 决定 |
|----|------|
| 白名单 + ownership 存储 | Cloudflare **KV**：`invite:<codeHash>` → `{ inviteId, label, enabled }`；`owner:<modId>` → `inviteId`。认领/吊销改 KV，无需重发 Worker。 |
| 小文件策略（MVP） | DLL/预览写入 git 路径 `mods/<id>/...`；**单文件硬上限 95 MiB**（留余量低于 GitHub 100MB）。超限返回 `payload_too_large`；Release/`originUrl` 留 M3。 |
| 上传传输 | 仅 **multipart** 一次请求；不做预签名二段上传（MVP）。 |
| UI 入口 | `MainWindow` 投稿旁「白名单直传」；对话框 `DirectUploadDialog`；字符串键 `DirectUpload*`。 |
| 成功文案 | 必须含「已发布到 GitHub」+「国内镜像可能延迟」。 |
| Worker 源码位置 | 本仓库 `services/mod-publish-worker/`（Wrangler）；部署说明见同目录 README。 |
| 默认 API | `https://mod-publish.<your-subdomain>.workers.dev`（README 填写真实域；客户端用常量 + 可覆盖）。 |
