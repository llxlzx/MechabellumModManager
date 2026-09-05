using System.IO.Compression;
using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderDualStoreSyncTests
{
    [Fact]
    public void InstallFromZip_seeds_UnityDependencies_before_offline_optimize()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-seed-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var redist = Path.Combine(root, "redist");
        var melonZipDir = Path.Combine(redist, "melonloader");
        var unityDeps = Path.Combine(redist, "unity-deps");
        try
        {
            SeedGame(game);
            WriteFakeGlobalgamemanagers(game, "2022.3.62f3");
            Directory.CreateDirectory(melonZipDir);
            Directory.CreateDirectory(unityDeps);
            File.WriteAllText(Path.Combine(unityDeps, "UnityDependencies_2022.3.62.zip"), "deps-payload");
            var cpp2Il = Path.Combine(redist, "cpp2il");
            Directory.CreateDirectory(cpp2Il);
            File.WriteAllText(Path.Combine(cpp2Il, "Cpp2IL.exe"), "fake-cpp2il");
            File.WriteAllText(Path.Combine(cpp2Il, "Cpp2IL.Plugin.StrippedCodeRegSupport.dll"), "fake-plugin");
            var zipPath = CreateFakeMelonZip(melonZipDir);

            var result = new MelonLoaderDualStoreSync().InstallFromZip(game, zipPath);

            result.Success.Should().BeTrue(result.Message);
            result.Message.Should().Contain("UnityDependencies");
            File.Exists(Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator",
                "UnityDependencies_2022.3.62.zip")).Should().BeTrue();
            File.Exists(Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator",
                "Cpp2IL", "Cpp2IL.exe")).Should().BeTrue();
            var cfg = File.ReadAllText(Path.Combine(game, "UserData", "Loader.cfg"));
            cfg.Should().MatchRegex(@"(?im)^\s*force_offline_generation\s*=\s*true\s*$");
            cfg.Should().MatchRegex(@"(?im)^\s*force_quit\s*=\s*true\s*$");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void SeedDependencies_copies_matching_zip_when_redist_provided()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-seeddep-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var redist = Path.Combine(root, "redist");
        try
        {
            Directory.CreateDirectory(game);
            WriteFakeGlobalgamemanagers(game, "2022.3.62f3");
            Directory.CreateDirectory(Path.Combine(redist, "unity-deps"));
            File.WriteAllText(
                Path.Combine(redist, "unity-deps", "UnityDependencies_2022.3.62.zip"),
                "deps-payload");
            var cpp2IlDir = Path.Combine(redist, "cpp2il");
            Directory.CreateDirectory(cpp2IlDir);
            File.WriteAllText(Path.Combine(cpp2IlDir, "Cpp2IL.exe"), "fake-cpp2il");
            File.WriteAllText(Path.Combine(cpp2IlDir, "Cpp2IL.Plugin.StrippedCodeRegSupport.dll"), "fake-plugin");

            var seed = new MelonLoaderDualStoreSync().SeedDependencies(game, redist);

            seed.Success.Should().BeTrue(seed.Message);
            seed.Copied.Should().BeTrue();
            File.Exists(Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator",
                "UnityDependencies_2022.3.62.zip")).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void EnsureOnStore_copies_framework_from_sibling_without_il2cpp_assemblies()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-sync-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            SeedGame(src);
            SeedGame(dst);
            SeedLoader(src);
            Directory.CreateDirectory(Path.Combine(src, "MelonLoader", "Il2CppAssemblies"));
            File.WriteAllText(Path.Combine(src, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "gen");

            var sync = new MelonLoaderDualStoreSync();
            var result = sync.EnsureOnStore(dst, siblingGamePath: src);

            result.Success.Should().BeTrue();
            File.Exists(Path.Combine(dst, "version.dll")).Should().BeTrue();
            Directory.Exists(Path.Combine(dst, "MelonLoader", "net6")).Should().BeTrue();
            File.Exists(Path.Combine(dst, "MelonLoader", "net6", "MelonLoader.dll")).Should().BeTrue();
            Directory.Exists(Path.Combine(dst, "MelonLoader", "Il2CppAssemblies")).Should().BeFalse();
            new GameDetector().Detect(dst).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.LoaderPresentAssembliesMissing);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void EnsureOnStore_copies_from_sibling_when_assemblies_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-sync-miss-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            SeedGame(src);
            SeedGame(dst);
            SeedLoader(src);
            new GameDetector().Detect(src).Kind.Should().Be(
                MechabellumModManager.Models.GameStatusKind.LoaderPresentAssembliesMissing);

            var result = new MelonLoaderDualStoreSync().EnsureOnStore(dst, siblingGamePath: src);

            result.Success.Should().BeTrue(result.Message);
            File.Exists(Path.Combine(dst, "version.dll")).Should().BeTrue();
            File.Exists(Path.Combine(dst, "MelonLoader", "net6", "MelonLoader.dll")).Should().BeTrue();
            Directory.Exists(Path.Combine(dst, "MelonLoader", "Il2CppAssemblies")).Should().BeFalse();
            new GameDetector().Detect(dst).Kind.Should().Be(
                MechabellumModManager.Models.GameStatusKind.LoaderPresentAssembliesMissing);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void EnsureOnStore_does_not_copy_Latest_log()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-log-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            SeedGame(src);
            SeedGame(dst);
            SeedLoader(src);
            File.WriteAllText(Path.Combine(src, "MelonLoader", "Latest.log"), "from-src");

            var result = new MelonLoaderDualStoreSync().EnsureOnStore(dst, siblingGamePath: src);

            result.Success.Should().BeTrue();
            File.Exists(Path.Combine(dst, "MelonLoader", "Latest.log")).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void EnsureOnBothStores_fills_missing_side()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-both-" + Guid.NewGuid().ToString("N"));
        var official = Path.Combine(root, "Mechabellum_official");
        var beta = Path.Combine(root, "Mechabellum_beta");
        try
        {
            SeedGame(official);
            SeedGame(beta);
            SeedLoader(beta);

            var result = new MelonLoaderDualStoreSync().EnsureOnBothStores(official, beta);

            result.Success.Should().BeTrue();
            new GameDetector().Detect(official).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.LoaderPresentAssembliesMissing);
            new GameDetector().Detect(beta).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.LoaderPresentAssembliesMissing);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }


    [Fact]
    public void EnsureOnStore_does_not_copy_stale_assembly_generator_Config()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-cfg-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            SeedGame(src);
            SeedGame(dst);
            SeedLoader(src);
            Directory.CreateDirectory(Path.Combine(src, "MelonLoader", "Il2CppAssemblies"));
            File.WriteAllText(Path.Combine(src, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "gen");
            var cfgDir = Path.Combine(src, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
            Directory.CreateDirectory(cfgDir);
            File.WriteAllText(Path.Combine(cfgDir, "Config.cfg"), "GameAssemblyHash = \"FROM_SRC\"\n");
            File.WriteAllText(Path.Combine(cfgDir, "Il2CppAssemblyGenerator.dll"), "dll");

            var result = new MelonLoaderDualStoreSync().EnsureOnStore(dst, siblingGamePath: src);

            result.Success.Should().BeTrue();
            File.Exists(Path.Combine(dst, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Il2CppAssemblyGenerator.dll")).Should().BeTrue();
            File.Exists(Path.Combine(dst, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Config.cfg")).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
    static void SeedGame(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "exe");
        File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "dll");
    }

    static void SeedLoader(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "MelonLoader", "net6"));
        File.WriteAllBytes(Path.Combine(dir, "MelonLoader", "net6", "MelonLoader.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(Path.Combine(dir, "version.dll"), new byte[] { 0x4D, 0x5A });
    }

    static void WriteFakeGlobalgamemanagers(string game, string unityVersion)
    {
        var data = Path.Combine(game, "Mechabellum_Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(
            Path.Combine(data, "globalgamemanagers"),
            System.Text.Encoding.ASCII.GetBytes("xxxx" + unityVersion + "yyyy"));
    }

    static string CreateFakeMelonZip(string directory)
    {
        Directory.CreateDirectory(directory);
        var zipPath = Path.Combine(directory, "MelonLoader.x64.zip");
        var stage = Path.Combine(directory, "stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(stage, "MelonLoader"));
        File.WriteAllText(Path.Combine(stage, "MelonLoader", "placeholder.txt"), "ml");
        File.WriteAllText(Path.Combine(stage, "version.dll"), "");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(stage, zipPath);
        try { Directory.Delete(stage, true); } catch { /* ignore */ }
        return zipPath;
    }
}

