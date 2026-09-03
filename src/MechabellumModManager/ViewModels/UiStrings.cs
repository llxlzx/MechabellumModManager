using System.ComponentModel;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

/// <summary>
/// Observable UI string bag. Call <see cref="Refresh"/> after culture changes.
/// </summary>
public sealed class UiStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Settings => T("Settings");
    public string BrowseMods => T("BrowseMods");
    public string ImportDll => T("ImportDll");
    public string ImportZip => T("ImportZip");
    public string ImportFolder => T("ImportFolder");
    public string ApplyProfile => T("ApplyProfile");
    public string ApplyAndLaunch => T("ApplyAndLaunch");
    public string CheckUpdates => T("CheckUpdates");
    public string Language => T("Language");
    public string LanguageSystem => T("LanguageSystem");
    public string CreditsTitle => T("CreditsTitle");
    public string CreditsBody => T("CreditsBody");
    public string Report => T("Report");
    public string ReportConfirm => T("ReportConfirm");
    public string ReportCategoryCheat => T("ReportCategoryCheat");
    public string ReportCategoryVirus => T("ReportCategoryVirus");
    public string ReportCategoryUnrelated => T("ReportCategoryUnrelated");
    public string ReportCategoryOther => T("ReportCategoryOther");
    public string ReportOtherHint => T("ReportOtherHint");
    public string ReportSuccess => T("ReportSuccess");
    public string ReportFailed => T("ReportFailed");
    public string SubmitMod => T("SubmitMod");
    public string SubmitModConfirm => T("SubmitModConfirm");
    public string SubmitModSuccess => T("SubmitModSuccess");
    public string SubmitModFailed => T("SubmitModFailed");
    public string SubmitGuideTitle => T("SubmitGuideTitle");
    public string SubmitGuideIntro => T("SubmitGuideIntro");
    public string SubmitGuideBody => T("SubmitGuideBody");
    public string SubmitGuideTip => T("SubmitGuideTip");
    public string SubmitGuideOpen => T("SubmitGuideOpen");
    public string SubmitGuideOpenEmail => T("SubmitGuideOpenEmail");
    public string Confirm => T("Confirm");
    public string Cancel => T("Cancel");
    public string Ok => T("Ok");
    public string RefreshCatalog => T("RefreshCatalog");
    public string AddToLibrary => T("AddToLibrary");
    public string ImportFromGame => T("ImportFromGame");
    public string GamePath => T("GamePath");
    public string LaunchMode => T("LaunchMode");
    public string PortableDataRoot => T("PortableDataRoot");
    public string Profiles => T("Profiles");
    public string ModLibrary => T("ModLibrary");
    public string SyncLog => T("SyncLog");
    public string CurrentProfile => T("CurrentProfile");

    public void Refresh()
    {
        foreach (var prop in typeof(UiStrings).GetProperties())
        {
            if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop.Name));
        }
    }

    static string T(string key) => LocalizationService.T(key);
}
