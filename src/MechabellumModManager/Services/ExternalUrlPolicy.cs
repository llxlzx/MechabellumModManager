namespace MechabellumModManager.Services;

/// <summary>
/// Gate for everything handed to the Windows shell as a URL. Update manifests come from a
/// remote mirror, so a raw string must never reach ShellExecute: <c>file:</c>, UNC paths and
/// custom protocol handlers would run a local program after a single confirm click.
/// </summary>
public static class ExternalUrlPolicy
{
    public static bool IsAllowed(string? url) => TryParse(url, out _);

    /// <summary>Stricter than <see cref="IsAllowed"/>: no mailto, for endpoints we fetch from.</summary>
    public static bool IsHttpsUrl(string? url) =>
        TryParse(url, out var uri) && uri!.Scheme == Uri.UriSchemeHttps;

    public static bool TryParse(string? url, out Uri? uri)
    {
        uri = null;
        var value = (url ?? "").Trim();
        if (value.Length == 0)
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            return false;

        if (parsed.Scheme == Uri.UriSchemeMailto)
        {
            uri = parsed;
            return true;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps)
            return false;

        // https://github.com@evil.example/ reads as GitHub but resolves to the attacker's host.
        if (!string.IsNullOrEmpty(parsed.UserInfo))
            return false;

        if (parsed.IsUnc || string.IsNullOrWhiteSpace(parsed.Host))
            return false;

        uri = parsed;
        return true;
    }
}
