# Scale-Friendly Update Detection（分层新鲜度）

**Date:** 2026-09-13  
**Status:** Approved — implemented (approach C)  
**Constraint:** 日常安静，发版后仍能较快刷到（折中，非极致省流）

## Goal

用户量上升后，降低镜像上的**重复全量读**（catalog / latest.json body），同时保留：

1. 已装页仍能自主发现 Mod 更新（不必先去工坊点刷新）。  
2. 镜像落后时仍可用 GitHub 纠偏（并行取新语义不删）。  
3. 发版同步镜像后，玩家在约一个热缓存窗口内能发现更新。  
4. 手动「刷新目录 / 检查更新」仍立即、完整。

## Non-goals

- 后台定时轮询（进程挂着每 N 分钟自动打网）。  
- 推送 / WebSocket / 中心通知。  
- 静默自动下载或安装 Mod / Setup。  
- 一期做预览图磁盘缓存（列为二期）。  
- 改 Mod 二进制的镜像优先下载顺序。  
- 默认把热缓存时长暴露到设置 UI（可先用常量 / 可选 `AppConfig`，UI 以后再说）。

## Background（现状）

见 `2026-09-10-mod-update-autodetect-design.md`：

- 切到已装页 → `SilentRefreshCatalogForLibraryAsync`，节流 **60s**。  
- 每次 `FetchCatalogAsync` → `GetAllAsync` **并行全量**拉镜像 + GitHub catalog body。  
- 启动检查、工坊刷新同路径。  
- 无 `ETag` / `If-None-Match`；未变也重复下 JSON。  
- 管理器「检查更新」手动触发，并行读多份 `latest.json`。

请求次数在小用户量下费用可忽略；用户变多后，贵的是「每人每次切页都打满 body + 并行双源」，不是「发现更新」本身。

## Approach C — 分层新鲜度

客户端对 **catalog**（及可选的 manager `latest.json`）维护统一的新鲜度状态；**触发点不变**（启动检查、切入已装页、工坊刷新、手动检查），只改「这一次要不要联网、联什么网」。

### States

| 状态 | 条件 | 切入已装 / 启动静默检查时 |
|------|------|---------------------------|
| **热缓存 Hot** | 距上次成功应用目录 &lt; `HotCacheTtl` | **不联网**；沿用内存 / `catalog-cache.json` enrichment |
| **温探测 Warm** | 热缓存已过期，且距上次探测 ≥ `HotCacheTtl` | **轻探**（见下）；未变则续热；已变则冷刷新 |
| **冷刷新 Cold** | 探针表明有变；无可用条件请求；或用户手动刷新 | 现有 `GetAllAsync` 全量并行 + 新鲜度择新 |

默认：`HotCacheTtl = 15 minutes`（与温探测最短间隔对齐，避免抖）。

启动检查与静默刷新**共用** `_lastSilentCatalogFetch` / 新的「上次成功应用」时间戳，避免启动全量后再切已装立刻再打。

### Warm probe（轻探）

**首选（有可靠校验子时）：**

1. 对镜像 `MechabellumMods/catalog.json` 发条件 GET：`If-None-Match`（及可选 `If-Modified-Since`），带上本地缓存的 ETag / 修改时间。  
2. **304** → 目录未变；刷新热缓存时钟；**不**打 GitHub；不重写 cache（或只触碰时间戳）。  
3. **200** → 读新 body；可直接反序列化应用，或进入 Cold 再与 GitHub 比新鲜度。  
   - 推荐：**200 后走 Cold（GetAllAsync）**一次，避免「镜像刚更新但仍落后 GitHub」时只吃镜像。若发版流程保证先 sync-mirror 再推指针，也可「200 且 `updatedAt` 已新于本地则先应用镜像，再异步 Cold」——一期取简单路径：**200 → Cold**。

**保底（无 ETag / 304 不可用）：**

