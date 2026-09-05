using System.IO;
using System.IO.Compression;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Ensures MelonLoader exists on both Official/Beta dual-folder stores.
/// Prefer installing from a local MelonLoader.x64.zip; otherwise copy the framework
/// from the sibling store (excluding build-specific Il2CppAssemblies).
/// </summary>
public sealed class MelonLoaderDualStoreSync
{
    static readonly string[] ProxyDlls = ["version.dll", "winhttp.dll"];
    static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Il2CppAssemblies",
        "Logs",
        "CrashReports"
    };

    static readonly HashSet<string> SkipFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Latest.log",
        // Melon uses this hash to skip Il2Cpp generation. Copying it without Il2CppAssemblies
        // leaves the destination permanently "up to date" with no assemblies.
        "Config.cfg"
    };

    readonly GameDetector _detector;
    readonly MelonLoaderConfigOptimizer _optimizer;

    public MelonLoaderDualStoreSync(
        GameDetector? detector = null,
        MelonLoaderConfigOptimizer? optimizer = null)
    {
        _detector = detector ?? new GameDetector();
        _optimizer = optimizer ?? new MelonLoaderConfigOptimizer();
    }

    public MelonLoaderInstallResult EnsureOnStore(string gamePath, string? siblingGamePath = null, string? localZipPath = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !SteamGameLocator.LooksLikeGameRoot(gamePath))
            return Fail("目标游戏目录无效。");

        var status = _detector.Detect(gamePath);
        if (status.Kind is GameStatusKind.Ready or GameStatusKind.LoaderPresentAssembliesMissing)
        {
            var zipReady = ResolveLocalZip(localZipPath);
            var seedReady = SeedDependencies(gamePath, DeriveRedistDirFromMelonZip(zipReady));
            // Re-apply after seed so offline can flip true when zip just landed (incl. seed.Version fallback).
            var optimizeReady = seedReady.Success && seedReady.Version != null
                ? _optimizer.ApplyRecommendedSettings(gamePath, seedReady.Version)
                : _optimizer.ApplyRecommendedSettings(gamePath);
            return new MelonLoaderInstallResult
            {
                Success = true,
                Message = "MelonLoader 已就绪，无需补齐。"
                          + FormatSeedMessage(seedReady)
                          + (optimizeReady.Changed ? "\n" + optimizeReady.Message : "")
            };
        }

        var zip = ResolveLocalZip(localZipPath);
        if (!string.IsNullOrWhiteSpace(zip))
        {
            var fromZip = InstallFromZip(gamePath, zip);
            if (fromZip.Success)
                return fromZip;
            // Fall through to sibling copy if zip install fails.
        }

        if (!string.IsNullOrWhiteSpace(siblingGamePath)
            && SteamGameLocator.LooksLikeGameRoot(siblingGamePath)
            && _detector.Detect(siblingGamePath).Kind is GameStatusKind.Ready
                or GameStatusKind.LoaderPresentAssembliesMissing)
        {
            return CopyFrameworkFromSibling(siblingGamePath, gamePath);
        }

        return Fail(
            "另一服目录缺少 MelonLoader，且未找到本地 MelonLoader.x64.zip。" +
            "请重新运行管理器安装包并勾选 MelonLoader，或先在有 Loader 的那一服装好后再切服。");
    }

    public MelonLoaderInstallResult EnsureOnBothStores(string officialStore, string betaStore, string? localZipPath = null)
    {
        var messages = new List<string>();
        var ok = true;

        foreach (var (target, sibling) in new[]
                 {
                     (officialStore, betaStore),
                     (betaStore, officialStore)
                 })
        {
            if (!SteamGameLocator.LooksLikeGameRoot(target))
                continue;

            var result = EnsureOnStore(target, sibling, localZipPath);
            if (!string.IsNullOrWhiteSpace(result.Message))
                messages.Add($"{Path.GetFileName(target)}: {result.Message}");
            if (!result.Success)
                ok = false;
        }

        return new MelonLoaderInstallResult
        {
            Success = ok,
            Message = messages.Count == 0 ? "无需补齐。" : string.Join("\n", messages)
        };
    }

    public static string? ResolveLocalZip(string? preferred = null)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && File.Exists(preferred))
            return preferred;

        var candidates = new List<string>();
        try
        {
            var baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "installer-redist", "melonloader", "MelonLoader.x64.zip"));
            candidates.Add(Path.Combine(baseDir, "redist", "melonloader", "MelonLoader.x64.zip"));
            candidates.Add(Path.Combine(baseDir, "MelonLoader.x64.zip"));
        }
        catch
        {
            // ignore
        }

        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MechabellumModManager",
                "redist",
                "MelonLoader.x64.zip");
            candidates.Add(appData);
        }
        catch
        {
            // ignore
        }

        // Dev / repo layout when running from publish\
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, "installer", "redist", "melonloader", "MelonLoader.x64.zip"));
            }
        }
        catch
        {
            // ignore
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public MelonLoaderInstallResult InstallFromZip(string gamePath, string zipPath)
    {
        if (!File.Exists(zipPath))
            return Fail("MelonLoader 压缩包不存在。");

        var staging = Path.Combine(Path.GetTempPath(), "mmm-ml-sync-" + Guid.NewGuid().ToString("N"));
        var extract = Path.Combine(staging, "extract");
        var written = new List<string>();
        try
        {
            Directory.CreateDirectory(extract);
            ZipFile.ExtractToDirectory(zipPath, extract, overwriteFiles: true);
            MelonLoaderInstaller.CopyExtractedPayloadForSync(extract, gamePath, written);

            var status = _detector.Detect(gamePath);
            if (status.Kind is not (GameStatusKind.Ready or GameStatusKind.LoaderPresentAssembliesMissing))
                return Fail($"已从压缩包写入，但检测仍为：{status.Message}");

            // Seed matching UnityDependencies zip BEFORE offline optimize so CanForceOffline can become true.
            var seed = SeedDependencies(gamePath, DeriveRedistDirFromMelonZip(zipPath));
            var optimize = seed.Success && seed.Version != null
                ? _optimizer.ApplyRecommendedSettings(gamePath, seed.Version)
                : _optimizer.ApplyRecommendedSettings(gamePath);
            var assembliesNote = status.Kind == GameStatusKind.LoaderPresentAssembliesMissing
                ? "\n首次启动该服时 MelonLoader 会重新生成程序集，可能需要一两分钟。"
                : "";
            return new MelonLoaderInstallResult
            {
                Success = true,
                Message = "已从本地 MelonLoader 压缩包安装到该服目录。"
                          + FormatSeedMessage(seed)
                          + (optimize.Changed ? "\n" + optimize.Message : "")
                          + assembliesNote
            };
        }
        catch (Exception ex)
        {
            return Fail($"从压缩包安装失败：{ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Seeds the exact <c>UnityDependencies_{version}.zip</c> into the game Melon AG folder.
    /// </summary>
    public UnityDependenciesSeedResult SeedDependencies(string gamePath, string? redistDir = null)
        => new UnityDependenciesSeeder().Seed(gamePath, redistDir);

    /// <summary>
    /// When Melon zip lives at <c>.../melonloader/MelonLoader.x64.zip</c>, redist root is the parent of <c>melonloader</c>.
    /// </summary>
    public static string? DeriveRedistDirFromMelonZip(string? zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            return null;
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(zipPath));
            if (string.IsNullOrWhiteSpace(dir))
                return null;
            if (string.Equals(Path.GetFileName(dir), "melonloader", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(dir);
            return null;
        }
        catch
        {
            return null;
        }
    }

    MelonLoaderInstallResult CopyFrameworkFromSibling(string sourceGamePath, string destGamePath)
    {
        try
        {
            foreach (var dll in ProxyDlls)
            {
                var src = Path.Combine(sourceGamePath, dll);
                if (!File.Exists(src))
                    continue;
                File.Copy(src, Path.Combine(destGamePath, dll), overwrite: true);
            }

            var srcMelon = Path.Combine(sourceGamePath, "MelonLoader");
            var destMelon = Path.Combine(destGamePath, "MelonLoader");
            if (!Directory.Exists(srcMelon))
                return Fail("源目录没有 MelonLoader 文件夹。");

            CopyDirectoryFiltered(srcMelon, destMelon);
            MelonLoaderAssemblyGenerator.InvalidateStaleGenerationState(destGamePath);

            var status = _detector.Detect(destGamePath);
            if (status.Kind is not (GameStatusKind.Ready or GameStatusKind.LoaderPresentAssembliesMissing))
                return Fail($"已从另一服复制 Loader，但检测仍为：{status.Message}");

            var zip = ResolveLocalZip();
            var seed = SeedDependencies(destGamePath, DeriveRedistDirFromMelonZip(zip));
            var optimize = seed.Success && seed.Version != null
                ? _optimizer.ApplyRecommendedSettings(destGamePath, seed.Version)
                : _optimizer.ApplyRecommendedSettings(destGamePath);
            return new MelonLoaderInstallResult
            {
                Success = true,
                Message = "已从另一服复制 MelonLoader（未复制 Il2CppAssemblies / Latest.log）。"
                          + FormatSeedMessage(seed)
                          + (optimize.Changed ? "\n" + optimize.Message : "")
                          + "\n将尝试自动生成程序集，或首次启动时由 MelonLoader 生成。"
            };
        }
        catch (Exception ex)
        {
            return Fail($"复制 MelonLoader 失败：{ex.Message}");
        }
    }

    static string FormatSeedMessage(UnityDependenciesSeedResult seed)
    {
        if (string.IsNullOrWhiteSpace(seed.Message))
            return "";
        if (seed.Success)
            return "\n" + seed.Message;
        return "\n警告：UnityDependencies 播种失败 — " + seed.Message;
    }

    static void CopyDirectoryFiltered(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (SkipFileNames.Contains(name))
                continue;
            File.Copy(file, Path.Combine(destDir, name), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (SkipDirNames.Contains(name))
                continue;
            CopyDirectoryFiltered(dir, Path.Combine(destDir, name));
        }
    }

    static MelonLoaderInstallResult Fail(string message) =>
        new() { Success = false, Message = message };
}


