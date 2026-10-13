export type FederationCredentials = {
  tmpSecretId: string;
  tmpSecretKey: string;
  sessionToken: string;
};

export type CredentialIssuer = (encodedPolicy: string) => Promise<FederationCredentials>;

const STS_HOST = "sts.tencentcloudapi.com";
const STS_URL = `https://${STS_HOST}`;
const CONTENT_TYPE = "application/json; charset=utf-8";
const SIGNED_HEADERS = "content-type;host";

export function createDefaultIssuer(args: {
  secretId: string;
  secretKey: string;
  fetchImpl?: typeof fetch;
  now?: () => number;
}): CredentialIssuer {
  const fetchImpl = args.fetchImpl ?? fetch;
  return async (encodedPolicy: string) => {
    const timestamp = args.now ? args.now() : Math.floor(Date.now() / 1000);
    const body = JSON.stringify({
      Name: "inboxgrant",
      Policy: encodedPolicy,
      DurationSeconds: 3600,
    });
    const authorization = await signTc3({
      secretId: args.secretId,
      secretKey: args.secretKey,
      timestamp,
      body,
    });
    let response: Response;
    try {
      response = await fetchImpl(STS_URL, {
        method: "POST",
        headers: {
          Authorization: authorization,
          "Content-Type": CONTENT_TYPE,
          Host: STS_HOST,
          "X-TC-Action": "GetFederationToken",
          "X-TC-Version": "2018-08-13",
          "X-TC-Region": "ap-shanghai",
          "X-TC-Timestamp": String(timestamp),
        },
        body,
      });
    } catch {
      throw new Error("sts_failed");
    }
    if (!response.ok) throw new Error("sts_failed");
    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new Error("sts_failed");
    }
    return readCredentials(payload);
  };
}

async function signTc3(args: {
  secretId: string;
  secretKey: string;
  timestamp: number;
  body: string;
}): Promise<string> {
  const service = "sts";
  const date = new Date(args.timestamp * 1000).toISOString().slice(0, 10);
  const hashedPayload = await sha256Hex(args.body);
  const canonicalHeaders = `content-type:${CONTENT_TYPE}\nhost:${STS_HOST}\n`;
  const canonical = ["POST", "/", "", canonicalHeaders, SIGNED_HEADERS, hashedPayload].join("\n");
  const scope = `${date}/${service}/tc3_request`;
  const stringToSign = [
    "TC3-HMAC-SHA256",
    String(args.timestamp),
    scope,
    await sha256Hex(canonical),
  ].join("\n");
  const secretDate = await hmacSha256(utf8(`TC3${args.secretKey}`), date);
  const secretService = await hmacSha256(secretDate, service);
  const secretSigning = await hmacSha256(secretService, "tc3_request");
  const signature = hex(await hmacSha256(secretSigning, stringToSign));
  return `TC3-HMAC-SHA256 Credential=${args.secretId}/${scope}, SignedHeaders=${SIGNED_HEADERS}, Signature=${signature}`;
}

function readCredentials(payload: unknown): FederationCredentials {
  if (!isRecord(payload)) throw new Error("sts_failed");
  const response = payload.Response;
  if (!isRecord(response)) throw new Error("sts_failed");
  const credentials = response.Credentials;
  if (!isRecord(credentials)) throw new Error("sts_failed");
  const { TmpSecretId, TmpSecretKey, Token } = credentials;
  if (typeof TmpSecretId !== "string" || typeof TmpSecretKey !== "string" || typeof Token !== "string") {
    throw new Error("sts_failed");
  }
  return {
    tmpSecretId: TmpSecretId,
    tmpSecretKey: TmpSecretKey,
    sessionToken: Token,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
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
