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

    public CatalogModItemViewModel(CatalogMod mod, CatalogEntryState state, string? mirrorBaseUrl = null)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        _state = state;
        PreviewCandidateUrls = ModCatalogService.GetPreviewCandidateUrls(mod, mirrorBaseUrl);
        PreviewUrl = PreviewCandidateUrls.Count == 0 ? null : PreviewCandidateUrls[0];
    }

    public string Id => Mod.Id;
    public string Name => CatalogLocaleResolver.ResolveName(Mod);
    public string? Author => Mod.Author;
    public string? Version => Mod.Version;
    public string? UpdatedAt => Mod.UpdatedAt;
    public string? Summary => CatalogLocaleResolver.ResolveSummary(Mod);
    public string File => Mod.File;
    public string? Type => Mod.Type;

    public ModCategory EffectiveCategory =>
        ModTaxonomy.ParseCategoryOrUncategorized(Mod.Category);

    public IReadOnlyList<string> EffectiveTags =>
        ModTaxonomy.NormalizeTags(Mod.Tags);

    public string EffectiveCategoryDisplay => LocalizationService.T(EffectiveCategory switch
    {
        ModCategory.OverlayUI => "CategoryOverlayUI",
        ModCategory.QoL => "CategoryQoL",
        ModCategory.Camera => "CategoryCamera",
        ModCategory.CombatAssist => "CategoryCombatAssist",
        ModCategory.Economy => "CategoryEconomy",
        ModCategory.ReplayDebug => "CategoryReplayDebug",
        ModCategory.Misc => "CategoryMisc",
        _ => "CategoryUncategorized"
    });

    public string EffectiveTagsText => ModTaxonomy.FormatTagsDisplay(EffectiveTags);

    public string? PreviewUrl { get; }
    public IReadOnlyList<string> PreviewCandidateUrls { get; }

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsInLibrary))]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private CatalogEntryState _state;

    public bool IsInLibrary => State != CatalogEntryState.NotInstalled;

    /// <summary>Installed, but the catalog now serves a different copy.</summary>
    public bool HasUpdate => State == CatalogEntryState.UpdateAvailable;

    public string StatusText => State switch
    {
        CatalogEntryState.UpdateAvailable => LocalizationService.T("CatalogStatusUpdateAvailable"),
        CatalogEntryState.UpToDate => LocalizationService.T("CatalogStatusInLibrary"),
        _ => LocalizationService.T("CatalogStatusNotInstalled")
    };

    public void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(EffectiveCategoryDisplay));
        OnPropertyChanged(nameof(EffectiveTagsText));
        OnPropertyChanged(nameof(StatusText));
    }

    public async Task LoadPreviewImageAsync()
    {
        var urls = PreviewCandidateUrls;
        var url = urls.Count == 0 ? PreviewUrl : string.Join('\n', urls);
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewLoadUrl = url;

        if (urls.Count == 0)
        {
            PreviewImage = null;
            return;
        }

        var bmp = await PreviewImageLoader.TryLoadCandidatesAsync(urls, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
            return;
        if (!string.Equals(_previewLoadUrl, url, StringComparison.Ordinal))
            return;
        PreviewImage = bmp;
    }
}
