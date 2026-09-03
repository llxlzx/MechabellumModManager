using CommunityToolkit.Mvvm.ComponentModel;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class CatalogModItemViewModel : ObservableObject
{
    public CatalogMod Mod { get; }

    public CatalogModItemViewModel(CatalogMod mod, bool isInLibrary)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        _isInLibrary = isInLibrary;
    }

    public string Id => Mod.Id;
    public string Name => Mod.Name;
    public string? Author => Mod.Author;
    public string? Version => Mod.Version;
    public string? UpdatedAt => Mod.UpdatedAt;
    public string? Summary => Mod.Summary;
    public string File => Mod.File;
    public string? Type => Mod.Type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isInLibrary;

    public string StatusText => IsInLibrary ? "已在库" : "未安装";
}
