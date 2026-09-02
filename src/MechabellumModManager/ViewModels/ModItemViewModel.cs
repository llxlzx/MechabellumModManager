using CommunityToolkit.Mvvm.ComponentModel;
using MechabellumModManager.Models;

namespace MechabellumModManager.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject
{
    readonly MainViewModel _owner;
    bool _suppressEnabledCallback;

    public ModPackage Package { get; }

    public ModItemViewModel(MainViewModel owner, ModPackage package, bool isEnabled)
    {
        _owner = owner;
        Package = package;
        _isEnabled = isEnabled;
    }

    public string DisplayName => Package.DisplayName;
    public string? Version => Package.Version;
    public string TypeLabel => Package.Type switch
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
