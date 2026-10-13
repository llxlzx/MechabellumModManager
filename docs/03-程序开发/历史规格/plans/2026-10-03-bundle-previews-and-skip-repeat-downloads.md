# Bundle Previews And Skip Repeat Downloads Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the current mod preview images inside the manager, download a preview only when the catalog hash is one this install has never stored, and stop downloading the mirror catalog a second time after a conditional request already returned its body.

**Architecture:** `previewSha256` (and the same field on a locale that has its own `preview`) is the identity of a poster. `PreviewStore` resolves bytes from `Assets/previews` or `{DataRoot}/preview-cache/{sha256}` before any HTTP call. `PreviewImageLoader` downloads only on a miss, checks the hash, and writes the cache with a temp file plus replace. A conditional catalog 200 reuses that body as the mirror candidate and still asks GitHub, so a stale mirror cannot hide a newer catalog.

**Tech Stack:** C# / .NET 8 WPF, xUnit, Python catalog stamp (`MechabellumMods/scripts/stamp_hashes.py`).

**Working branch:** none (working tree only, no commits)

## Global Constraints

- A preview is identified by the SHA-256 of its bytes, lowercase hex, 64 characters. The catalog path is only where to look inside the install and which URL to fetch.
- A locale `preview` is paired with that locale's `previewSha256` only. Never compare an English poster to the default preview's hash.
- When the catalog hash matches a bundled or cached file, do not open a network connection for that image.
- When the hash is absent, a bundled file at that catalog path is used and not downloaded. This covers catalogs published before the field exists.
- When the hash is present and differs from the bundle, download once. A failed download may show the bundled file at the same path so the card is not blank.
- Catalog paths must stay relative. Reject empty paths, `..`, `.`, and any `://`.
- Cache files are named by the hash, never by the catalog path.
- A conditional catalog response of 200 must not be the only copy considered. GitHub is still fetched and the newer stamp wins. The mirror URL is not fetched again in that pass.
- Do not add `If-None-Match` to `latest.json`. A 304 from the mirror would be easy to treat as "no manager update" and would hide a newer GitHub release. The file is about 1 KB; the bug is not worth the savings.
- Do not remove large mods from the domestic mirror.
- Single-file publish does not embed `Content` items. Previews live under `Assets/previews/` and the installer already copies `publish\Assets\*` recursively. `dotnet publish` must emit that folder, and `build-installer.ps1` must copy it with `-Recurse` so a later top-level copy cannot be the only way the files arrive.
- No git commit in this plan.

## Self-check (applied before implementation)

| Risk | Decision |
|---|---|
| Reusing a mirror 200 and skipping GitHub hides a newer catalog. Existing test `Smart_fetch_Warm_200_runs_Cold_and_can_prefer_github` requires GitHub to win. | Keep the GitHub fetch. Do not GET the mirror again. Add a count assertion. |
| Fingerprint mismatch already downloaded the mirror body, then `ColdAsync` downloads it again. | Feed that body into the same freshest-copy pick. |
| Locale preview without its own hash accidentally uses the default hash, so every English poster misses the cache and re-downloads, or worse, accepts the wrong bytes. | Hash is null unless it sits on the same locale object as the chosen path. |
| `preview.png` replaced, path unchanged, hash not yet published. | Bundled file is used. After `previewSha256` is published, a mismatch downloads once. |
| Hash matches a truncated cache file that happens to be named correctly. | Read the cache file and re-hash it. Mismatch means miss, then download. |
| Cache write torn by a crash or two loads of the same hash. | Write `{hash}.tmp-{guid}` and `File.Move` over the destination. |
| `..\..\Windows` in `preview`. | `NormalizeCatalogRelativePath` throws; the loader shows nothing and does not touch disk outside the cache. |
| `PublishSingleFile` drops loose content; `Copy-Item Assets\*` is not recursive. | Content item preserves `Assets/previews/**`. Installer script copies with `-Recurse`. Inno already has `recursesubdirs`. |
| `latest.json` conditional request. | Rejected. See Global Constraints. |
| Taking CUT Guide off the hot mirror. | Rejected. Saves egress, makes the 344 MB download come from GitHub for domestic players. |

