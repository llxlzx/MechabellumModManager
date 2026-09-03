using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

internal static class PreviewImageLoader
{
    static readonly HttpClient Http = ModCatalogService.CreateDefaultClient();

    /// <summary>
    /// Obsolete: sync remote loads block the UI thread. Use <see cref="TryLoadAsync"/>.
    /// Returns null for remote URLs; only loads local file:// URIs.
    /// </summary>
    [Obsolete("Use TryLoadAsync. Sync remote loads block the UI thread.")]
    public static BitmapImage? TryLoad(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(url, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            if (bmp.CanFreeze)
                bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<BitmapImage?> TryLoadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(url, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.EndInit();
                    if (bmp.CanFreeze)
                        bmp.Freeze();
                    return bmp;
                }, ct).ConfigureAwait(false);
            }

            var bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                if (bmp.CanFreeze)
                    bmp.Freeze();
                return bmp;
            }, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
