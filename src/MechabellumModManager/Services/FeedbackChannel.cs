namespace MechabellumModManager.Services;

public enum FeedbackKind { Hidden, Mail, OpenHttps, Copy }

public readonly record struct FeedbackDecision(FeedbackKind Kind, string? Target, string Original);

public static class FeedbackChannel
{
    public static FeedbackDecision Classify(string? raw)
    {
        var value = (raw ?? "").Trim();
        var original = value;
        if (value.Length == 0)
            return new(FeedbackKind.Hidden, null, "");

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme;
            if (!scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase))
                return new(FeedbackKind.Hidden, null, original);

            if (scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                if (!ExternalUrlPolicy.TryParse(value, out _))
                    return new(FeedbackKind.Hidden, null, original);
                return new(FeedbackKind.OpenHttps, original, original);
            }

            if (!value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                return new(FeedbackKind.Hidden, null, original);
            var mailtoPrefixLength = value.IndexOf(':') + 1;
            var address = value[mailtoPrefixLength..];
            var query = address.IndexOf('?');
            if (query >= 0)
                address = address[..query];
            if (IsOneEmail(address))
                return new(FeedbackKind.Mail, address, original);
            return new(FeedbackKind.Hidden, null, original);
        }

        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return new(FeedbackKind.Hidden, null, original);

        if (IsOneEmail(value))
            return new(FeedbackKind.Mail, value, original);
        return new(FeedbackKind.Copy, original, original);
    }

    static bool IsOneEmail(string value)
    {
        if (value.Length == 0 || value.IndexOfAny([' ', '\r', '\n', ',']) >= 0)
            return false;
        var at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && value.IndexOf('.', at + 1) > at + 1;
    }
}
