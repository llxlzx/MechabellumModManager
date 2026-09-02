using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MechabellumModManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryApplyBrandAssets();
    }

    void TryApplyBrandAssets()
    {
        try
        {
            var brandPath = Path.Combine(AppContext.BaseDirectory, "Assets", "brand.png");
            if (!File.Exists(brandPath))
                return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(brandPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            BrandImage.Source = bitmap;
            Icon = bitmap;
        }
        catch
        {
            // Brand art is optional; keep UI usable without it.
        }
    }
}
