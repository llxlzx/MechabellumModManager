using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

public static class CatalogCache
{
    public const string FileName = "catalog-cache.json";

    public static string GetPath(string dataRoot) =>
        Path.Combine(dataRoot, FileName);

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
}
