# Whitelist Direct Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship dual-track Mod publishing: keep email submit; add invite-code direct publish via Cloudflare Worker into `MechabellumMods`, with manager UI.

**Architecture:** Manager multipart-posts to Worker; Worker validates invite (KV), enforces ownership, writes `mods/<id>/` + `catalog.json` via GitHub Contents API with SHA retries. MVP rejects files ≥95 MiB (no Release path yet).

**Tech Stack:** C# / WPF (.NET), xUnit; Cloudflare Worker (TypeScript) + KV + Wrangler; GitHub Contents API.

**Working branch:** `feature/whitelist-direct-upload`  
**Commits:** none (working-tree only; commit only if user explicitly asks later)

## Global Constraints

- Email `SubmitGuideDialog` / `SubmitMod` path must keep working unchanged.
- Never embed GitHub token in the manager.
- Every published catalog entry MUST include `sha256` and `size`; `file` path under `mods/<id>/`.
- Catalog updates MUST use GitHub blob SHA conditional put + retry on conflict.
- Unowned existing mods → reject until KV claim `owner:<modId>=inviteId`.
- Success UX must mention GitHub published + possible COS mirror lag.
- Spec: `docs/dev/superpowers/specs/2026-09-17-whitelist-direct-upload-design.md`

## File map

| Path | Role |
|------|------|
| `services/mod-publish-worker/` | Worker source, wrangler.toml, README |
| `src/.../Services/DirectUploadService.cs` | HTTP client for check/owned/publish |
| `src/.../Models/AppConfig.cs` | `AuthorInviteCode`, `DirectUploadApiBaseUrl` |
| `src/.../Dialogs/DirectUploadDialog.*` | UI |
| `src/.../ViewModels/MainViewModel.cs` | `DirectUpload` command |
| `src/.../MainWindow.xaml` | Sibling button |
| `src/.../Resources/Strings*.resx` + `UiStrings.cs` | i18n |
| `tests/.../DirectUploadServiceTests.cs` | Client unit tests |
| `services/mod-publish-worker/src/*.test.ts` | Ownership/catalog merge unit tests |

---

### Task 1: Worker core — invite, ownership, catalog merge (pure functions + tests)

**Files:**
- Create: `services/mod-publish-worker/package.json`
- Create: `services/mod-publish-worker/tsconfig.json`
- Create: `services/mod-publish-worker/src/crypto.ts` — `sha256Hex(bytes)`, `hashInviteCode(code)`
- Create: `services/mod-publish-worker/src/ownership.ts` — authorize create/update
- Create: `services/mod-publish-worker/src/catalog.ts` — merge entry, require sha256/size
- Create: `services/mod-publish-worker/src/ownership.test.ts`
- Create: `services/mod-publish-worker/src/catalog.test.ts`

**Interfaces:**
- Produces:
  - `hashInviteCode(code: string): Promise<string>`
  - `authorizePublish(args: { modId: string; inviteId: string; catalogHasMod: boolean; ownerInviteId: string | null }): "allow_create" | "allow_update" | "forbidden_not_owner"`
  - `mergeCatalogMod(root: CatalogRoot, entry: CatalogMod): CatalogRoot` (replace by id or append; set `updatedAt`)

- [x] **Step 1: Scaffold package** with `"type":"module"`, vitest, typescript. Scripts: `test`, `build`.

- [x] **Step 2: Write failing ownership tests**

- [x] **Step 3: Implement `ownership.ts` + `catalog.ts` minimally; run `npm test` — expect PASS.**

---

### Task 2: Worker HTTP handler — GitHub put with retry + multipart publish

**Files:**
- Create: `services/mod-publish-worker/src/github.ts` — get/put file by path, base64, sha retry
- Create: `services/mod-publish-worker/src/index.ts` — fetch router
- Create: `services/mod-publish-worker/wrangler.toml`
- Create: `services/mod-publish-worker/README.md` — secrets, KV bind `PUBLISH_KV`, seed invite script notes
- Create: `services/mod-publish-worker/src/github.test.ts` — retry logic with mock fetch

**Env/secrets:** `GITHUB_TOKEN`, `GITHUB_OWNER=llxlzx`, `GITHUB_REPO=MechabellumMods`, `GITHUB_BRANCH=master`  
**KV:** binding `PUBLISH_KV`  
**Limits:** reject if dll size > `95 * 1024 * 1024`

**Routes:**
- `POST /v1/auth/check` body JSON `{ inviteCode }` → `{ ok: true, inviteId }` or 401 `invalid_invite`
- `GET /v1/mods/owned` header `X-Invite-Code` → `{ modIds: string[] }`
- `POST /v1/mods/publish` multipart: `inviteCode`, `id`, `name`, `summary?`, `version?`, `category?`, `type?`, `dll`, `preview?`
  - On allow_create after successful catalog put: `PUBLISH_KV.put("owner:"+id, inviteId)`
  - Response JSON: `{ modId, sha256, size, commitSha, mirrorNote }` with fixed `mirrorNote` Chinese+English short string about mirror lag
  - Errors: `{ error: "<code>" }` with codes from spec §9