1. 仅顺序 GET 镜像 catalog（`GetAsync` 镜像优先，首个 2xx）。  
2. 与本地缓存比较新鲜度指纹（根 `updatedAt` 与条目最大 `updatedAt`，算法与现 `FetchCatalogAsync` 择新一致）。  
3. **相同** → 续热；不打 GitHub。  
4. **不同或不可比** → Cold（`GetAllAsync`）。

手动「刷新目录」：**始终 Cold**，绕过 Hot/Warm。

### Manager self-update

- 手动「检查更新」：保持现有并行 `latest.json` 行为（玩家显式要求）。  
- 若启动时检查管理器（若已有或以后加）：与 catalog **共用节流时钟**；优先对镜像 `MechabellumModManager/latest.json` 条件 GET；304 则跳过；200 / 无 ETag 再走现有并行逻辑。  
- **绝不**因探测预拉 Setup 安装包。

### Preview images（二期）

- URL（或 sha）键磁盘缓存；同键不重复下载。  
- 不纳入一期验收。

### Ops

- `sync-mirror.ps1` / 发版文档补一句：COS 对象默认 ETag 即可；勿配置成迫使客户端每次全量 200 的 Cache-Control（若曾手动改过需核对）。  
- 发版顺序不变：先同步可下载资产与 catalog，再推 `release/latest.json` 指针。

## Defaults

| 参数 | 值 | 说明 |
|------|-----|------|
| `HotCacheTtl` | 15 min | 热缓存与温探测最短间隔 |
| 手动刷新 | 绕过节流 | Cold |
| 后台轮询 | 无 | Non-goal |

参数可放内部常量；可选写入 `AppConfig` 便于测试注入，**一期不要求设置页控件**。

## Correctness invariants（必须保留）

1. Cold 路径仍：镜像落后时 GitHub 更新的 catalog 能胜出（现有新鲜度比较）。  
2. 下载 Mod 仍镜像优先 + sha256 校验循环（本设计不改）。  
3. `_checkingCatalog` / `_addingCatalogMod` 互斥保留。  
4. 静默失败：写日志、保留已有 enrichment、不弹窗。  
5. Fail-open：探针异常时宁可 Cold 一次或沿用缓存，不因省流导致已装页永久空白。

## Rough scale（预期）

100 DAU × 日均打开 2 次 ≈ 数百次/日探测；多数可为 **304** 或零请求（热缓存）。相对「每 60s 切页就全量并行」，请求与 body 流量应明显下降，而发版后发现延迟约 **≤ HotCacheTtl**（外加玩家下次打开/切已装页）。

## Testing

- Hot：TTL 内二次切入已装页 → **零** HTTP（catalog 候选）。  
- Warm 304：过期后切入 → 仅镜像条件 GET；无 GitHub；enrichment 不变。  
- Warm 200 → Cold：镜像 ETag 变 → 触发 GetAllAsync；落后镜像 + 新 GitHub 时仍选 GitHub。  
- 保底无 ETag：镜像 body 指纹相同 → 不 Cold；不同 → Cold。  
- 手动刷新：TTL 内仍 Cold。  
- 启动检查与切入已装：不在同一热窗内双打全量。  
- 既有 `ModUpdateFlowTests` / `ModCatalogServiceTests` / `RemoteFetchTests` 回归；新增条件请求用例（scripted handler 返回 304/200）。

## Rollout

- 随管理器小版本发布（版本号实现时再定）。  
- 不要求玩家改设置；默认即分层行为。  
- 可通过日志区分 `catalog hot|warm-304|warm-full|cold` 便于对照 COS 用量。

## Open points（实现时可定，不阻塞本规格）

- 是否把 `HotCacheTtl` 写入 `AppConfig`：建议写入便于测试，UI 不做。  
- Warm **200 → Cold** 已定为一期行为；若线上镜像几乎总与 GitHub 同步，可后续再改成「200 直接应用镜像」以再省一轮并行。
