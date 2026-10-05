using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

public static class CatalogCache
{
    public const string FileName = "catalog-cache.json";
    public const string EtagFileName = "catalog-cache.etag";
    public const string SourceFileName = "catalog-cache.source";
    public const string ManagerVersionFileName = "catalog-manager-version.txt";

    public static string GetPath(string dataRoot) =>
        Path.Combine(dataRoot, FileName);

    public static string GetEtagPath(string dataRoot) =>
        Path.Combine(dataRoot, EtagFileName);

    public static string GetSourcePath(string dataRoot) =>
        Path.Combine(dataRoot, SourceFileName);

    public static string GetManagerVersionPath(string dataRoot) =>
        Path.Combine(dataRoot, ManagerVersionFileName);

    /// <summary>
    /// Deletes the catalog cache when the running manager version differs from the stamp.
    /// The stamp is written after the delete. Does not touch update-check.stamp or notice-cache.json.
    /// </summary>
    public static bool DropIfManagerVersionChanged(string dataRoot, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;

        var stampPath = GetManagerVersionPath(dataRoot);
        string? stored = null;
        if (File.Exists(stampPath))
        {
            try { stored = File.ReadAllText(stampPath, Encoding.UTF8).Trim(); }
            catch { stored = null; }
        }

        if (string.Equals(stored, currentVersion, StringComparison.Ordinal))
            return false;

        TryDelete(GetPath(dataRoot));
        TryDelete(GetEtagPath(dataRoot));
        TryDelete(GetSourcePath(dataRoot));

        try
        {
            Directory.CreateDirectory(dataRoot);
            var tmp = stampPath + ".tmp";
            File.WriteAllText(tmp, currentVersion ?? "", Encoding.UTF8);
            File.Copy(tmp, stampPath, overwrite: true);
            try { File.Delete(tmp); } catch { /* best effort */ }
        }
        catch
        {
            // The next launch deletes again while the stamp is missing or stale.
        }

        return true;
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }

    /// <summary>True when the cached catalog was written less than <paramref name="maxAge"/> ago.</summary>
    public static bool IsYoungerThan(string dataRoot, TimeSpan maxAge)
    {
        if (string.IsNullOrWhiteSpace(dataRoot) || maxAge <= TimeSpan.Zero)
            return false;
        var path = GetPath(dataRoot);
        if (!File.Exists(path))
            return false;
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        return age >= TimeSpan.Zero && age < maxAge;
    }

    public static void Touch(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            return;
        var path = GetPath(dataRoot);
        if (File.Exists(path))
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }

    public static void Write(string dataRoot, string json)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("Data root is required.", nameof(dataRoot));
        ArgumentNullException.ThrowIfNull(json);

        Directory.CreateDirectory(dataRoot);
        var path = GetPath(dataRoot);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* best effort */ }
    }

    public static bool TryRead(string dataRoot, out string json)
    {
        json = "";
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;

        var path = GetPath(dataRoot);
        if (!File.Exists(path))
            return false;

        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            json = "";
            return false;
        }
    }

    public static void WriteEtag(string dataRoot, string? etag)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("Data root is required.", nameof(dataRoot));
        Directory.CreateDirectory(dataRoot);
        var path = GetEtagPath(dataRoot);
        if (string.IsNullOrWhiteSpace(etag))
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ok */ }
            return;
        }
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, etag.Trim(), Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* ok */ }
    }

    public static bool TryReadEtag(string dataRoot, out string etag)
    {
        etag = "";
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;
        var path = GetEtagPath(dataRoot);
        if (!File.Exists(path))
            return false;
        try
        {
            etag = File.ReadAllText(path, Encoding.UTF8).Trim();
            return !string.IsNullOrWhiteSpace(etag);
        }
        catch
        {
            etag = "";
            return false;
        }
    }

    public static void WriteSource(string dataRoot, string? source)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("Data root is required.", nameof(dataRoot));
        Directory.CreateDirectory(dataRoot);
        var path = GetSourcePath(dataRoot);
        if (string.IsNullOrWhiteSpace(source))
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ok */ }
            return;
        }
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, source.Trim(), Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* ok */ }
    }

    public static bool TryReadSource(string dataRoot, out string source)
    {
        source = "";
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;
        var path = GetSourcePath(dataRoot);
        if (!File.Exists(path))
            return false;
        try
        {
            source = File.ReadAllText(path, Encoding.UTF8).Trim();
            return !string.IsNullOrWhiteSpace(source);
        }
        catch
        {
            source = "";
            return false;
        }
    }
}
