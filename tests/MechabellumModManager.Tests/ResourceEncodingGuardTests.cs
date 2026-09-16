using System.Text;
using FluentAssertions;

/// <summary>
/// Catches encoding corruption that replaces CJK/Cyrillic with U+FFFD (�)
/// when a .resx is saved under the wrong code page.
/// </summary>
public class ResourceEncodingGuardTests
{
    const char Replacement = '\uFFFD';

    [Fact]
    public void Detection_flags_U_FFFD_in_sample_text()
    {
        CountReplacementChars($"隐藏{Replacement}控制台").Should().Be(1);
        CountReplacementChars("隐藏 Melon 控制台").Should().Be(0);
    }

    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.ja.resx")]
    [InlineData("Strings.de.resx")]
    [InlineData("Strings.ru.resx")]
    public void Strings_resx_contains_no_replacement_characters(string fileName)
    {
        var path = Path.Combine(ResourcesDir(), fileName);
        File.Exists(path).Should().BeTrue(because: $"{fileName} must exist");

        // Invalid UTF-8 byte sequences must fail hard, not silently become �.
        var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var text = File.ReadAllText(path, utf8Strict);

        var count = CountReplacementChars(text);
        count.Should().Be(0, because: $"{fileName} must not contain U+FFFD (encoding corruption)");
    }

    static int CountReplacementChars(string text)
    {
        var n = 0;
        foreach (var c in text)
        {
            if (c == Replacement)
                n++;
        }
        return n;
    }

    static string ResourcesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        return Path.Combine(dir!, "src", "MechabellumModManager", "Resources");
    }
}
