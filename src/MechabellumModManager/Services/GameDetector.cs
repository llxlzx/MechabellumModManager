using System.Diagnostics;
using System.IO;
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
                Message = "已找到游戏，但未安装 MelonLoader。请重新运行管理器安装包并勾选 MelonLoader，或自行安装后再使用。"
            };

        if (!(melon && proxy))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPartial,
                GamePath = gamePath,
                Message = "MelonLoader 安装不完整（需要 MelonLoader 目录以及 version.dll 或 winhttp.dll）。请重新运行安装包勾选 MelonLoader，或手动补全。"
            };

        if (!HasIl2CppAssemblies(gamePath))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPresentAssembliesMissing,
                GamePath = gamePath,
                Message = "MelonLoader 框架已安装，但尚未生成 Il2Cpp 程序集（可立即生成，或稍后在应用方案时生成；首次约一两分钟）。",
                MelonLoaderVersion = TryReadMelonVersion(gamePath)
            };

        return new GameStatus
        {
            Kind = GameStatusKind.Ready,
            GamePath = gamePath,
            Message = "游戏与 MelonLoader 已就绪。",
            MelonLoaderVersion = TryReadMelonVersion(gamePath)
        };
    }

    public static bool HasIl2CppAssemblies(string gamePath) =>
        File.Exists(Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));

    static string? TryReadMelonVersion(string gamePath)
    {
        var melonRoot = Path.Combine(gamePath, "MelonLoader");
        // Prefer known runtime folders — avoid scanning Il2CppAssemblies / Dependencies.
        foreach (var candidate in new[]
                 {
                     Path.Combine(melonRoot, "net6", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "net472", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "net35", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "MelonLoader.dll")
                 })
        {
            if (!File.Exists(candidate)) continue;
            try { return FileVersionInfo.GetVersionInfo(candidate).FileVersion; }
            catch { /* try next */ }
        }

        return null;
    }
}
