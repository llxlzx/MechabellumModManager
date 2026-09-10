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

`FetchCatalogAsync` 改为并行取回后择新：

| 步骤 | 规则 |
|---|---|
| 1 | 比较 `CatalogRoot.updatedAt`（`DateTimeOffset.TryParse`，`AssumeUniversal`/`AdjustToUniversal`） |
| 2 | 根字段缺失或不可解析时，退化为各 `mod.updatedAt` 的最大值 |
| 3 | 仍无法比较，或两侧时间**相等** | 保持镜像优先（沿用旧语义） |

胜出那份写入 `catalog-cache.json`；`LastFetchSource` 记胜出源。镜像旧于 GitHub 时写一条日志，便于排查镜像同步滞后。

## B. 已装页自主静默刷新

- `ActiveContentPage` 变为 `MainContentPage.Library` 时触发 `SilentRefreshCatalogForLibraryAsync()`（fire-and-forget）。
- 与既有 `SoftRefreshCatalogForLibraryDetailAsync` 的关键差异：**不**要求 `CatalogMods.Count == 0`，所以已加载过旧目录也会刷新。
- 节流 60 秒（`_lastSilentCatalogFetchUtc`）；与 `_checkingCatalog`、`_addingCatalogMod` 互斥，避免与工坊刷新或下载并发。
- 失败静默：写日志、保留既有 enrichment、不弹窗。
- 顺带修 `RunStartupModUpdateCheckAsync`：拉取失败时回落 `TryLoadCachedCatalog()` + `ApplyCatalogRoot`，与 `RefreshCatalogAsync` 行为一致，避免启动失败后状态列长期空白。

## C. 「检查更新」增加 raw 仓库指针

- 新常量 `RawLatestJsonUri` = `https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json`。
- `BuildLatestJsonCandidates()` = 镜像（若配置） + Release `latest.json` + raw 指针。
- `TryFetchLatestJsonAsync` 改并行，取 `version` **最高**的 manifest；版本相同按 `publishedAt` 较新；仍无法区分时优先级 镜像 > Release > raw。
- 全部 JSON 源失败时保留 `TryFetchFromApiAsync` 兜底。
- `setupUrl` 仍须 https 且经 `ExternalUrlPolicy` 校验。`ClassifySource` 对 `raw.githubusercontent.com` 已归类为 `github`，无需新来源标签。

## D. 版本 1.2.1

`MechabellumModManager.csproj`、`installer/MechabellumModManager.iss`、新建 `release/v1.2.1/latest.json`，以及新建仓库指针 `release/latest.json`（raw 通道读取，每次发版同步更新）。发版文档补记这一条。

## 已知边界

raw 指针只解决**发现**新版本。`setupUrl` 必须指向可访问的 https —— GitHub Release 资产，或 `sync-mirror.ps1 -IncludeSetup -MirrorBaseUrl ...` 镜像出来的地址。仅把 Setup 提交进 git 树不足以让玩家装上新版。

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
