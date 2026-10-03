using System.Net.Http;
using System.Security.Cryptography;
using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Resolves preview bytes from the install or the local cache, and downloads only on a miss.
/// </summary>
public static class PreviewAcquisition
{
    public static async Task<byte[]?> AcquireAsync(
        HttpClient http,
        string bundleRoot,
        string? cacheRoot,
        string? relativePath,
        string? sha256,
        IReadOnlyList<string> urls,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var local = PreviewStore.TryLocal(bundleRoot, cacheRoot, relativePath, sha256);
            if (local is not null)
                return local.Value.Bytes;
        }

        var expected = PreviewStore.NormalizeHash(sha256);
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
                continue;
            if (ct.IsCancellationRequested)
                break;

            try
            {
                using var response = await http
                    .GetAsync(url, HttpCompletionOption.ResponseContentRead, ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    continue;

                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (expected is not null && !string.Equals(actual, expected, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrWhiteSpace(cacheRoot) && !string.IsNullOrWhiteSpace(relativePath))
                {
                    try
                    {
                        PreviewStore.Save(cacheRoot, relativePath, actual, bytes);
                    }
                    catch (ArgumentException)
                    {
                        // An illegal catalog path must not stop us returning bytes we already verified.
                    }
                }

                return bytes;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Try the next candidate. A bad mirror must not hide the GitHub copy.
            }
        }

        if (!string.IsNullOrWhiteSpace(relativePath))
            return PreviewStore.TryStaleBundle(bundleRoot, relativePath)?.Bytes;
        return null;
    }
}
