using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject
{
    readonly MainViewModel _owner;
    bool _suppressEnabledCallback;
    CancellationTokenSource? _previewCts;
    string? _previewLoadUrl;

    public ModPackage Package { get; }
    public bool IsMissing { get; }

    public ModItemViewModel(MainViewModel owner, ModPackage package, bool isEnabled, bool isMissing = false)
    {
        _owner = owner;
        Package = package;
        IsMissing = isMissing;
        _isEnabled = isEnabled;
        RefreshCatalogFieldsFromPackage();
    }

    public static ModItemViewModel CreateMissing(MainViewModel owner, string packageId) =>
        new(
            owner,
            new ModPackage
            {
                Id = packageId,
                DisplayName = string.Format(
                    LocalizationService.T("PackageMissingNameFormat"),
                    packageId),
                Type = ModPackageType.MelonMod,
                PackageDirectory = ""
            },
            isEnabled: true,
            isMissing: true);

    public string DisplayName =>
        Package.CatalogDisplayName is not null || Package.CatalogLocales is not null
            ? CatalogLocaleResolver.ResolveName(
                Package.CatalogDisplayName ?? Package.DisplayName,
                Package.CatalogLocales)
            : Package.DisplayName;
    public string? Version => Package.Version;
    public string? Author => Package.Author;
    public string? Summary =>
        Package.CatalogLocales is not null
            ? CatalogLocaleResolver.ResolveSummary(Package.Summary, Package.CatalogLocales)
            : Package.Summary;
    public string? CatalogUpdatedAt => Package.CatalogUpdatedAt;
    public string? Preview => Package.Preview;
    public string? PreviewUrl { get; private set; }

    public ModCategory EffectiveCategory =>
        ModTaxonomy.ResolveEffectiveCategory(Package.CategoryOverride, Package.CatalogCategory);

    public IReadOnlyList<string> EffectiveTags =>
        ModTaxonomy.ResolveEffectiveTags(Package.CatalogTags, Package.ExtraTags);

    public string EffectiveCategoryDisplay => _owner.Ui.CategoryLabel(EffectiveCategory);

    public string EffectiveTagsText => ModTaxonomy.FormatTagsDisplay(EffectiveTags);

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
    private bool _hasUpdate;

    /// <summary>
    /// True after <see cref="ApplyCatalogEnrichment"/> successfully matched this row to a catalog entry.
    /// Distinguishes "up to date" from "never compared" so the status column does not lie.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
    private bool _catalogMatched;

    /// <summary>
    /// The version the catalog currently serves, which <see cref="Version"/> deliberately is not:
    /// that one describes the bytes on disk. Null when the catalog names no version, which is
    /// common for entries whose staleness was decided by hash rather than by version.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateStatusText))]
    private string? _latestVersion;

    public string UpdateStatusText
    {
        get
        {
            if (IsMissing || !CatalogMatched)
                return "";

            if (!HasUpdate)
                return LocalizationService.T("LibraryStatusUpToDate");

            // "1.0.0 -> 1.2.0" answers "how far behind am I", which a bare "update available"
            // does not. Both halves have to be there for the arrow to mean anything.
            return !string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(LatestVersion)
                ? $"{Version} → {LatestVersion}"
                : LocalizationService.T("CatalogStatusUpdateAvailable");
        }
    }

    public string TypeLabel => IsMissing
        ? LocalizationService.T("PackageMissing")
        : Package.Type switch
    {
        ModPackageType.MelonMod => LocalizationService.T("PackageTypeMelonMod"),
        ModPackageType.MelonPlugin => LocalizationService.T("PackageTypeMelonPlugin"),
        ModPackageType.MelonUserLibs => LocalizationService.T("PackageTypeMelonUserLibs"),
        ModPackageType.MelonUserData => LocalizationService.T("PackageTypeMelonUserData"),
        _ => Package.Type.ToString()
    };
    public bool HighRisk => Package.HighRisk;
    public string HighRiskLabel => HighRisk
        ? LocalizationService.T("HighRiskYes")
        : LocalizationService.T("HighRiskNo");
    public string? RequiredMelonLoaderVersion => Package.RequiredMelonLoaderVersion;
    public string VersionWarningHint =>
        string.IsNullOrWhiteSpace(RequiredMelonLoaderVersion)
            ? ""
            : string.Format(
                LocalizationService.T("RequiredMelonLoaderFormat"),
                RequiredMelonLoaderVersion);

    public void NotifyRiskChanged()
    {
        OnPropertyChanged(nameof(HighRisk));
        OnPropertyChanged(nameof(HighRiskLabel));
    }

    public void NotifyDetailChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(Author));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CatalogUpdatedAt));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(PreviewUrl));
        OnPropertyChanged(nameof(EffectiveCategory));
        OnPropertyChanged(nameof(EffectiveTags));
        OnPropertyChanged(nameof(EffectiveCategoryDisplay));
        OnPropertyChanged(nameof(EffectiveTagsText));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(HighRiskLabel));
        OnPropertyChanged(nameof(VersionWarningHint));
        OnPropertyChanged(nameof(LatestVersion));
        OnPropertyChanged(nameof(CatalogMatched));
        OnPropertyChanged(nameof(UpdateStatusText));
    }

    public void RefreshCatalogFieldsFromPackage()
    {
        PreviewUrl = ModCatalogService.TryGetRawUrl(Package.Preview);
        NotifyDetailChanged();
    }

    public void ApplyCatalogEnrichment(CatalogMod catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!string.IsNullOrWhiteSpace(catalog.Author))
            Package.Author = catalog.Author;
        // Version and CatalogUpdatedAt describe the bytes on disk, not the catalog's latest.
        // Overwriting them would show a player running an old DLL the new version number, which
        // is exactly the "everyone thinks they are current" problem update detection has to solve.
        // Empty values are still filled in, since a package that never recorded one has nothing to lose.
        if (string.IsNullOrWhiteSpace(Package.Version) && !string.IsNullOrWhiteSpace(catalog.Version))
            Package.Version = catalog.Version;
        if (!string.IsNullOrWhiteSpace(catalog.Summary))
            Package.Summary = catalog.Summary;
        if (string.IsNullOrWhiteSpace(Package.CatalogUpdatedAt) && !string.IsNullOrWhiteSpace(catalog.UpdatedAt))
            Package.CatalogUpdatedAt = catalog.UpdatedAt;
        if (!string.IsNullOrWhiteSpace(catalog.Preview))
            Package.Preview = catalog.Preview;
        Package.CatalogCategory = catalog.Category;
        Package.CatalogTags = catalog.Tags is null ? null : new List<string>(catalog.Tags);
        Package.CatalogDisplayName = string.IsNullOrWhiteSpace(catalog.Name) ? null : catalog.Name;
        Package.CatalogLocales = CloneLocales(catalog.Locales);
        if (!string.IsNullOrWhiteSpace(catalog.Category) &&
            !ModTaxonomy.TryParseCategory(catalog.Category, out _))
        {
            _owner.LogTaxonomyWarning($"Mod '{Package.Id}': invalid catalog category '{catalog.Category}', treating as Uncategorized.");
        }
        LatestVersion = string.IsNullOrWhiteSpace(catalog.Version) ? null : catalog.Version;
        HasUpdate = ModCatalogService.GetEntryState([Package], catalog) == CatalogEntryState.UpdateAvailable;
        CatalogMatched = true;
        RefreshCatalogFieldsFromPackage();
    }

    static Dictionary<string, CatalogModLocale>? CloneLocales(
        Dictionary<string, CatalogModLocale>? source)
    {
        if (source is null || source.Count == 0)
            return null;
        var copy = new Dictionary<string, CatalogModLocale>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            copy[key] = new CatalogModLocale
            {
                Name = value?.Name,
                Summary = value?.Summary
            };
        }
        return copy;
    }

    public async Task LoadPreviewImageAsync(string? urlOverride = null)
    {
        var url = urlOverride ?? PreviewUrl;
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

    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        if (_suppressEnabledCallback) return;
        _owner.OnModEnabledChanged(this, value);
    }

    public void SetEnabledSilent(bool value)
    {
        _suppressEnabledCallback = true;
        IsEnabled = value;
        _suppressEnabledCallback = false;
    }
}
