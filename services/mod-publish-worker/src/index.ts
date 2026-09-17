import { hashInviteCode, sha256Hex } from "./crypto";
import { mergeCatalogMod, type CatalogRoot, type CatalogMod } from "./catalog";
import { authorizePublish } from "./ownership";
import { getRepoFile, putRepoBinary, putRepoFileWithRetry, type GitHubEnv } from "./github";

export interface Env {
  PUBLISH_KV: KVNamespace;
  GITHUB_TOKEN: string;
  GITHUB_OWNER: string;
  GITHUB_REPO: string;
  GITHUB_BRANCH: string;
}

const MAX_DLL_BYTES = 95 * 1024 * 1024;
const MIRROR_NOTE =
  "Published to GitHub MechabellumMods. Domestic COS mirror may lag until synced. / 已发布到 GitHub；国内镜像可能延迟。";

type InviteRecord = { inviteId: string; label?: string; enabled?: boolean };

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "access-control-allow-origin": "*",
      "access-control-allow-headers": "content-type, x-invite-code",
      "access-control-allow-methods": "GET, POST, OPTIONS",
    },
  });
}

function err(code: string, status: number): Response {
  return json({ error: code }, status);
}

function ghEnv(env: Env): GitHubEnv {
  return {
    token: env.GITHUB_TOKEN,
    owner: env.GITHUB_OWNER || "llxlzx",
    repo: env.GITHUB_REPO || "MechabellumMods",
    branch: env.GITHUB_BRANCH || "master",
  };
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

function safeFileName(name: string): string {
  const base = name.replace(/^.*[\\/]/, "").replace(/[^\w.\-]+/g, "_");
  return base || "mod.dll";
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

async function handlePublish(request: Request, env: Env): Promise<Response> {
  const form = await request.formData();
  const inviteCode = String(form.get("inviteCode") ?? "");
  const id = String(form.get("id") ?? "").trim();
  const name = String(form.get("name") ?? "").trim();
  const summary = String(form.get("summary") ?? "").trim() || null;
  const version = String(form.get("version") ?? "").trim() || null;
  const category = String(form.get("category") ?? "").trim() || null;
  const type = String(form.get("type") ?? "").trim() || null;
  const dll = form.get("dll");

  const resolved = await resolveInvite(env, inviteCode);
  if (resolved instanceof Response) return resolved;
  const { inviteId } = resolved;

  if (!id || !name || !(dll instanceof File)) return err("validation_failed", 400);
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/.test(id)) return err("validation_failed", 400);

  const dllBytes = new Uint8Array(await dll.arrayBuffer());
  if (dllBytes.byteLength > MAX_DLL_BYTES) return err("payload_too_large", 413);
  if (dllBytes.byteLength === 0) return err("validation_failed", 400);

  const sha256 = await sha256Hex(dllBytes);
  const dllName = safeFileName(dll.name || `${id}.dll`);
  const filePath = `mods/${id}/${dllName}`;

  let previewPath: string | null = null;
  let previewBytes: Uint8Array | null = null;
  const preview = form.get("preview");
  if (preview instanceof File && preview.size > 0) {
    if (preview.size > MAX_DLL_BYTES) return err("payload_too_large", 413);
    previewBytes = new Uint8Array(await preview.arrayBuffer());
    previewPath = `mods/${id}/preview.png`;
  }

  const g = ghEnv(env);
  const catalogFile = await getRepoFile(g, "catalog.json");
  let root: CatalogRoot = { mods: [] };
  if (catalogFile) {
    try {
      root = JSON.parse(catalogFile.content) as CatalogRoot;
      if (!Array.isArray(root.mods)) root.mods = [];
    } catch {
      return err("upstream_github", 502);
    }
  }

  const catalogHasMod = root.mods.some((m) => m.id === id);
  const ownerInviteId = await env.PUBLISH_KV.get(`owner:${id}`);
  const decision = authorizePublish({
    modId: id,
    inviteId,
    catalogHasMod,
    ownerInviteId,
  });
  if (decision === "forbidden_not_owner") return err("forbidden_not_owner", 403);

  const existingDll = await getRepoFile(g, filePath);
  await putRepoBinary(
    g,
    filePath,
    `publish: ${id} dll`,
    dllBytes,
    existingDll?.sha,
  );

  if (previewPath && previewBytes) {
    const existingPreview = await getRepoFile(g, previewPath);
    await putRepoBinary(
      g,
      previewPath,
      `publish: ${id} preview`,
      previewBytes,
      existingPreview?.sha,
    );
  }

  const entry: CatalogMod = {
    id,
    name,
    summary,
    version,
    category,
    type,
    file: filePath,
    sha256,
    size: dllBytes.byteLength,
    preview: previewPath,
    updatedAt: new Date().toISOString(),
  };

  const put = await putRepoFileWithRetry(
    g,
    "catalog.json",
    `publish: ${id} catalog`,
    (latest) => {
      let base: CatalogRoot = { mods: [] };
      if (latest) {
        try {
          base = JSON.parse(latest) as CatalogRoot;
          if (!Array.isArray(base.mods)) base.mods = [];
        } catch {
          base = { mods: [] };
        }
      }
      // Re-check ownership against latest catalog presence
      const has = base.mods.some((m) => m.id === id);
      const d = authorizePublish({
        modId: id,
        inviteId,
        catalogHasMod: has,
        ownerInviteId,
      });
      if (d === "forbidden_not_owner") throw new Error("forbidden_not_owner");
      const merged = mergeCatalogMod(base, entry);
      return `${JSON.stringify(merged, null, 2)}\n`;
    },
  );

  if (decision === "allow_create" || !ownerInviteId) {
    await env.PUBLISH_KV.put(`owner:${id}`, inviteId);
  }

  return json({
    modId: id,
    sha256,
    size: dllBytes.byteLength,
    commitSha: put.commitSha,
    mirrorNote: MIRROR_NOTE,
  });
}
