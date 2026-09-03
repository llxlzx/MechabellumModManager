using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class CatalogModItemViewModel : ObservableObject
{
    public CatalogMod Mod { get; }

    public CatalogModItemViewModel(CatalogMod mod, bool isInLibrary)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        _isInLibrary = isInLibrary;
        PreviewUrl = ModCatalogService.PreviewUrl(mod);
    }

    public string Id => Mod.Id;
    public string Name => Mod.Name;
    public string? Author => Mod.Author;
    public string? Version => Mod.Version;
    public string? UpdatedAt => Mod.UpdatedAt;
    public string? Summary => Mod.Summary;
    public string File => Mod.File;
    public string? Type => Mod.Type;
    public string? PreviewUrl { get; }

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isInLibrary;

    public string StatusText => IsInLibrary ? "已在库" : "未安装";

    public void LoadPreviewImage()
    {
        PreviewImage = PreviewImageLoader.TryLoad(PreviewUrl);
    }
}
