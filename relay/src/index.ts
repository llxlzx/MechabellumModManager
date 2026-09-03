/**
 * Minimal Cloudflare Worker relay:
 *   POST /v1/submissions  — multipart (name, author, version, summary, sha256, file) → R2 + GitHub Issue
 *   POST /v1/reports      — JSON { modId, category, notes, ... } → GitHub Issue
 */

export interface Env {
  UPLOADS: R2Bucket;
  GITHUB_TOKEN: string;
  GITHUB_OWNER: string;
  GITHUB_REPO: string;
  MAX_UPLOAD_BYTES: string;
}

const RATE = new Map<string, { count: number; reset: number }>();
const RATE_LIMIT = 20;
const RATE_WINDOW_MS = 60_000;

function clientIp(req: Request): string {
  return req.headers.get("cf-connecting-ip") || req.headers.get("x-forwarded-for") || "unknown";
}

function rateLimit(ip: string): boolean {
  const now = Date.now();
  const cur = RATE.get(ip);
  if (!cur || cur.reset < now) {
    RATE.set(ip, { count: 1, reset: now + RATE_WINDOW_MS });
    return true;
  }
  if (cur.count >= RATE_LIMIT) return false;
  cur.count += 1;
  return true;
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "access-control-allow-origin": "*",
    },
  });
}

async function createIssue(
  env: Env,
  title: string,
  body: string,
  labels: string[],
): Promise<Response> {
  if (!env.GITHUB_TOKEN) {
    return json(500, { error: "GITHUB_TOKEN not configured" });
  }
  const url = `https://api.github.com/repos/${env.GITHUB_OWNER}/${env.GITHUB_REPO}/issues`;
  const res = await fetch(url, {
    method: "POST",
    headers: {
      authorization: `Bearer ${env.GITHUB_TOKEN}`,
      accept: "application/vnd.github+json",
      "user-agent": "mechabellum-mod-relay",
      "content-type": "application/json",
    },
    body: JSON.stringify({ title, body, labels }),
  });
  const text = await res.text();
  if (!res.ok) {
    return json(502, { error: "GitHub issue create failed", detail: text.slice(0, 400) });
  }
  return json(200, { ok: true, github: JSON.parse(text) });
}

async function handleReport(req: Request, env: Env): Promise<Response> {
  let data: Record<string, unknown>;
  try {
    data = (await req.json()) as Record<string, unknown>;
  } catch {
    return json(400, { error: "invalid JSON" });
  }

  const modId = String(data.modId ?? "").trim();
  const category = String(data.category ?? "").trim().toLowerCase();
  const notes = String(data.notes ?? "").trim();
  const allowed = new Set(["cheat", "virus", "unrelated", "other"]);
  if (!modId) return json(400, { error: "modId required" });
  if (!allowed.has(category)) return json(400, { error: "invalid category" });
  if (category === "other" && !notes) return json(400, { error: "notes required for other" });

  const title = `[report] ${category} — ${modId}`;
  const body = [
    `**modId:** ${modId}`,
    `**modName:** ${data.modName ?? ""}`,
    `**source:** ${data.source ?? ""}`,
    `**category:** ${category}`,
    `**notes:** ${notes || "(none)"}`,
    `**appVersion:** ${data.appVersion ?? ""}`,
    `**receivedAt:** ${new Date().toISOString()}`,
  ].join("\n");

  return createIssue(env, title, body, ["report", category]);
}

async function handleSubmission(req: Request, env: Env): Promise<Response> {
  const maxBytes = Number(env.MAX_UPLOAD_BYTES || "20971520");
  const form = await req.formData();
  const name = String(form.get("name") ?? "").trim();
  const author = String(form.get("author") ?? "").trim();
  const version = String(form.get("version") ?? "").trim();
  const summary = String(form.get("summary") ?? "").trim();
  const sha256 = String(form.get("sha256") ?? "").trim().toLowerCase();
  const appVersion = String(form.get("appVersion") ?? "").trim();
  const file = form.get("file");

  if (!name) return json(400, { error: "name required" });
  if (!(file instanceof File)) return json(400, { error: "file required" });
  if (!file.name.toLowerCase().endsWith(".dll")) return json(400, { error: "only .dll allowed" });
  if (file.size <= 0 || file.size > maxBytes) return json(400, { error: "file size invalid or too large" });

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  const safeName = file.name.replace(/[^\w.\-]+/g, "_");
  const key = `pending/${stamp}_${safeName}`;
  const bytes = await file.arrayBuffer();
  await env.UPLOADS.put(key, bytes, {
    httpMetadata: { contentType: "application/octet-stream" },
    customMetadata: { name, author, version, sha256, appVersion },
  });

  const title = `[submission] ${name}${version ? " " + version : ""}`;
  const body = [
    `**name:** ${name}`,
    `**author:** ${author || "(unknown)"}`,
    `**version:** ${version || "(unknown)"}`,
    `**summary:** ${summary || "(none)"}`,
    `**sha256:** ${sha256 || "(none)"}`,
    `**file:** ${file.name} (${file.size} bytes)`,
    `**r2Key:** ${key}`,
    `**appVersion:** ${appVersion || "(unknown)"}`,
    `**receivedAt:** ${new Date().toISOString()}`,
    "",
    "Maintainer: download from R2 `pending/…`, review, then merge into catalog.",
  ].join("\n");

  return createIssue(env, title, body, ["submission"]);
}

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    if (req.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "access-control-allow-origin": "*",
          "access-control-allow-methods": "POST, OPTIONS",
          "access-control-allow-headers": "content-type",
        },
      });
    }

    const url = new URL(req.url);
    if (req.method === "GET" && url.pathname === "/health") {
      return json(200, { ok: true });
    }

    if (req.method !== "POST") {
      return json(405, { error: "method not allowed" });
    }

    if (!rateLimit(clientIp(req))) {
      return json(429, { error: "rate limited" });
    }

    if (url.pathname === "/v1/reports") return handleReport(req, env);
    if (url.pathname === "/v1/submissions") return handleSubmission(req, env);
    return json(404, { error: "not found" });
  },
};
