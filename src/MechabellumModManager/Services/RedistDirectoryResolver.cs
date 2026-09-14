using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Picks a writable installer-redist root. Program Files installs often cannot write
/// under AppContext.BaseDirectory; fall back to LocalAppData so offline Melon deps can seed.
/// </summary>
public static class RedistDirectoryResolver
{
    public const string FolderName = "installer-redist";
    public const string AppFolderName = "MechabellumModManager";

    public static string Resolve(
        string? preferredBaseDir = null,
        string? localAppDataRoot = null,
        Func<string, bool>? canWrite = null)
        => Resolve(preferredBaseDir, localAppDataRoot, canWrite, out _);

    public static string Resolve(out bool usedFallback)
        => Resolve(null, null, null, out usedFallback);

    public static string Resolve(
        string? preferredBaseDir,
        string? localAppDataRoot,
        Func<string, bool>? canWrite,
        out bool usedFallback)
    {
        canWrite ??= ProbeWritable;
        var baseDir = string.IsNullOrWhiteSpace(preferredBaseDir)
            ? AppContext.BaseDirectory
            : preferredBaseDir.Trim();
        var primary = Path.GetFullPath(Path.Combine(baseDir, FolderName));

        if (canWrite(primary))
        {
            Directory.CreateDirectory(primary);
            usedFallback = false;
            return primary;
        }

        var localRoot = string.IsNullOrWhiteSpace(localAppDataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localAppDataRoot.Trim();
        var fallback = Path.GetFullPath(Path.Combine(localRoot, AppFolderName, FolderName));
        Directory.CreateDirectory(fallback);
        usedFallback = true;
        return fallback;
    }

    public static bool ProbeWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
