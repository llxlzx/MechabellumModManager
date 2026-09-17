namespace MechabellumModManager.Services;

/// <summary>Default Worker URL for whitelist direct upload. Override via AppConfig.DirectUploadApiBaseUrl.</summary>
public static class DirectUploadDefaults
{
    /// <summary>Replace after deploy; empty disables until configured.</summary>
    public const string ApiBaseUrl = "";
}
