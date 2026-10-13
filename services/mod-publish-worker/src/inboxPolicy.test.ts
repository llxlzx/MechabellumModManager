import { describe, expect, it } from "vitest";
import { buildInboxPolicy } from "./inboxPolicy";

describe("buildInboxPolicy", () => {
  it("scopes put and multipart to one mod prefix and url-encodes once", () => {
    const built = buildInboxPolicy("author1", "quick-mark");
    expect(built.resource).toBe(
      "qcs::cos:ap-shanghai:uid/1312774738:mmm-inbox-1312774738/inbox/author1/quick-mark/*",
    );
    expect(built.policy.statement[0].action).toEqual([
      "cos:PutObject",
      "cos:HeadObject",
      "cos:InitiateMultipartUpload",
      "cos:ListParts",
      "cos:UploadPart",
      "cos:CompleteMultipartUpload",
      "cos:AbortMultipartUpload",
    ]);
    expect(built.encoded.startsWith("%7B")).toBe(true);
    expect(built.encoded.includes("%257B")).toBe(false);
    expect(built.encoded.length).toBeLessThan(2048);
    expect(JSON.stringify(built.policy)).not.toContain("DeleteObject");
    expect(JSON.stringify(built.policy)).not.toContain("GetBucket");
    expect(JSON.stringify(built.policy)).not.toContain("principal");
  });

  it("refuses an invite id that could widen the prefix", () => {
    expect(() => buildInboxPolicy("a/b", "quick-mark")).toThrow(/unsafe id/);
    expect(() => buildInboxPolicy("author1", "..")).toThrow(/unsafe id/);
  });
});
