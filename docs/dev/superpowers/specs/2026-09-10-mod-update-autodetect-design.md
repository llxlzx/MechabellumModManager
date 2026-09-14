# Mod 更新自主检测 + 1.2.1 自更新通道

**Date:** 2026-09-10
**Status:** Approved
**Version:** 1.2.1 (unpublished)

## Goal

1. 玩家在「已装 Mod」页就能看到「可更新」，不必先去 Mod 工坊点一次「刷新目录」。
2. 镜像上的 `catalog.json` 落后于 GitHub 时，客户端仍能看到最新目录。
3. 管理器「检查更新」除镜像与 GitHub Release 外，还能从仓库树的 raw 指针发现新版本。
4. 版本推进到 1.2.1。

## 现状（问题根因）

- 已装行的 `HasUpdate` 只由 `MainViewModel.ApplyCatalogRoot` → `EnrichModsFromCatalog` 点亮。触发它的路径只有：启动检查、工坊「刷新目录」、首次打开工坊、以及仅在 `CatalogMods.Count == 0` 时才跑的详情静默刷新。切到已装页本身不触发任何拉取。
- `ModCatalogService.FetchCatalogAsync` 走 `RemoteFetch.GetAsync`：候选**顺序**遍历、镜像优先、首个 2xx 即返回。镜像返回过期 `catalog.json`（HTTP 200）时永不回落 GitHub。
- `UpdateChecker` 的候选只有镜像 `MechabellumModManager/latest.json` 与 Release `releases/latest/download/latest.json`，加 GitHub API 兜底；不读仓库树，因此「把安装包与 manifest push 到仓库」不产生任何可发现性。

## A. catalog 并行取新

`RemoteFetch` 新增 `GetAllAsync`：并行 GET 全部候选，返回全部成功项；全失败时抛与 `GetAsync` 同形的聚合 `HttpRequestException`。`GetAsync` 语义不变，**mod 二进制下载仍为顺序镜像优先**。

并行必须有界，否则镜像秒回的玩家每次刷新都要等被墙的 GitHub 连接超时，而目录用的 `HttpClient` 是无限 `Timeout`（刻意如此，避免给目录体积设上限）：

- `AllCandidatesTimeout`（60s）：一份都没回来时的硬上限。它界定的是「卡住」而不是「体积」——`GetAllAsync` 只服务 catalog 与更新 manifest 这两种小 JSON；mod 二进制仍走 `GetAsync` 与逐次读取的 `IdleTimeout`，不受影响。
- `StragglerGrace`（5s）：已有候选完整返回后，其余候选只再给这么久。代价是比它更慢的源会输掉本轮新鲜度比较，下次刷新再问。

`GetAllAsync` 用 `ResponseContentRead`：成功即意味着 body 已在手。若改用 `ResponseHeadersRead`，返回的响应流仍绑在该候选的 token 上，宽限计时器一触发就会把胜出者的 body 读到一半掐断。

`FetchCatalogAsync` 并行取回后按新鲜度择新。每份的新鲜度 = `CatalogRoot.updatedAt` 与所有 `mod.updatedAt` 中的**最大值**（`DateTimeOffset.TryParse`，`AssumeUniversal`/`AdjustToUniversal`）。取最大值而非只看根字段，是因为维护者更新了某个 mod 却忘记 bump 根字段时，两侧根字段相同会让滞后镜像看起来一样新——正是本机制要防的情形；这同时兼容了没有根字段的旧目录。时间相等或都不可解析时保持镜像优先。

胜出那份写入 `catalog-cache.json`；`LastFetchSource` 记胜出源。镜像旧于 GitHub 时写一条日志，便于排查镜像同步滞后。

目录可能来自 GitHub 而二进制仍镜像优先，于是滞后镜像有可能返回同样大小的旧 DLL（程序集按 512 字节对齐，改动后大小不变很常见）而躲过 size 校验。因此 `DownloadModAsync` 的 sha256 校验移入候选循环：不匹配就删掉 part、记一次失败并换下一个候选，而不是直接抛错。

## B. 已装页自主静默刷新

