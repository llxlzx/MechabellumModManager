using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class PureGameCleanupResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = "";
    public CleanupPlan? Plan { get; set; }
    public List<string> Log { get; } = new();
}

public sealed class PureGameCleanupExecutor
{
    readonly IProcessProbe _probe;
    readonly BranchSwitchService? _branchSwitch;
    readonly Action<string>? _log;

    public PureGameCleanupExecutor(
        IProcessProbe? probe = null,
        BranchSwitchService? branchSwitch = null,
        Action<string>? log = null)
    {
        _probe = probe ?? new ProcessProbe();
        _branchSwitch = branchSwitch;
        _log = log;
    }

    public PureGameCleanupResult Execute(PureGameCleanupRequest request, bool dryRun)
    {
        var plan = PureGameCleanupPlanner.Build(request);
        var result = new PureGameCleanupResult { Plan = plan };

        void L(string m)
        {
            result.Log.Add(m);
            _log?.Invoke(m);
        }

        if (plan.IsAborted)
        {
            L(plan.AbortReason!);
            result.Success = false;
            result.ExitCode = plan.AbortExitCode;
            result.Message = plan.AbortReason!;
            return result;
        }

        if (_probe.IsGameOrSteamRunning())
        {
            const string msg = "游戏或 Steam 客户端仍在运行。请先完全退出后再清理。";
            L(msg);
            result.Success = false;
            result.ExitCode = 2;
            result.Message = msg;
            return result;
        }

        if (dryRun)
        {
            foreach (var a in plan.Actions)
                L($"[dry-run] {a.Kind} {a.Path} — {a.Reason}");
            result.Success = true;
            result.ExitCode = 0;
            result.Message = "dry-run ok";
            return result;
        }

        foreach (var action in plan.Actions)
        {
            try
            {
                switch (action.Kind)
                {
                    case CleanupActionKind.TeardownDualFolder:
                        if (_branchSwitch is null)
                        {
                            const string msg = "缺少 BranchSwitchService，无法解除双服。";
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 5;
                            result.Message = msg;
                            return result;
                        }

                        var cfg = _branchSwitch.LoadConfig();
                        if (!string.IsNullOrWhiteSpace(cfg.OfficialStorePath)
                            && !SteamGameLocator.LooksLikeGameRoot(cfg.OfficialStorePath))
                        {
                            const string msg = "正式服不完整，拒绝解除双服/删仓。";
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 3;
                            result.Message = msg;
                            return result;
                        }

                        if (cfg.ActiveBranch != GameBranch.Official)
                        {
                            L("当前不在正式服，尝试切换联接到正式服…");
                            var swap = _branchSwitch.TrySwapJunction(GameBranch.Official);
                            if (!swap.Success)
                            {
                                var msg = "切换到正式服失败：" + swap.Message;
                                L(msg);
                                result.Success = false;
                                result.ExitCode = 5;
                                result.Message = msg;
                                return result;
                            }
                        }

                        var tear = _branchSwitch.TryTeardown(request.ConfirmDeleteOtherStore);
                        if (!tear.Success)
                        {
                            var msg = "解除双服失败：" + tear.Message;
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 5;
                            result.Message = msg;
                            return result;
                        }

                        L("双服已解除：" + action.Reason);
                        break;

                    case CleanupActionKind.DeleteDirectory:
                        if (!IsStillAllowedUnderLiveGameRoot(action.Path, isDir: true))
                        {
                            var msg = "拒绝删除目录（白名单失败）：" + action.Path;
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 4;
                            result.Message = msg;
                            return result;
                        }

                        if (Directory.Exists(action.Path))
                        {
                            Directory.Delete(action.Path, recursive: true);
                            L("已删目录：" + action.Path);
                        }

                        break;

                    case CleanupActionKind.DeleteFile:
                        if (!IsStillAllowedUnderLiveGameRoot(action.Path, isDir: false))
                        {
                            var msg = "拒绝删除文件（白名单失败）：" + action.Path;
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 4;
                            result.Message = msg;
                            return result;
                        }

                        if (File.Exists(action.Path))
                        {
                            File.Delete(action.Path);
                            L("已删文件：" + action.Path);
                        }

                        break;

                    case CleanupActionKind.DeleteAppDataRoot:
                        if (!PureGameCleanupWhitelist.IsAllowedManagerAppData(action.Path))
                        {
                            var msg = "拒绝删除 AppData（白名单失败）：" + action.Path;
                            L(msg);
                            result.Success = false;
                            result.ExitCode = 4;
                            result.Message = msg;
                            return result;
                        }

                        if (Directory.Exists(action.Path))
                        {
                            Directory.Delete(action.Path, recursive: true);
                            L("已删管理器数据：" + action.Path);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                var msg = $"执行失败 ({action.Kind}): {ex.Message}";
                L(msg);
                result.Success = false;
                result.ExitCode = 5;
                result.Message = msg;
                return result;
            }
        }

        result.Success = true;
        result.ExitCode = 0;
        result.Message = "清理完成";
        return result;
    }

    static bool IsStillAllowedUnderLiveGameRoot(string path, bool isDir)
    {
        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(parent) || !SteamGameLocator.LooksLikeGameRoot(parent))
                return false;
            return isDir
                ? PureGameCleanupWhitelist.IsAllowedMelonDirectory(parent, path)
                : PureGameCleanupWhitelist.IsAllowedMelonFile(parent, path);
        }
        catch
        {
            return false;
        }
    }
}