## File structure

- Create `src/MechabellumModManager/Services/PreviewStore.cs` — local bundle and hash cache. No HTTP, no WPF.
- Create `src/MechabellumModManager/Assets/previews/**` — copies of the current catalog previews.
- Modify `src/MechabellumModManager/Models/CatalogModLocale.cs` — `PreviewSha256`.
- Modify `src/MechabellumModManager/Services/ModCatalogService.cs` — `CatalogMod.PreviewSha256`; conditional 200 and fingerprint mismatch do not re-GET the mirror.
- Modify `src/MechabellumModManager/Services/CatalogLocaleResolver.cs` — `ResolvePreviewIdentity`.
- Modify `src/MechabellumModManager/ViewModels/PreviewImageLoader.cs` — consult the store, then download.
- Modify `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs` and `ModItemViewModel.cs` — pass path, hash, and cache root.
- Modify `src/MechabellumModManager/ViewModels/MainViewModel.cs` — pass `_paths.DataRoot` into preview loads.
- Modify `src/MechabellumModManager/MechabellumModManager.csproj` — copy `Assets/previews/**/*.png`.
- Modify `packaging/installer/build-installer.ps1` — `Copy-Item -Recurse`.
- Modify `D:\gongzuo\独立工作区\MechabellumMods\scripts\stamp_hashes.py` and `validate_catalog.py` — stamp and check `previewSha256`.
- Modify `D:\gongzuo\独立工作区\MechabellumMods\catalog.json` — stamped hashes only.
- Test `tests/MechabellumModManager.Tests/PreviewStoreTests.cs`.
- Test additions in `CatalogLocaleResolverTests.cs` and `CatalogFreshnessTests.cs`.

## Interfaces

```csharp
public readonly record struct ResolvedPreview(string? RelativePath, string? Sha256);

public static class CatalogLocaleResolver
{
    public static ResolvedPreview ResolvePreviewIdentity(CatalogMod mod, string? culture = null);
}

public static class PreviewStore
{
    public enum LocalKind { Bundle, Cache }

    public readonly record struct LocalHit(LocalKind Kind, byte[] Bytes);

    public static LocalHit? TryLocal(string bundleRoot, string? cacheRoot, string relativePath, string? sha256);

    public static LocalHit? TryStaleBundle(string bundleRoot, string relativePath);

    public static void Save(string cacheRoot, string relativePath, string sha256, byte[] bytes);
}
```

`TryLocal` returns null when a download is required. `TryStaleBundle` is only for after every candidate has failed.

`sha256` arguments are normalized: trim, strip a `sha256:` prefix, require 64 hex digits, lowercase. Anything else is treated as absent.

---

### Task 1: PreviewStore

**Files:**
- Create: `src/MechabellumModManager/Services/PreviewStore.cs`
- Test: `tests/MechabellumModManager.Tests/PreviewStoreTests.cs`

**Interfaces:**
- Consumes: `ModCatalogService.NormalizeCatalogRelativePath`
- Produces: `PreviewStore.TryLocal`, `TryStaleBundle`, `Save`

- [x] **Step 1: Write failing tests**

Cover: bundle hash hit does not need the cache; cache hit when the bundle hash differs; hash mismatch returns null and `TryStaleBundle` still returns the old bytes; absent hash uses the bundle; absent hash with no bundle uses a previously saved path entry; `..` returns null and does not create files; `Save` then `TryLocal` round-trips; a truncated cache file named with the right hash is a miss.

- [x] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter PreviewStoreTests`

- [x] **Step 3: Implement `PreviewStore`**

Bundle file is `bundleRoot` plus the normalized relative path. Cache file is `cacheRoot` plus the 64-char hash (no extension). Path index is `cacheRoot/by-path.txt`, one `relative-path<TAB>hash` line. `Save` replaces that path's line and writes the hash file via a sibling temp name.

- [x] **Step 4: Re-run the filter and confirm pass**

### Task 2: Locale identity

**Files:**
- Modify: `src/MechabellumModManager/Models/CatalogModLocale.cs`
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs` (`CatalogMod.PreviewSha256`)
- Modify: `src/MechabellumModManager/Services/CatalogLocaleResolver.cs`
- Test: `tests/MechabellumModManager.Tests/CatalogLocaleResolverTests.cs`

