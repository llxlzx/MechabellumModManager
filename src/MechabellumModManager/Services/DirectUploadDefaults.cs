namespace MechabellumModManager.Services;

/// <summary>Default Worker URL for whitelist direct upload and inbox grant/complete. Override via AppConfig.DirectUploadApiBaseUrl.</summary>
public static class DirectUploadDefaults
{
    /// <summary>
    /// Replace after deploy; empty disables until configured.
    /// InboxGrantClient accepts only an https base and reports NotConfigured otherwise. Do not put a live URL here.
    /// </summary>
    public const string ApiBaseUrl = "";
}
