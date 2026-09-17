# Mod publish Worker (whitelist direct upload)

Cloudflare Worker that accepts invite-code authenticated multipart uploads and writes into `llxlzx/MechabellumMods` (git paths + `catalog.json`). See `docs/dev/superpowers/specs/2026-09-17-whitelist-direct-upload-design.md`.

## Setup

```bash
cd services/mod-publish-worker
npm install
npx wrangler kv namespace create PUBLISH_KV
# put the id into wrangler.toml [[kv_namespaces]] id =
npx wrangler secret put GITHUB_TOKEN   # fine-grained: Contents R/W on MechabellumMods
npm test
npx wrangler deploy
```

Default public URL shape: `https://mod-publish.<account>.workers.dev`  
Point the manager at it via `DirectUploadDefaults.ApiBaseUrl` or `AppConfig.DirectUploadApiBaseUrl`.

## Seed an invite

Hash the invite code (SHA-256 hex of trimmed UTF-8), then:

```bash
# PowerShell example
$code = "your-invite-code"
$sha = [System.BitConverter]::ToString(
  [System.Security.Cryptography.SHA256]::Create().ComputeHash(
    [Text.Encoding]::UTF8.GetBytes($code.Trim())
  )
).Replace("-","").ToLowerInvariant()
npx wrangler kv key put --binding=PUBLISH_KV "invite:$sha" "{\"inviteId\":\"author1\",\"enabled\":true,\"label\":\"Alice\"}"
```

## Claim an existing catalog mod for an invite

```bash
npx wrangler kv key put --binding=PUBLISH_KV "owner:SomeModId" "author1"
```

(`author1` must match the `inviteId` in the invite record.)

## Revoke

Overwrite invite JSON with `"enabled":false`, or delete the `invite:<hash>` key.

## Limits (MVP)

- Single DLL/preview ≤ **95 MiB** (no GitHub Release path yet).
- Ownership: first successful create binds `owner:<modId>`; updates require same `inviteId`.

## API

| Method | Path | Auth |
|--------|------|------|
| POST | `/v1/auth/check` | JSON `{ "inviteCode" }` |
| GET | `/v1/mods/owned` | Header `X-Invite-Code` |
| POST | `/v1/mods/publish` | multipart fields: `inviteCode`, `id`, `name`, `summary?`, `version?`, `category?`, `type?`, `dll`, `preview?` |
