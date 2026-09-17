import { describe, expect, it, vi } from "vitest";
import { putRepoFileWithRetry } from "./github";

describe("putRepoFileWithRetry", () => {
  it("retries on 409 then succeeds", async () => {
    let puts = 0;
    const fetchImpl = vi.fn(async (url: string, init?: RequestInit) => {
      if (!init || init.method !== "PUT") {
        return new Response(
          JSON.stringify({
            content: btoa('{"mods":[]}'),
            encoding: "base64",
            sha: "sha-old",
          }),
          { status: 200 },
        );
      }
      puts++;
      if (puts === 1) return new Response("conflict", { status: 409 });
      return new Response(
        JSON.stringify({ commit: { sha: "c1" }, content: { sha: "sha-new" } }),
        { status: 200 },
      );
    }) as unknown as typeof fetch;

    const result = await putRepoFileWithRetry(
      { token: "t", owner: "o", repo: "r", branch: "master" },
      "catalog.json",
      "msg",
      () => JSON.stringify({ mods: [{ id: "x" }] }),
      { fetchImpl, maxAttempts: 5 },
    );
    expect(result.commitSha).toBe("c1");
    expect(puts).toBe(2);
  });
});
