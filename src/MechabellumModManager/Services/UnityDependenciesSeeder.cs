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

            if (File.Exists(destZip))
            {
                if (sourceZip is not null && ShouldRefresh(sourceZip, destZip))
                {
                    File.Copy(sourceZip, destZip, overwrite: true);
                    return new UnityDependenciesSeedResult
                    {
                        Success = true,
                        Copied = true,
                        Version = version,
                        Message = $"已更新 UnityDependencies_{version}.zip。{fallbackNote}"
                    };
                }

                return new UnityDependenciesSeedResult
                {
                    Success = true,
                    Copied = false,
                    Version = version,
                    Message = $"UnityDependencies_{version}.zip 已存在，跳过复制。{fallbackNote}"
                };
            }

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
            return new UnityDependenciesSeedResult
            {
                Success = true,
                Copied = true,
                Version = version,
                Message = $"已复制 UnityDependencies_{version}.zip。{fallbackNote}"
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
