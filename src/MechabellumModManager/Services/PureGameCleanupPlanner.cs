using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public enum CleanupActionKind
{
    DeleteDirectory,
    DeleteFile,
    DeleteAppDataRoot,
    TeardownDualFolder
}

public sealed record CleanupAction(CleanupActionKind Kind, string Path, string Reason);

public sealed class CleanupPlan
{
    public List<CleanupAction> Actions { get; init; } = new();
    public string? AbortReason { get; init; }
    public int AbortExitCode { get; init; }
    public bool IsAborted => !string.IsNullOrWhiteSpace(AbortReason);
    public string? GameRootForMelon { get; init; }
    public List<string> Notes { get; init; } = new();
}

public sealed class PureGameCleanupRequest
{
    public string? GamePathOverride { get; init; }
    public string? ConfigGamePath { get; init; }
    public BranchSwitchConfig? Branch { get; init; }
    public string? ManagerAppDataRoot { get; init; }
    public bool SkipMelon { get; init; }
    public bool SkipAppData { get; init; }
    public bool ConfirmDeleteOtherStore { get; init; }
}

public static class PureGameCleanupPlanner
{
    public static CleanupPlan Build(PureGameCleanupRequest request)
    {
        var branch = request.Branch;
        var dualEnabled = branch is { Enabled: true };

        string? official = NullIfEmpty(branch?.OfficialStorePath);
        string? beta = NullIfEmpty(branch?.BetaStorePath);
        string? link = NullIfEmpty(branch?.SteamLinkPath);
        string? configGame = NullIfEmpty(request.GamePathOverride) ?? NullIfEmpty(request.ConfigGamePath);

        if (string.IsNullOrWhiteSpace(official)
            && !string.IsNullOrWhiteSpace(link)
            && PureGameCleanupWhitelist.TryNormalizeFullPath(link, out var linkFull))
        {
            var parent = Path.GetDirectoryName(linkFull);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var sibling = Path.Combine(parent, SteamBranchLayout.OfficialStoreFolderName);
                if (Directory.Exists(sibling))
                    official = sibling;
            }
        }

        var notes = new List<string>();
        var actions = new List<CleanupAction>();

        var officialOk = !string.IsNullOrWhiteSpace(official) && SteamGameLocator.LooksLikeGameRoot(official);
        if (dualEnabled)
        {
            if (!officialOk)
            {
                notes.Add("正式服不完整：跳过解除双服/删仓，仍尝试清理各仓内 Melon/Mods。");
            }
            else if (!string.IsNullOrWhiteSpace(beta)
                     && Directory.Exists(beta)
                     && request.ConfirmDeleteOtherStore
                     && !PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(official!, beta))
            {
                return new CleanupPlan
                {
                    AbortReason = "另一侧仓路径未通过白名单校验，已中止删除。",
                    AbortExitCode = 4,
                    Notes = notes
                };
            }
            else
            {
                actions.Add(new CleanupAction(
                    CleanupActionKind.TeardownDualFolder,
                    link ?? official!,
                    request.ConfirmDeleteOtherStore
                        ? "解除双服并删除另一侧仓（正式服已完整）"
                        : "解除双服（保留另一侧仓；未确认删除）"));
            }
        }

        var melonRoots = new List<string>();
        if (!request.SkipMelon)
        {
            foreach (var candidate in new[] { link, configGame, official, beta })
                TryAddMelonRoot(melonRoots, candidate);

            foreach (var root in melonRoots)
                AppendMelonActions(actions, root);
        }

        if (!request.SkipAppData)
        {
            var appData = request.ManagerAppDataRoot
                          ?? Path.Combine(
                              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                              PathsService.AppFolderName);
            if (Directory.Exists(appData) && PureGameCleanupWhitelist.IsAllowedManagerAppData(appData))
            {
                actions.Add(new CleanupAction(
                    CleanupActionKind.DeleteAppDataRoot,
                    Path.GetFullPath(appData),
                    "管理器 AppData"));
            }
        }

        return new CleanupPlan
        {
            Actions = actions,
            GameRootForMelon = melonRoots.FirstOrDefault(),
            Notes = notes
        };
    }

    static void TryAddMelonRoot(List<string> roots, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !SteamGameLocator.LooksLikeGameRoot(candidate))
            return;
        var full = Path.GetFullPath(candidate);
        if (roots.Any(r => string.Equals(r, full, StringComparison.OrdinalIgnoreCase)))
            return;
        roots.Add(full);
    }

    static void AppendMelonActions(List<CleanupAction> actions, string melonRoot)
    {
        foreach (var dirName in PureGameCleanupWhitelist.MelonDirectoryNames)
        {
            var dir = Path.Combine(melonRoot, dirName);
            if (Directory.Exists(dir) && PureGameCleanupWhitelist.IsAllowedMelonDirectory(melonRoot, dir))
            {
                if (actions.Any(a => a.Kind == CleanupActionKind.DeleteDirectory
                                     && string.Equals(a.Path, dir, StringComparison.OrdinalIgnoreCase)))
                    continue;
                actions.Add(new CleanupAction(CleanupActionKind.DeleteDirectory, dir, "Melon/Mod 目录白名单"));
            }
        }

        foreach (var fileName in PureGameCleanupWhitelist.MelonFileNames)
        {
            var file = Path.Combine(melonRoot, fileName);
            if (File.Exists(file) && PureGameCleanupWhitelist.IsAllowedMelonFile(melonRoot, file))
            {
                if (actions.Any(a => a.Kind == CleanupActionKind.DeleteFile
                                     && string.Equals(a.Path, file, StringComparison.OrdinalIgnoreCase)))
                    continue;
                actions.Add(new CleanupAction(CleanupActionKind.DeleteFile, file, "Melon 代理 DLL 白名单"));
            }
        }
    }

    static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
