using FluentAssertions;

namespace MechabellumModManager.Tests;

/// <summary>
/// The CN / Global Setup flavors differ only in what the Inno script writes and passes on.
/// Pascal Script has no test harness, so these pin the script text the flavors depend on.
/// </summary>
public class InstallerScriptContractTests
{
    [Fact]
    public void Flavor_defaults_to_cn_and_rejects_any_other_value()
    {
        var iss = Iss();

        iss.Should().Contain("#ifndef MMM_FLAVOR");
        iss.Should().Contain("#define MMM_FLAVOR \"cn\"");
        iss.Should().Contain("#if MMM_FLAVOR == \"cn\"");
        iss.Should().Contain("#elif MMM_FLAVOR == \"global\"");
        iss.Should().Contain("#error");
    }

    [Fact]
    public void Each_flavor_gets_its_own_setup_file_name()
    {
        Iss().Should().Contain("OutputBaseFilename=MechabellumModManager_Setup_v{#MyAppVersion}_{#FlavorSuffix}");
    }

    [Fact]
    public void Runtime_download_route_is_not_picked_by_installer_language_or_a_hardcoded_mirror()
    {
        var iss = Iss();
        var ensure = Routine(iss, "function EnsureRedistViaApp");

        iss.Should().NotContain("myqcloud.com", "the mirror root lives in DomesticMirrorDefaults only");
        ensure.Should().NotContain("ActiveLanguage");
        ensure.Should().Contain("RouteArg");
        Routine(iss, "procedure CurStepChanged").Should().Contain("RedistRouteArg");
    }

    [Fact]
    public void Runtime_download_follows_the_saved_route_before_the_flavor()
    {
        var route = Routine(Iss(), "function RedistRouteArg");

        route.Should().Contain("{#FlavorRoute}");
        route.Should().Contain("'downloadRoutePreference'");
        route.Should().Contain("'mirrorBaseUrl'");
        route.Should().Contain("'--no-mirror'");
        route.Should().Contain("'--route-mirror'", "route 1 means the mirror without a region probe");
    }

    [Fact]
    public void Config_writer_keeps_route_and_mirror_and_presets_the_flavor_route()
    {
        var writer = Routine(Iss(), "function WriteManagerConfigNative");

        writer.Should().Contain("TryExtractJsonInt(ExistingContent, 'downloadRoutePreference'");
        writer.Should().Contain("TryExtractJsonRaw(ExistingContent, 'mirrorBaseUrl'");
        writer.Should().Contain("{#FlavorRoute}");
        writer.Should().Contain("'  \"downloadRoutePreference\": '");
        writer.Should().Contain("'  \"mirrorBaseUrl\": '");
    }

    [Fact]
    public void Each_flavor_defines_its_edition_and_display_suffix()
    {
        var iss = Iss();

        iss.Should().Contain("#define FlavorEdition \"cn\"");
        iss.Should().Contain("#define FlavorEdition \"global\"");
        iss.Should().Contain("AppName={cm:AppDisplayNameEdition}");
        foreach (var lang in new[] { "chinesesimplified", "english", "russian", "japanese", "german" })
            iss.Should().Contain(lang + ".AppDisplayNameEdition=");
        iss.Should().Contain("VersionInfoProductName=");
    }

    [Fact]
    public void Shortcuts_keep_one_name_across_flavors()
    {
        var iss = Iss();

        iss.Should().Contain("DefaultGroupName={cm:AppDisplayName}");
        iss.Should().Contain("Name: \"{group}\\{cm:AppDisplayName}\"");
        iss.Should().Contain("Name: \"{autodesktop}\\{cm:AppDisplayName}\"");
    }

    /// <summary>
    /// An update delivered through the old feed is the Global package. Writing its edition over an
    /// existing config would turn every mainland player into the Global edition.
    /// </summary>
    [Fact]
    public void Config_writer_keeps_an_existing_edition_and_writes_one_only_for_a_new_config()
    {
        var writer = Routine(Iss(), "function WriteManagerConfigNative");

        writer.Should().Contain("TryExtractJsonRaw(ExistingContent, 'edition'");
        writer.Should().Contain("ConfigExisted");
        writer.Should().Contain("{#FlavorEdition}");
        writer.Should().Contain("'  \"edition\": '");
    }

    [Fact]
    public void Machine_wide_seed_carries_the_route()
    {
        var writer = Routine(Iss(), "function WriteManagerConfigNative");
        var seed = writer[writer.IndexOf("SeedJson :=", StringComparison.Ordinal)..];

        seed.Should().Contain("downloadRoutePreference");
        seed.Should().Contain("edition");
    }

    static string Iss()
    {
        var path = Path.Combine(RepoRoot(), "packaging", "installer", "MechabellumModManager.iss");
        File.Exists(path).Should().BeTrue(because: path);
        return File.ReadAllText(path);
    }

    /// <summary>Text from <paramref name="header"/> up to the next top-level routine.</summary>
    static string Routine(string iss, string header)
    {
        var start = iss.IndexOf(header, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, because: header);
        var end = iss.Length;
        foreach (var next in new[] { "\nfunction ", "\nprocedure " })
        {
            var at = iss.IndexOf(next, start + header.Length, StringComparison.Ordinal);
            if (at > 0 && at < end)
                end = at;
        }

        return iss[start..end];
    }

    static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "packaging")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue();
        return dir!;
    }
}
