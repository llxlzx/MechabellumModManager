using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class CatalogModItemViewModel : ObservableObject
{
    CancellationTokenSource? _previewCts;
    string? _previewLoadUrl;

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

    public ModCategory EffectiveCategory =>
        ModTaxonomy.ParseCategoryOrUncategorized(Mod.Category);

    public IReadOnlyList<string> EffectiveTags =>
        ModTaxonomy.NormalizeTags(Mod.Tags);

    public string EffectiveCategoryDisplay => EffectiveCategory.ToString();

    public string EffectiveTagsText => EffectiveTags.Count == 0 ? "" : string.Join(", ", EffectiveTags);

    public string? PreviewUrl { get; }

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isInLibrary;

    public string StatusText => IsInLibrary ? "已在库" : "未安装";

    public async Task LoadPreviewImageAsync()
    {
        var url = PreviewUrl;
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewLoadUrl = url;

        if (string.IsNullOrWhiteSpace(url))
        {
            PreviewImage = null;
            return;
        }

        var bmp = await PreviewImageLoader.TryLoadAsync(url, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
            return;
        if (!string.Equals(_previewLoadUrl, url, StringComparison.Ordinal))
            return;
        PreviewImage = bmp;
    }
}
