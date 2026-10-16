import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { hashInviteCode } from "./crypto";
import { buildInboxPolicy } from "./inboxPolicy";
import worker from "./index";
import type { Env } from "./index";

const CATALOG_URL =
  "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json";
const INVITE_CODE = "invite-plaintext-9f3a";
const INVITE_ID = "author1";
const MOD_ID = "quick-mark";
const SHA_DLL = "a".repeat(64);
const SHA_EXTRA = "b".repeat(64);
const ADMIN_TOKEN = "admin-token";

const FAKE_CREDENTIALS = {
  tmpSecretId: "tmp-id",
  tmpSecretKey: "tmp-secret",
  sessionToken: "tmp-token",
};

function memoryKv(): KVNamespace {
  const store = new Map<string, string>();
  return {
    async get(key: string) {
      return store.has(key) ? store.get(key)! : null;
    },
    async put(key: string, value: string) {
      store.set(key, value);
    },
    async delete(key: string) {
      store.delete(key);
    },
    async list(options?: { prefix?: string }) {
      const prefix = options?.prefix ?? "";
      const keys = [...store.keys()]
        .filter((name) => name.startsWith(prefix))
        .sort()
        .map((name) => ({ name }));
      return { keys, list_complete: true, cacheStatus: null };
    },
  } as unknown as KVNamespace;
}

function singleGrantBody() {
  return {
    inviteCode: INVITE_CODE,
    id: MOD_ID,
    name: "Quick Mark",
    summary: "marks things",
    version: "1.2.3",
    kind: "single",
    feedback: "author@example.com",
    files: [{ role: "dll", name: "QuickMark.dll", size: 10, sha256: SHA_DLL }],
  };
}

function bundleGrantBody() {
  return {
    ...singleGrantBody(),
    kind: "bundle",
    files: [
      { role: "bundle", name: "QuickMark.dll", size: 10, sha256: SHA_DLL, main: true },
      { role: "bundle", name: "Extra.dll", size: 8, sha256: SHA_EXTRA, main: false },
    ],
  };
}