- `ActiveContentPage` 变为 `MainContentPage.Library` 时触发 `SilentRefreshCatalogForLibraryAsync()`（fire-and-forget）。
- 与既有 `SoftRefreshCatalogForLibraryDetailAsync` 的关键差异：**不**要求 `CatalogMods.Count == 0`，所以已加载过旧目录也会刷新。
- 节流 60 秒（`_lastSilentCatalogFetch`）；与 `_checkingCatalog`、`_addingCatalogMod` 互斥，避免与工坊刷新或下载并发。
- 失败静默：写日志、保留既有 enrichment、不弹窗。
- `RunStartupModUpdateCheckAsync` 也占用 `_checkingCatalog` 并预先打上节流时间戳，否则启动拉取途中切到已装页会并发再拉一次。
- `RefreshCatalogAsync` 在 `_checkingCatalog` 时不再静默返回，而是写一行「正在拉取目录…」——否则玩家点刷新看起来像按钮失灵。
- 选中态重置（`SelectedCatalogMod` / `SetCatalogSelection` / `_unselectCatalog`）移入 `ApplyCatalogRootCore`：静默刷新会重建 `CatalogMods`，若不重置，选中项会指向已丢弃的 view model，按钮仍按旧目录状态可用。
- 顺带修 `RunStartupModUpdateCheckAsync`：拉取失败时回落 `TryLoadCachedCatalog()` + `ApplyCatalogRoot`，与 `RefreshCatalogAsync` 行为一致，避免启动失败后状态列长期空白。

## C. 「检查更新」增加 raw 仓库指针

- 新常量 `RawLatestJsonUri` = `https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json`。
- `BuildLatestJsonCandidates()` = 镜像（若配置） + Release `latest.json` + raw 指针。
- `TryFetchLatestJsonAsync` 改并行，取 `version` **最高**的 manifest；版本相同且两侧都带可解析 `publishedAt` 时按较新的算；仍无法区分时优先级 镜像 > Release > raw。
- 版本号无法被 `IsNewer` 排序的 manifest（`IsComparableVersion` 为 false）直接丢弃：`IsNewer` 对不可解析版本双向返回 false，否则 `publishedAt` 会把决定权交给一个没人能比较的 manifest。
- 全部 JSON 源失败时保留 `TryFetchFromApiAsync` 兜底。
- `setupUrl` 改用 `ExternalUrlPolicy.IsHttpsUrl` 校验（通用 `TryParse` 还允许 `mailto:`，那不该成为安装包链接）；不合规则回落到 Releases 页面。`ClassifySource` 对 `raw.githubusercontent.com` 已归类为 `github`，无需新来源标签。

## D. 版本 1.2.1

`MechabellumModManager.csproj`、`installer/MechabellumModManager.iss`、新建 `release/v1.2.1/latest.json`，以及新建仓库指针 `release/latest.json`（raw 通道读取，每次发版**最后单独推送**，因此它此刻指向的是已经能下载的 1.2.0）。发版文档补记这一条。

## 已知边界

raw 指针只解决**发现**新版本。`setupUrl` 必须指向可访问的 https —— GitHub Release 资产，或 `sync-mirror.ps1 -IncludeSetup -MirrorBaseUrl ...` 镜像出来的地址。仅把 Setup 提交进 git 树不足以让玩家装上新版。

由此推出一条硬性发版顺序：**`release/latest.json` 必须最后推送**，且仓库里的它始终指向当前已经能下载的那一版（本次改动中即 1.2.0）。它一进 master 就对所有玩家生效，若此时 `setupUrl` 指向的资产还不存在，玩家会被提示更新却下载 404。发版流程写在 `docs/releasing.md` 第 8 节：先发 Release / 跑 sync-mirror → curl 确认可下载 → 再单独提交推送指针。

## 不做

- 后台定时轮询（只在切入已装页时拉）。
- 改 mod 二进制下载的镜像优先顺序。
- 进程内自动下载或静默安装；发现新版本仍是打开 `setupUrl`。
- 代跑 GitHub Release 发布与 `sync-mirror.ps1`。

## 测试

- `ModCatalogServiceTests`：镜像旧 + GitHub 新 → 用 GitHub；仅镜像成功 → 用镜像；`updatedAt` 相等 → 镜像；根字段缺失 → 用条目最大 `updatedAt`。
- `RemoteFetchTests`：`GetAllAsync` 部分失败仍返回成功项；全失败抛聚合异常并含各 host。
- `ModUpdateFlowTests`：切到 Library 触发一次拉取并点亮 `HasUpdate`；节流窗口内二次切入不再请求；启动检查失败回落缓存。
- `UpdateCheckerTests` / `MirrorContractTests`：候选含 raw 指针且顺序符合预期；raw 版本更高而镜像旧 → `UpdateAvailable`。
