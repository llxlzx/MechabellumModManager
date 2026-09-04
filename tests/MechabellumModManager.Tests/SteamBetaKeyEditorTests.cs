using FluentAssertions;
using MechabellumModManager.Services;

public class SteamBetaKeyEditorTests
{
    const string SampleAcf = """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
	}
}
""";

    [Fact]
    public void SetBetaKey_inserts_under_UserConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, SampleAcf);
            var probe = new FakeProbe { SteamRunning = false };
            var editor = new SteamBetaKeyEditor(probe);

            var result = editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak"));
            result.Success.Should().BeTrue();
            editor.ReadBetaKey(acf).Should().Be("publicbeta");
            Directory.GetFiles(Path.Combine(dir, "bak")).Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Refuses_when_steam_running()
    {
        var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = true });
        var result = editor.BackupAndSetBetaKey("x", "y", "z");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void FindAppManifest_rejects_wrong_appid_name()
    {
        var game = @"D:\SteamLibrary\steamapps\common\Mechabellum";
        var path = SteamBetaKeyEditor.FindAppManifestPath(game);
        Path.GetFileName(path).Should().Be("appmanifest_669330.acf");
    }

    [Fact]
    public void FindAppManifest_resolves_beside_steamapps_common()
    {
        var game = @"D:\SteamLibrary\steamapps\common\Mechabellum";
        var path = SteamBetaKeyEditor.FindAppManifestPath(game);
        Path.GetFullPath(path).Should().Be(Path.GetFullPath(@"D:\SteamLibrary\steamapps\appmanifest_669330.acf"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetBetaKey_removes_BetaKey_for_official(string? betaKey)
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
		"BetaKey"		"publicbeta"
	}
}
""");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            var result = editor.BackupAndSetBetaKey(acf, betaKey, Path.Combine(dir, "bak"));
            result.Success.Should().BeTrue();
            editor.ReadBetaKey(acf).Should().BeNull();
            File.ReadAllText(acf).Should().Contain("\"language\"").And.NotContain("BetaKey");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetBetaKey_preserves_other_keys_and_utf8()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, """
"AppState"
{
	"appid"		"669330"
	"name"		"钢铁指挥官"
	"UserConfig"
	{
		"language"		"schinese"
		"DisabledDLC"		"none"
	}
	"MountedConfig"
	{
		"language"		"schinese"
	}
}
""");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak")).Success.Should().BeTrue();

            var text = File.ReadAllText(acf);
            text.Should().Contain("钢铁指挥官");
            text.Should().Contain("\"language\"\t\t\"schinese\"");
            text.Should().Contain("\"DisabledDLC\"\t\t\"none\"");
            text.Should().Contain("\"MountedConfig\"");
            RegexCount(text, "\"BetaKey\"").Should().Be(2);
            editor.ReadBetaKey(acf).Should().Be("publicbeta");
            editor.ReadMountedBetaKey(acf).Should().Be("publicbeta");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetBetaKey_syncs_MountedConfig_BetaKey()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
		"BetaKey"		"public_test"
	}
	"MountedConfig"
	{
		"language"		"english"
		"BetaKey"		"public_test"
	}
}
""");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            editor.BackupAndSetBetaKey(acf, null, Path.Combine(dir, "bak")).Success.Should().BeTrue();

            var text = File.ReadAllText(acf);
            text.Should().NotContain("BetaKey");
            editor.ReadBetaKey(acf).Should().BeNull();
            editor.ReadMountedBetaKey(acf).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetBetaKey_throws_when_appid_is_not_669330()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, """
"AppState"
{
	"appid"		"440"
	"UserConfig"
	{
		"language"		"english"
	}
}
""");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });
            var act = () => editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak"));
            act.Should().Throw<InvalidOperationException>();
            File.ReadAllText(acf).Should().NotContain("BetaKey");
            Directory.Exists(Path.Combine(dir, "bak")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetBetaKey_does_not_touch_other_appid_filename()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_440.acf");
            var original = SampleAcf.Replace("669330", "440");
            File.WriteAllText(acf, original);
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            var result = editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak"));
            result.Success.Should().BeFalse();
            File.ReadAllText(acf).Should().Be(original);
            Directory.Exists(Path.Combine(dir, "bak")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetBetaKey_replaces_existing_BetaKey()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
		"BetaKey"		"oldbeta"
	}
}
""");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });
            editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak")).Success.Should().BeTrue();
            editor.ReadBetaKey(acf).Should().Be("publicbeta");
            File.ReadAllText(acf).Should().NotContain("oldbeta");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Backup_is_written_before_edit_and_matches_original()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, SampleAcf);
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak")).Success.Should().BeTrue();

            var backups = Directory.GetFiles(Path.Combine(dir, "bak"));
            backups.Should().NotBeEmpty();
            File.ReadAllText(backups[0]).Should().Be(SampleAcf);
            File.ReadAllText(acf).Should().NotBe(SampleAcf);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReadBetaKey_returns_null_when_absent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, SampleAcf);
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });
            editor.ReadBetaKey(acf).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ProcessProbe_can_be_injected()
    {
        var editor = new SteamBetaKeyEditor(new ProcessProbe());
        editor.Should().NotBeNull();
    }

    [Fact]
    public void SetBetaKey_replaces_existing_on_crlf_acf()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var acf = Path.Combine(dir, "appmanifest_669330.acf");
            File.WriteAllText(acf, "\"AppState\"\r\n{\r\n\t\"appid\"\t\t\"669330\"\r\n\t\"UserConfig\"\r\n\t{\r\n\t\t\"language\"\t\t\"english\"\r\n\t\t\"BetaKey\"\t\t\"oldbeta\"\r\n\t}\r\n}\r\n");
            var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = false });

            editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak")).Success.Should().BeTrue();
            editor.ReadBetaKey(acf).Should().Be("publicbeta");
            var text = File.ReadAllText(acf);
            text.Should().Contain("\r\n");
            text.Should().NotContain("oldbeta");
            RegexCount(text, "\"BetaKey\"").Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    static int RegexCount(string text, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var i = text.IndexOf(value, start, StringComparison.Ordinal);
            if (i < 0) return count;
            count++;
            start = i + value.Length;
        }
    }
}

sealed class FakeProbe : ISteamRunningProbe
{
    public bool SteamRunning { get; set; }
    public bool IsSteamRunning() => SteamRunning;
}
