using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

/// <summary>
/// zh-CN is the authoring locale, so every other .resx must carry the same key set:
/// a missing key silently falls back to Chinese text in a non-Chinese UI.
/// </summary>
public class ResourceKeyParityTests
{
    [Theory]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.ja.resx")]
    [InlineData("Strings.de.resx")]
    [InlineData("Strings.ru.resx")]
    public void Locale_has_the_same_keys_as_zh_CN(string localeFile)
    {
        var dir = ResourcesDir();
        var expected = ReadKeys(Path.Combine(dir, "Strings.resx"));
        var actual = ReadKeys(Path.Combine(dir, localeFile));

        expected.Except(actual).Should().BeEmpty(because: $"{localeFile} is missing keys");
        actual.Except(expected).Should().BeEmpty(because: $"{localeFile} has keys zh-CN does not");
    }

    [Theory]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.ja.resx")]
    [InlineData("Strings.de.resx")]
    [InlineData("Strings.ru.resx")]
    public void Locale_uses_the_same_format_placeholders_as_zh_CN(string localeFile)
    {
        var dir = ResourcesDir();
        var expected = ReadValues(Path.Combine(dir, "Strings.resx"));
        var actual = ReadValues(Path.Combine(dir, localeFile));

        foreach (var (key, value) in expected)
        {
            if (!actual.TryGetValue(key, out var translated))
                continue;
            Placeholders(translated).Should().BeEquivalentTo(
                Placeholders(value),
                because: $"{localeFile}:{key} must format the same arguments");
        }
    }

    static SortedSet<string> ReadKeys(string path) => new(ReadValues(path).Keys, StringComparer.Ordinal);

    static Dictionary<string, string> ReadValues(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Root!
            .Elements("data")
            .Where(e => e.Attribute("name") is not null && e.Attribute("type") is null)
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? "",
                StringComparer.Ordinal);
    }

    static SortedSet<string> Placeholders(string value) =>
        new(Regex.Matches(value, @"\{(\d+)[^}]*\}").Select(m => m.Groups[1].Value), StringComparer.Ordinal);

    static string ResourcesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        return Path.Combine(dir!, "src", "MechabellumModManager", "Resources");
    }
}
