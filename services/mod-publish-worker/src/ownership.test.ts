import { describe, expect, it } from "vitest";
import { authorizeInbox, authorizePublish } from "./ownership";

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

describe("authorizeInbox", () => {
  it("rejects when another invite already owns the id even if the catalog entry is gone", () => {
    expect(authorizeInbox({
      catalogHasMod: false,
      ownerInviteId: "alice",
      inviteId: "bob",
    })).toBe("forbidden_not_owner");
  });

  it("rejects an existing catalog id that has no owner", () => {
    expect(authorizeInbox({
      catalogHasMod: true,
      ownerInviteId: null,
      inviteId: "alice",
    })).toBe("forbidden_not_owner");
  });

  it("allows the recorded owner to submit again", () => {
    expect(authorizeInbox({
      catalogHasMod: true,
      ownerInviteId: "alice",
      inviteId: "alice",
    })).toBe("allow");
  });

  it("allows a new id with no owner", () => {
    expect(authorizeInbox({
      catalogHasMod: false,
      ownerInviteId: null,
      inviteId: "alice",
    })).toBe("allow");
  });
});
