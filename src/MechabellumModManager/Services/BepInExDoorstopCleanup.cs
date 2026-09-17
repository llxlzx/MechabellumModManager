using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// MelonLoader (version.dll) and BepInEx UnityDoorstop (winhttp.dll + doorstop_config.ini)
/// cannot both inject the same Unity process. When Melon is the active loader, disable Doorstop
/// so a leftover BepInEx console / preloader no longer appears or stalls startup.
/// </summary>
public sealed class BepInExDoorstopCleanup
{
    public sealed class Result
    {
        public bool Changed { get; init; }
        public string Message { get; init; } = "";
        public IReadOnlyList<string> RemovedPaths { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// When Melon bootstrap is present, remove UnityDoorstop entry points that would race it.
    /// Does not delete the BepInEx folder (plugins may still be useful after a Melon port).
    /// </summary>
    public Result TryDisableConflictingDoorstop(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return new Result { Changed = false, Message = "游戏路径无效，跳过 BepInEx Doorstop 清理。" };
        }

        var melonDir = Path.Combine(gamePath, "MelonLoader");
        var versionDll = Path.Combine(gamePath, "version.dll");
        if (!Directory.Exists(melonDir) || !File.Exists(versionDll))
        {
            return new Result { Changed = false, Message = "未检测到 MelonLoader 代理，跳过 Doorstop 清理。" };
        }

        if (!HasDoorstopArtifacts(gamePath))
        {
            return new Result { Changed = false, Message = "未发现冲突的 BepInEx Doorstop。" };
        }

        var removed = new List<string>();
        // Prefer Melon's version.dll; winhttp.dll here is almost always UnityDoorstop for BepInEx.
        TryDeleteFile(Path.Combine(gamePath, "winhttp.dll"), removed);
        TryDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"), removed);
        TryDeleteFile(Path.Combine(gamePath, ".doorstop_version"), removed);
        // Legacy / alternate Doorstop names occasionally left by installers.
        TryDeleteFile(Path.Combine(gamePath, "doorstop_config.cfg"), removed);

        if (removed.Count == 0)
        {
            return new Result
            {
                Changed = false,
                Message = "发现 Doorstop 痕迹但未能删除（文件可能被占用）。请关闭游戏后重试。"
            };
        }

        return new Result
        {
            Changed = true,
            Message = "已移除与 MelonLoader 冲突的 BepInEx Doorstop：" +
                      string.Join(", ", removed.Select(Path.GetFileName)),
            RemovedPaths = removed
        };
    }

    public static bool HasDoorstopArtifacts(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        if (File.Exists(Path.Combine(gamePath, "doorstop_config.ini")) ||
            File.Exists(Path.Combine(gamePath, "doorstop_config.cfg")) ||
            File.Exists(Path.Combine(gamePath, ".doorstop_version")))
            return true;

        // winhttp + BepInEx folder is the classic Doorstop layout; Melon alone uses version.dll.
        var winhttp = Path.Combine(gamePath, "winhttp.dll");
        var bepinex = Path.Combine(gamePath, "BepInEx");
        return File.Exists(winhttp) && Directory.Exists(bepinex);
    }

    static void TryDeleteFile(string path, List<string> removed)
    {
        if (!File.Exists(path))
            return;
        try
        {
            File.Delete(path);
            removed.Add(path);
        }
        catch
        {
            // best-effort while the game may still hold handles
        }
    }
}
