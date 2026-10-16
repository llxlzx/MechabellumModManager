import { hashInviteCode } from "./crypto";
import { assignKeys, inspectGrant, type GrantRequest } from "./inboxGrant";
import { buildInboxPolicy } from "./inboxPolicy";
import { authorizeInbox } from "./ownership";
import { createDefaultIssuer, type CredentialIssuer } from "./sts";

export interface Env {
  PUBLISH_KV: KVNamespace;
  GITHUB_TOKEN: string;
  GITHUB_OWNER: string;
  GITHUB_REPO: string;
  GITHUB_BRANCH: string;
  ADMIN_TOKEN?: string;
  COS_SECRET_ID?: string;
  COS_SECRET_KEY?: string;
  credentialIssuer?: CredentialIssuer;
  loadCatalog?: () => Promise<string>;
}

const BUCKET = "mmm-inbox-1312774738";
const REGION = "ap-shanghai";
const DURATION_SECONDS = 3600;
const CATALOG_URL = "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json";

type InviteRecord = { inviteId: string; label?: string; enabled?: boolean };

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "access-control-allow-origin": "*",
      "access-control-allow-headers": "content-type, x-invite-code, authorization",
      "access-control-allow-methods": "GET, POST, OPTIONS",
    },
  });
}

function err(code: string, status: number): Response {
  return json({ error: code }, status);
}

async function resolveInvite(
  env: Env,
  code: string,
): Promise<{ inviteId: string } | Response> {
  if (!code?.trim()) return err("invalid_invite", 401);
  const hash = await hashInviteCode(code);
  const raw = await env.PUBLISH_KV.get(`invite:${hash}`);
  if (!raw) return err("invalid_invite", 401);
  let rec: InviteRecord;
  try {
    rec = JSON.parse(raw) as InviteRecord;
  } catch {
    return err("invalid_invite", 401);
  }
  if (rec.enabled === false || !rec.inviteId) return err("invalid_invite", 401);
  return { inviteId: rec.inviteId };
}

async function listOwned(env: Env, inviteId: string): Promise<string[]> {
  const listed = await env.PUBLISH_KV.list({ prefix: "owner:" });
  const ids: string[] = [];
  for (const k of listed.keys) {
    const v = await env.PUBLISH_KV.get(k.name);
    if (v === inviteId) ids.push(k.name.slice("owner:".length));
  }
  return ids.sort();
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method === "OPTIONS") {
      return json({ ok: true });
    }

    const url = new URL(request.url);
    try {
      if (url.pathname === "/v1/auth/check" && request.method === "POST") {
        const body = (await request.json()) as { inviteCode?: string };
        const resolved = await resolveInvite(env, body.inviteCode ?? "");
        if (resolved instanceof Response) return resolved;
        return json({ ok: true, inviteId: resolved.inviteId });
      }

      if (url.pathname === "/v1/mods/owned" && request.method === "GET") {
        const code = request.headers.get("X-Invite-Code") ?? "";
        const resolved = await resolveInvite(env, code);
        if (resolved instanceof Response) return resolved;
        const modIds = await listOwned(env, resolved.inviteId);
        return json({ modIds });
      }

      if (url.pathname === "/v1/inbox/grant" && request.method === "POST") {
        return await handleGrant(request, env);
      }

      if (url.pathname === "/v1/inbox/complete" && request.method === "POST") {
        return await handleComplete(request, env);
      }

      if (url.pathname === "/v1/admin/grants" && request.method === "GET") {
        return await handleAdminGrants(request, env);
      }

      if (url.pathname === "/v1/mods/publish" && request.method === "POST") {
        return await handlePublish(request, env);
      }

      return err("not_found", 404);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      if (msg.startsWith("validation_failed")) return err("validation_failed", 400);
      if (msg.startsWith("catalog_conflict")) return err("catalog_conflict", 409);
      if (msg.startsWith("upstream_github")) return err("upstream_github", 502);
      if (msg.startsWith("payload_too_large")) return err("payload_too_large", 413);
      if (msg.startsWith("forbidden_not_owner")) return err("forbidden_not_owner", 403);
      return err("internal", 500);
    }
  },
};

async function handlePublish(_request: Request, _env: Env): Promise<Response> {
  return err("publish_disabled", 410);
}

