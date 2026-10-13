export type GrantKind = "single" | "parts" | "bundle";

export type GrantRole = "dll" | "part" | "bundle" | "preview";

export interface GrantFile {
  role: GrantRole;
  name: string;
  size: number;
  sha256: string;
  main?: boolean;
}

export interface GrantRequest {
  id: string;
  name: string;
  summary: string;
  version: string;
  kind: GrantKind;
  files: GrantFile[];
}

export interface AssignedKey {
  key: string;
  role: GrantRole;
  size: number;
  sha256: string;
}

export type InspectGrantResult =
  | { ok: true; value: GrantRequest }
  | { ok: false; error: "validation_failed" | "payload_too_large" };

const MAX_FILE_BYTES = 104857599;
const MAX_PREVIEW_BYTES = 8388608;
const MAX_FILE_COUNT = 20;
const MAX_TOTAL_BYTES = 536870912;

const MOD_ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;
const DLL_NAME = /^[A-Za-z0-9._-]+\.dll$/;
const PART_NAME = /^\d{4}$/;
const SHA256 = /^[a-f0-9]{64}$/;

const ROLES: Record<GrantKind, ReadonlySet<string>> = {
  single: new Set(["dll", "preview"]),
  parts: new Set(["part", "preview"]),
  bundle: new Set(["bundle", "preview"]),
};

function failed<E extends "validation_failed" | "payload_too_large">(error: E): { ok: false; error: E } {
  return { ok: false, error };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function optionalText(value: unknown, max: number): string | null {
  if (value === undefined) return "";
  if (typeof value !== "string" || value.length > max) return null;
  return value;
}

export function inspectGrant(body: unknown): InspectGrantResult {
  if (!isRecord(body)) return failed("validation_failed");
  const kind = body.kind;
  if (kind !== "single" && kind !== "parts" && kind !== "bundle")
    return failed("validation_failed");

  if (typeof body.id !== "string" || !MOD_ID.test(body.id))
    return failed("validation_failed");
  if (typeof body.name !== "string" || body.name.length < 1 || body.name.length > 80 || /[\r\n]/.test(body.name))
    return failed("validation_failed");

  const summary = optionalText(body.summary, 400);
  const version = optionalText(body.version, 32);
  if (summary === null || version === null) return failed("validation_failed");
  if (!Array.isArray(body.files)) return failed("validation_failed");

  const files: GrantFile[] = [];
  for (const raw of body.files) {
    const parsed = parseFile(raw, kind);
    if (!parsed.ok) return parsed;
    files.push(parsed.file);
  }

  if (!rolesMatchKind(kind, files)) return failed("validation_failed");

  if (files.length > MAX_FILE_COUNT) return failed("payload_too_large");
  let total = 0;
  for (const file of files) {
    if (file.size < 1 || file.size > MAX_FILE_BYTES) return failed("payload_too_large");
    if (file.role === "preview" && file.size > MAX_PREVIEW_BYTES) return failed("payload_too_large");
    total += file.size;
  }
  if (total > MAX_TOTAL_BYTES) return failed("payload_too_large");

  return {
    ok: true,
    value: { id: body.id, name: body.name, summary, version, kind, files },
  };
}

function parseFile(
  raw: unknown,
  kind: GrantKind,
): { ok: true; file: GrantFile } | { ok: false; error: "validation_failed" } {
  if (!isRecord(raw)) return failed("validation_failed");
  const role = raw.role;
  if (typeof role !== "string" || !ROLES[kind].has(role))
    return failed("validation_failed");
  if (typeof raw.name !== "string" || !nameMatchesRole(role, raw.name))
    return failed("validation_failed");
  if (typeof raw.sha256 !== "string" || !SHA256.test(raw.sha256))
    return failed("validation_failed");
  if (typeof raw.size !== "number" || !Number.isInteger(raw.size))
    return failed("validation_failed");

  const file: GrantFile = {
    role: role as GrantRole,
    name: raw.name,
    size: raw.size,
    sha256: raw.sha256,
  };
  if (role === "bundle" && raw.main === true) file.main = true;
  return { ok: true, file };
}

function nameMatchesRole(role: string, name: string): boolean {
  if (role === "preview") return name === "preview.png";
  if (role === "part") return PART_NAME.test(name);
  return DLL_NAME.test(name);
}

function rolesMatchKind(kind: GrantKind, files: GrantFile[]): boolean {
  const dlls = files.filter((file) => file.role === "dll");
  const parts = files.filter((file) => file.role === "part");
  const bundles = files.filter((file) => file.role === "bundle");
  const previews = files.filter((file) => file.role === "preview");
  if (previews.length > 1) return false;
  if (new Set(files.map((file) => file.name)).size !== files.length) return false;

  if (kind === "single") return dlls.length === 1 && parts.length === 0 && bundles.length === 0;
  if (kind === "parts") {
    if (parts.length < 1 || dlls.length !== 0 || bundles.length !== 0) return false;
    const names = parts.map((file) => file.name).sort();
    return names.every((name, index) => name === String(index).padStart(4, "0"));
  }
  return bundles.length >= 2
    && dlls.length === 0
    && parts.length === 0
    && bundles.filter((file) => file.main === true).length === 1;
}

export function assignKeys(inviteId: string, req: GrantRequest): AssignedKey[] {
  const prefix = `inbox/${inviteId}/${req.id}`;
  return req.files.map((file) => ({
    key: objectKey(prefix, file),
    role: file.role,
    size: file.size,
    sha256: file.sha256,
  }));
}

function objectKey(prefix: string, file: GrantFile): string {
  if (file.role === "dll") return `${prefix}/${file.name}`;
  if (file.role === "part") return `${prefix}/parts/${file.name}`;
  if (file.role === "bundle") return `${prefix}/payload/${file.name}`;
  return `${prefix}/preview.png`;
}
