using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
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
    readonly string _apiBaseUrl;
    readonly string? _coscliPath;
    readonly ObservableCollection<ListedFile> _files = new();
    CancellationTokenSource? _cts;
    string? _sessionConfig;

    public DirectUploadDialog(AppConfig config, Action<AppConfig> saveConfig, UiStrings ui)
    {
        InitializeComponent();
        _config = config;
        _saveConfig = saveConfig;
        _ui = ui;
        DataContext = new Labels(ui);
        Title = ui.DirectUploadTitle;

        _apiBaseUrl = ResolveApiBase(config);
        _coscliPath = CoscliMirrorUploader.TryFind();

        InviteBox.Text = config.AuthorInviteCode ?? "";
        KindBox.Items.Add(new KindItem("single", ui.DirectUploadKindSingle));
        KindBox.Items.Add(new KindItem("parts", ui.DirectUploadKindParts));
        KindBox.Items.Add(new KindItem("bundle", ui.DirectUploadKindBundle));
        KindBox.DisplayMemberPath = nameof(KindItem.Label);
        KindBox.SelectedIndex = 0;
        FileList.ItemsSource = _files;

        if (string.IsNullOrWhiteSpace(_apiBaseUrl) || _coscliPath is null)
            StatusText.Text = ui.DirectUploadNotConfigured;
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        TryDelete(_sessionConfig);
        base.OnClosed(e);
    }

    void SaveCode_Click(object sender, RoutedEventArgs e)
    {
        _config.AuthorInviteCode = InviteBox.Text?.Trim() ?? "";
        _saveConfig(_config);
        StatusText.Text = _ui.DirectUploadCodeSaved;
    }

    void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationService.T("FileFilterDll"),
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
        {
            if (_files.Any(file => string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;
            var row = new ListedFile(path);
            if (SelectedKind() == "bundle" && row.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && !_files.Any(file => file.Main))
                row.Main = true;
            _files.Add(row);
        }
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureReady())
            return;

        var id = IdBox.Text?.Trim() ?? "";
        var name = NameBox.Text?.Trim() ?? "";
        var kind = SelectedKind();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(kind) || _files.Count == 0)
        {
            StatusText.Text = _ui.DirectUploadNeedFields;
            return;
        }

        _config.AuthorInviteCode = InviteBox.Text?.Trim() ?? "";
        _saveConfig(_config);

        ReplaceUploadCancellation();
        var ct = _cts!.Token;
        SetBusy(true);
        StatusText.Text = _ui.DirectUploadPublishing;
        _sessionConfig = Path.Combine(Path.GetTempPath(), "mmm-inbox-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            var submissionFiles = new List<InboxSubmissionFile>(_files.Count);
            foreach (var row in _files)
            {
                ct.ThrowIfCancellationRequested();
                var info = new FileInfo(row.FullPath);
                if (!info.Exists)
                {
                    StatusText.Text = _ui.DirectUploadNeedFields;
                    return;
                }

                var submissionName = SubmissionName(kind, row.Name);
                var role = SubmissionRole(kind, row.Name);
                var sha = InboxSubmission.HashFile(row.FullPath);
                submissionFiles.Add(new InboxSubmissionFile(role, submissionName, row.FullPath, info.Length, sha, row.Main));
            }

            if (kind == "bundle")
                submissionFiles = WithOneDefaultMain(submissionFiles);

            var built = InboxSubmission.Build(
                kind,
                id,
                name,
                string.IsNullOrWhiteSpace(SummaryBox.Text) ? null : SummaryBox.Text.Trim(),
                string.IsNullOrWhiteSpace(VersionBox.Text) ? null : VersionBox.Text.Trim(),
                submissionFiles);
            if (!built.Ok || built.Request is null)
            {
                StatusText.Text = MapError(built.Error);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var client = new InboxGrantClient(http, _apiBaseUrl);
            var invite = InviteBox.Text?.Trim() ?? "";
            var grant = await client.RequestAsync(invite, built.Request, ct).ConfigureAwait(true);
            if (!grant.Ok || grant.Credentials is null
                || string.IsNullOrWhiteSpace(grant.Bucket) || string.IsNullOrWhiteSpace(grant.Region))
            {
                StatusText.Text = MapError(grant.Error == DirectUploadErrorCode.None
                    ? DirectUploadErrorCode.Internal
                    : grant.Error);
                return;
            }

            var localByName = submissionFiles.ToDictionary(file => file.Name, StringComparer.Ordinal);
            foreach (var obj in grant.Objects)
            {
                ct.ThrowIfCancellationRequested();
                var leaf = ObjectLeaf(obj.Key);
                if (!localByName.TryGetValue(leaf, out var local))
                {
                    StatusText.Text = _ui.DirectUploadErrValidation;
                    return;
                }

                InboxCoscliSession.Write(
                    _sessionConfig,
                    grant.Credentials.TmpSecretId,
                    grant.Credentials.TmpSecretKey,
                    grant.Credentials.SessionToken,
                    grant.Bucket,
                    grant.Region);
                await InboxCoscliSession.RunAsync(
                    _coscliPath!,
                    _sessionConfig,
                    local.FullPath,
                    grant.Bucket,
                    obj.Key,
                    string.IsNullOrEmpty(obj.Sha256) ? local.Sha256 : obj.Sha256,
                    ct).ConfigureAwait(true);
            }

            var complete = await client.CompleteAsync(invite, id, ct).ConfigureAwait(true);
            if (!complete.Ok)
            {
                StatusText.Text = MapError(complete.Error);
                return;
            }

            MessageBox.Show(
                this,
                _ui.DirectUploadWaiting,
                _ui.DirectUploadTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            if (IsLoaded)
                StatusText.Text = _ui.DirectUploadFailed;
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
            TryDelete(_sessionConfig);
            SetBusy(false);
        }
    }

    bool EnsureReady()
    {
        if (string.IsNullOrWhiteSpace(_apiBaseUrl) || _coscliPath is null)
        {
            StatusText.Text = _ui.DirectUploadNotConfigured;
            return false;
        }

        if (string.IsNullOrWhiteSpace(InviteBox.Text))
        {
            StatusText.Text = _ui.DirectUploadNeedInvite;
            return false;
        }

        return true;
    }

    string SelectedKind() => (KindBox.SelectedItem as KindItem)?.Kind ?? "";

    void ReplaceUploadCancellation()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _cts = new CancellationTokenSource();
    }

    static string ResolveApiBase(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.DirectUploadApiBaseUrl))
            return config.DirectUploadApiBaseUrl.Trim().TrimEnd('/');
        return (DirectUploadDefaults.ApiBaseUrl ?? "").Trim().TrimEnd('/');
    }

    static string SubmissionRole(string kind, string fileName)
    {
        if (IsPreview(fileName))
            return "preview";
        return kind switch
        {
            "parts" => "part",
            "bundle" => "bundle",
            _ => "dll"
        };
    }

    static string SubmissionName(string kind, string fileName)
    {
        if (IsPreview(fileName))
            return "preview.png";
        if (kind == "parts")
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (PartStem.IsMatch(stem))
                return stem;
            if (PartStem.IsMatch(fileName))
                return fileName;
        }

        return fileName;
    }

    static bool IsPreview(string fileName) =>
        fileName.Equals("preview.png", StringComparison.OrdinalIgnoreCase);

    static readonly Regex PartStem = new("^\\d{4}$", RegexOptions.CultureInvariant);

    static List<InboxSubmissionFile> WithOneDefaultMain(List<InboxSubmissionFile> files)
    {
        var bundles = files.Where(file => file.Role == "bundle").ToList();
        if (bundles.Count == 0 || bundles.Any(file => file.Main))
            return files;

        var first = bundles[0];
        return files.Select(file => file == first
            ? file with { Main = true }
            : file).ToList();
    }

    static string ObjectLeaf(string? key)
    {
        var trimmed = (key ?? "").Replace('\\', '/').Trim('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    void SetBusy(bool busy)
    {
        if (!IsLoaded)
            return;
        PublishButton.IsEnabled = !busy;
        AddFileButton.IsEnabled = !busy;
    }

    static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    string MapError(DirectUploadErrorCode code) => code switch
    {
        DirectUploadErrorCode.InvalidInvite => _ui.DirectUploadErrInvalidInvite,
        DirectUploadErrorCode.ForbiddenNotOwner => _ui.DirectUploadErrForbidden,
        DirectUploadErrorCode.ValidationFailed => _ui.DirectUploadErrValidation,
        DirectUploadErrorCode.PayloadTooLarge => _ui.DirectUploadErrTooLarge,
        DirectUploadErrorCode.CatalogConflict => _ui.DirectUploadErrConflict,
        DirectUploadErrorCode.IdConflict => _ui.DirectUploadErrConflict,
        DirectUploadErrorCode.UpstreamGithub => _ui.DirectUploadErrGithub,
        DirectUploadErrorCode.NotConfigured => _ui.DirectUploadNotConfigured,
        DirectUploadErrorCode.Network => _ui.DirectUploadErrNetwork,
        DirectUploadErrorCode.EntryNotFound => _ui.DirectUploadEntryMissing,
        _ => _ui.DirectUploadFailed
    };

    public sealed class KindItem
    {
        public KindItem(string kind, string label)
        {
            Kind = kind;
            Label = label;
        }

        public string Kind { get; }
        public string Label { get; }
    }

    public sealed class ListedFile : INotifyPropertyChanged
    {
        public ListedFile(string fullPath)
        {
            FullPath = fullPath;
            Name = Path.GetFileName(fullPath);
        }

        public string FullPath { get; }
        public string Name { get; }

        bool _main;
        public bool Main
        {
            get => _main;
            set
            {
                if (_main == value)
                    return;
                _main = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Main)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

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
        public string KindLabel => _ui.DirectUploadKind;
        public string AddFileLabel => _ui.DirectUploadPickDll;
        public string PublishLabel => _ui.DirectUploadPublish;
        public string CancelLabel => _ui.Cancel;
    }
}
