using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject
{
    readonly MainViewModel _owner;
    bool _suppressEnabledCallback;

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
                DisplayName = "(缺失) " + packageId,
                Type = ModPackageType.MelonMod,
                PackageDirectory = ""
            },
            isEnabled: true,
            isMissing: true);

    public string DisplayName => Package.DisplayName;
    public string? Version => Package.Version;
    public string? Author => Package.Author;
    public string? Summary => Package.Summary;
    public string? CatalogUpdatedAt => Package.CatalogUpdatedAt;
    public string? Preview => Package.Preview;
    public string? PreviewUrl { get; private set; }

    [ObservableProperty]
    private BitmapImage? _previewImage;

    public string TypeLabel => IsMissing
        ? "缺失"
        : Package.Type switch
    {
        ModPackageType.MelonMod => "Mod",
        ModPackageType.MelonPlugin => "插件",
        ModPackageType.MelonUserLibs => "UserLibs",
        ModPackageType.MelonUserData => "UserData",
        _ => Package.Type.ToString()
    };
    public bool HighRisk => Package.HighRisk;
    public string HighRiskLabel => HighRisk ? "高风险" : "—";
    public string? RequiredMelonLoaderVersion => Package.RequiredMelonLoaderVersion;
    public string VersionWarningHint =>
        string.IsNullOrWhiteSpace(RequiredMelonLoaderVersion)
            ? ""
            : $"需要 MelonLoader {RequiredMelonLoaderVersion}";

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
    }

    public void RefreshCatalogFieldsFromPackage()
    {
        PreviewUrl = string.IsNullOrWhiteSpace(Package.Preview)
            ? null
            : ModCatalogService.GetRawUrl(Package.Preview);
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
        RefreshCatalogFieldsFromPackage();
    }

    public void LoadPreviewImage(string? urlOverride = null)
    {
        var url = urlOverride ?? PreviewUrl;
        PreviewImage = PreviewImageLoader.TryLoad(url);
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
