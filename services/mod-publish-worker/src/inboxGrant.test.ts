import { describe, expect, it } from "vitest";
import { assignKeys, inspectGrant } from "./inboxGrant";

const sha = "a".repeat(64);

describe("inspectGrant", () => {
  it("rejects a file at the git limit", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "single",
      files: [{ role: "dll", name: "QuickMark.dll", size: 100 * 1024 * 1024, sha256: sha }],
    });
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toBe("payload_too_large");
  });

  it("rejects parts and a bundle file together", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "parts",
      files: [
        { role: "part", name: "0000", size: 10, sha256: sha },
        { role: "bundle", name: "Other.dll", size: 10, sha256: sha, main: true },
      ],
    });
    expect(result.ok).toBe(false);
  });

  it("rejects an uppercase mod id", () => {
    const result = inspectGrant({
      id: "QuickMark",
      name: "Quick Mark",
      kind: "single",
      files: [{ role: "dll", name: "QuickMark.dll", size: 10, sha256: sha }],
    });
    expect(result.ok).toBe(false);
  });

  it("assigns server keys for a contiguous part list", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "parts",
      files: [
        { role: "part", name: "0000", size: 10, sha256: sha },
        { role: "part", name: "0001", size: 12, sha256: sha },
      ],
    });
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(assignKeys("author1", result.value).map((item) => item.key)).toEqual([
      "inbox/author1/quick-mark/parts/0000",
      "inbox/author1/quick-mark/parts/0001",
    ]);
  });
});
