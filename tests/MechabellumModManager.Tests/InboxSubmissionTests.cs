using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FluentAssertions;
using MechabellumModManager.Services;

public class InboxSubmissionTests
{
    const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Single_dll_builds_one_dll_role()
    {
        var built = InboxSubmission.Build(
            "single",
            "quick-mark",
            "Quick Mark",
            "a mark",
            "1.0",
            new[]
            {
                new InboxSubmissionFile("dll", "QuickMark.dll", @"C:\mods\QuickMark.dll", 10, Sha, false)
            },
            "author@example.com");

        built.Ok.Should().BeTrue();
        built.Error.Should().Be(DirectUploadErrorCode.None);
        built.Request.Should().NotBeNull();
        built.Request!.Kind.Should().Be("single");
        built.Request.Id.Should().Be("quick-mark");
        built.Request.Name.Should().Be("Quick Mark");
        built.Request.Files.Should().ContainSingle();
        var file = built.Request.Files[0];
        file.Role.Should().Be("dll");
        file.Name.Should().Be("QuickMark.dll");
        file.Size.Should().Be(10);
        file.Sha256.Should().Be(Sha);

        var node = JsonSerializerSerialize(built.Request);
        node["kind"]!.GetValue<string>().Should().Be("single");
        node["files"]![0]!["role"]!.GetValue<string>().Should().Be("dll");
        node["feedback"]!.GetValue<string>().Should().Be("author@example.com");
        var json = node.ToJsonString();
        json.Should().Contain("QuickMark.dll");
        json.Should().NotContain(@"C:\\mods");
        json.Should().NotContain("FullPath");
    }

    [Fact]
    public void Parts_missing_0001_fail()
    {
        var built = InboxSubmission.Build(
            "parts",
            "quick-mark",
            "Quick Mark",
            null,
            null,
            new[]
            {
                new InboxSubmissionFile("part", "0000", @"C:\mods\0000", 10, Sha, false),
                new InboxSubmissionFile("part", "0002", @"C:\mods\0002", 12, Sha, false)
            },
            "author@example.com");

        built.Ok.Should().BeFalse();
        built.Error.Should().Be(DirectUploadErrorCode.ValidationFailed);
        built.Request.Should().BeNull();
    }

    [Fact]
    public void Bundle_with_two_main_files_fails()
    {
        var built = InboxSubmission.Build(
            "bundle",
            "quick-mark",
            "Quick Mark",
            "",
            "",
            new[]
            {
                new InboxSubmissionFile("bundle", "QuickMark.dll", @"C:\a\QuickMark.dll", 10, Sha, true),
                new InboxSubmissionFile("bundle", "Extra.dll", @"C:\a\Extra.dll", 8, Sha, true)
            },
            "author@example.com");

        built.Ok.Should().BeFalse();
        built.Error.Should().Be(DirectUploadErrorCode.ValidationFailed);
    }

    [Fact]
    public void File_length_of_100_mib_fails()
    {
        var built = InboxSubmission.Build(
            "single",
            "quick-mark",
            "Quick Mark",
            null,
            null,
            new[]
            {
                new InboxSubmissionFile(
                    "dll",
                    "QuickMark.dll",
                    @"C:\mods\QuickMark.dll",
                    100L * 1024 * 1024,
                    Sha,
                    false)
            },
            "author@example.com");

        built.Ok.Should().BeFalse();
        built.Error.Should().Be(DirectUploadErrorCode.PayloadTooLarge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:not-an-email")]
    public void Hidden_or_blank_feedback_fails(string? feedback)
    {
        var built = InboxSubmission.Build(
            "single",
            "quick-mark",
            "Quick Mark",
            "a mark",
            "1.0",
            new[]
            {
                new InboxSubmissionFile("dll", "QuickMark.dll", @"C:\mods\QuickMark.dll", 10, Sha, false)
            },
            feedback);

        built.Ok.Should().BeFalse();
        built.Error.Should().Be(DirectUploadErrorCode.ValidationFailed);
        built.Request.Should().BeNull();
    }

    [Theory]
    [InlineData("https://example.com/form")]
    [InlineData("QQ 12345")]
    public void Usable_feedback_is_copied_into_the_request(string feedback)
    {
        var built = InboxSubmission.Build(
            "single",
            "quick-mark",
            "Quick Mark",
            null,
            null,
            new[]
            {
                new InboxSubmissionFile("dll", "QuickMark.dll", @"C:\mods\QuickMark.dll", 10, Sha, false)
            },
            feedback);

        built.Ok.Should().BeTrue();
        built.Request!.Feedback.Should().Be(feedback);
    }

    [Fact]
    public void HashFile_matches_sha256_without_buffering_the_whole_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-inbox-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "QuickMark.dll");
            var payload = new byte[] { 1, 2, 3, 4, 9 };
            File.WriteAllBytes(path, payload);
            var expected = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

            InboxSubmission.HashFile(path).Should().Be(expected);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp */ }
        }
    }

    static JsonObject JsonSerializerSerialize(object request)
    {
        var node = System.Text.Json.JsonSerializer.SerializeToNode(request) as JsonObject;
        node.Should().NotBeNull();
        return node!;
    }
}
