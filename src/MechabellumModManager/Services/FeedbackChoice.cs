using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// One labeled https destination an author left beside the free-text <c>feedback</c> field.
/// Older managers ignore the array and still copy <c>feedback</c>.
/// </summary>
public sealed class FeedbackChoice
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public static class FeedbackChoices
{
    /// <summary>
    /// Labels that are blank, and urls the single-channel classifier would not open, are dropped.
    /// </summary>
    public static IReadOnlyList<FeedbackChoice> Acceptable(IEnumerable<FeedbackChoice>? choices)
    {
        if (choices is null)
            return Array.Empty<FeedbackChoice>();

        var accepted = new List<FeedbackChoice>();
        foreach (var choice in choices)
        {
            if (choice is null)
                continue;
            var label = (choice.Label ?? "").Trim();
            var url = (choice.Url ?? "").Trim();
            if (label.Length == 0)
                continue;
            if (FeedbackChannel.Classify(url).Kind != FeedbackKind.OpenHttps)
                continue;
            accepted.Add(new FeedbackChoice { Label = label, Url = url });
        }

        return accepted;
    }

    /// <summary>
    /// Pulls openable https addresses out of a free-text feedback note. The current catalog
    /// stores Baba's two links this way, and older managers still copy that note whole.
    /// </summary>
    public static IReadOnlyList<FeedbackChoice> FromText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<FeedbackChoice>();

        var accepted = new List<FeedbackChoice>();
        var index = 0;
        while (index < raw.Length)
        {
            var start = raw.IndexOf("https://", index, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;
            var end = start + "https://".Length;
            while (end < raw.Length && !char.IsWhiteSpace(raw[end]) && raw[end] is not '，' and not '。' and not '、')
                end++;
            var url = raw[start..end].TrimEnd('.', ',', ';', ')', '）');
            if (FeedbackChannel.Classify(url).Kind == FeedbackKind.OpenHttps)
                accepted.Add(new FeedbackChoice { Label = url, Url = url });
            index = Math.Max(end, start + 1);
        }

        return accepted;
    }

    /// <summary>
    /// China opens the QQ Channel link. International opens Discord. A single link opens as itself.
    /// </summary>
    public static string? Pick(IReadOnlyList<FeedbackChoice> choices, AppEdition edition)
    {
        if (choices.Count == 0)
            return null;
        if (choices.Count == 1)
            return choices[0].Url;
        if (edition == AppEdition.Cn)
            return FirstHost(choices, "pd.qq.com", "qq.com") ?? choices[0].Url;
        return FirstHost(choices, "discord.com", "discord.gg") ?? choices[^1].Url;
    }

    static string? FirstHost(IReadOnlyList<FeedbackChoice> choices, params string[] hosts)
    {
        foreach (var choice in choices)
        {
            if (!Uri.TryCreate(choice.Url, UriKind.Absolute, out var uri))
                continue;
            foreach (var host in hosts)
            {
                if (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase))
                    return choice.Url;
            }
        }

        return null;
    }
}
