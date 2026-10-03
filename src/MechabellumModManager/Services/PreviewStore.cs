using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MechabellumModManager.Services;

/// <summary>
/// Local preview bytes. The catalog hash decides identity. The relative path only selects
/// the bundled file and the URL the caller fetches on a miss.
/// </summary>
public static class PreviewStore
{
    public enum LocalKind { Bundle, Cache }

    public readonly record struct LocalHit(LocalKind Kind, byte[] Bytes);

    const string IndexName = "by-path.txt";

    public static string? NormalizeHash(string? sha256)
    {
        var value = (sha256 ?? "").Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            return null;
        return value.ToLowerInvariant();
    }

    public static LocalHit? TryLocal(string bundleRoot, string? cacheRoot, string relativePath, string? sha256)
    {
        if (!TryNormalize(relativePath, out var relative))
            return null;

        var hash = NormalizeHash(sha256);
        var bundled = ReadUnder(bundleRoot, relative);

        if (hash is null)
        {
            if (bundled is not null)
                return new LocalHit(LocalKind.Bundle, bundled);
            if (!string.IsNullOrWhiteSpace(cacheRoot) &&
                TryIndexed(cacheRoot, relative, out var indexed))
                return new LocalHit(LocalKind.Cache, indexed);
            return null;
        }

        if (bundled is not null && HashOf(bundled) == hash)
            return new LocalHit(LocalKind.Bundle, bundled);

        if (!string.IsNullOrWhiteSpace(cacheRoot) && TryReadVerified(cacheRoot, hash, out var cached))
            return new LocalHit(LocalKind.Cache, cached);

        return null;
    }

    public static LocalHit? TryStaleBundle(string bundleRoot, string relativePath)
    {
        if (!TryNormalize(relativePath, out var relative))
            return null;
        var bundled = ReadUnder(bundleRoot, relative);
        return bundled is null ? null : new LocalHit(LocalKind.Bundle, bundled);
    }

    public static void Save(string cacheRoot, string relativePath, string sha256, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        ArgumentNullException.ThrowIfNull(bytes);
        if (!TryNormalize(relativePath, out var relative))
            throw new ArgumentException("Catalog path must stay relative.", nameof(relativePath));
        var hash = NormalizeHash(sha256) ?? throw new ArgumentException("A 64-digit sha256 is required.", nameof(sha256));
        if (HashOf(bytes) != hash)
            throw new InvalidOperationException("Refusing to cache bytes that do not match the declared sha256.");

        Directory.CreateDirectory(cacheRoot);
        var dest = Path.Combine(cacheRoot, hash);
        var tmp = dest + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, dest, overwrite: true);
        WriteIndex(cacheRoot, relative, hash);
    }

    static bool TryNormalize(string relativePath, out string relative)
    {
        try
        {
            relative = ModCatalogService.NormalizeCatalogRelativePath(relativePath);
            return true;
        }
        catch (ArgumentException)
        {
            relative = "";
            return false;
        }
    }

    static byte[]? ReadUnder(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!File.Exists(full))
            return null;
        return File.ReadAllBytes(full);
    }

    static bool TryReadVerified(string cacheRoot, string hash, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var path = Path.Combine(cacheRoot, hash);
        if (!File.Exists(path))
            return false;
        var read = File.ReadAllBytes(path);
        if (HashOf(read) != hash)
            return false;
        bytes = read;
        return true;
    }

    static bool TryIndexed(string cacheRoot, string relative, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var index = Path.Combine(cacheRoot, IndexName);
        if (!File.Exists(index))
            return false;
        foreach (var line in File.ReadAllLines(index))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0)
                continue;
            if (!string.Equals(line[..tab], relative, StringComparison.Ordinal))
                continue;
            var hash = NormalizeHash(line[(tab + 1)..]);
            if (hash is null)
                return false;
            return TryReadVerified(cacheRoot, hash, out bytes);
        }

        return false;
    }

    static void WriteIndex(string cacheRoot, string relative, string hash)
    {
        var index = Path.Combine(cacheRoot, IndexName);
        var lines = File.Exists(index)
            ? File.ReadAllLines(index).Where(line =>
            {
                var tab = line.IndexOf('\t');
                return tab <= 0 || !string.Equals(line[..tab], relative, StringComparison.Ordinal);
            }).ToList()
            : new List<string>();
        lines.Add(relative + "\t" + hash);
        var tmp = index + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllLines(tmp, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tmp, index, overwrite: true);
    }

    static string HashOf(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
