using System.IO;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed class UnityDependenciesSeedResult
{
    public bool Success { get; init; }
    public bool Copied { get; init; }
    public string? Version { get; init; }
    public string Message { get; init; } = "";
}

public sealed class UnityDependenciesSeeder
{
    static readonly Regex ZipVersionRx = new(
        @"^UnityDependencies_(.+)\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    readonly UnityVersionResolver _resolver;

    public UnityDependenciesSeeder(UnityVersionResolver? resolver = null)
    {
        _resolver = resolver ?? new UnityVersionResolver();
    }

    public UnityDependenciesSeedResult Seed(string gamePath, string? redistDir = null, string? versionOverride = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            {
                return new UnityDependenciesSeedResult
                {
                    Success = false,
                    Message = "游戏目录无效或不存在。"
                };
            }

            if (!TryResolveTargetVersion(gamePath, redistDir, versionOverride, out var version, out var usedFallback, out var resolveMessage))
            {
                return new UnityDependenciesSeedResult
                {
                    Success = false,
                    Message = resolveMessage
                };
            }

            var fallbackNote = usedFallback ? "（Unity 版本来自红配唯一 zip 回退）" : "";

            var destDir = Path.Combine(gamePath, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            var destZip = Path.Combine(destDir, UnityVersionNormalizer.ExpectedZipFileName(version!));
            var unityDepsDir = ResolveUnityDepsRedistDir(redistDir);
            var sourceZip = unityDepsDir is null
                ? null
                : FindMatchingZipInRedist(unityDepsDir, version!);

            var messages = new List<string>();
            var copied = false;

            if (File.Exists(destZip))
            {
                if (sourceZip is not null && ShouldRefresh(sourceZip, destZip))
                {
                    File.Copy(sourceZip, destZip, overwrite: true);
                    copied = true;
                    messages.Add($"已更新 UnityDependencies_{version}.zip。{fallbackNote}");
                }
                else
                {
                    messages.Add($"UnityDependencies_{version}.zip 已存在，跳过复制。{fallbackNote}");
                }
            }
            else
            {
                if (sourceZip is null)
                {
                    return new UnityDependenciesSeedResult
                    {
                        Success = false,
                        Version = version,
                        Message = unityDepsDir is null
                            ? "未找到 unity-deps 红配目录。"
                            : $"红配中缺少 UnityDependencies_{version}.zip。"
                    };
                }

                Directory.CreateDirectory(destDir);
                File.Copy(sourceZip, destZip, overwrite: false);
                copied = true;
                messages.Add($"已复制 UnityDependencies_{version}.zip。{fallbackNote}");
            }

            var cpp2Il = SeedCpp2Il(gamePath, redistDir);
            if (!cpp2Il.Success)
            {
                return new UnityDependenciesSeedResult
                {
                    Success = false,
                    Copied = copied,
                    Version = version,
                    Message = string.Join(" ", messages) + " " + cpp2Il.Message
                };
            }

            if (cpp2Il.Copied)
                copied = true;
            if (!string.IsNullOrWhiteSpace(cpp2Il.Message))
                messages.Add(cpp2Il.Message);

            return new UnityDependenciesSeedResult
            {
                Success = true,
                Copied = copied,
                Version = version,
                Message = string.Join(" ", messages)
            };
        }
        catch (Exception ex)
        {
            return new UnityDependenciesSeedResult
            {
                Success = false,
                Message = "播种 UnityDependencies 失败：" + ex.Message
            };
        }
    }

    /// <summary>
    /// Melon 0.7.3 expects Cpp2IL.exe under Il2CppAssemblyGenerator\Cpp2IL\ when offline.
    /// </summary>
    public static UnityDependenciesSeedResult SeedCpp2Il(string gamePath, string? redistDir = null)
    {
        try
        {
            var cpp2IlRedist = ResolveCpp2IlRedistDir(redistDir);
            if (cpp2IlRedist is null)
            {
                return new UnityDependenciesSeedResult
                {
                    Success = false,
                    Message = "未找到 cpp2il 红配目录。"
                };
            }

            var srcExe = Path.Combine(cpp2IlRedist, "Cpp2IL.exe");
            var srcPlugin = Path.Combine(cpp2IlRedist, "Cpp2IL.Plugin.StrippedCodeRegSupport.dll");
            if (!File.Exists(srcExe) || !File.Exists(srcPlugin))
            {
                return new UnityDependenciesSeedResult
                {
                    Success = false,
                    Message = "红配中缺少 Cpp2IL.exe 或 Cpp2IL.Plugin.StrippedCodeRegSupport.dll。"
                };
            }

            var destExe = Path.Combine(
                gamePath,
                "MelonLoader",
                "Dependencies",
                "Il2CppAssemblyGenerator",
                "Cpp2IL",
                "Cpp2IL.exe");
            var destPlugin = Path.Combine(
                gamePath,
                "MelonLoader",
                "Dependencies",
                "Il2CppAssemblyGenerator",
                "Cpp2IL",
                "Plugins",
                "Cpp2IL.Plugin.StrippedCodeRegSupport.dll");

            Directory.CreateDirectory(Path.GetDirectoryName(destExe)!);
            Directory.CreateDirectory(Path.GetDirectoryName(destPlugin)!);

            var copied = false;
            if (!File.Exists(destExe) || ShouldRefresh(srcExe, destExe))
            {
                File.Copy(srcExe, destExe, overwrite: true);
                copied = true;
            }

            if (!File.Exists(destPlugin) || ShouldRefresh(srcPlugin, destPlugin))
            {
                File.Copy(srcPlugin, destPlugin, overwrite: true);
                copied = true;
            }

            return new UnityDependenciesSeedResult
            {
                Success = File.Exists(destExe) && File.Exists(destPlugin),
                Copied = copied,
                Message = copied ? "已播种 Cpp2IL.exe 与 StrippedCodeRegSupport 插件。" : "Cpp2IL 依赖已存在，跳过复制。"
            };
        }
        catch (Exception ex)
        {
            return new UnityDependenciesSeedResult
            {
                Success = false,
                Message = "播种 Cpp2IL 失败：" + ex.Message
            };
        }
    }

    public static string? ResolveCpp2IlRedistDir(string? preferredRedistDir = null)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredRedistDir))
            candidates.Add(Path.Combine(preferredRedistDir, "cpp2il"));

        try
        {
            var baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "installer-redist", "cpp2il"));
            candidates.Add(Path.Combine(baseDir, "redist", "cpp2il"));
        }
        catch
        {
            // ignore
        }

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, "installer", "redist", "cpp2il"));
            }
        }
        catch
        {
            // ignore
        }

        return candidates.FirstOrDefault(d =>
            Directory.Exists(d)
            && File.Exists(Path.Combine(d, "Cpp2IL.exe"))
            && File.Exists(Path.Combine(d, "Cpp2IL.Plugin.StrippedCodeRegSupport.dll")));
    }

    public static string? ResolveUnityDepsRedistDir(string? preferredRedistDir = null)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredRedistDir))
            candidates.Add(Path.Combine(preferredRedistDir, "unity-deps"));

        try
        {
            var baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "installer-redist", "unity-deps"));
            candidates.Add(Path.Combine(baseDir, "redist", "unity-deps"));
        }
        catch
        {
            // ignore
        }

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, "installer", "redist", "unity-deps"));
            }
        }
        catch
        {
            // ignore
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? FindMatchingZipInRedist(string unityDepsDir, string majorMinorPatch)
    {
        if (string.IsNullOrWhiteSpace(unityDepsDir) || !Directory.Exists(unityDepsDir))
            return null;
        if (string.IsNullOrWhiteSpace(majorMinorPatch))
            return null;

        var path = Path.Combine(unityDepsDir, UnityVersionNormalizer.ExpectedZipFileName(majorMinorPatch));
        return File.Exists(path) ? path : null;
    }

    bool TryResolveTargetVersion(
        string gamePath,
        string? redistDir,
        string? versionOverride,
        out string? version,
        out bool usedFallback,
        out string message)
    {
        version = null;
        usedFallback = false;
        message = "";

        if (!string.IsNullOrWhiteSpace(versionOverride))
        {
            if (UnityVersionNormalizer.TryNormalize(versionOverride, out version))
                return true;

            message = "versionOverride 无效。";
            return false;
        }

        try
        {
            if (_resolver.TryResolve(gamePath, out version) && !string.IsNullOrWhiteSpace(version))
                return true;
        }
        catch
        {
            // locked / unreadable globalgamemanagers — treat as resolve failure
        }

        version = null;
        if (TryFallbackVersionFromSingleRedistZip(redistDir, out version))
        {
            usedFallback = true;
            return true;
        }

        message = "无法解析游戏 Unity 版本，且红配中无唯一 UnityDependencies_*.zip 可回退。";
        return false;
    }

    static bool TryFallbackVersionFromSingleRedistZip(string? redistDir, out string? version)
    {
        version = null;
        var unityDepsDir = ResolveUnityDepsRedistDir(redistDir);
        if (unityDepsDir is null)
            return false;

        string[] zips;
        try
        {
            zips = Directory.GetFiles(unityDepsDir, "UnityDependencies_*.zip");
        }
        catch
        {
            return false;
        }

        if (zips.Length != 1)
            return false;

        var fileName = Path.GetFileName(zips[0]);
        var m = ZipVersionRx.Match(fileName);
        if (!m.Success)
            return false;

        return UnityVersionNormalizer.TryNormalize(m.Groups[1].Value, out version);
    }

    static bool ShouldRefresh(string sourceZip, string destZip)
    {
        try
        {
            var srcInfo = new FileInfo(sourceZip);
            var destInfo = new FileInfo(destZip);
            if (srcInfo.Length != destInfo.Length)
                return true;
            if (srcInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc)
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }
}
