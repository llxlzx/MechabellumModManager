using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class CatalogModItemViewModel : ObservableObject
{
    readonly string? _mirrorBaseUrl;
    readonly string? _cacheRoot;
    CancellationTokenSource? _previewCts;
    string? _previewLoadUrl;

    public CatalogMod Mod { get; }

    public CatalogModItemViewModel(
        CatalogMod mod,
        CatalogEntryState state,
        string? mirrorBaseUrl = null,
        string? cacheRoot = null)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        _state = state;
        _mirrorBaseUrl = mirrorBaseUrl;
        _cacheRoot = cacheRoot;
        PreviewCandidateUrls = ModCatalogService.GetPreviewCandidateUrls(mod, mirrorBaseUrl);
        PreviewUrl = PreviewCandidateUrls.Count == 0 ? null : PreviewCandidateUrls[0];
    }

    public string Id => Mod.Id;
    public string Name => CatalogLocaleResolver.ResolveName(Mod);
    public string? Author => Mod.Author;
    public string? Version => Mod.Version;
    public string? UpdatedAt => Mod.UpdatedAt;

    /// <summary>True for the first card in the curated workshop strip.</summary>
    public bool IsFeaturedLead { get; set; }

    public string? Summary => CatalogLocaleResolver.ResolveSummary(Mod);

    public string ManagerFloorText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Mod.MinManagerVersion))
                return "";
            var decision = ManagerVersionFloor.Evaluate(Mod.MinManagerVersion, UpdateChecker.ReadLocalVersion());
            if (decision.Unreadable)
                return string.Format(LocalizationService.T("ManagerFloorUnreadable"), Mod.MinManagerVersion.Trim());
            return string.Format(LocalizationService.T("ManagerFloorLine"), decision.Required);
        }
    }

    public bool IsBlockedByManagerVersion =>
        !string.IsNullOrWhiteSpace(Mod.MinManagerVersion) &&
        !ManagerVersionFloor.Evaluate(Mod.MinManagerVersion, UpdateChecker.ReadLocalVersion()).Allowed;

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

    public string? PreviewUrl { get; private set; }
    public IReadOnlyList<string> PreviewCandidateUrls { get; private set; }

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
        _ = LoadPreviewImageAsync();
    }

    public async Task LoadPreviewImageAsync()
    {
        PreviewCandidateUrls = ModCatalogService.GetPreviewCandidateUrls(Mod, _mirrorBaseUrl);
        PreviewUrl = PreviewCandidateUrls.Count == 0 ? null : PreviewCandidateUrls[0];
        var urls = PreviewCandidateUrls;
        var url = urls.Count == 0 ? PreviewUrl : string.Join('\n', urls);
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewLoadUrl = url;

        if (urls.Count == 0 && string.IsNullOrWhiteSpace(CatalogLocaleResolver.ResolvePreviewIdentity(Mod).RelativePath))
        {
            PreviewImage = null;
            return;
        }

        var identity = CatalogLocaleResolver.ResolvePreviewIdentity(Mod);
        var bmp = await PreviewImageLoader.LoadResolvedAsync(
            identity.RelativePath, identity.Sha256, urls, _cacheRoot, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
            return;
        if (!string.Equals(_previewLoadUrl, url, StringComparison.Ordinal))
            return;
        PreviewImage = bmp;
    }
}
