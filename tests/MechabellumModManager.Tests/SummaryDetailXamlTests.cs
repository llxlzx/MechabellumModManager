using FluentAssertions;

public class SummaryDetailXamlTests
{
    [Theory]
    [InlineData("SelectedCatalogMod")]
    [InlineData("SelectedLibraryMod")]
    public void Detail_summary_is_labelled_and_hidden_when_empty(string selected)
    {
        var xaml = File.ReadAllText(MainWindowPath());
        var visibility = $"Visibility=\"{{Binding {selected}.Summary, Converter={{StaticResource NonEmptyVis}}}}\"";
        var start = xaml.IndexOf(visibility, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, because: $"the {selected} summary line must hide when the text is empty");

        var end = xaml.IndexOf("</TextBlock>", start, StringComparison.Ordinal);
        var block = xaml[start..end];
        block.Should().Contain("<Run Text=\"{Binding Ui.LabelSummary, Mode=OneWay}\" />");
        block.Should().Contain($"<Run Text=\"{{Binding {selected}.Summary, Mode=OneWay}}\" />");
    }

    static string MainWindowPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        return Path.Combine(dir!, "src", "MechabellumModManager", "MainWindow.xaml");
    }
}
