using FluentAssertions;

public class DirectUploadDialogSourceTests
{
    static readonly string[] Banned =
    {
        "SecretKeyBox",
        "SecretIdBox",
        "UpdateCatalogBox",
        "MergeCatalog_Click",
        "CoscliUserConfig.Save",
        "CatalogKey"
    };

    [Fact]
    public void Dialog_sources_do_not_keep_secret_fields_or_catalog_merge()
    {
        var text = DialogXaml() + "\n" + DialogCode();
        foreach (var name in Banned)
            text.Should().NotContain(name);

        text.Should().Contain("x:Name=\"KindBox\"");
        text.Should().Contain("x:Name=\"FileList\"");
        text.Should().Contain("InboxSubmission.Build");
        text.Should().Contain("RequestAsync");
        text.Should().Contain("RunAsync");
        text.Should().Contain("CompleteAsync");
        text.Should().Contain("DirectUploadWaiting");
        text.Should().NotContain("catalog.json");
        text.Should().NotContain("DownloadCatalog");
        text.Should().Contain("CancellationTokenSource");
    }

    [Fact]
    public void Submission_hashes_from_a_stream()
    {
        var path = Path.Combine(RepoRoot(), "src", "MechabellumModManager", "Services", "InboxSubmission.cs");
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().NotContain("ReadAllBytes");
        text.Should().Contain("SHA256");
    }

    static string DialogXaml() => File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "MechabellumModManager", "Dialogs", "DirectUploadDialog.xaml"));

    static string DialogCode() => File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "MechabellumModManager", "Dialogs", "DirectUploadDialog.xaml.cs"));

    static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue();
        return dir!;
    }
}
