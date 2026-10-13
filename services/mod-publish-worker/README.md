# Mod publish Worker (whitelist inbox grant)

Cloudflare Worker that checks an invite code, then issues a one-hour Tencent Cloud STS credential for the private inbox bucket. DLL bytes do not pass through the Worker. Players still see mods only after a maintainer merges them into the GitHub catalog and syncs the mirror.

## Setup

```bash
cd services/mod-publish-worker
npm install
npx wrangler kv namespace create PUBLISH_KV
# put the id into wrangler.toml [[kv_namespaces]] id =
npx wrangler secret put COS_SECRET_ID    # sub-account secret id
npx wrangler secret put COS_SECRET_KEY   # sub-account secret key
npx wrangler secret put ADMIN_TOKEN      # bearer token for GET /v1/admin/grants
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

## Limits

- `/v1/mods/publish` is closed. It returns **410** `{"error":"publish_disabled"}`. DLL bytes do not pass through the Worker.
- A grant is refused when the public catalog already contains the id and this invite does not own it. Catalog presence is an anonymous GET of `https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json`. A failed fetch returns **502** `upstream_github` and is not treated as a new id.
- Ownership for an existing id is `owner:<modId>` = `inviteId`. The grant record itself does not store the invite code or the temporary secret.
- Temporary credentials last **3600** seconds and only cover `inbox/<inviteId>/<modId>/*` in bucket `mmm-inbox-1312774738` (`ap-shanghai`).

## API

| Method | Path | Auth |
|--------|------|------|
| POST | `/v1/auth/check` | JSON `{ "inviteCode" }` |
| GET | `/v1/mods/owned` | Header `X-Invite-Code` |
| POST | `/v1/inbox/grant` | JSON invite code plus mod id, name, summary, version, kind, and files. Returns bucket, region, `durationSeconds: 3600`, credentials, and object keys. |
| POST | `/v1/inbox/complete` | JSON `{ "inviteCode", "id" }`. Marks an existing `grant:<inviteId>:<modId>` record `complete-claimed`. Ignores any new hashes in the body. |
| GET | `/v1/admin/grants` | `Authorization: Bearer <ADMIN_TOKEN>`. Lists stored grant records only. |
| POST | `/v1/mods/publish` | Closed. **410** `{"error":"publish_disabled"}`. |
