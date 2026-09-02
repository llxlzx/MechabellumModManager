using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderConfigOptimizerTests
{
    [Fact]
    public void Creates_cfg_with_recommended_flags_when_missing()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            var opt = new MelonLoaderConfigOptimizer();
            var result = opt.ApplyRecommendedSettings(root);
            result.Changed.Should().BeTrue();
            result.NeedsFirstAssemblyGeneration.Should().BeTrue();

            var cfg = File.ReadAllText(opt.GetLoaderConfigPath(root));
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Flips_false_flags_to_true()
    {
        var root = CreateTempGame(withAssemblies: true);
        try
        {
            var path = Path.Combine(root, "UserData", "Loader.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                """
                [loader]
                force_quit = false

                [unityengine]
                force_offline_generation = false
                """);

            var opt = new MelonLoaderConfigOptimizer();
            var result = opt.ApplyRecommendedSettings(root);
            result.Changed.Should().BeTrue();
            result.NeedsFirstAssemblyGeneration.Should().BeFalse();

            var cfg = File.ReadAllText(path);
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = true");
            cfg.Should().NotContain("force_quit = false");
            cfg.Should().NotContain("force_offline_generation = false");

            var again = opt.ApplyRecommendedSettings(root);
            again.Changed.Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Appends_missing_keys_into_existing_sections()
    {
        var root = CreateTempGame(withAssemblies: true);
        try
        {
            var path = Path.Combine(root, "UserData", "Loader.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                """
                [loader]
                disable = false

                [unityengine]
                version_override = ""
                """);

            var opt = new MelonLoaderConfigOptimizer();
            opt.ApplyRecommendedSettings(root).Changed.Should().BeTrue();

            var cfg = File.ReadAllText(path);
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    static string CreateTempGame(bool withAssemblies)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-opt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        if (withAssemblies)
        {
            var dir = Path.Combine(root, "MelonLoader", "Il2CppAssemblies");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Il2CppDummy.dll"), "");
        }

        return root;
    }
}
