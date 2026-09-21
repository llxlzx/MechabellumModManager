using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MechabellumModManager.ViewModels;

public sealed class UiScaleOption : INotifyPropertyChanged
{
    string _label;

    public UiScaleOption(string code, string label)
    {
        Code = code;
        _label = label;
    }

    public string Code { get; }

    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
