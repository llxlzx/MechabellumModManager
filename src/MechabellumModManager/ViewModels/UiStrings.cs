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
    public string PreviewImageTitle => T("PreviewImageTitle");
    public string PreviewImageHint => T("PreviewImageHint");
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
    public string SubmitMailOpenedDomestic => T("SubmitMailOpenedDomestic");
    public string SubmitMailOpenedInternational => T("SubmitMailOpenedInternational");
    public string ReportMailOpenedDomestic => T("ReportMailOpenedDomestic");
    public string ReportMailOpenedInternational => T("ReportMailOpenedInternational");
    public string MailOpenFailed => T("MailOpenFailed");
    public string SubmitGuideTitle => T("SubmitGuideTitle");
    public string SubmitGuideIntro => T("SubmitGuideIntro");
    public string SubmitGuideBody => T("SubmitGuideBody");
    public string SubmitGuideTip => T("SubmitGuideTip");
    public string SubmitGuideWaitNotice => T("SubmitGuideWaitNotice");
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
    public string FilterSearch => T("FilterSearch");
    public string FilterCategory => T("FilterCategory");
    public string FilterTag => T("FilterTag");
    public string FilterSort => T("FilterSort");
    public string FilterAll => T("FilterAll");
    public string SortByName => T("SortByName");
    public string SortByUpdatedAtDesc => T("SortByUpdatedAtDesc");
    public string ColumnCategory => T("ColumnCategory");
    public string TagsLabel => T("TagsLabel");
    public string EditModTaxonomy => T("EditModTaxonomy");
    public string CategoryFollowCatalog => T("CategoryFollowCatalog");
    public string ExtraTagsHint => T("ExtraTagsHint");

    public string BranchSwitchTitle => T("BranchSwitchTitle");
    public string BranchSwitchStatus => T("BranchSwitchStatus");
    public string BranchSwitchBetaName => T("BranchSwitchBetaName");
    public string BranchSwitchOfficialProfile => T("BranchSwitchOfficialProfile");
    public string BranchSwitchBetaProfile => T("BranchSwitchBetaProfile");
    public string BranchSwitchToOfficial => T("BranchSwitchToOfficial");
    public string BranchSwitchToBeta => T("BranchSwitchToBeta");
    public string BranchSwitchStartWizard => T("BranchSwitchStartWizard");
    public string BranchSwitchTeardown => T("BranchSwitchTeardown");
    public string BranchSwitchConfirmManual => T("BranchSwitchConfirmManual");
    public string BranchSwitchConfirmSettle => T("BranchSwitchConfirmSettle");
    public string BranchSwitchHint => T("BranchSwitchHint");

    public string CategoryLabel(Models.ModCategory category) => category switch
    {
        Models.ModCategory.OverlayUI => T("CategoryOverlayUI"),
        Models.ModCategory.QoL => T("CategoryQoL"),
        Models.ModCategory.Camera => T("CategoryCamera"),
        Models.ModCategory.CombatAssist => T("CategoryCombatAssist"),
        Models.ModCategory.Economy => T("CategoryEconomy"),
        Models.ModCategory.ReplayDebug => T("CategoryReplayDebug"),
        Models.ModCategory.Misc => T("CategoryMisc"),
        _ => T("CategoryUncategorized")
    };

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
