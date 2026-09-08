namespace MechabellumModManager.Services;

/// <summary>
/// Built-in mainland-China COS root used when <c>AppConfig.MirrorBaseUrl</c> is null
/// (never configured). An explicit empty string means the player opted out.
/// </summary>
public static class DomesticMirrorDefaults
{
    public const string BaseUrl =
        "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com";
}
