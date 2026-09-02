using System.Diagnostics;
using System.IO;
using System.Linq;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class GameDetector
{
    public GameStatus Detect(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) ||
            !File.Exists(Path.Combine(gamePath, "Mechabellum.exe")) ||
            !File.Exists(Path.Combine(gamePath, "GameAssembly.dll")))
        {
            return new GameStatus
            {
                Kind = GameStatusKind.GameMissing,
                GamePath = gamePath ?? "",
                Message = "未找到有效的 Mechabellum 安装（需要 Mechabellum.exe 与 GameAssembly.dll）。"
            };
        }

        var melon = Directory.Exists(Path.Combine(gamePath, "MelonLoader"));
        var proxy = File.Exists(Path.Combine(gamePath, "version.dll"))
                    || File.Exists(Path.Combine(gamePath, "winhttp.dll"));

        if (!melon && !proxy)
            return new GameStatus
            {
                Kind = GameStatusKind.GameOkLoaderMissing,
                GamePath = gamePath,
                Message = "已找到游戏，但未安装 MelonLoader。请自行安装后再部署。"
            };

        if (!(melon && proxy))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPartial,
                GamePath = gamePath,
                Message = "MelonLoader 安装不完整（需要 MelonLoader 目录以及 version.dll 或 winhttp.dll）。"
            };

        return new GameStatus
        {
            Kind = GameStatusKind.Ready,
            GamePath = gamePath,
            Message = "游戏与 MelonLoader 已就绪。",
            MelonLoaderVersion = TryReadMelonVersion(gamePath)
        };
    }

    static string? TryReadMelonVersion(string gamePath)
    {
        // Best-effort: MelonLoader/Documentation/MelonLoader.xml or version on MelonLoader.dll if present
        var dll = Directory.GetFiles(Path.Combine(gamePath, "MelonLoader"), "MelonLoader.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (dll is null) return null;
        try { return FileVersionInfo.GetVersionInfo(dll).FileVersion; }
        catch { return null; }
    }
}
