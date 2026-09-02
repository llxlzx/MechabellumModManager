using CommunityToolkit.Mvvm.ComponentModel;
using MechabellumModManager.Models;

namespace MechabellumModManager.ViewModels;

public sealed partial class ProfileItemViewModel : ObservableObject
{
    public Profile Profile { get; }

    public ProfileItemViewModel(Profile profile)
    {
        Profile = profile;
    }

    public string Id => Profile.Id;

    public string Name
    {
        get => Profile.Name;
        set
        {
            if (Profile.Name == value) return;
            Profile.Name = value;
            OnPropertyChanged();
        }
    }
}