async function handleGrant(request: Request, env: Env): Promise<Response> {
  const body = await readJson(request);
  if (body === undefined) return err("validation_failed", 400);

  const resolved = await resolveInvite(env, readInviteCode(body, request));
  if (resolved instanceof Response) return resolved;

  const inspected = inspectGrant(body);
  if (!inspected.ok) {
    return err(inspected.error, inspected.error === "payload_too_large" ? 413 : 400);
  }
  const grant = inspected.value;

  let catalogHasMod: boolean;
  try {
    const raw = await (env.loadCatalog ?? defaultLoadCatalog)();
    catalogHasMod = catalogIncludes(raw, grant.id);
  } catch {
    return err("upstream_github", 502);
  }

  const ownerInviteId = await env.PUBLISH_KV.get(`owner:${grant.id}`);
  if (authorizeInbox({
    catalogHasMod,
    ownerInviteId,
    inviteId: resolved.inviteId,
  }) === "forbidden_not_owner") {
    return err("forbidden_not_owner", 403);
  }

  let encoded: string;
  try {
    encoded = buildInboxPolicy(resolved.inviteId, grant.id).encoded;
  } catch {
    return err("validation_failed", 400);
  }

  const objects = issuedObjects(resolved.inviteId, grant);
  const record = {
    inviteId: resolved.inviteId,
    id: grant.id,
    kind: grant.kind,
    name: grant.name,
    summary: grant.summary,
    version: grant.version,
    feedback: grant.feedback,
    status: "issued",
    objects,
  };
  await env.PUBLISH_KV.put(`grant:${resolved.inviteId}:${grant.id}`, JSON.stringify(record));

  const credentials = await issuerFor(env)(encoded);
  return json({
    ok: true,
    inviteId: resolved.inviteId,
    bucket: BUCKET,
    region: REGION,
    durationSeconds: DURATION_SECONDS,
    credentials,
    objects,
  });
}

async function handleComplete(request: Request, env: Env): Promise<Response> {
  const body = await readJson(request);
  if (body === undefined) return err("validation_failed", 400);

  const resolved = await resolveInvite(env, readInviteCode(body, request));
  if (resolved instanceof Response) return resolved;

  const id = isRecord(body) && typeof body.id === "string" ? body.id : "";
  if (!id) return err("validation_failed", 400);

  const key = `grant:${resolved.inviteId}:${id}`;
  const raw = await env.PUBLISH_KV.get(key);
  if (!raw) return err("not_found", 404);

  let stored: Record<string, unknown>;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!isRecord(parsed)) return err("internal", 500);
    stored = parsed;
  } catch {
    return err("internal", 500);
  }
  stored.status = "complete-claimed";
  await env.PUBLISH_KV.put(key, JSON.stringify(stored));
  return json({ ok: true, status: "complete-claimed" });
}

async function handleAdminGrants(request: Request, env: Env): Promise<Response> {
  const token = env.ADMIN_TOKEN ?? "";
  const header = request.headers.get("Authorization") ?? "";
  if (!token || header !== `Bearer ${token}`) return err("unauthorized", 401);

  const grants: unknown[] = [];
  let cursor: string | undefined;
  for (;;) {
    const listed = await env.PUBLISH_KV.list({ prefix: "grant:", cursor });
    for (const key of listed.keys) {
      const raw = await env.PUBLISH_KV.get(key.name);
      if (!raw) continue;
      try {
        grants.push(JSON.parse(raw));
      } catch {
        continue;
      }
    }
    if (listed.list_complete || !listed.cursor) break;
    cursor = listed.cursor;
  }
  return json({ grants });
}

function issuerFor(env: Env): CredentialIssuer {
  if (env.credentialIssuer) return env.credentialIssuer;
  if (!env.COS_SECRET_ID || !env.COS_SECRET_KEY) {
    return async () => {
      throw new Error("sts_failed");
    };
  }
  return createDefaultIssuer({
    secretId: env.COS_SECRET_ID,
    secretKey: env.COS_SECRET_KEY,
  });
}

async function defaultLoadCatalog(): Promise<string> {
  let response: Response;
  try {
    response = await fetch(CATALOG_URL, { method: "GET" });
  } catch {
    throw new Error("upstream_github");
  }
  if (!response.ok) throw new Error("upstream_github");
  return response.text();
}

function catalogIncludes(raw: string, id: string): boolean {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error("upstream_github");
  }
  if (!isRecord(parsed) || !Array.isArray(parsed.mods)) throw new Error("upstream_github");
  return parsed.mods.some((mod) => isRecord(mod) && mod.id === id);
}

function issuedObjects(inviteId: string, grant: GrantRequest) {
  return assignKeys(inviteId, grant).map((item) => {
    const name = item.key.slice(item.key.lastIndexOf("/") + 1);
    const file = grant.files.find((candidate) => candidate.role === item.role && candidate.name === name);
    return {
      key: item.key,
      role: item.role,
      name,
      size: item.size,
      sha256: item.sha256,
      main: file?.main === true,
    };
  });
}

async function readJson(request: Request): Promise<unknown> {
  try {
    return await request.json();
  } catch {
    return undefined;
  }
}

function readInviteCode(body: unknown, request: Request): string {
  if (isRecord(body) && typeof body.inviteCode === "string" && body.inviteCode.trim()) {
    return body.inviteCode;
  }
  return request.headers.get("X-Invite-Code") ?? "";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
