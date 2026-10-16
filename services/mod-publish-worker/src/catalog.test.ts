import { describe, expect, it } from "vitest";
import { mergeCatalogMod, type CatalogMod, type CatalogRoot } from "./catalog";

function entry(overrides: Partial<CatalogMod> = {}): CatalogMod {
  return {
    id: "demo",
    name: "Demo",
    file: "mods/demo/Demo.dll",
    sha256: "a".repeat(64),
    size: 12,
    feedback: "author@example.com",
    ...overrides,
  };
}

function omitFeedback(value: CatalogMod): CatalogMod {
  const copy = { ...value };
  delete copy.feedback;
  return copy;
}

describe("classifyFeedback", () => {
  it("hides empty text, non-https schemes, unsafe https, and broken mailto", async () => {
    const { classifyFeedback } = await import("./catalog");
    const hidden = [
      "",
      "   ",
      "http://example.com",
      "http://example.com/form",
      "file:///C:/Windows/notepad.exe",
      "javascript:alert(1)",
      "JavaScript:alert(1)",
      "data:text/html,hi",
      "vbscript:msgbox",
      "myapp://open",
      "steam://run/669330",
      "https://user@evil.example/form",
      "https://user:pass@example.com/a",
      "https://github.com@evil.example/path",
      "https://:@host/x",
      "mailto:not-an-email",
      "mailto:a@b.com,c@d.com",
      "mailto:",
      "mailto:a@b.com c@d.com",
      "C:\\Windows\\System32\\calc.exe",
      "\\\\attacker\\share\\malware.exe",
    ];
    for (const value of hidden) {
      expect(`${JSON.stringify(value)} => ${classifyFeedback(value)}`).toBe(
        `${JSON.stringify(value)} => hidden`,
      );
    }
  });

  it("accepts one email, mailto to that email, acceptable https, and other text", async () => {
    const { classifyFeedback } = await import("./catalog");
    const ok = [
      "author@example.com",
      "Player@Example.com",
      "mailto:player@example.com?subject=old",
      "MAILTO:player@example.com",
      "https://example.com/form",
      "HTTPS://example.com/form",
      "QQ 12345",
      "example.com/form",
      "邮箱是 a@b.com",
    ];
    for (const value of ok) {
      expect(`${JSON.stringify(value)} => ${classifyFeedback(value)}`).toBe(
        `${JSON.stringify(value)} => ok`,
      );
    }
  });
});

describe("mergeCatalogMod", () => {
  it("appends new mod and requires sha256/size", () => {
    const root: CatalogRoot = { mods: [] };
    const next = mergeCatalogMod(root, entry());
    expect(next.mods).toHaveLength(1);
    expect(next.mods[0].id).toBe("demo");
    expect(next.mods[0].feedback).toBe("author@example.com");
    expect(next.updatedAt).toBeTruthy();
  });

  it("replaces existing mod by id", () => {
    const root: CatalogRoot = {
      mods: [
        {
          id: "demo",
          name: "Old",
          file: "mods/demo/Demo.dll",
          sha256: "b".repeat(64),
          size: 1,
        },
      ],
    };
    const next = mergeCatalogMod(root, omitFeedback(entry({
      name: "New",
      sha256: "c".repeat(64),
      size: 99,
    })));
    expect(next.mods).toHaveLength(1);
    expect(next.mods[0].name).toBe("New");
    expect(next.mods[0].size).toBe(99);
    expect(Object.prototype.hasOwnProperty.call(next.mods[0], "feedback")).toBe(false);
  });

  it("rejects missing sha256", () => {
    expect(() =>
      mergeCatalogMod(rootEmpty(), entry({ sha256: "" })),
    ).toThrow("validation_failed");
  });

  it("rejects a new id that omits feedback", () => {
    expect(() => mergeCatalogMod(rootEmpty(), omitFeedback(entry()))).toThrow("validation_failed");
  });

  it.each(["", "   ", "http://example.com", "javascript:alert(1)", "mailto:not-an-email"])(
    "rejects a new id whose feedback is %j",
    (feedback) => {
      expect(() => mergeCatalogMod(rootEmpty(), entry({ feedback }))).toThrow("validation_failed");
    },
  );

  it("rejects a new id whose feedback is null", () => {
    expect(() =>
      mergeCatalogMod(rootEmpty(), entry({ feedback: null })),
    ).toThrow("validation_failed");
  });

  it("keeps the previous feedback when an existing id omits the key", () => {
    const root: CatalogRoot = {
      mods: [entry({ name: "Old", feedback: "old@example.com" })],
    };
    const next = mergeCatalogMod(root, omitFeedback(entry({ name: "New", size: 99 })));
    expect(next.mods).toHaveLength(1);
    expect(next.mods[0].name).toBe("New");
    expect(next.mods[0].feedback).toBe("old@example.com");
  });

  it.each(["", "   ", null, "http://example.com", "javascript:alert(1)", "mailto:not-an-email"])(
    "rejects an existing id whose feedback is %j",
    (feedback) => {
      const root: CatalogRoot = {
        mods: [entry({ feedback: "old@example.com" })],
      };
      expect(() => mergeCatalogMod(root, entry({ feedback }))).toThrow("validation_failed");
    },
  );

  it("stores acceptable https and other text on a new id", () => {
    const https = mergeCatalogMod(rootEmpty(), entry({ feedback: "https://example.com/form" }));
    expect(https.mods[0].feedback).toBe("https://example.com/form");
    const copy = mergeCatalogMod(rootEmpty(), entry({ id: "copy", feedback: "QQ 12345" }));
    expect(copy.mods[0].feedback).toBe("QQ 12345");
  });
});

function rootEmpty(): CatalogRoot {
  return { mods: [] };
}
