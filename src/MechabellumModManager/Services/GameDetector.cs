using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class GameDetector
{
    public static readonly TimeSpan LoaderInjectSettle = TimeSpan.FromSeconds(20);

    /// <param name="gameAlreadyRunning">
    /// Deliberately not part of the injection verdict: a game that was already running proves
    /// nothing about the launch this session requested. Kept so callers can pass what they know.
    /// </param>
    public GameStatus Detect(
        string gamePath,
        DateTimeOffset? lastLaunchRequestedAt = null,
        DateTimeOffset? now = null,
        DateTimeOffset? applyStartedAt = null,
        bool gameAlreadyRunning = false)
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
                Message = "已找到游戏，但未安装 MelonLoader。可点右上角「安装 MelonLoader」，或重新运行安装包勾选 MelonLoader。"
            };

        if (!(melon && proxy))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPartial,
                GamePath = gamePath,
                Message = "MelonLoader 安装不完整（需要 MelonLoader 目录以及 version.dll 或 winhttp.dll）。可点「安装 MelonLoader」补全，或重新运行安装包。"
            };

        var version = MelonLoaderVersionGate.TryReadDisplayVersion(gamePath);
        var logAge = TryReadLatestLogAge(gamePath, now ?? DateTimeOffset.Now);

        if (!HasIl2CppAssemblies(gamePath))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPresentAssembliesMissing,
                GamePath = gamePath,
                Message = "MelonLoader 框架已安装，但尚未生成 Il2Cpp 程序集（可立即生成，或稍后在应用方案时生成；首次约一两分钟）。",
                MelonLoaderVersion = version,
                LatestLogAge = logAge
            };

        var injected = EvaluateInjection(
            gamePath,
            lastLaunchRequestedAt,
            applyStartedAt,
            now ?? DateTimeOffset.Now);
        return new GameStatus
        {
            Kind = GameStatusKind.Ready,
            GamePath = gamePath,
            Message = FormatReadyMessage(version, injected),
            MelonLoaderVersion = version,
            LatestLogAge = logAge,
            LoaderInjected = injected
        };
    }

    public static bool HasIl2CppAssemblies(string gamePath) =>
        File.Exists(Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));

    static string FormatReadyMessage(string? version, bool? injected)
    {
        if (injected == false)
            return "Loader 未在本次启动注入";

        if (string.IsNullOrWhiteSpace(version))
            return "游戏与 MelonLoader 已就绪。";

        if (MelonLoaderVersionGate.ShouldUpgradeInstalled(version))
            return $"Melon {version}（低于内置 {MelonLoaderVersionGate.FormatBundledMinimum()}）";

        return $"游戏与 MelonLoader 已就绪。Melon {version}";
    }

    static bool? EvaluateInjection(
        string gamePath,
        DateTimeOffset? lastLaunchRequestedAt,
        DateTimeOffset? applyStartedAt,
        DateTimeOffset now)
    {
        if (lastLaunchRequestedAt is null)
            return null;

        if (now - lastLaunchRequestedAt.Value < LoaderInjectSettle)
            return null;

        // Il2Cpp generation during Apply also writes Latest.log. When the launch stamp is older
        // than this Apply, only a log written after the Apply started can prove an injection —
        // and a launch stamp from a previous session must never be the one that proves it.
        var since = lastLaunchRequestedAt.Value;
        if (applyStartedAt is { } applyStart && applyStart > since)
            return false;

        var logPath = Path.Combine(gamePath, "MelonLoader", "Latest.log");
        if (!File.Exists(logPath))
            return false;

        try
        {
            var write = new DateTimeOffset(File.GetLastWriteTimeUtc(logPath), TimeSpan.Zero);
            return write >= since;
        }
        catch
        {
            return null;
        }
    }

    static TimeSpan? TryReadLatestLogAge(string gamePath, DateTimeOffset now)
    {
        var logPath = Path.Combine(gamePath, "MelonLoader", "Latest.log");
        if (!File.Exists(logPath))
            return null;
        try
        {
            var write = File.GetLastWriteTime(logPath);
            var age = now - new DateTimeOffset(write);
            return age < TimeSpan.Zero ? TimeSpan.Zero : age;
        }
        catch
        {
            return null;
        }
    }
}
