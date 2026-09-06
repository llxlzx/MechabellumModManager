using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class SteamAcfSnapshotSanitizerTests
{
    static string AcfWithKeys(string? userBetaKey, bool includeMounted, string? mountedBetaKey)
    {
        var userInner = userBetaKey is null
            ? ""
            : $"\t\t\"BetaKey\"\t\t\"{userBetaKey}\"\n";

        var mountedBlock = "";
        if (includeMounted)
        {
            var mountedInner = mountedBetaKey is null
                ? ""
                : $"\t\t\"BetaKey\"\t\t\"{mountedBetaKey}\"\n";
            mountedBlock =
                "\t\"MountedConfig\"\n" +
                "\t{\n" +
                mountedInner +
                "\t}\n";
        }

        return
            "\"AppState\"\n" +
            "{\n" +
            "\t\"appid\"\t\t\"669330\"\n" +
            "\t\"UserConfig\"\n" +
            "\t{\n" +
            userInner +
            "\t}\n" +
            mountedBlock +
            "}\n";
    }

    [Fact]
    public void Official_valid_when_no_beta_keys()
    {
        var text = AcfWithKeys(userBetaKey: null, includeMounted: false, mountedBetaKey: null);
        SteamAcfSnapshotSanitizer.IsValidSnapshot(text, GameBranch.Official, "public_test")
            .Should().BeTrue();
    }

    [Fact]
    public void Official_invalid_when_user_has_public_test()
    {
        var text = AcfWithKeys("public_test", includeMounted: true, "public_test");
        SteamAcfSnapshotSanitizer.IsValidSnapshot(text, GameBranch.Official, "public_test")
            .Should().BeFalse();
    }

    [Fact]
    public void Official_invalid_when_mounted_has_key_even_if_user_blank()
    {
        var text = AcfWithKeys(userBetaKey: null, includeMounted: true, mountedBetaKey: "public_test");
        SteamAcfSnapshotSanitizer.IsValidSnapshot(text, GameBranch.Official, "public_test")
            .Should().BeFalse();
    }

    [Fact]
    public void Beta_valid_when_keys_match_branch_name()
    {
        var text = AcfWithKeys("public_test", includeMounted: true, "public_test");
        SteamAcfSnapshotSanitizer.IsValidSnapshot(text, GameBranch.Beta, "public_test")
            .Should().BeTrue();
    }

    [Fact]
    public void Beta_invalid_when_keys_blank()
    {
        var text = AcfWithKeys(userBetaKey: null, includeMounted: false, mountedBetaKey: null);
        SteamAcfSnapshotSanitizer.IsValidSnapshot(text, GameBranch.Beta, "public_test")
            .Should().BeFalse();
    }

    [Fact]
    public void SanitizeDirectory_deletes_dirty_official_keeps_clean_beta()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-sanitize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "official.acf"),
                AcfWithKeys("public_test", includeMounted: true, "public_test"));
            File.WriteAllText(Path.Combine(dir, "beta.acf"),
                AcfWithKeys("public_test", includeMounted: true, "public_test"));

            var result = SteamAcfSnapshotSanitizer.SanitizeDirectory(dir, "public_test");

            result.DeletedFiles.Should().ContainSingle(f =>
                f.EndsWith("official.acf", StringComparison.OrdinalIgnoreCase));
            File.Exists(Path.Combine(dir, "official.acf")).Should().BeFalse();
            File.Exists(Path.Combine(dir, "beta.acf")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SanitizeDirectory_missing_dir_is_noop()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mmm-acf-missing-" + Guid.NewGuid().ToString("N"));
        var result = SteamAcfSnapshotSanitizer.SanitizeDirectory(missing, "public_test");
        result.DeletedFiles.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeDirectory_deletes_same_buildid_pair()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-sanitize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "official.acf"), SettledAcf(buildId: "25139974", betaKey: null));
            File.WriteAllText(Path.Combine(dir, "beta.acf"), SettledAcf(buildId: "25139974", betaKey: "public_test"));

            var result = SteamAcfSnapshotSanitizer.SanitizeDirectory(dir, "public_test");

            result.DeletedFiles.Should().HaveCount(2);
            File.Exists(Path.Combine(dir, "official.acf")).Should().BeFalse();
            File.Exists(Path.Combine(dir, "beta.acf")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SanitizeDirectory_keeps_distinct_buildid_pair()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-sanitize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "official.acf"), SettledAcf(buildId: "100", betaKey: null));
            File.WriteAllText(Path.Combine(dir, "beta.acf"), SettledAcf(buildId: "200", betaKey: "public_test"));

            var result = SteamAcfSnapshotSanitizer.SanitizeDirectory(dir, "public_test");

            result.DeletedFiles.Should().BeEmpty();
            File.Exists(Path.Combine(dir, "official.acf")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "beta.acf")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    static string SettledAcf(string buildId, string? betaKey)
    {
        var betaLine = betaKey is null ? "" : $"\t\t\"BetaKey\"\t\t\"{betaKey}\"\n";
        return
            "\"AppState\"\n" +
            "{\n" +
            "\t\"appid\"\t\t\"669330\"\n" +
            "\t\"StateFlags\"\t\t\"4\"\n" +
            $"\t\"buildid\"\t\t\"{buildId}\"\n" +
            $"\t\"TargetBuildID\"\t\t\"{buildId}\"\n" +
            "\t\"BytesToDownload\"\t\t\"0\"\n" +
            "\t\"BytesDownloaded\"\t\t\"0\"\n" +
            "\t\"UserConfig\"\n" +
            "\t{\n" +
            betaLine +
            "\t}\n" +
            "\t\"MountedConfig\"\n" +
            "\t{\n" +
            betaLine +
            "\t}\n" +
            "}\n";
    }
}