- [ ] **Step 1: Implement github get/put with up to 5 retries on 409/sha mismatch.**
- [ ] **Step 2: Implement index router; CORS allow manager origins `*` for MVP (HTTPS only in prod note).**
- [ ] **Step 3: Unit-test retry helper; `npm test` PASS.**
- [ ] **Step 4: README documents: create invite via `wrangler kv key put --binding=PUBLISH_KV "invite:<sha256hex>" '{"inviteId":"author1","enabled":true}'` after hashing code with a one-liner in README.**

---

### Task 3: Manager `DirectUploadService` + config + tests

**Files:**
- Modify: `src/MechabellumModManager/Models/AppConfig.cs` — add:
  - `public string AuthorInviteCode { get; set; } = "";`
  - `public string? DirectUploadApiBaseUrl { get; set; }`
- Create: `src/MechabellumModManager/Services/DirectUploadService.cs`
- Create: `tests/MechabellumModManager.Tests/DirectUploadServiceTests.cs`
- Optionally: `src/.../Services/DirectUploadDefaults.cs` with `public const string ApiBaseUrl = "https://mod-publish.llxlzx.workers.dev";` (placeholder OK; README/worker must match deploy)

**Produces:**
```csharp
public sealed class DirectUploadService
{
    public DirectUploadService(HttpClient http, string apiBaseUrl);
    public Task<DirectUploadAuthResult> CheckAsync(string inviteCode, CancellationToken ct = default);
    public Task<IReadOnlyList<string>> ListOwnedAsync(string inviteCode, CancellationToken ct = default);
    public Task<DirectUploadPublishResult> PublishAsync(DirectUploadPublishRequest req, CancellationToken ct = default);
}
// Map HTTP error JSON {error} to DirectUploadErrorCode enum matching spec.
```

- [ ] **Step 1: Write failing tests with `HttpMessageHandler` stub for check 401 / publish 200 / forbidden.**
- [ ] **Step 2: Implement service; tests PASS.**
- [ ] **Step 3: Run `dotnet test --filter DirectUploadServiceTests`.**

---

### Task 4: DirectUploadDialog + MainViewModel command + i18n + MainWindow button

**Files:**
- Create: `Dialogs/DirectUploadDialog.xaml`, `DirectUploadDialog.xaml.cs`
- Modify: `MainViewModel.cs` — `DirectUpload` relay command opening dialog via injected prompt (mirror `promptSubmitGuide` pattern in `App.xaml.cs`)
- Modify: `MainWindow.xaml` — button beside Submit Mod
- Modify: `App.xaml.cs` — wire dialog
- Modify: `Strings.resx`, `Strings.en.resx`, `Strings.de.resx`, `Strings.ja.resx`, `Strings.ru.resx`, `UiStrings.cs`, `docs/dev/i18n/source-zh-CN.tsv` for keys:
  - `DirectUpload`, `DirectUploadTitle`, `DirectUploadInviteCode`, `DirectUploadSaveCode`, `DirectUploadModId`, `DirectUploadName`, `DirectUploadSummary`, `DirectUploadVersion`, `DirectUploadPickDll`, `DirectUploadPickPreview`, `DirectUploadPublish`, `DirectUploadSuccess`, `DirectUploadMirrorLagHint`, `DirectUploadFailed`, plus error-code strings as needed

**Dialog behavior:**
1. Show/edit invite code → save to config via callback.
2. Fields for id/name/summary/version; pick dll (required); preview optional.
3. Publish → `DirectUploadService.PublishAsync`; on success show Success + MirrorLagHint; save invite if changed.
4. Optional: load owned ids into a combo after check.

- [ ] **Step 1: Add strings + UiStrings properties.**
- [ ] **Step 2: Implement dialog + command wiring.**
- [ ] **Step 3: Manual smoke: open dialog (build app). Automated: extend a thin test that `DirectUploadCommand` invokes prompt when injected.**
- [ ] **Step 4: `dotnet build` + `dotnet test` relevant filters PASS.**

---

### Task 5: Spec/README polish + verification

- [ ] Confirm worker README lists deploy + seed invite + claim owner key `owner:<modId>`.
- [ ] Confirm manager default base URL documented as must match deploy.
- [ ] Run full `dotnet test` (or at least DirectUpload + mail submit regression `MailComposeFlowTests`).
- [ ] Mark plan checkboxes done in this file as work completes.

---

## Spec coverage checklist

| Spec area | Task |
|-----------|------|
| Dual channel email unchanged | Task 4 (do not alter SubmitMod body) |
| Invite + KV ownership | Task 1–2 |
| Full publish transaction sha256/size | Task 2 |
| Catalog concurrency retry | Task 2 |
| Unowned reject + claim via KV | Task 2 README |
| Mirror lag copy | Task 2 response + Task 4 strings |
| 95 MiB cap MVP | Task 2 |
| Manager UI | Task 4 |
| M3 Release / M4 COS | Out of this plan |
