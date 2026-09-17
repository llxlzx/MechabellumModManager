export type CatalogMod = {
  id: string;
  name: string;
  author?: string | null;
  version?: string | null;
  updatedAt?: string | null;
  summary?: string | null;
  file: string;
  sha256: string;
  size: number;
  originUrl?: string | null;
  preview?: string | null;
  type?: string | null;
  category?: string | null;
  tags?: string[] | null;
};

export type CatalogRoot = {
  updatedAt?: string | null;
  mods: CatalogMod[];
};

export function assertPublishableEntry(entry: CatalogMod): void {
  if (!entry.id?.trim()) throw new Error("validation_failed");
  if (!entry.name?.trim()) throw new Error("validation_failed");
  if (!entry.file?.trim()) throw new Error("validation_failed");
  if (!entry.sha256 || !/^[a-f0-9]{64}$/i.test(entry.sha256)) throw new Error("validation_failed");
  if (!Number.isFinite(entry.size) || entry.size <= 0) throw new Error("validation_failed");
}

/** Replace by id or append; bump root updatedAt. Does not mutate input. */
export function mergeCatalogMod(root: CatalogRoot, entry: CatalogMod): CatalogRoot {
  assertPublishableEntry(entry);
  const mods = [...(root.mods ?? [])];
  const idx = mods.findIndex((m) => m.id === entry.id);
  const stamped: CatalogMod = {
    ...entry,
    updatedAt: entry.updatedAt ?? new Date().toISOString(),
  };
  if (idx >= 0) mods[idx] = { ...mods[idx], ...stamped };
  else mods.push(stamped);
  return {
    ...root,
    updatedAt: new Date().toISOString(),
    mods,
  };
}
