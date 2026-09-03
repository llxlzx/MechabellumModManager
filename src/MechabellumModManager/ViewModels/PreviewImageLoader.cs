using System.Windows.Media.Imaging;

namespace MechabellumModManager.ViewModels;

internal static class PreviewImageLoader
{
    public static BitmapImage? TryLoad(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
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
}
