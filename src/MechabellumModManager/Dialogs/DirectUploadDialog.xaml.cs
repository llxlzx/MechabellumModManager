using System.IO;
using System.Net.Http;
using System.Windows;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Microsoft.Win32;

namespace MechabellumModManager.Dialogs;

public partial class DirectUploadDialog : Window
{
    readonly AppConfig _config;
    readonly Action<AppConfig> _saveConfig;
    readonly UiStrings _ui;
    readonly DirectUploadService _service;

    public DirectUploadDialog(AppConfig config, Action<AppConfig> saveConfig, UiStrings ui)
    {
        InitializeComponent();
        _config = config;
        _saveConfig = saveConfig;
        _ui = ui;
        DataContext = new Labels(ui);
        Title = ui.DirectUploadTitle;

        var baseUrl = string.IsNullOrWhiteSpace(config.DirectUploadApiBaseUrl)
            ? DirectUploadDefaults.ApiBaseUrl
            : config.DirectUploadApiBaseUrl!;
        _service = new DirectUploadService(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, baseUrl);

        InviteBox.Text = config.AuthorInviteCode ?? "";
        if (!_service.IsConfigured)
            StatusText.Text = ui.DirectUploadNotConfigured;
    }

    void SaveCode_Click(object sender, RoutedEventArgs e)
    {
        _config.AuthorInviteCode = InviteBox.Text?.Trim() ?? "";
        _saveConfig(_config);
        StatusText.Text = _ui.DirectUploadCodeSaved;
    }

    void PickDll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationService.T("FileFilterDll"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            DllPathBox.Text = dialog.FileName;
    }

    void PickPreview_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PNG|*.png|All|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            PreviewPathBox.Text = dialog.FileName;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (!_service.IsConfigured)
        {
            StatusText.Text = _ui.DirectUploadNotConfigured;
            return;
        }

        var invite = InviteBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(invite))
        {
            StatusText.Text = _ui.DirectUploadNeedInvite;
            return;
        }

        var id = IdBox.Text?.Trim() ?? "";
        var name = NameBox.Text?.Trim() ?? "";
        var dll = DllPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dll) || !File.Exists(dll))
        {
            StatusText.Text = _ui.DirectUploadNeedFields;
            return;
        }

        _config.AuthorInviteCode = invite;
        _saveConfig(_config);

        PublishButton.IsEnabled = false;
        StatusText.Text = _ui.DirectUploadPublishing;
        try
        {
            var result = await _service.PublishAsync(new DirectUploadPublishRequest
            {
                InviteCode = invite,
                Id = id,
                Name = name,
                Summary = string.IsNullOrWhiteSpace(SummaryBox.Text) ? null : SummaryBox.Text.Trim(),
                Version = string.IsNullOrWhiteSpace(VersionBox.Text) ? null : VersionBox.Text.Trim(),
                DllPath = dll,
                PreviewPath = string.IsNullOrWhiteSpace(PreviewPathBox.Text) ? null : PreviewPathBox.Text.Trim()
            }).ConfigureAwait(true);

            if (!result.Ok)
            {
                StatusText.Text = MapError(result.Error);
                return;
            }

            var msg = _ui.DirectUploadSuccess;
            if (!string.IsNullOrWhiteSpace(result.MirrorNote))
                msg = msg + "\n\n" + result.MirrorNote;
            else
                msg = msg + "\n\n" + _ui.DirectUploadMirrorLagHint;

            MessageBox.Show(this, msg, _ui.DirectUploadTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = _ui.DirectUploadFailed + ": " + ex.Message;
        }
        finally
        {
            PublishButton.IsEnabled = true;
        }
    }

    string MapError(DirectUploadErrorCode code) => code switch
    {
        DirectUploadErrorCode.InvalidInvite => _ui.DirectUploadErrInvalidInvite,
        DirectUploadErrorCode.ForbiddenNotOwner => _ui.DirectUploadErrForbidden,
        DirectUploadErrorCode.ValidationFailed => _ui.DirectUploadErrValidation,
        DirectUploadErrorCode.PayloadTooLarge => _ui.DirectUploadErrTooLarge,
        DirectUploadErrorCode.CatalogConflict => _ui.DirectUploadErrConflict,
        DirectUploadErrorCode.UpstreamGithub => _ui.DirectUploadErrGithub,
        DirectUploadErrorCode.NotConfigured => _ui.DirectUploadNotConfigured,
        DirectUploadErrorCode.Network => _ui.DirectUploadErrNetwork,
        _ => _ui.DirectUploadFailed
    };

    sealed class Labels
    {
        readonly UiStrings _ui;
        public Labels(UiStrings ui) => _ui = ui;
        public string TitleText => _ui.DirectUploadTitle;
        public string InviteLabel => _ui.DirectUploadInviteCode;
        public string SaveCodeLabel => _ui.DirectUploadSaveCode;
        public string ModIdLabel => _ui.DirectUploadModId;
        public string NameLabel => _ui.DirectUploadName;
        public string SummaryLabel => _ui.DirectUploadSummary;
        public string VersionLabel => _ui.DirectUploadVersion;
        public string PickDllLabel => _ui.DirectUploadPickDll;
        public string PickPreviewLabel => _ui.DirectUploadPickPreview;
        public string PublishLabel => _ui.DirectUploadPublish;
        public string CancelLabel => _ui.Cancel;
    }
}
