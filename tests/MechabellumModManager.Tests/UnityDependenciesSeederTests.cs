using FluentAssertions;
using MechabellumModManager.Services;

public class UnityDependenciesSeederTests
{
    [Fact]
    public void Seed_copies_matching_zip_into_Il2CppAssemblyGenerator()
    {
        var game = CreateGameRoot();
        var redist = CreateRedistWithZip("2022.3.62");
        try
        {
            WriteFakeGlobalgamemanagers(game, "2022.3.62f3");

            var result = new UnityDependenciesSeeder().Seed(game, redist);
            result.Success.Should().BeTrue();
            result.Copied.Should().BeTrue();
            File.Exists(Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator",
                "UnityDependencies_2022.3.62.zip")).Should().BeTrue();
        }
        finally
        {
            Cleanup(game, redist);
        }
    }

    [Fact]
    public void Seed_skips_when_already_present()
    {
        var game = CreateGameRoot();
        var redist = CreateRedistWithZip("2022.3.62");
        try
        {
            WriteFakeGlobalgamemanagers(game, "2022.3.62f3");
            var destDir = Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(destDir);
            // Same payload as redist zip so length matches → skip refresh/copy
            var payload = "fake-zip-2022.3.62";
            File.WriteAllText(Path.Combine(destDir, "UnityDependencies_2022.3.62.zip"), payload);

            var result = new UnityDependenciesSeeder().Seed(game, redist);
            result.Success.Should().BeTrue();
            result.Copied.Should().BeFalse();
            File.ReadAllText(Path.Combine(destDir, "UnityDependencies_2022.3.62.zip"))
                .Should().Be(payload);
        }
        finally
        {
            Cleanup(game, redist);
        }
    }

    [Fact]
    public void Seed_fails_when_redist_missing_match()
    {
        var game = CreateGameRoot();
        var redist = CreateEmptyUnityDepsRedist();
        try
        {
            WriteFakeGlobalgamemanagers(game, "2022.3.62f3");
            var result = new UnityDependenciesSeeder().Seed(game, redist);
            result.Success.Should().BeFalse();
        }
        finally
        {
            Cleanup(game, redist);
        }
    }

    [Fact]
    public void Seed_does_not_treat_wrong_version_zip_as_success()
    {
        var game = CreateGameRoot();
        var redist = CreateRedistWithZip("2022.3.62");
        try
        {
            WriteFakeGlobalgamemanagers(game, "2022.3.63f1");
            var destDir = Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(destDir);
            File.WriteAllText(Path.Combine(destDir, "UnityDependencies_2022.3.62.zip"), "wrong-version");

            var result = new UnityDependenciesSeeder().Seed(game, redist);
            result.Success.Should().BeFalse();
            File.Exists(Path.Combine(destDir, "UnityDependencies_2022.3.63.zip")).Should().BeFalse();
        }
        finally
        {
            Cleanup(game, redist);
        }
    }

    static string CreateGameRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-uds-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    static string CreateRedistWithZip(string version)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-uds-redist-" + Guid.NewGuid().ToString("N"));
        var unityDeps = Path.Combine(root, "unity-deps");
        Directory.CreateDirectory(unityDeps);
        File.WriteAllText(Path.Combine(unityDeps, $"UnityDependencies_{version}.zip"), "fake-zip-" + version);
        return root;
    }

    static string CreateEmptyUnityDepsRedist()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-uds-redist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "unity-deps"));
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

    static void Cleanup(params string[] paths)
    {
        foreach (var path in paths)
        {
            try { Directory.Delete(path, true); } catch { /* ignore */ }
        }
    }
}
