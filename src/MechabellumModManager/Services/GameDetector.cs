using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class GameDetector
{
    public static readonly TimeSpan LoaderInjectSettle = TimeSpan.FromSeconds(20);

    /// <param name="gameAlreadyRunning">
    /// A stale Latest.log after the launch stamp is "not injected" only when the process is
    /// actually up. If the process never appeared, the missing log update is unknown, not a
    /// failed injection.
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
                Message = FormatMessage(GameStatusKind.GameMissing, null, null)
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
                Message = FormatMessage(GameStatusKind.GameOkLoaderMissing, null, null)
            };

        if (!(melon && proxy))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPartial,
                GamePath = gamePath,
                Message = FormatMessage(GameStatusKind.LoaderPartial, null, null)
            };

        var version = MelonLoaderVersionGate.TryReadDisplayVersion(gamePath);
        var logAge = TryReadLatestLogAge(gamePath, now ?? DateTimeOffset.Now);

        if (!HasIl2CppAssemblies(gamePath))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPresentAssembliesMissing,
                GamePath = gamePath,
                Message = FormatMessage(GameStatusKind.LoaderPresentAssembliesMissing, version, null),
                MelonLoaderVersion = version,
                LatestLogAge = logAge
            };

        var injected = EvaluateInjection(
            gamePath,
            lastLaunchRequestedAt,
            applyStartedAt,
            now ?? DateTimeOffset.Now,
            gameAlreadyRunning);
        return new GameStatus
        {
            Kind = GameStatusKind.Ready,
            GamePath = gamePath,
            Message = FormatMessage(GameStatusKind.Ready, version, injected),
            MelonLoaderVersion = version,
            LatestLogAge = logAge,
            LoaderInjected = injected
        };
    }

    public static bool HasIl2CppAssemblies(string gamePath) =>
        File.Exists(Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));

    /// <summary>Status sentence for the current UI language. Safe to call again after a language switch.</summary>
    public static string FormatMessage(GameStatusKind kind, string? melonVersion, bool? loaderInjected) =>
        kind switch
        {
            GameStatusKind.GameMissing => LocalizationService.T("StatusDetailGameMissing"),
            GameStatusKind.GameOkLoaderMissing => LocalizationService.T("StatusDetailLoaderMissing"),
            GameStatusKind.LoaderPartial => LocalizationService.T("StatusDetailLoaderPartial"),
            GameStatusKind.LoaderPresentAssembliesMissing => LocalizationService.T("StatusDetailAssembliesMissing"),
            GameStatusKind.Ready => FormatReadyMessage(melonVersion, loaderInjected),
            _ => LocalizationService.T("StatusKindUnknown")
        };

    static string FormatReadyMessage(string? version, bool? injected)
    {
        if (injected == false)
            return LocalizationService.T("StatusDetailNotInjected");

        if (string.IsNullOrWhiteSpace(version))
            return LocalizationService.T("StatusDetailReady");

        if (MelonLoaderVersionGate.ShouldUpgradeInstalled(version))
            return string.Format(
                LocalizationService.T("StatusDetailVersionBelow"),
                version,
                MelonLoaderVersionGate.FormatBundledMinimum());

        return string.Format(LocalizationService.T("StatusDetailReadyVersion"), version);
    }

    static bool? EvaluateInjection(
        string gamePath,
        DateTimeOffset? lastLaunchRequestedAt,
        DateTimeOffset? applyStartedAt,
        DateTimeOffset now,
        bool gameAlreadyRunning)
    {
        if (lastLaunchRequestedAt is null)
            return null;

        if (now - lastLaunchRequestedAt.Value < LoaderInjectSettle)
            return null;

        // Il2Cpp generation during Apply also writes Latest.log. A launch stamp from a previous
        // session must not prove this Apply injected. Before the new launch, with no process,
        // that is unknown — not a failed injection. A process already up still counts as not injected.
        var since = lastLaunchRequestedAt.Value;
        if (applyStartedAt is { } applyStart && applyStart > since)
            return gameAlreadyRunning ? false : null;

        var logPath = Path.Combine(gamePath, "MelonLoader", "Latest.log");
        if (!File.Exists(logPath))
            return gameAlreadyRunning ? false : null;

        try
        {
            var write = new DateTimeOffset(File.GetLastWriteTimeUtc(logPath), TimeSpan.Zero);
            if (write >= since)
                return true;

            // Steam can take longer than the settle window. No process and no new log means
            // the launch has not been observed, not that Melon failed to inject.
            return gameAlreadyRunning ? false : null;
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
