using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class MelonLoaderAssemblyGeneratorTests
{
    [Fact]
    public async Task EnsureAssemblies_skips_when_already_Ready()
    {
        var root = SeedStore(withAssemblies: true);
        try
        {
            var gen = new MelonLoaderAssemblyGenerator(startProcess: _ => null);
            var result = await gen.EnsureAssembliesAsync(root, timeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(50));
            result.Success.Should().BeTrue();
            result.Skipped.Should().BeTrue();
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task EnsureAssemblies_polls_until_marker_stable()
    {
        var root = SeedStore(withAssemblies: false);
        try
        {
            var marker = Path.Combine(root, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll");
            var started = 0;
            var gen = new MelonLoaderAssemblyGenerator(
                startProcess: _ =>
                {
                    started++;
                    Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                    File.WriteAllText(marker, "asm");
                    return null; // no real process
                },
                delay: async (ts, ct) => await Task.Delay(10, ct));

            var result = await gen.EnsureAssembliesAsync(
                root,
                timeout: TimeSpan.FromSeconds(5),
                pollInterval: TimeSpan.FromMilliseconds(20));

            result.Success.Should().BeTrue(result.Message);
            started.Should().Be(1);
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.Ready);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task EnsureAssemblies_second_attempt_same_session_fails_fast()
    {
        var root = SeedStore(withAssemblies: false);
        try
        {
            var gen = new MelonLoaderAssemblyGenerator(
                startProcess: _ => null,
                delay: async (ts, ct) => await Task.Delay(5, ct));

            var first = await gen.EnsureAssembliesAsync(root, timeout: TimeSpan.FromMilliseconds(80), pollInterval: TimeSpan.FromMilliseconds(20));
            first.Success.Should().BeFalse();

            var second = await gen.EnsureAssembliesAsync(root, timeout: TimeSpan.FromMilliseconds(80), pollInterval: TimeSpan.FromMilliseconds(20));
            second.Success.Should().BeFalse();
            second.Message.Should().Contain("本会话已尝试");
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task EnsureAssemblies_clears_stale_GameAssemblyHash_before_launch()
    {
        var root = SeedStore(withAssemblies: false);
        try
        {
            var cfgDir = Path.Combine(root, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(cfgDir);
            var cfgPath = Path.Combine(cfgDir, "Config.cfg");
            File.WriteAllText(cfgPath,
                """
                [Il2CppAssemblyGenerator]
                GameAssemblyHash = "DEADBEEFCAFE"
                UnityVersion = "2022.3.62"
                """);

            string? hashAtLaunch = null;
            var gen = new MelonLoaderAssemblyGenerator(
                startProcess: _ =>
                {
                    hashAtLaunch = File.ReadAllText(cfgPath);
                    var marker = Path.Combine(root, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                    File.WriteAllText(marker, "asm");
                    return null;
                },
                delay: async (ts, ct) => await Task.Delay(10, ct));

            var result = await gen.EnsureAssembliesAsync(
                root,
                timeout: TimeSpan.FromSeconds(5),
                pollInterval: TimeSpan.FromMilliseconds(20));

            result.Success.Should().BeTrue(result.Message);
            hashAtLaunch.Should().NotBeNull();
            hashAtLaunch!.Should().Contain("GameAssemblyHash = \"\"");
            hashAtLaunch.Should().NotContain("DEADBEEFCAFE");
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void InvalidateStaleGenerationState_clears_hash_when_assemblies_missing()
    {
        var root = SeedStore(withAssemblies: false);
        try
        {
            var cfgDir = Path.Combine(root, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(cfgDir);
            var cfgPath = Path.Combine(cfgDir, "Config.cfg");
            File.WriteAllText(cfgPath, "GameAssemblyHash = \"ABC123\"\nUnityVersion = \"2022.3.62\"\n");

            var changed = MelonLoaderAssemblyGenerator.InvalidateStaleGenerationState(root);

            changed.Should().BeTrue();
            var text = File.ReadAllText(cfgPath);
            text.Should().Contain("GameAssemblyHash = \"\"");
            text.Should().NotContain("ABC123");
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void InvalidateStaleGenerationState_noop_when_assemblies_present()
    {
        var root = SeedStore(withAssemblies: true);
        try
        {
            var cfgDir = Path.Combine(root, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(cfgDir);
            var cfgPath = Path.Combine(cfgDir, "Config.cfg");
            File.WriteAllText(cfgPath, "GameAssemblyHash = \"KEEPME\"\n");

            var changed = MelonLoaderAssemblyGenerator.InvalidateStaleGenerationState(root);

            changed.Should().BeFalse();
            File.ReadAllText(cfgPath).Should().Contain("KEEPME");
        }
        finally { TryDelete(root); }
    }

    static string SeedStore(bool withAssemblies)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-gen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "exe");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "ga");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "net6"));
        File.WriteAllBytes(Path.Combine(root, "MelonLoader", "net6", "MelonLoader.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(Path.Combine(root, "version.dll"), new byte[] { 0x4D, 0x5A });
        if (withAssemblies)
        {
            var il2 = Path.Combine(root, "MelonLoader", "Il2CppAssemblies");
            Directory.CreateDirectory(il2);
            File.WriteAllText(Path.Combine(il2, "Assembly-CSharp.dll"), "asm");
        }
        return root;
    }

    static void TryDelete(string root)
    {
        try { Directory.Delete(root, true); } catch { /* ignore */ }
    }
}
