export async function sha256Hex(data: ArrayBuffer | Uint8Array): Promise<string> {
  const buf = data instanceof Uint8Array
    ? data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength)
    : data;
  const digest = await crypto.subtle.digest("SHA-256", buf);
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** Normalize invite code then SHA-256 hex for KV key suffix. */
export async function hashInviteCode(code: string): Promise<string> {
  const normalized = code.trim();
  const bytes = new TextEncoder().encode(normalized);
  return sha256Hex(bytes);
}
