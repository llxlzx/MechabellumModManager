using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

public static class CatalogCache
{
    public const string FileName = "catalog-cache.json";
    public const string EtagFileName = "catalog-cache.etag";

    public static string GetPath(string dataRoot) =>
        Path.Combine(dataRoot, FileName);

    public static string GetEtagPath(string dataRoot) =>
        Path.Combine(dataRoot, EtagFileName);

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
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
            return;
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, etag.Trim(), Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* best effort */ }
    }

    public static bool TryReadEtag(string dataRoot, out string? etag)
    {
        etag = null;
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;

        var path = GetEtagPath(dataRoot);
        if (!File.Exists(path))
            return false;

        try
        {
            var value = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;
            etag = value;
            return true;
        }
        catch
        {
            etag = null;
            return false;
        }
    }
}
