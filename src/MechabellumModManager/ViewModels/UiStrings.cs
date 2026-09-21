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
    public string GuideNav => T("GuideNav");
    public string GuideTitle => T("GuideTitle");
    public string GuideIntro => T("GuideIntro");
    public string GuideLanguageTitle => T("GuideLanguageTitle");
    public string GuideLanguageBody => T("GuideLanguageBody");
    public string GuidePathTitle => T("GuidePathTitle");
    public string GuidePathBody => T("GuidePathBody");
    public string GuideLoaderTitle => T("GuideLoaderTitle");
    public string GuideLoaderBody => T("GuideLoaderBody");
    public string GuideNoteMelon => T("GuideNoteMelon");
    public string GuideLaunchTitle => T("GuideLaunchTitle");
    public string GuideLaunchBody => T("GuideLaunchBody");
    public string GuideBrowseTitle => T("GuideBrowseTitle");
    public string GuideBrowseBody => T("GuideBrowseBody");
    public string GuideNoteRisk => T("GuideNoteRisk");
    public string GuideLibraryTitle => T("GuideLibraryTitle");
    public string GuideLibraryBody => T("GuideLibraryBody");
    public string GuideProfilesTitle => T("GuideProfilesTitle");
    public string GuideProfilesBody => T("GuideProfilesBody");
    public string GuideApplyTitle => T("GuideApplyTitle");
    public string GuideApplyBody => T("GuideApplyBody");
    public string GuideNoteApply => T("GuideNoteApply");
    public string GuideLaterTitle => T("GuideLaterTitle");
    public string GuideImportTitle => T("GuideImportTitle");
    public string GuideImportBody => T("GuideImportBody");
    public string GuideUpdatesTitle => T("GuideUpdatesTitle");
    public string GuideUpdatesBody => T("GuideUpdatesBody");
    public string GuideBranchTitle => T("GuideBranchTitle");
    public string GuideBranchBody => T("GuideBranchBody");
    public string GuideNoteBranch => T("GuideNoteBranch");
    public string GuideSupportTitle => T("GuideSupportTitle");
    public string GuideSupportBody => T("GuideSupportBody");
    public string GuideCommunityTitle => T("GuideCommunityTitle");
    public string GuideCommunityBody => T("GuideCommunityBody");
    public string GuideOpenSettings => T("GuideOpenSettings");
    public string GuideOpenCatalog => T("GuideOpenCatalog");
    public string GuideOpenLibrary => T("GuideOpenLibrary");
    public string GuideDismissStartup => T("GuideDismissStartup");
    public string BrowseMods => T("BrowseMods");
    public string CatalogEmptyHint => T("CatalogEmptyHint");
    public string LibraryEmptyHint => T("LibraryEmptyHint");
    public string NavLibraryTip => T("NavLibraryTip");
    public string NavWorkshopTip => T("NavWorkshopTip");
    public string CollapseBrowse => T("CollapseBrowse");
    public string ExpandBrowse => T("ExpandBrowse");
    public string ImportDll => T("ImportDll");
    public string ImportZip => T("ImportZip");
    public string ImportFolder => T("ImportFolder");
    public string ApplyProfile => T("ApplyProfile");
    public string NotifyApplySucceeded => T("NotifyApplySucceeded");
    public string ApplyAndLaunch => T("ApplyAndLaunch");
    public string CheckUpdates => T("CheckUpdates");
    public string Language => T("Language");
    public string LanguageSystem => T("LanguageSystem");
    public string UiScale => T("UiScale");
    public string UiScaleAuto => T("UiScaleAuto");
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
    public string DirectUpload => T("DirectUpload");
    public string DirectUploadTitle => T("DirectUploadTitle");
    public string DirectUploadInviteCode => T("DirectUploadInviteCode");
    public string DirectUploadSaveCode => T("DirectUploadSaveCode");
    public string DirectUploadSecretId => T("DirectUploadSecretId");
    public string DirectUploadSecretKey => T("DirectUploadSecretKey");
    public string DirectUploadSecretHint => T("DirectUploadSecretHint");
    public string DirectUploadSecretSaved => T("DirectUploadSecretSaved");
    public string DirectUploadNeedSecret => T("DirectUploadNeedSecret");
    public string DirectUploadModId => T("DirectUploadModId");
    public string DirectUploadName => T("DirectUploadName");
    public string DirectUploadSummary => T("DirectUploadSummary");
    public string DirectUploadVersion => T("DirectUploadVersion");
    public string DirectUploadPickDll => T("DirectUploadPickDll");
    public string DirectUploadPickPreview => T("DirectUploadPickPreview");
    public string DirectUploadPublish => T("DirectUploadPublish");
    public string DirectUploadUpdateCatalog => T("DirectUploadUpdateCatalog");
    public string DirectUploadUpdateCatalogHint => T("DirectUploadUpdateCatalogHint");
    public string DirectUploadMergeCatalog => T("DirectUploadMergeCatalog");
    public string DirectUploadMergeSuccess => T("DirectUploadMergeSuccess");
    public string DirectUploadFilesUploaded => T("DirectUploadFilesUploaded");
    public string DirectUploadCatalogDenied => T("DirectUploadCatalogDenied");
    public string DirectUploadCatalogWriteFailed => T("DirectUploadCatalogWriteFailed");
    public string DirectUploadEntryMissing => T("DirectUploadEntryMissing");
    public string DirectUploadNeedModId => T("DirectUploadNeedModId");
    public string DirectUploadSuccess => T("DirectUploadSuccess");
    public string DirectUploadMirrorLagHint => T("DirectUploadMirrorLagHint");
    public string DirectUploadFailed => T("DirectUploadFailed");
    public string DirectUploadNotConfigured => T("DirectUploadNotConfigured");
    public string DirectUploadNeedInvite => T("DirectUploadNeedInvite");
    public string DirectUploadNeedFields => T("DirectUploadNeedFields");
    public string DirectUploadCodeSaved => T("DirectUploadCodeSaved");
    public string DirectUploadPublishing => T("DirectUploadPublishing");
    public string DirectUploadErrInvalidInvite => T("DirectUploadErrInvalidInvite");
    public string DirectUploadErrForbidden => T("DirectUploadErrForbidden");
    public string DirectUploadErrValidation => T("DirectUploadErrValidation");
    public string DirectUploadErrTooLarge => T("DirectUploadErrTooLarge");
    public string DirectUploadErrConflict => T("DirectUploadErrConflict");
    public string DirectUploadErrGithub => T("DirectUploadErrGithub");
    public string DirectUploadErrNetwork => T("DirectUploadErrNetwork");
    public string SubmitMailOpenedDomestic => T("SubmitMailOpenedDomestic");
    public string SubmitMailOpenedInternational => T("SubmitMailOpenedInternational");
    public string ReportMailOpenedDomestic => T("ReportMailOpenedDomestic");
    public string ReportMailOpenedInternational => T("ReportMailOpenedInternational");
    public string MailOpenedQq => T("MailOpenedQq");
    public string MailOpenedGmail => T("MailOpenedGmail");
    public string MailCancelled => T("MailCancelled");
    public string MailOpenFailed => T("MailOpenFailed");
    public string FeedbackButton => T("FeedbackButton");
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
    public string RefreshInstalledMods => T("RefreshInstalledMods");
    public string AddToLibrary => T("AddToLibrary");
    public string ImportFromGame => T("ImportFromGame");
    public string ExportDiagnosticsButton => T("ExportDiagnosticsButton");
    public string ExportDiagnosticsTitle => T("ExportDiagnosticsTitle");
    public string ExportDiagnosticsConsent => T("ExportDiagnosticsConsent");
    public string ExportDiagnosticsModeFull => T("ExportDiagnosticsModeFull");
    public string ExportDiagnosticsModeStrong => T("ExportDiagnosticsModeStrong");
    public string ExportDiagnosticsContinue => T("ExportDiagnosticsContinue");
    public string ExportDiagnosticsSaved => T("ExportDiagnosticsSaved");
    public string ExportDiagnosticsFailed => T("ExportDiagnosticsFailed");
    public string ExportDiagnosticsMailOpenedDomestic => T("ExportDiagnosticsMailOpenedDomestic");
    public string ExportDiagnosticsMailOpenedInternational => T("ExportDiagnosticsMailOpenedInternational");
    public string MailProviderTitle => T("MailProviderTitle");
    public string MailProviderHint => T("MailProviderHint");
    public string MailProviderQq => T("MailProviderQq");
    public string MailProviderGmail => T("MailProviderGmail");
    public string GamePath => T("GamePath");
    public string LaunchMode => T("LaunchMode");
    public string BrowseEllipsis => T("BrowseEllipsis");
    public string ProfileNew => T("ProfileNew");
    public string ProfileRename => T("ProfileRename");
    public string ProfileDuplicate => T("ProfileDuplicate");
    public string ProfileDelete => T("ActionDelete");
    public string PortableDataRoot => T("PortableDataRoot");
    public string CheckModUpdatesOnStartup => T("CheckModUpdatesOnStartup");
    public string HideMelonConsole => T("HideMelonConsole");
    public string HideMelonConsoleTip => T("HideMelonConsoleTip");
    public string SettingsAdvanced => T("SettingsAdvanced");
    public string MirrorBaseUrl => T("MirrorBaseUrl");
    public string MirrorBaseUrlHint => T("MirrorBaseUrlHint");
    public string MirrorSummaryOnDefault => T("MirrorSummaryOnDefault");
    public string MirrorSummaryCustom => T("MirrorSummaryCustom");
    public string MirrorSummaryOff => T("MirrorSummaryOff");
    public string Profiles => T("Profiles");
    public string ModLibrary => T("ModLibrary");
    public string SyncLog => T("SyncLog");
    public string LogExpand => T("LogExpand");
    public string LogCollapse => T("LogCollapse");
    public string InstallMelonLoader => T("InstallMelonLoader");
    public string InstallMelonLoaderTip => T("InstallMelonLoaderTip");
    public string CurrentProfile => T("CurrentProfile");
    public string FilterSearch => T("FilterSearch");
    public string FilterCategory => T("FilterCategory");
    public string FilterTag => T("FilterTag");
    public string FilterSort => T("FilterSort");
    public string FilterAll => T("FilterAll");
    public string SortByName => T("SortByName");
    public string SortByUpdatedAtDesc => T("SortByUpdatedAtDesc");
    public string ColumnCategory => T("ColumnCategory");
    public string ColumnTag => T("ColumnTag");
    public string ColumnName => T("ColumnName");
    public string ColumnAuthor => T("ColumnAuthor");
    public string ColumnVersion => T("ColumnVersion");
    public string ColumnUpdated => T("ColumnUpdated");
    public string ColumnStatus => T("ColumnStatus");
    public string ColumnEnabled => T("ColumnEnabled");
    public string ColumnType => T("ColumnType");
    public string ColumnRisk => T("ColumnRisk");
    public string ColumnLoader => T("ColumnLoader");
    public string LabelAuthor => T("LabelAuthor");
    public string LabelVersion => T("LabelVersion");
    public string LabelUpdated => T("LabelUpdated");
    public string LabelName => T("LabelName");
    public string LabelSummary => T("LabelSummary");
    public string ActionDelete => T("ActionDelete");
    public string ActionUpdate => T("ActionUpdate");
    public string ActionCollapseDetail => T("ActionCollapseDetail");
    public string TipToggleHighRisk => T("TipToggleHighRisk");
    public string TipDeleteFromLibrary => T("TipDeleteFromLibrary");
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
    public string BranchSwitchEmergencyRecover => T("BranchSwitchEmergencyRecover");
    public string BranchSwitchTeardown => T("BranchSwitchTeardown");
    public string BranchSwitchRepairOrphan => T("BranchSwitchRepairOrphan");
    public string PureGameCleanupButton => T("PureGameCleanupButton");
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
