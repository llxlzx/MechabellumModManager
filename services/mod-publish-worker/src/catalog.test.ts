import { describe, expect, it } from "vitest";
import { mergeCatalogMod, type CatalogRoot } from "./catalog";

describe("mergeCatalogMod", () => {
  it("appends new mod and requires sha256/size", () => {
    const root: CatalogRoot = { mods: [] };
    const next = mergeCatalogMod(root, {
      id: "demo",
      name: "Demo",
      file: "mods/demo/Demo.dll",
      sha256: "a".repeat(64),
      size: 12,
    });
    expect(next.mods).toHaveLength(1);
    expect(next.mods[0].id).toBe("demo");
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
    const next = mergeCatalogMod(root, {
      id: "demo",
      name: "New",
      file: "mods/demo/Demo.dll",
      sha256: "c".repeat(64),
      size: 99,
    });
    expect(next.mods).toHaveLength(1);
    expect(next.mods[0].name).toBe("New");
    expect(next.mods[0].size).toBe(99);
  });

  it("rejects missing sha256", () => {
    expect(() =>
      mergeCatalogMod(
        { mods: [] },
        {
          id: "x",
          name: "X",
          file: "mods/x/x.dll",
          sha256: "",
          size: 1,
        },
      ),
    ).toThrow("validation_failed");
  });
});