function post(path: string, body: unknown): Request {
  return new Request(`https://worker.test${path}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
}

async function seedInvite(kv: KVNamespace) {
  const hash = await hashInviteCode(INVITE_CODE);
  await kv.put(
    `invite:${hash}`,
    JSON.stringify({ inviteId: INVITE_ID, enabled: true, label: "Alice" }),
  );
}

function testEnv(kv: KVNamespace, overrides: Partial<Env> = {}): Env {
  return {
    PUBLISH_KV: kv,
    GITHUB_TOKEN: "",
    GITHUB_OWNER: "llxlzx",
    GITHUB_REPO: "MechabellumMods",
    GITHUB_BRANCH: "master",
    ADMIN_TOKEN,
    COS_SECRET_ID: "sid",
    COS_SECRET_KEY: "skey",
    loadCatalog: async () => JSON.stringify({ mods: [] }),
    credentialIssuer: async () => FAKE_CREDENTIALS,
    ...overrides,
  };
}

describe("inbox http", () => {
  beforeEach(() => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("network disabled"));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("rejects an existing catalog id that has no owner", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    let issued = 0;
    const env = testEnv(kv, {
      loadCatalog: async () => JSON.stringify({ mods: [{ id: MOD_ID }] }),
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(403);
    expect(await response.json()).toEqual({ error: "forbidden_not_owner" });
    expect(issued).toBe(0);
    expect((await kv.list({ prefix: "grant:" })).keys).toEqual([]);
  });

  it("issues a one-hour credential for a new id and keeps secrets out of KV", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    const seen: string[] = [];
    let kvAtIssue: string | null = null;
    const env = testEnv(kv, {
      credentialIssuer: async (encodedPolicy: string) => {
        seen.push(encodedPolicy);
        kvAtIssue = await kv.get(`grant:${INVITE_ID}:${MOD_ID}`);
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(200);
    const body = await response.json() as {
      ok: boolean;
      inviteId: string;
      bucket: string;
      region: string;
      durationSeconds: number;
      credentials: { tmpSecretId: string; tmpSecretKey: string; sessionToken: string };
      objects: { key: string; role: string; size: number; sha256: string; main: boolean }[];
    };

    expect(body.ok).toBe(true);
    expect(body.inviteId).toBe(INVITE_ID);
    expect(body.bucket).toBe("mmm-inbox-1312774738");
    expect(body.region).toBe("ap-shanghai");
    expect(body.durationSeconds).toBe(3600);
    expect(body.credentials).toEqual(FAKE_CREDENTIALS);
    expect(body.objects).toEqual([
      {
        key: "inbox/author1/quick-mark/QuickMark.dll",
        role: "dll",
        name: "QuickMark.dll",
        size: 10,
        sha256: SHA_DLL,
        main: false,
      },
    ]);
    expect(body.objects.every((item) => item.key.startsWith(`inbox/${INVITE_ID}/${MOD_ID}/`))).toBe(true);

    expect(seen).toEqual([buildInboxPolicy(INVITE_ID, MOD_ID).encoded]);
    expect(seen[0].startsWith("%7B")).toBe(true);
    expect(seen[0].includes("%257B")).toBe(false);

    const raw = await kv.get(`grant:${INVITE_ID}:${MOD_ID}`);
    expect(raw).toBeTruthy();
    expect(kvAtIssue).toBe(raw);
    expect(raw).not.toContain("tmp-secret");
    expect(raw).not.toContain(INVITE_CODE);
    const stored = JSON.parse(raw!) as {
      kind: string;
      name: string;
      summary: string;
      version: string;
      feedback: string;
      objects: { sha256: string; size: number; main: boolean }[];
    };
    expect(stored.kind).toBe("single");
    expect(stored.name).toBe("Quick Mark");
    expect(stored.summary).toBe("marks things");
    expect(stored.feedback).toBe("author@example.com");
    expect(stored.version).toBe("1.2.3");
    expect(stored.objects[0]).toMatchObject({ sha256: SHA_DLL, size: 10, main: false });
    expect(raw).not.toContain("inviteCode");
    expect(await kv.get(`owner:${MOD_ID}`)).toBeNull();
  });

  it("copies main from the matching request file", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    const env = testEnv(kv);
    const response = await worker.fetch(post("/v1/inbox/grant", bundleGrantBody()), env);
    expect(response.status).toBe(200);
    const body = await response.json() as {
      objects: { key: string; role: string; name: string; main: boolean }[];
    };
    expect(body.objects).toEqual([
      {
        key: "inbox/author1/quick-mark/payload/QuickMark.dll",
        role: "bundle",
        name: "QuickMark.dll",
        size: 10,
        sha256: SHA_DLL,
        main: true,
      },
      {
        key: "inbox/author1/quick-mark/payload/Extra.dll",
        role: "bundle",
        name: "Extra.dll",
        size: 8,
        sha256: SHA_EXTRA,
        main: false,
      },
    ]);
    const stored = JSON.parse((await kv.get(`grant:${INVITE_ID}:${MOD_ID}`))!);
    expect(stored.objects.map((item: { main: boolean }) => item.main)).toEqual([true, false]);
  });

  it("returns 502 when the catalog cannot be loaded and does not treat that as a new id", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    let issued = 0;
    const env = testEnv(kv, {
      loadCatalog: async () => {
        throw new Error("upstream_github");
      },
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({ error: "upstream_github" });
    expect(issued).toBe(0);
    expect((await kv.list({ prefix: "grant:" })).keys).toEqual([]);
  });

  it("returns 502 when the catalog payload is not a catalog", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    let issued = 0;
    const env = testEnv(kv, {
      loadCatalog: async () => "not-json",
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({ error: "upstream_github" });
    expect(issued).toBe(0);
  });

  it("loads the public catalog anonymously when no loader hook is set", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      expect(String(input)).toBe(CATALOG_URL);
      expect(init?.method ?? "GET").toBe("GET");
      const headers = new Headers(init?.headers);
      expect(headers.get("authorization")).toBeNull();
      return new Response(JSON.stringify({ mods: [{ id: MOD_ID }] }), { status: 200 });
    });
    let issued = 0;
    const env = testEnv(kv, {
      loadCatalog: undefined,
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(403);
    expect(issued).toBe(0);
  });

  it("returns 502 from the default catalog loader when GitHub is unreachable", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("down", { status: 500 }));
    let issued = 0;
    const env = testEnv(kv, {
      loadCatalog: undefined,
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({ error: "upstream_github" });
    expect(issued).toBe(0);
    const fetchMock = vi.mocked(globalThis.fetch);
    expect(fetchMock).toHaveBeenCalled();
    expect(String(fetchMock.mock.calls[0][0])).toBe(CATALOG_URL);
  });

  it("disables direct publish and does not call the issuer", async () => {
    const kv = memoryKv();
    let issued = 0;
    const env = testEnv(kv, {
      credentialIssuer: async () => {
        issued += 1;
        return FAKE_CREDENTIALS;
      },
    });

    const response = await worker.fetch(post("/v1/mods/publish", { inviteCode: INVITE_CODE }), env);
    expect(response.status).toBe(410);
    expect(await response.json()).toEqual({ error: "publish_disabled" });
    expect(issued).toBe(0);
  });

  it("rejects admin grant listing without a matching bearer token", async () => {
    const kv = memoryKv();
    const env = testEnv(kv);
    const missing = await worker.fetch(new Request("https://worker.test/v1/admin/grants"), env);
    expect(missing.status).toBe(401);

    const wrong = await worker.fetch(new Request("https://worker.test/v1/admin/grants", {
      headers: { Authorization: "Bearer wrong-token" },
    }), env);
    expect(wrong.status).toBe(401);

    const unset = testEnv(kv, { ADMIN_TOKEN: undefined });
    const open = await worker.fetch(new Request("https://worker.test/v1/admin/grants", {
      headers: { Authorization: "Bearer " },
    }), unset);
    expect(open.status).toBe(401);
  });

  it("lists only stored grant records for a matching admin token", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    const env = testEnv(kv);
    expect((await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env)).status).toBe(200);

    const response = await worker.fetch(new Request("https://worker.test/v1/admin/grants", {
      headers: { Authorization: `Bearer ${ADMIN_TOKEN}` },
    }), env);
    expect(response.status).toBe(200);
    const body = await response.json() as { grants: { id: string; objects: { sha256: string }[] }[] };
    expect(body.grants).toHaveLength(1);
    expect(body.grants[0].id).toBe(MOD_ID);
    expect(body.grants[0].objects[0].sha256).toBe(SHA_DLL);
    const text = JSON.stringify(body);
    expect(text).not.toContain("tmp-secret");
    expect(text).not.toContain(INVITE_CODE);
  });

  it("marks an existing grant complete-claimed without accepting new hashes", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    const env = testEnv(kv);
    expect((await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env)).status).toBe(200);

    const response = await worker.fetch(post("/v1/inbox/complete", {
      inviteCode: INVITE_CODE,
      id: MOD_ID,
      sha256: "c".repeat(64),
      objects: [{ key: "inbox/author1/quick-mark/QuickMark.dll", sha256: "d".repeat(64), size: 99 }],
    }), env);
    expect(response.status).toBe(200);
    const stored = JSON.parse((await kv.get(`grant:${INVITE_ID}:${MOD_ID}`))!) as {
      status: string;
      objects: { sha256: string; size: number }[];
    };
    expect(stored.status).toBe("complete-claimed");
    expect(stored.objects).toEqual([
      expect.objectContaining({ sha256: SHA_DLL, size: 10, main: false }),
    ]);
    expect(JSON.stringify(stored)).not.toContain("c".repeat(64));
    expect(JSON.stringify(stored)).not.toContain(INVITE_CODE);
  });

  it("does not log or echo a temporary secret", async () => {
    const kv = memoryKv();
    await seedInvite(kv);
    const log = vi.spyOn(console, "log").mockImplementation(() => {});
    const info = vi.spyOn(console, "info").mockImplementation(() => {});
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    const env = testEnv(kv, {
      credentialIssuer: async () => {
        throw new Error(`issuer failed ${FAKE_CREDENTIALS.tmpSecretKey} ${INVITE_CODE}`);
      },
    });

    const response = await worker.fetch(post("/v1/inbox/grant", singleGrantBody()), env);
    expect(response.status).toBe(500);
    const text = await response.text();
    expect(text).not.toContain("tmp-secret");
    expect(text).not.toContain(INVITE_CODE);
    expect(log).not.toHaveBeenCalled();
    expect(info).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
    expect(error).not.toHaveBeenCalled();
    const raw = await kv.get(`grant:${INVITE_ID}:${MOD_ID}`);
    expect(raw).not.toContain("tmp-secret");
    expect(raw).not.toContain(INVITE_CODE);
  });

  it("posts GetFederationToken with the already-encoded policy", async () => {
    const { createDefaultIssuer } = await import("./sts");
    const encoded = buildInboxPolicy(INVITE_ID, MOD_ID).encoded;
    const timestamp = 1551113065;
    const body = JSON.stringify({
      Name: "inboxgrant",
      Policy: encoded,
      DurationSeconds: 3600,
    });
    let captured: { url: string; init: RequestInit } | null = null;
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      captured = { url: String(input), init: init ?? {} };
      return new Response(JSON.stringify({
        Response: {
          Credentials: {
            TmpSecretId: "tmp-id",
            TmpSecretKey: "tmp-secret",
            Token: "tmp-token",
          },
        },
      }), { status: 200 });
    });

    const issuer = createDefaultIssuer({
      secretId: "sid",
      secretKey: "skey",
      now: () => timestamp,
    });
    await expect(issuer(encoded)).resolves.toEqual(FAKE_CREDENTIALS);

    expect(captured).not.toBeNull();
    expect(captured!.url).toBe("https://sts.tencentcloudapi.com");
    expect(captured!.init.method).toBe("POST");
    expect(captured!.init.body).toBe(body);
    const sent = JSON.parse(String(captured!.init.body)) as {
      Name: string;
      Policy: string;
      DurationSeconds: number;
    };
    expect(sent.Name).toBe("inboxgrant");
    expect(sent.DurationSeconds).toBe(3600);
    expect(sent.Policy).toBe(encoded);
    expect(sent.Policy.startsWith("%7B")).toBe(true);
    expect(sent.Policy.includes("%257B")).toBe(false);

    const headers = new Headers(captured!.init.headers);
    expect(headers.get("X-TC-Action")).toBe("GetFederationToken");
    expect(headers.get("X-TC-Version")).toBe("2018-08-13");
    expect(headers.get("X-TC-Region")).toBe("ap-shanghai");
    expect(headers.get("X-TC-Timestamp")).toBe(String(timestamp));
    expect(headers.get("Authorization")).toBe(await expectedAuthorization("sid", "skey", timestamp, body));
  });

  it.each([
    ["omits feedback", () => {
      const body = singleGrantBody() as { feedback?: string };
      delete body.feedback;
      return body;
    }],
    ["sends blank feedback", () => ({ ...singleGrantBody(), feedback: "" })],
    ["sends http feedback", () => ({ ...singleGrantBody(), feedback: "http://example.com" })],
  ] as const)("rejects a grant that %s", async (_name, buildBody) => {
    const kv = memoryKv();
    await seedInvite(kv);
    const response = await worker.fetch(post("/v1/inbox/grant", buildBody()), testEnv(kv));
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ error: "validation_failed" });
  });
});

async function expectedAuthorization(
  secretId: string,
  secretKey: string,
  timestamp: number,
  body: string,
): Promise<string> {
  const host = "sts.tencentcloudapi.com";
  const service = "sts";
  const date = new Date(timestamp * 1000).toISOString().slice(0, 10);
  const hashedPayload = await sha256Hex(body);
  const canonicalHeaders = `content-type:application/json; charset=utf-8\nhost:${host}\n`;
  const signedHeaders = "content-type;host";
  const canonical = ["POST", "/", "", canonicalHeaders, signedHeaders, hashedPayload].join("\n");
  const scope = `${date}/${service}/tc3_request`;
  const stringToSign = [
    "TC3-HMAC-SHA256",
    String(timestamp),
    scope,
    await sha256Hex(canonical),
  ].join("\n");
  const secretDate = await hmacSha256(utf8(`TC3${secretKey}`), date);
  const secretService = await hmacSha256(secretDate, service);
  const secretSigning = await hmacSha256(secretService, "tc3_request");
  const signature = hex(await hmacSha256(secretSigning, stringToSign));
  return `TC3-HMAC-SHA256 Credential=${secretId}/${scope}, SignedHeaders=${signedHeaders}, Signature=${signature}`;
}

function utf8(value: string): Uint8Array {
  return new TextEncoder().encode(value);
}

function hex(buffer: ArrayBuffer): string {
  return [...new Uint8Array(buffer)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function sha256Hex(value: string): Promise<string> {
  return hex(await crypto.subtle.digest("SHA-256", utf8(value)));
}

async function hmacSha256(key: BufferSource, message: string): Promise<ArrayBuffer> {
  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    key,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  return crypto.subtle.sign("HMAC", cryptoKey, utf8(message));
}
