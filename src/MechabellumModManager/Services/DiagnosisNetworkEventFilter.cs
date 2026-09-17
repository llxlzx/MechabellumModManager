using System.Windows;
using System.Windows.Markup;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Distinguishes WPF/UI presentation failures from real catalog/update network failures
/// so diagnosis does not show "目录或检查更新网络失败" for dialog resource errors.
/// </summary>
public static class DiagnosisNetworkEventFilter
{
    public static bool IsLikelyUiPresentationFailure(Exception? ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is XamlParseException or ResourceReferenceKeyNotFoundException)
                return true;
            if (LooksLikeUiFailureMessage(cur.Message))
                return true;
        }

        return false;
    }

    public static bool IsLikelyUiPresentationFailure(DiagnosisEvent? ev)
    {
        if (ev is null)
            return false;
        if (ev.Data is not null && ev.Data.TryGetValue("error", out var err) && LooksLikeUiFailureMessage(err))
            return true;
        return false;
    }

    public static bool LooksLikeUiFailureMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        return message.Contains("StaticResourceExtension", StringComparison.OrdinalIgnoreCase)
            || message.Contains("DynamicResourceExtension", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ResourceReferenceKeyNotFound", StringComparison.OrdinalIgnoreCase)
            || message.Contains("XamlParseException", StringComparison.OrdinalIgnoreCase)
            || message.Contains("提供值时引发了异常", StringComparison.Ordinal);
    }
}
