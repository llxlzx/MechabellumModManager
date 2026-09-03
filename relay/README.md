# Mechabellum Mod Relay (Cloudflare Worker)

Receives **mod submissions** and **categorized reports** from the desktop manager, stores DLLs in **R2**, and opens **GitHub Issues** on the catalog repo for maintainer review.

## Endpoints

| Method | Path | Body |
|--------|------|------|
| `POST` | `/v1/submissions` | `multipart/form-data`: `name`, `author`, `version`, `summary`, `sha256`, `file` (.dll ≤ 20MB) |
| `POST` | `/v1/reports` | JSON: `modId`, `category` (`cheat`\|`virus`\|`unrelated`\|`other`), `notes` (required when `other`) |
| `GET` | `/health` | liveness |

## Deploy

1. Create an R2 bucket named `mechabellum-mod-pending` (or change `wrangler.toml`).
2. Create a GitHub fine-grained or classic token with **`issues:write`** on [`llxlzx/MechabellumMods`](https://github.com/llxlzx/MechabellumMods) (or your fork).
3. From this folder:

```bash
npm install
npx wrangler login
npx wrangler secret put GITHUB_TOKEN
npx wrangler deploy
```

4. Copy the Worker URL (e.g. `https://mechabellum-mod-relay.<subdomain>.workers.dev`).

## Configure the manager

In the manager config JSON (`config.json` under the data root), set:

```json
"relayBaseUrl": "https://YOUR_DEPLOYED_WORKER.workers.dev"
```

Or leave the placeholder / empty to disable submit & report network calls (UI shows a “not configured” message).

## Notes

- In-memory rate limit (~20 req/min/IP) resets when the isolate recycles; tighten with Cloudflare Rate Limiting if needed.
- Submissions are **never** auto-published to the catalog; maintainers merge after review.
- Do not commit `GITHUB_TOKEN`.