**Interfaces:**
- Produces: `CatalogMod.PreviewSha256`, `CatalogModLocale.PreviewSha256`, `CatalogLocaleResolver.ResolvePreviewIdentity`

- [x] **Step 1: Failing test**

English locale with its own preview and hash resolves to that pair. English locale with a preview and no hash resolves to that path and a null hash, even when the mod-level `previewSha256` is set. No locale preview uses the mod-level path and hash.

- [x] **Step 2: Run and confirm fail**

- [x] **Step 3: Implement the properties and `ResolvePreviewIdentity`**

- [x] **Step 4: Run and confirm pass**

### Task 3: Loader uses the store

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/PreviewImageLoader.cs`
- Modify: `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `PreviewStore`, `ResolvePreviewIdentity`, `ModCatalogService.GetPreviewCandidateUrls`

- [x] **Step 1: Add an internal download seam and a test that a local hit performs zero HTTP**

`PreviewImageLoader` stays the WPF decoder. Put the fetch decision in `PreviewAcquisition.AcquireAsync` (same file or `PreviewStore` companion) which returns bytes. Test it with a fake `HttpMessageHandler` that fails the test if called.

- [x] **Step 2: Run and confirm fail**

- [x] **Step 3: Implement acquire: TryLocal, else GET candidates in order, hash the body, Save, on total failure TryStaleBundle**

Wire catalog cards and the library detail to pass the resolved path, hash, candidate URLs, bundle root `AppContext.BaseDirectory/Assets/previews`, and cache root `_paths.DataRoot`.

- [x] **Step 4: Run the new test and the existing preview-related tests**

### Task 4: Do not download the mirror catalog twice

**Files:**
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs`
- Test: `tests/MechabellumModManager.Tests/CatalogFreshnessTests.cs`

**Interfaces:**
- Consumes: `FetchCatalogAsync` / `PickFreshestIndex` behavior
- Produces: unchanged `CatalogFetchResult` kinds

- [x] **Step 1: Extend `Smart_fetch_Warm_200_runs_Cold_and_can_prefer_github`**

After the probe, mirror host requests that happened during the probe (not the seed) equal 1. GitHub still supplies `2026-09-13`.

Add a fingerprint-mismatch test: first mirror body is older than GitHub, no ETag, cached catalog is older. Result is the GitHub stamp, and the mirror host is requested once.

- [x] **Step 2: Run and confirm the new assertion fails (mirror requested twice today)**

- [x] **Step 3: Implement**

On conditional 200, keep that JSON and ETag as the mirror `CatalogCopy`. `GetAllAsync` only the non-mirror candidates. `PickFreshestIndex` as today. Persist the winner the same way `FetchCatalogAsync` does.

On fingerprint mismatch, do the same with the body already read.

- [x] **Step 4: Run `CatalogFreshnessTests`**

### Task 5: Bundle the files and stamp the catalog

**Files:**
- Create: `src/MechabellumModManager/Assets/previews/mods/**/preview*.png`
- Modify: `src/MechabellumModManager/MechabellumModManager.csproj`
- Modify: `packaging/installer/build-installer.ps1`
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\scripts\stamp_hashes.py`
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\scripts\validate_catalog.py`
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\catalog.json`

- [x] **Step 1: Copy every `preview*.png` from the mods repo into `Assets/previews/`, preserving `mods/{id}/...`**

- [x] **Step 2: csproj content include and recursive installer copy**

- [x] **Step 3: Stamp `previewSha256` beside each `preview` string, including locale previews. Validate the file hash. Do not rewrite unrelated catalog lines.**

- [x] **Step 4: `dotnet test` for the manager test project**

Preview store, acquisition, locale identity, and catalog freshness: 24 passed. A full-suite run also reported 31 failures while Mechabellum was running; deploy was refused with the in-game lock, so those failures are outside this change.

Catalog publish to GitHub and the mirror is not part of this plan. Until `previewSha256` is published, the manager uses bundled files because the hash is absent. Poster changes start downloading once the stamped catalog is what players fetch.
