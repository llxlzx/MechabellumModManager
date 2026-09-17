export type GitHubEnv = {
  token: string;
  owner: string;
  repo: string;
  branch: string;
};

export type RepoFile = {
  content: string; // utf8 text or decoded
  sha: string;
  encoding: "utf-8" | "base64";
};

function apiBase(env: GitHubEnv): string {
  return `https://api.github.com/repos/${env.owner}/${env.repo}/contents`;
}

export async function getRepoFile(
  env: GitHubEnv,
  path: string,
  fetchImpl: typeof fetch = fetch,
): Promise<RepoFile | null> {
  const url = `${apiBase(env)}/${path.replace(/^\/+/, "")}?ref=${encodeURIComponent(env.branch)}`;
  const res = await fetchImpl(url, {
    headers: {
      Authorization: `Bearer ${env.token}`,
      Accept: "application/vnd.github+json",
      "User-Agent": "mod-publish-worker",
    },
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`upstream_github:${res.status}`);
  const body = (await res.json()) as { content?: string; encoding?: string; sha?: string };
  if (!body.sha || body.content == null) throw new Error("upstream_github");
  let text: string;
  if (body.encoding === "base64") {
    text = atob(body.content.replace(/\n/g, ""));
  } else {
    text = body.content;
  }
  return { content: text, sha: body.sha, encoding: "utf-8" };
}

export type PutResult = { commitSha: string; contentSha: string };

/**
 * Conditional put using sha. Retries on 409 by re-getting and invoking rebuild.
 * rebuild receives latest file text (or null if missing) and must return new utf8 body.
 */
export async function putRepoFileWithRetry(
  env: GitHubEnv,
  path: string,
  message: string,
  rebuild: (latestText: string | null) => string,
  opts: { maxAttempts?: number; fetchImpl?: typeof fetch } = {},
): Promise<PutResult> {
  const maxAttempts = opts.maxAttempts ?? 5;
  const fetchImpl = opts.fetchImpl ?? fetch;
  const cleanPath = path.replace(/^\/+/, "");

  let lastErr: unknown;
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    const existing = await getRepoFile(env, cleanPath, fetchImpl);
    const content = rebuild(existing?.content ?? null);
    const payload: Record<string, string> = {
      message,
      content: btoa(unescape(encodeURIComponent(content))),
      branch: env.branch,
    };
    if (existing?.sha) payload.sha = existing.sha;

    const res = await fetchImpl(`${apiBase(env)}/${cleanPath}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${env.token}`,
        Accept: "application/vnd.github+json",
        "User-Agent": "mod-publish-worker",
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    });

    if (res.status === 409 || res.status === 422) {
      lastErr = new Error(`catalog_conflict:${res.status}`);
      continue;
    }
    if (!res.ok) {
      const t = await res.text();
      throw new Error(`upstream_github:${res.status}:${t.slice(0, 200)}`);
    }
    const body = (await res.json()) as {
      commit?: { sha?: string };
      content?: { sha?: string };
    };
    return {
      commitSha: body.commit?.sha ?? "",
      contentSha: body.content?.sha ?? existing?.sha ?? "",
    };
  }
  throw lastErr instanceof Error ? lastErr : new Error("catalog_conflict");
}

/** Put raw binary (base64) with optional sha for update. */
export async function putRepoBinary(
  env: GitHubEnv,
  path: string,
  message: string,
  bytes: Uint8Array,
  sha: string | undefined,
  fetchImpl: typeof fetch = fetch,
): Promise<PutResult> {
  const cleanPath = path.replace(/^\/+/, "");
  const payload: Record<string, string> = {
    message,
    content: bufferToBase64(bytes),
    branch: env.branch,
  };
  if (sha) payload.sha = sha;

  const res = await fetchImpl(`${apiBase(env)}/${cleanPath}`, {
    method: "PUT",
    headers: {
      Authorization: `Bearer ${env.token}`,
      Accept: "application/vnd.github+json",
      "User-Agent": "mod-publish-worker",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(`upstream_github:${res.status}:${t.slice(0, 200)}`);
  }
  const body = (await res.json()) as {
    commit?: { sha?: string };
    content?: { sha?: string };
  };
  return {
    commitSha: body.commit?.sha ?? "",
    contentSha: body.content?.sha ?? "",
  };
}

function bufferToBase64(bytes: Uint8Array): string {
  let binary = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}
