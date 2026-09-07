using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public enum EmergencyRecoverPromptKind
{
    Standard,
    OfficialMissing
}

public readonly record struct EmergencyRecoverPrompt(
    EmergencyRecoverPromptKind Kind,
    string ConfirmKey,
    bool SkipDeleteOtherStore);

/// <summary>
/// Honest emergency copy: missing/hollow Official cannot be invented; skip delete-other.
/// </summary>
public static class EmergencyRecoverGuidance
{
    public static EmergencyRecoverPrompt Build(
        BranchSwitchConfig cfg,
        string? preferredLinkPath,
        Func<string?, bool>? looksLikeGameRoot = null)
    {
        looksLikeGameRoot ??= SteamGameLocator.LooksLikeGameRoot;

        if (OfficialLooksComplete(cfg, preferredLinkPath, looksLikeGameRoot))
        {
            return new EmergencyRecoverPrompt(
                EmergencyRecoverPromptKind.Standard,
                "ConfirmEmergencyRecoverSingle",
                SkipDeleteOtherStore: false);
        }

        return new EmergencyRecoverPrompt(
            EmergencyRecoverPromptKind.OfficialMissing,
            "ConfirmEmergencyRecoverSingleOfficialMissing",
            SkipDeleteOtherStore: true);
    }

    static bool OfficialLooksComplete(
        BranchSwitchConfig cfg,
        string? preferredLinkPath,
        Func<string?, bool> looksLikeGameRoot)
    {
        if (looksLikeGameRoot(NullIfEmpty(cfg.OfficialStorePath)))
            return true;

        var link = NullIfEmpty(preferredLinkPath) ?? NullIfEmpty(cfg.SteamLinkPath);
        if (link is null)
            return false;

        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(link));
            if (string.IsNullOrWhiteSpace(parent))
                return false;
            return looksLikeGameRoot(Path.Combine(parent, SteamBranchLayout.OfficialStoreFolderName));
        }
        catch
        {
            return false;
        }
    }

    static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
