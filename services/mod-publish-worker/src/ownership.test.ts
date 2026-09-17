import { describe, expect, it } from "vitest";
import { authorizePublish } from "./ownership";

describe("authorizePublish", () => {
  it("allows create when catalog lacks mod", () => {
    expect(
      authorizePublish({
        modId: "foo",
        inviteId: "a",
        catalogHasMod: false,
        ownerInviteId: null,
      }),
    ).toBe("allow_create");
  });

  it("forbids update when no owner", () => {
    expect(
      authorizePublish({
        modId: "foo",
        inviteId: "a",
        catalogHasMod: true,
        ownerInviteId: null,
      }),
    ).toBe("forbidden_not_owner");
  });

  it("allows update when same invite owns", () => {
    expect(
      authorizePublish({
        modId: "foo",
        inviteId: "a",
        catalogHasMod: true,
        ownerInviteId: "a",
      }),
    ).toBe("allow_update");
  });

  it("forbids update when other invite owns", () => {
    expect(
      authorizePublish({
        modId: "foo",
        inviteId: "a",
        catalogHasMod: true,
        ownerInviteId: "b",
      }),
    ).toBe("forbidden_not_owner");
  });
});
