using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class ManagerUpdateDialog : Window
{
    readonly ManagerUpdatePrompt _prompt;
    readonly ManagerSetupDownloader _downloader;
    readonly bool _canDownload;
    CancellationTokenSource? _downloadCts;
    bool _downloading;

    public ManagerUpdateUiResult Result { get; private set; } =
        new(ManagerUpdateUiAction.Skip);

    public ManagerUpdateDialog(ManagerUpdatePrompt prompt, ManagerSetupDownloader downloader)
    {
        InitializeComponent();
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _canDownload = ExternalUrlPolicy.IsDownloadableSetupUrl(prompt.SetupUrl);

        Title = LocalizationService.T("ManagerUpdateDialogTitle");
        TitleText.Text = string.Format(
            LocalizationService.T("ManagerUpdateFoundFormat"),
            prompt.LocalVersion,
            prompt.RemoteVersion);
        NotesText.Text = string.IsNullOrWhiteSpace(prompt.Notes)
            ? LocalizationService.T("ManagerUpdateNoNotes")
            : prompt.Notes;

        SkipButton.Content = LocalizationService.T("ManagerUpdateSkip");
        if (_canDownload)
        {
            PrimaryButton.Content = LocalizationService.T("ManagerUpdateNow");
            PrimaryButton.IsDefault = true;
        }
        else
        {
            PrimaryButton.Content = LocalizationService.T("ManagerUpdateOpenReleases");
            PrimaryButton.IsDefault = true;
        }

        CancelDownloadButton.Content = LocalizationService.T("ManagerUpdateCancelDownload");
        Closing += OnClosing;
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_downloading)
        {
            e.Cancel = true;
            return;
        }

        // Closing the dialog without an explicit primary action counts as skip.
        if (DialogResult != true)
            Result = new ManagerUpdateUiResult(ManagerUpdateUiAction.Skip);
    }

    async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_downloading)
            return;

        if (!_canDownload)
        {
            Result = new ManagerUpdateUiResult(ManagerUpdateUiAction.OpenBrowseUrl);
            DialogResult = true;
            return;
        }

        await RunDownloadAsync().ConfigureAwait(true);
    }

    void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_downloading)
            return;
        Result = new ManagerUpdateUiResult(ManagerUpdateUiAction.Skip);
        DialogResult = false;
    }

    void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        try { _downloadCts?.Cancel(); } catch { /* ignore */ }
    }

    async Task RunDownloadAsync()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        _downloading = true;
        _downloadCts = new CancellationTokenSource();
        PrimaryButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;
        DownloadProgress.IsIndeterminate = true;
        DownloadProgress.Value = 0;

        var progress = new Progress<ManagerSetupDownloadProgress>(p =>
        {
            ProgressText.Text = p.Message;
            if (p.Percent is { } pct)
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = pct;
            }
        });

        try
        {
            var path = await _downloader.DownloadAsync(
                _prompt.SetupUrl!,
                _prompt.RemoteVersion,
                progress,
                _downloadCts.Token).ConfigureAwait(true);

            _downloading = false;
            Result = new ManagerUpdateUiResult(ManagerUpdateUiAction.LaunchSetup, path);
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            // Cancel ≠ skip: return to pre-download state.
            ResetDownloadUi();
            ProgressText.Text = LocalizationService.T("ManagerUpdateDownloadCancelled");
            ProgressText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ResetDownloadUi();
            ErrorText.Text = string.Format(LocalizationService.T("ManagerUpdateDownloadFailed"), ex.Message);
            ErrorText.Visibility = Visibility.Visible;
            PrimaryButton.Content = LocalizationService.T("ManagerUpdateRetry");
        }
        finally
        {
            _downloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    void ResetDownloadUi()
    {
        PrimaryButton.IsEnabled = true;
        SkipButton.IsEnabled = true;
        CancelDownloadButton.Visibility = Visibility.Collapsed;
        DownloadProgress.Visibility = Visibility.Collapsed;
        DownloadProgress.IsIndeterminate = false;
        DownloadProgress.Value = 0;
        PrimaryButton.Content = LocalizationService.T("ManagerUpdateNow");
    }
}
