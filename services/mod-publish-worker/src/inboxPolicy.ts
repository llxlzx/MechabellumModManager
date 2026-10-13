const ACTIONS = [
  "cos:PutObject",
  "cos:HeadObject",
  "cos:InitiateMultipartUpload",
  "cos:ListParts",
  "cos:UploadPart",
  "cos:CompleteMultipartUpload",
  "cos:AbortMultipartUpload",
] as const;

export interface CamPolicy {
  version: "2.0";
  statement: [{ effect: "allow"; action: string[]; resource: string[] }];
}

const SAFE_ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;

export function buildInboxPolicy(inviteId: string, modId: string) {
  if (!SAFE_ID.test(inviteId) || !SAFE_ID.test(modId))
    throw new Error("unsafe id");
  const resource =
    `qcs::cos:ap-shanghai:uid/1312774738:mmm-inbox-1312774738/inbox/${inviteId}/${modId}/*`;
  const policy: CamPolicy = {
    version: "2.0",
    statement: [{ effect: "allow", action: [...ACTIONS], resource: [resource] }],
  };
  return { policy, encoded: encodeURIComponent(JSON.stringify(policy)), resource };
}
