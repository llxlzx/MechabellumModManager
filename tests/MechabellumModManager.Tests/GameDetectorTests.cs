using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class GameDetectorTests
{
    [Fact]
    public void Missing_exe_is_GameMissing()
    {
        var root = CreateTempGame(exe: false, ga: false, melonDir: false, proxy: false);
        try
        {
            var s = new GameDetector().Detect(root);
            s.Kind.Should().Be(GameStatusKind.GameMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Game_without_loader_is_GameOkLoaderMissing()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: false, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.GameOkLoaderMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Melon_dir_without_proxy_is_LoaderPartial()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.LoaderPartial);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Melon_with_proxy_but_no_assemblies_is_AssembliesMissing()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Full_install_is_Ready()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.Ready);
        }
        finally { Directory.Delete(root, true); }
    }

    static string CreateTempGame(bool exe, bool ga, bool melonDir, bool proxy, bool assemblies = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (exe) File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        if (ga) File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        if (melonDir) Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        if (proxy) File.WriteAllText(Path.Combine(root, "version.dll"), "");
        if (assemblies)
        {
            var il2cpp = Path.Combine(root, "MelonLoader", "Il2CppAssemblies");
            Directory.CreateDirectory(il2cpp);
            File.WriteAllText(Path.Combine(il2cpp, "Assembly-CSharp.dll"), "asm");
        }
        return root;
    }
}
