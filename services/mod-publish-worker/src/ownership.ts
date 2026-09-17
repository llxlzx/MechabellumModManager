export type AuthorizeResult = "allow_create" | "allow_update" | "forbidden_not_owner";

export function authorizePublish(args: {
  modId: string;
  inviteId: string;
  catalogHasMod: boolean;
  ownerInviteId: string | null;
}): AuthorizeResult {
  const { catalogHasMod, ownerInviteId, inviteId } = args;
  if (!catalogHasMod) return "allow_create";
  if (ownerInviteId != null && ownerInviteId === inviteId) return "allow_update";
  return "forbidden_not_owner";
}
