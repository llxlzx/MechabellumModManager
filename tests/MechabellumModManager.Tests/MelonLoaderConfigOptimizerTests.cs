using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderConfigOptimizerTests
{
    [Fact]
    public void Creates_cfg_with_force_quit_true_but_offline_false_when_no_assemblies_or_zip()
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
            cfg.Should().Contain("force_offline_generation = false");
            cfg.Should().NotContain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Flips_false_flags_to_true_when_assemblies_present()
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
    public void Appends_missing_keys_into_existing_sections_when_assemblies_present()
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

    [Fact]
    public void CanForceOfflineGeneration_true_when_assemblies_present()
    {
        var root = CreateTempGame(withAssemblies: true);
        try
        {
            new MelonLoaderConfigOptimizer().CanForceOfflineGeneration(root).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CanForceOfflineGeneration_true_when_exact_zip_matches_resolved_version()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            WriteFakeGlobalgamemanagers(root, "2022.3.62f3");
            PlaceUnityDependenciesZip(root, "2022.3.62");

            new MelonLoaderConfigOptimizer().CanForceOfflineGeneration(root).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CanForceOfflineGeneration_false_when_wrong_zip_version()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            WriteFakeGlobalgamemanagers(root, "2022.3.63f1");
            PlaceUnityDependenciesZip(root, "2022.3.62");

            new MelonLoaderConfigOptimizer().CanForceOfflineGeneration(root).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CanForceOfflineGeneration_false_when_resolve_fails_and_no_assemblies()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            PlaceUnityDependenciesZip(root, "2022.3.62");

            new MelonLoaderConfigOptimizer().CanForceOfflineGeneration(root).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CanForceOfflineGeneration_uses_version_override_for_exact_zip()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            PlaceUnityDependenciesZip(root, "2022.3.62");

            var opt = new MelonLoaderConfigOptimizer();
            opt.CanForceOfflineGeneration(root, "2022.3.62f3").Should().BeTrue();
            opt.CanForceOfflineGeneration(root, "2022.3.63").Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Writes_offline_false_when_wrong_zip_version()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            WriteFakeGlobalgamemanagers(root, "2022.3.63f1");
            PlaceUnityDependenciesZip(root, "2022.3.62");

            var path = Path.Combine(root, "UserData", "Loader.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                """
                [loader]
                force_quit = false

                [unityengine]
                force_offline_generation = true
                """);

            var opt = new MelonLoaderConfigOptimizer();
            var result = opt.ApplyRecommendedSettings(root);
            result.Changed.Should().BeTrue();

            var cfg = File.ReadAllText(path);
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = false");
            cfg.Should().NotContain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Writes_offline_true_when_exact_zip_present()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            WriteFakeGlobalgamemanagers(root, "2022.3.62f3");
            PlaceUnityDependenciesZip(root, "2022.3.62");

            var opt = new MelonLoaderConfigOptimizer();
            opt.ApplyRecommendedSettings(root);

            var cfg = File.ReadAllText(opt.GetLoaderConfigPath(root));
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ApplyRecommendedSettings_knownUnityVersion_enables_offline_when_resolve_fails()
    {
        // Seed fallback: zip already in AG folder, but no globalgamemanagers to resolve.
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            PlaceUnityDependenciesZip(root, "2022.3.62");

            var opt = new MelonLoaderConfigOptimizer();
            opt.CanForceOfflineGeneration(root).Should().BeFalse();

            var result = opt.ApplyRecommendedSettings(root, knownUnityVersion: "2022.3.62");
            result.Changed.Should().BeTrue();

            var cfg = File.ReadAllText(opt.GetLoaderConfigPath(root));
            cfg.Should().Contain("force_quit = true");
            cfg.Should().Contain("force_offline_generation = true");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void NeedsFirstAssemblyGeneration_true_when_assemblies_missing()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            new MelonLoaderConfigOptimizer().NeedsFirstAssemblyGeneration(root).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void NeedsFirstAssemblyGeneration_false_when_dll_present()
    {
        var root = CreateTempGame(withAssemblies: true);
        try
        {
            new MelonLoaderConfigOptimizer().NeedsFirstAssemblyGeneration(root).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void NeedsFirstAssemblyGeneration_true_when_folder_empty()
    {
        var root = CreateTempGame(withAssemblies: false);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "Il2CppAssemblies"));
            new MelonLoaderConfigOptimizer().NeedsFirstAssemblyGeneration(root).Should().BeTrue();
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

    static void WriteFakeGlobalgamemanagers(string game, string unityVersion)
    {
        var data = Path.Combine(game, "Mechabellum_Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(
            Path.Combine(data, "globalgamemanagers"),
            System.Text.Encoding.ASCII.GetBytes("xxxx" + unityVersion + "yyyy"));
    }

    static void PlaceUnityDependenciesZip(string game, string majorMinorPatch)
    {
        var dir = Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, UnityVersionNormalizer.ExpectedZipFileName(majorMinorPatch)),
            "fake-zip");
    }
}
