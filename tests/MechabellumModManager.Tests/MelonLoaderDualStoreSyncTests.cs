using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderDualStoreSyncTests
{
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
            new GameDetector().Detect(dst).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.Ready);
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
            new GameDetector().Detect(official).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.Ready);
            new GameDetector().Detect(beta).Kind.Should().Be(MechabellumModManager.Models.GameStatusKind.Ready);
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
}
