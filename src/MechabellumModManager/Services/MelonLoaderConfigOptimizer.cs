using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed class MelonLoaderOptimizeResult
{
    public bool Changed { get; init; }
    public string Message { get; init; } = "";
    public bool NeedsFirstAssemblyGeneration { get; init; }
}

/// <summary>
/// Applies MelonLoader Loader.cfg tweaks that reduce start/quit freezes for IL2CPP games.
/// </summary>
public sealed class MelonLoaderConfigOptimizer
{
    static readonly Regex ForceQuitRegex = new(
        @"^(?<indent>\s*)force_quit\s*=\s*(?<value>true|false)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex ForceOfflineRegex = new(
        @"^(?<indent>\s*)force_offline_generation\s*=\s*(?<value>true|false)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    readonly UnityVersionResolver _resolver;

    public MelonLoaderConfigOptimizer(UnityVersionResolver? resolver = null)
    {
        _resolver = resolver ?? new UnityVersionResolver();
    }

    public string GetLoaderConfigPath(string gamePath) =>
        Path.Combine(gamePath, "UserData", "Loader.cfg");

    public bool NeedsFirstAssemblyGeneration(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath)) return false;
        var assemblies = Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies");
        if (!Directory.Exists(assemblies)) return true;
        try
        {
            return !Directory.EnumerateFiles(assemblies, "*.dll").Any();
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Offline generation is safe only when assemblies already exist, or the exact
    /// UnityDependencies_{version}.zip for the resolved Unity version is present.
    /// </summary>
    public bool CanForceOfflineGeneration(string gamePath) =>
        CanForceOfflineGeneration(gamePath, resolvedVersion: null);

    /// <param name="resolvedVersion">
    /// Optional pre-resolved Unity version (raw or normalized) for tests / callers
    /// that already know the version. When null, resolves from the game path.
    /// </param>
    public bool CanForceOfflineGeneration(string gamePath, string? resolvedVersion)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        if (!NeedsFirstAssemblyGeneration(gamePath))
            return true;

        string? version;
        if (!string.IsNullOrWhiteSpace(resolvedVersion))
        {
            if (!UnityVersionNormalizer.TryNormalize(resolvedVersion, out version))
                return false;
        }
        else
        {
            try
            {
                if (!_resolver.TryResolve(gamePath, out version) || string.IsNullOrWhiteSpace(version))
                    return false;
            }
            catch
            {
                return false;
            }
        }

        var zipPath = Path.Combine(
            gamePath,
            "MelonLoader",
            "Dependencies",
            "Il2CppAssemblyGenerator",
            UnityVersionNormalizer.ExpectedZipFileName(version!));
        return File.Exists(zipPath);
    }

    public MelonLoaderOptimizeResult ApplyRecommendedSettings(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return new MelonLoaderOptimizeResult
            {
                Changed = false,
                Message = "游戏路径无效，跳过 MelonLoader 优化。",
                NeedsFirstAssemblyGeneration = false
            };
        }

        var needsGen = NeedsFirstAssemblyGeneration(gamePath);
        var forceOffline = CanForceOfflineGeneration(gamePath);
        var path = GetLoaderConfigPath(gamePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string text;
        if (File.Exists(path))
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        else
        {
            text = MinimalLoaderCfg(forceOffline);
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new MelonLoaderOptimizeResult
            {
                Changed = true,
                Message =
                    "已写入 MelonLoader 优化配置（退出防卡死" +
                    (forceOffline ? " + 离线程序集生成" : "") + "）。" +
                    (needsGen ? " 首次启动仍需生成 IL2CPP 程序集，可能需 1～2 分钟，请等待黑屏/控制台完成。" : ""),
                NeedsFirstAssemblyGeneration = needsGen
            };
        }

        var changedKeys = new List<string>();
        var updated = text;

        updated = SetBoolKey(updated, ForceQuitRegex, "force_quit", desired: true, out var quitChanged);
        if (quitChanged) changedKeys.Add("force_quit");

        updated = SetBoolKey(updated, ForceOfflineRegex, "force_offline_generation", desired: forceOffline, out var offlineChanged);
        if (offlineChanged) changedKeys.Add("force_offline_generation");

        // Older/minimal cfg may lack keys entirely — append under the right sections.
        if (!ForceQuitRegex.IsMatch(updated))
        {
            updated = EnsureSectionKey(updated, "[loader]", "force_quit = true");
            changedKeys.Add("force_quit");
        }

        if (!ForceOfflineRegex.IsMatch(updated))
        {
            var offlineLine = forceOffline
                ? "force_offline_generation = true"
                : "force_offline_generation = false";
            updated = EnsureSectionKey(updated, "[unityengine]", offlineLine);
            changedKeys.Add("force_offline_generation");
        }

        if (changedKeys.Count == 0)
        {
            return new MelonLoaderOptimizeResult
            {
                Changed = false,
                Message = needsGen
                    ? "MelonLoader 优化已启用。首次启动仍需生成 IL2CPP 程序集，可能需 1～2 分钟。"
                    : "MelonLoader 优化配置已是最新。",
                NeedsFirstAssemblyGeneration = needsGen
            };
        }

        File.WriteAllText(path, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new MelonLoaderOptimizeResult
        {
            Changed = true,
            Message =
                $"已优化 MelonLoader（{string.Join(", ", changedKeys)}）：减轻退出卡死" +
                (forceOffline ? "，跳过无效远程 API 探测。" : "。") +
                (needsGen ? " 首次启动仍需生成 IL2CPP 程序集，可能需 1～2 分钟，请等待完成后再操作。" : ""),
            NeedsFirstAssemblyGeneration = needsGen
        };
    }

    static string SetBoolKey(string text, Regex regex, string key, bool desired, out bool changed)
    {
        changed = false;
        var desiredText = desired ? "true" : "false";
        var match = regex.Match(text);
        if (!match.Success)
            return text;

        var current = match.Groups["value"].Value;
        if (string.Equals(current, desiredText, StringComparison.OrdinalIgnoreCase))
            return text;

        changed = true;
        var indent = match.Groups["indent"].Value;
        return regex.Replace(text, $"{indent}{key} = {desiredText}", count: 1);
    }

    static string EnsureSectionKey(string text, string sectionHeader, string keyLine)
    {
        var idx = text.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return text.TrimEnd() + Environment.NewLine + Environment.NewLine + sectionHeader + Environment.NewLine + keyLine + Environment.NewLine;

        var insertAt = idx + sectionHeader.Length;
        if (insertAt < text.Length && text[insertAt] is '\r' or '\n')
        {
            if (text[insertAt] == '\r' && insertAt + 1 < text.Length && text[insertAt + 1] == '\n')
                insertAt += 2;
            else
                insertAt += 1;
        }

        return text.Insert(insertAt, keyLine + Environment.NewLine);
    }

    static string MinimalLoaderCfg(bool forceOffline) =>
        $"""
        [loader]
        # Only use this if the game freezes when trying to quit. Equivalent to the '--quitfix' launch option
        force_quit = true

        [unityengine]
        # Forces the Il2Cpp Assembly Generator to run without contacting the remote API.
        force_offline_generation = {(forceOffline ? "true" : "false")}

        """;
}
