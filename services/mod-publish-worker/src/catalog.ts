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
  feedback?: string | null;
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

/** Same hide/ok outcomes as FeedbackChannel.Classify. Does not reference C#. */
export function classifyFeedback(value: string): "hidden" | "ok" {
  const trimmed = `${value ?? ""}`.trim();
  if (trimmed.length === 0) return "hidden";
  if (trimmed.startsWith("\\\\") || trimmed.startsWith("//")) return "hidden";
  if (/^[a-zA-Z]:[\\/]/.test(trimmed)) return "hidden";

  let url: URL | null = null;
  try {
    url = new URL(trimmed);
  } catch {
    url = null;
  }

  const scheme = url ? url.protocol.replace(/:$/, "").toLowerCase() : "";
  if (url && scheme.length >= 2) {
    if (scheme !== "https" && scheme !== "mailto") return "hidden";
    if (scheme === "https") return acceptableHttps(trimmed, url) ? "ok" : "hidden";
    if (!trimmed.toLowerCase().startsWith("mailto:")) return "hidden";
    return oneEmail(mailtoAddress(trimmed)) ? "ok" : "hidden";
  }

  if (trimmed.toLowerCase().startsWith("mailto:")) return "hidden";
  return "ok";
}

/** Replace by id or append; bump root updatedAt. Does not mutate input. */
export function mergeCatalogMod(root: CatalogRoot, entry: CatalogMod): CatalogRoot {
  assertPublishableEntry(entry);
  const hasFeedback = Object.prototype.hasOwnProperty.call(entry, "feedback");
  const mods = [...(root.mods ?? [])];
  const idx = mods.findIndex((m) => m.id === entry.id);
  let acceptedFeedback: string | undefined;
  if (!hasFeedback) {
    if (idx < 0) throw new Error("validation_failed");
  } else if (typeof entry.feedback !== "string" || classifyFeedback(entry.feedback) !== "ok") {
    throw new Error("validation_failed");
  } else {
    acceptedFeedback = entry.feedback.trim();
  }

  const { feedback: _ignored, ...rest } = entry;
  const stamped: CatalogMod = {
    ...rest,
    updatedAt: entry.updatedAt ?? new Date().toISOString(),
    ...(hasFeedback ? { feedback: acceptedFeedback } : {}),
  };
  if (idx >= 0) {
    const previous = mods[idx];
    const merged: CatalogMod = { ...previous, ...stamped };
    if (hasFeedback) merged.feedback = acceptedFeedback;
    else if (!Object.prototype.hasOwnProperty.call(previous, "feedback")) delete merged.feedback;
    mods[idx] = merged;
  } else {
    mods.push(stamped);
  }
  return {
    ...root,
    updatedAt: new Date().toISOString(),
    mods,
  };
}

function acceptableHttps(value: string, url: URL): boolean {
  if (url.username !== "" || url.password !== "") return false;
  const authority = /^https:\/\/([^/?#]*)/i.exec(value)?.[1] ?? "";
  if (authority.includes("@")) return false;
  if (url.hostname.trim() === "") return false;
  return true;
}

function mailtoAddress(value: string): string {
  const colon = value.indexOf(":");
  let address = value.slice(colon + 1);
  const query = address.indexOf("?");
  if (query >= 0) address = address.slice(0, query);
  return address;
}

function oneEmail(value: string): boolean {
  if (value.length === 0 || /[ \r\n,]/.test(value)) return false;
  const at = value.indexOf("@");
  return at > 0 && at === value.lastIndexOf("@") && value.indexOf(".", at + 1) > at + 1;
}
