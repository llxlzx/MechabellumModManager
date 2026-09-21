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
    readonly string? _mirrorBaseUrl;
    readonly string? _coscliPath;

    public DirectUploadDialog(AppConfig config, Action<AppConfig> saveConfig, UiStrings ui)
    {
        InitializeComponent();
        _config = config;
        _saveConfig = saveConfig;
        _ui = ui;
        DataContext = new Labels(ui);
        Title = ui.DirectUploadTitle;

        _mirrorBaseUrl = string.IsNullOrWhiteSpace(config.MirrorBaseUrl)
            ? (config.MirrorBaseUrl is null ? DomesticMirrorDefaults.BaseUrl : "")
            : config.MirrorBaseUrl.Trim().TrimEnd('/');
        _coscliPath = CoscliMirrorUploader.TryFind();

        InviteBox.Text = config.AuthorInviteCode ?? "";
        SecretIdBox.Text = CoscliUserConfig.ReadSecretId() ?? "";
        if (string.IsNullOrWhiteSpace(_mirrorBaseUrl) || MirrorCatalogPublisher.BucketFromMirrorUrl(_mirrorBaseUrl) is null || _coscliPath is null)
            StatusText.Text = ui.DirectUploadNotConfigured;
        else if (!CoscliUserConfig.HasSecret())
            StatusText.Text = ui.DirectUploadNeedSecret;
    }

    void SaveCode_Click(object sender, RoutedEventArgs e)
    {
        _config.AuthorInviteCode = InviteBox.Text?.Trim() ?? "";
        _saveConfig(_config);
        if (!TrySaveSecret(out var secretStatus))
        {
            StatusText.Text = secretStatus;
            return;
        }

        StatusText.Text = string.IsNullOrEmpty(secretStatus) ? _ui.DirectUploadCodeSaved : secretStatus;
    }

    bool TrySaveSecret(out string status)
    {
        status = "";
        var secretId = SecretIdBox.Text?.Trim() ?? "";
        var secretKey = SecretKeyBox.Password?.Trim() ?? "";
        var hasNewKey = secretKey.Length > 0;
        if (!hasNewKey)
        {
            if (secretId.Length > 0 && !CoscliUserConfig.HasSecret())
            {
                status = _ui.DirectUploadNeedSecret;
                return false;
            }

            return CoscliUserConfig.HasSecret() || secretId.Length == 0;
        }

        if (secretId.Length == 0)
        {
            status = _ui.DirectUploadNeedSecret;
            return false;
        }

        var bucket = MirrorCatalogPublisher.BucketFromMirrorUrl(_mirrorBaseUrl);
        var region = MirrorCatalogPublisher.RegionFromMirrorUrl(_mirrorBaseUrl);
        if (bucket is null || region is null)
        {
            status = _ui.DirectUploadNotConfigured;
            return false;
        }

        CoscliUserConfig.Save(CoscliUserConfig.DefaultPath, secretId, secretKey, bucket, region);
        SecretKeyBox.Password = "";
        status = _ui.DirectUploadSecretSaved;
        return true;
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
        if (!EnsureReady())
            return;

        var id = IdBox.Text?.Trim() ?? "";
        var name = NameBox.Text?.Trim() ?? "";
        var dll = DllPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dll) || !File.Exists(dll))
        {
            StatusText.Text = _ui.DirectUploadNeedFields;
            return;
        }

        _config.AuthorInviteCode = InviteBox.Text?.Trim() ?? "";
        _saveConfig(_config);

        var updateCatalog = UpdateCatalogBox.IsChecked == true;
        SetBusy(true);
        StatusText.Text = _ui.DirectUploadPublishing;
        string? entryTemp = null;
        string? catalogTemp = null;
        try
        {
            var input = new MirrorPublishInput
            {
                Id = id,
                Name = name,
                Summary = string.IsNullOrWhiteSpace(SummaryBox.Text) ? null : SummaryBox.Text.Trim(),
                Version = string.IsNullOrWhiteSpace(VersionBox.Text) ? null : VersionBox.Text.Trim(),
                DllPath = dll,
                PreviewPath = string.IsNullOrWhiteSpace(PreviewPathBox.Text) ? null : PreviewPathBox.Text.Trim()
            };
            MirrorPublishPlan plan;
            if (updateCatalog)
            {
                using var http = CreateHttp();
                var catalog = await MirrorCatalogPublisher.DownloadCatalogAsync(http, _mirrorBaseUrl!, CancellationToken.None)
                    .ConfigureAwait(true);
                plan = MirrorCatalogPublisher.Plan(catalog, input);
            }
            else
            {
                plan = MirrorCatalogPublisher.Plan(null, input);
            }

            entryTemp = Path.Combine(Path.GetTempPath(), "mmm-entry-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(entryTemp, plan.EntryJson).ConfigureAwait(true);

            var uploader = new CoscliMirrorUploader(_coscliPath!, MirrorCatalogPublisher.BucketFromMirrorUrl(_mirrorBaseUrl)!);
            foreach (var upload in plan.Uploads)
                await uploader.UploadAsync(upload.LocalPath, upload.RemoteKey, CancellationToken.None).ConfigureAwait(true);
            await uploader.UploadAsync(entryTemp, plan.EntryKey, CancellationToken.None).ConfigureAwait(true);

            if (updateCatalog)
            {
                catalogTemp = Path.Combine(Path.GetTempPath(), "mmm-catalog-" + Guid.NewGuid().ToString("N") + ".json");
                await File.WriteAllTextAsync(catalogTemp, plan.CatalogJson).ConfigureAwait(true);
                try
                {
                    await uploader.UploadAsync(catalogTemp, MirrorCatalogPublisher.CatalogKey, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    StatusText.Text = _ui.DirectUploadCatalogDenied + ": " + ex.Message;
                    return;
                }

                MessageBox.Show(
                    this,
                    _ui.DirectUploadSuccess + "\n\n" + _ui.DirectUploadMirrorLagHint,
                    _ui.DirectUploadTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    this,
                    _ui.DirectUploadFilesUploaded + "\n\n" + _ui.DirectUploadMirrorLagHint,
                    _ui.DirectUploadTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            DialogResult = true;
        }
        catch (MirrorPublishException ex)
        {
            StatusText.Text = MapError(ex.Code);
        }
        catch (Exception ex)
        {
            StatusText.Text = _ui.DirectUploadFailed + ": " + ex.Message;
        }
        finally
        {
            TryDelete(entryTemp);
            TryDelete(catalogTemp);
            SetBusy(false);
        }
    }

    async void MergeCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureReady())
            return;

        var id = IdBox.Text?.Trim() ?? "";
        if (!MirrorCatalogPublisher.IsSafeId(id))
        {
            StatusText.Text = _ui.DirectUploadNeedModId;
            return;
        }

        SetBusy(true);
        StatusText.Text = _ui.DirectUploadPublishing;
        string? catalogTemp = null;
        try
        {
            using var http = CreateHttp();
            var entry = await MirrorCatalogPublisher.DownloadEntryAsync(http, _mirrorBaseUrl!, id, CancellationToken.None)
                .ConfigureAwait(true);
            var catalog = await MirrorCatalogPublisher.DownloadCatalogAsync(http, _mirrorBaseUrl!, CancellationToken.None)
                .ConfigureAwait(true);
            var merged = MirrorCatalogPublisher.MergeListedEntry(catalog, entry, id);
            catalogTemp = Path.Combine(Path.GetTempPath(), "mmm-catalog-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(catalogTemp, merged).ConfigureAwait(true);

            var uploader = new CoscliMirrorUploader(_coscliPath!, MirrorCatalogPublisher.BucketFromMirrorUrl(_mirrorBaseUrl)!);
            await uploader.UploadAsync(catalogTemp, MirrorCatalogPublisher.CatalogKey, CancellationToken.None)
                .ConfigureAwait(true);

            MessageBox.Show(
                this,
                _ui.DirectUploadMergeSuccess + "\n\n" + _ui.DirectUploadMirrorLagHint,
                _ui.DirectUploadTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (MirrorPublishException ex) when (ex.Code is DirectUploadErrorCode.EntryNotFound or DirectUploadErrorCode.ValidationFailed)
        {
            StatusText.Text = MapError(ex.Code);
        }
        catch (Exception ex)
        {
            StatusText.Text = _ui.DirectUploadCatalogWriteFailed + ": " + ex.Message;
        }
        finally
        {
            TryDelete(catalogTemp);
            SetBusy(false);
        }
    }

    bool EnsureReady()
    {
        if (string.IsNullOrWhiteSpace(_mirrorBaseUrl) || _coscliPath is null
            || MirrorCatalogPublisher.BucketFromMirrorUrl(_mirrorBaseUrl) is null)
        {
            StatusText.Text = _ui.DirectUploadNotConfigured;
            return false;
        }

        if (!TrySaveSecret(out var secretStatus))
        {
            StatusText.Text = secretStatus;
            return false;
        }

        if (!CoscliUserConfig.HasSecret())
        {
            StatusText.Text = _ui.DirectUploadNeedSecret;
            return false;
        }

        return true;
    }

    static HttpClient CreateHttp() => new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    void SetBusy(bool busy)
    {
        PublishButton.IsEnabled = !busy;
        MergeButton.IsEnabled = !busy;
    }

    static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try { File.Delete(path); } catch { /* temp json */ }
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
        DirectUploadErrorCode.EntryNotFound => _ui.DirectUploadEntryMissing,
        _ => _ui.DirectUploadFailed
    };

    sealed class Labels
    {
        readonly UiStrings _ui;
        public Labels(UiStrings ui) => _ui = ui;
        public string TitleText => _ui.DirectUploadTitle;
        public string InviteLabel => _ui.DirectUploadInviteCode;
        public string SaveCodeLabel => _ui.DirectUploadSaveCode;
        public string SecretIdLabel => _ui.DirectUploadSecretId;
        public string SecretKeyLabel => _ui.DirectUploadSecretKey;
        public string SecretHint => _ui.DirectUploadSecretHint;
        public string ModIdLabel => _ui.DirectUploadModId;
        public string NameLabel => _ui.DirectUploadName;
        public string SummaryLabel => _ui.DirectUploadSummary;
        public string VersionLabel => _ui.DirectUploadVersion;
        public string PickDllLabel => _ui.DirectUploadPickDll;
        public string PickPreviewLabel => _ui.DirectUploadPickPreview;
        public string UpdateCatalogLabel => _ui.DirectUploadUpdateCatalog;
        public string UpdateCatalogHint => _ui.DirectUploadUpdateCatalogHint;
        public string MergeCatalogLabel => _ui.DirectUploadMergeCatalog;
        public string PublishLabel => _ui.DirectUploadPublish;
        public string CancelLabel => _ui.Cancel;
    }
}
