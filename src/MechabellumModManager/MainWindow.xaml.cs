using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MechabellumModManager.Dialogs;

namespace MechabellumModManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryApplyBrandAssets();
    }

    void ModPreviewImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image { Source: not null } image)
            return;
        e.Handled = true;
        var dialog = new PreviewImageDialog(image.Source)
        {
            Owner = this,
            Width = SystemParameters.WorkArea.Width * 0.85,
            Height = SystemParameters.WorkArea.Height * 0.85
        };
        dialog.ShowDialog();
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
