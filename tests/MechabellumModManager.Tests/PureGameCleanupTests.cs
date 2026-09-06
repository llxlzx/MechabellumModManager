using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class PureGameCleanupWhitelistTests
{
    [Fact]
    public void Melon_directory_under_game_root_allowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-wl-" + Guid.NewGuid().ToString("N"));
        var melon = Path.Combine(root, "MelonLoader");
        Directory.CreateDirectory(melon);
        try
        {
            PureGameCleanupWhitelist.IsAllowedMelonDirectory(root, melon).Should().BeTrue();
            PureGameCleanupWhitelist.IsAllowedMelonDirectory(root, Path.Combine(root, "Mods")).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Rejects_parent_escape_and_game_binaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-wl2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var exe = Path.Combine(root, "Mechabellum.exe");
        File.WriteAllText(exe, "x");
        try
        {
            PureGameCleanupWhitelist.IsAllowedMelonFile(root, exe).Should().BeFalse();
            PureGameCleanupWhitelist.IsAllowedMelonDirectory(root, root).Should().BeFalse();
            PureGameCleanupWhitelist.IsAllowedMelonDirectory(root, Path.Combine(root, "..", "Windows"))
                .Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Other_store_must_be_sibling_beta_name()
    {
        var common = Path.Combine(Path.GetTempPath(), "mmm-common-" + Guid.NewGuid().ToString("N"));
        var official = Path.Combine(common, "Mechabellum_official");
        var beta = Path.Combine(common, "Mechabellum_beta");
        Directory.CreateDirectory(official);
        Directory.CreateDirectory(beta);
        try
        {
            PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(official, beta).Should().BeTrue();
            PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(official, official).Should().BeFalse();
            PureGameCleanupWhitelist.IsAllowedOtherStoreDelete(official, Path.Combine(common, "Mechabellum"))
                .Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(common, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void AppData_only_exact_manager_folder()
    {
        var ok = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PathsService.AppFolderName);
        PureGameCleanupWhitelist.IsAllowedManagerAppData(ok).Should().BeTrue();
        PureGameCleanupWhitelist.IsAllowedManagerAppData(Path.Combine(ok, "library")).Should().BeFalse();
        PureGameCleanupWhitelist.IsAllowedManagerAppData(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).Should().BeFalse();
    }
}

public class PureGameCleanupPlannerTests
{
    [Fact]
    public void Hollow_official_skips_teardown_but_still_plans_melon_on_complete_stores()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-plan-" + Guid.NewGuid().ToString("N"));
        var official = Path.Combine(root, "Mechabellum_official");
        var beta = Path.Combine(root, "Mechabellum_beta");
        var link = Path.Combine(root, "Mechabellum");
        Directory.CreateDirectory(official);
        File.WriteAllText(Path.Combine(official, "Mechabellum.exe"), "x"); // missing GameAssembly
        Directory.CreateDirectory(beta);
        File.WriteAllText(Path.Combine(beta, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(beta, "GameAssembly.dll"), "x");
        Directory.CreateDirectory(Path.Combine(beta, "Mods"));
        try
        {
            var plan = PureGameCleanupPlanner.Build(new PureGameCleanupRequest
            {
                Branch = new BranchSwitchConfig
                {
                    Enabled = true,
                    SteamLinkPath = link,
                    OfficialStorePath = official,
                    BetaStorePath = beta,
                    ActiveBranch = GameBranch.Official
                },
                ConfirmDeleteOtherStore = true,
                SkipAppData = true
            });

            plan.IsAborted.Should().BeFalse();
            plan.Actions.Should().NotContain(a => a.Kind == CleanupActionKind.TeardownDualFolder);
            plan.Actions.Should().Contain(a =>
                a.Kind == CleanupActionKind.DeleteDirectory
                && a.Path.Contains("Mechabellum_beta", StringComparison.OrdinalIgnoreCase)
                && a.Path.EndsWith("Mods", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Plans_melon_whitelist_when_game_root_ready()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-plan2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "x");
        Directory.CreateDirectory(Path.Combine(root, "Mods"));
        Directory.CreateDirectory(Path.Combine(root, "Mechabellum_Data"));
        File.WriteAllText(Path.Combine(root, "version.dll"), "x");
        File.WriteAllText(Path.Combine(root, "Mechabellum_Data", "keep.bin"), "keep");
        try
        {
            var plan = PureGameCleanupPlanner.Build(new PureGameCleanupRequest
            {
                ConfigGamePath = root,
                SkipAppData = true
            });

            plan.IsAborted.Should().BeFalse();
            plan.Actions.Should().Contain(a =>
                a.Kind == CleanupActionKind.DeleteDirectory && a.Path.EndsWith("Mods", StringComparison.OrdinalIgnoreCase));
            plan.Actions.Should().Contain(a =>
                a.Kind == CleanupActionKind.DeleteFile && a.Path.EndsWith("version.dll", StringComparison.OrdinalIgnoreCase));
            plan.Actions.Should().NotContain(a =>
                a.Path.Contains("Mechabellum_Data", StringComparison.OrdinalIgnoreCase));
            plan.Actions.Should().NotContain(a =>
                a.Path.EndsWith("Mechabellum.exe", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
