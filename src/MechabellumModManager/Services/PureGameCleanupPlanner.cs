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
        var hasStorePaths = !string.IsNullOrWhiteSpace(branch?.OfficialStorePath)
                            || !string.IsNullOrWhiteSpace(branch?.BetaStorePath);
        var dualEnabled = branch is { Enabled: true };

        string? official = NullIfEmpty(branch?.OfficialStorePath);
        string? beta = NullIfEmpty(branch?.BetaStorePath);
        string? link = NullIfEmpty(branch?.SteamLinkPath);
        string? configGame = NullIfEmpty(request.GamePathOverride) ?? NullIfEmpty(request.ConfigGamePath);

        // Official-complete gate only when dual-folder is active or we intend to delete the other store.
        if (dualEnabled || (hasStorePaths && request.ConfirmDeleteOtherStore))
        {
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

            if (string.IsNullOrWhiteSpace(official) || !SteamGameLocator.LooksLikeGameRoot(official))
            {
                return new CleanupPlan
                {
                    AbortReason =
                        "正式服游戏仓不完整（缺少 Mechabellum.exe 或 GameAssembly.dll）。已中止，不会删除另一侧仓或游戏文件。请先用管理器切到完整仓或修好正式服后再清理。",
                    AbortExitCode = 3
                };
            }

            if (!string.IsNullOrWhiteSpace(beta)
                && Directory.Exists(beta)
                && request.ConfirmDeleteOtherStore
                && !PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(official, beta))
            {
                return new CleanupPlan
                {
                    AbortReason = "另一侧仓路径未通过白名单校验，已中止删除。",
                    AbortExitCode = 4
                };
            }
        }

        var actions = new List<CleanupAction>();

        if (dualEnabled)
        {
            actions.Add(new CleanupAction(
                CleanupActionKind.TeardownDualFolder,
                link ?? official ?? configGame ?? "",
                request.ConfirmDeleteOtherStore
                    ? "解除双服并删除另一侧仓（正式服已完整）"
                    : "解除双服（保留另一侧仓；未确认删除）"));
        }

        var melonRoot = ResolveMelonRoot(link, official, configGame);
        if (!request.SkipMelon
            && !string.IsNullOrWhiteSpace(melonRoot)
            && SteamGameLocator.LooksLikeGameRoot(melonRoot))
        {
            foreach (var dirName in PureGameCleanupWhitelist.MelonDirectoryNames)
            {
                var dir = Path.Combine(melonRoot, dirName);
                if (Directory.Exists(dir) && PureGameCleanupWhitelist.IsAllowedMelonDirectory(melonRoot, dir))
                    actions.Add(new CleanupAction(CleanupActionKind.DeleteDirectory, dir, "Melon/Mod 目录白名单"));
            }

            foreach (var fileName in PureGameCleanupWhitelist.MelonFileNames)
            {
                var file = Path.Combine(melonRoot, fileName);
                if (File.Exists(file) && PureGameCleanupWhitelist.IsAllowedMelonFile(melonRoot, file))
                    actions.Add(new CleanupAction(CleanupActionKind.DeleteFile, file, "Melon 代理 DLL 白名单"));
            }
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
            GameRootForMelon = melonRoot
        };
    }

    static string? ResolveMelonRoot(string? link, string? official, string? configGame)
    {
        foreach (var c in new[] { link, configGame, official })
        {
            if (!string.IsNullOrWhiteSpace(c) && SteamGameLocator.LooksLikeGameRoot(c))
                return Path.GetFullPath(c);
        }

        return NullIfEmpty(configGame) is { } g ? Path.GetFullPath(g) : null;
    }

    static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
