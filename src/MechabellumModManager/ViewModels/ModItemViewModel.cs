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
        if (!string.IsNullOrWhiteSpace(catalog.Version))
            Package.Version = catalog.Version;
        if (!string.IsNullOrWhiteSpace(catalog.Summary))
            Package.Summary = catalog.Summary;
        if (!string.IsNullOrWhiteSpace(catalog.UpdatedAt))
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
