using System.Xml.Linq;
using FluentAssertions;

public class SummaryCopyResourceTests
{
    [Theory]
    [InlineData("Strings.resx", "功能与用法")]
    [InlineData("Strings.en.resx", "How to use")]
    [InlineData("Strings.de.resx", "Bedienung")]
    [InlineData("Strings.ja.resx", "機能と使い方")]
    [InlineData("Strings.ru.resx", "Как пользоваться")]
    public void Summary_labels_say_how_to_use(string file, string expected)
    {
        var values = ReadValues(file);
        values["LabelSummary"].Should().Be(expected);
        values["DirectUploadSummary"].Should().Be(expected);
    }

    [Theory]
    [InlineData("Strings.resx", "第一句", "更新日志")]
    [InlineData("Strings.en.resx", "first sentence", "changelog")]
    [InlineData("Strings.de.resx", "erste Satz", "Änderungsprotokoll")]
    [InlineData("Strings.ja.resx", "最初の文", "更新履歴")]
    [InlineData("Strings.ru.resx", "Первое предложение", "список изменений")]
    public void Author_standard_puts_usage_first(string file, string usageFirst, string noChangelog)
    {
        var body = ReadValues(file)["AuthorCatalogBody"];
        body.Should().Contain(usageFirst);
        body.Should().Contain(noChangelog);
        body.Should().Contain("minManagerVersion");
        body.Should().NotContain("1.3.13");
    }

    static Dictionary<string, string> ReadValues(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        var doc = XDocument.Load(Path.Combine(dir!, "src", "MechabellumModManager", "Resources", file));
        return doc.Root!
            .Elements("data")
            .Where(e => e.Attribute("name") is not null && e.Attribute("type") is null)
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? "", StringComparer.Ordinal);
    }
}
