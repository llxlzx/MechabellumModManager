using System.IO.Compression;
using FluentAssertions;
using MechabellumModManager.Services;

public class InstallMelonLoaderCliTests
{
    [Fact]
    public void TryParseArgs_reads_game_and_redist()
    {
        var ok = InstallMelonLoaderCli.TryParseArgs(
            ["--install-melon-loader", "--game-path", @"D:\steam\common\Mechabellum", "--redist-dir", @"C:\redist"],
            out var game, out var redist);
        ok.Should().BeTrue();
        game.Should().Be(@"D:\steam\common\Mechabellum");
        redist.Should().Be(@"C:\redist");
    }

    [Fact]
    public void TryParseArgs_false_without_flag()
    {
        InstallMelonLoaderCli.TryParseArgs(["--game-path", @"D:\x"], out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Run_returns_seed_incomplete_when_unity_deps_redist_empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-cli-noseed-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var redist = Path.Combine(root, "redist");
        try
        {
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "exe");
            File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "dll");
            var data = Path.Combine(game, "Mechabellum_Data");
            Directory.CreateDirectory(data);
            File.WriteAllBytes(
                Path.Combine(data, "globalgamemanagers"),
                System.Text.Encoding.ASCII.GetBytes("xxxx2022.3.62f3yyyy"));

            // Empty preferred unity-deps wins over repo packaging walk → seed must fail.
            Directory.CreateDirectory(Path.Combine(redist, "unity-deps"));
            Directory.CreateDirectory(Path.Combine(redist, "melonloader"));
            var zipPath = CreateFakeMelonZip(Path.Combine(redist, "melonloader"));

            var code = InstallMelonLoaderCli.Run(game, redist);
            code.Should().Be(InstallMelonLoaderCli.ExitSeedIncomplete);
            File.Exists(Path.Combine(game, "version.dll")).Should().BeTrue();
            File.Exists(zipPath).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Run_reapplies_optimize_after_late_seed_from_redist_dir()
    {
        // Zip is NOT under .../melonloader/, so InstallFromZip cannot derive redist;
        // explicit --redist-dir must seed then re-apply optimize for force_offline.
        var root = Path.Combine(Path.GetTempPath(), "mmm-cli-seed-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var redist = Path.Combine(root, "redist");
        var looseZipDir = Path.Combine(root, "loose");
        try
        {
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "exe");
            File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "dll");
            var data = Path.Combine(game, "Mechabellum_Data");
            Directory.CreateDirectory(data);
            File.WriteAllBytes(
                Path.Combine(data, "globalgamemanagers"),
                System.Text.Encoding.ASCII.GetBytes("xxxx2022.3.62f3yyyy"));

            Directory.CreateDirectory(Path.Combine(redist, "unity-deps"));
            File.WriteAllText(
                Path.Combine(redist, "unity-deps", "UnityDependencies_2022.3.62.zip"),
                "deps-payload");

            var repoCpp2Il = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "packaging", "installer", "redist", "cpp2il"));
            var cpp2IlDir = Path.Combine(redist, "cpp2il");
            Directory.CreateDirectory(cpp2IlDir);
            if (Directory.Exists(repoCpp2Il))
            {
                foreach (var f in Directory.GetFiles(repoCpp2Il))
                    File.Copy(f, Path.Combine(cpp2IlDir, Path.GetFileName(f)), overwrite: true);
            }
            else
            {
                File.WriteAllBytes(Path.Combine(cpp2IlDir, "Cpp2IL.exe"), [0x4D, 0x5A]);
                File.WriteAllBytes(
                    Path.Combine(cpp2IlDir, "Cpp2IL.Plugin.StrippedCodeRegSupport.dll"),
                    [0x4D, 0x5A]);
            }

            Directory.CreateDirectory(looseZipDir);
            var zipPath = CreateFakeMelonZip(looseZipDir);
            var baseZip = Path.Combine(AppContext.BaseDirectory, "MelonLoader.x64.zip");
            File.Copy(zipPath, baseZip, overwrite: true);

            try
            {
                var code = InstallMelonLoaderCli.Run(game, redist);
                code.Should().Be(0);

                File.Exists(Path.Combine(
                    game,
                    "MelonLoader",
                    "Dependencies",
                    "Il2CppAssemblyGenerator",
                    "UnityDependencies_2022.3.62.zip")).Should().BeTrue();
                var cfg = File.ReadAllText(Path.Combine(game, "UserData", "Loader.cfg"));
                cfg.Should().MatchRegex(@"(?im)^\s*force_offline_generation\s*=\s*true\s*$");
            }
            finally
            {
                try { File.Delete(baseZip); } catch { /* ignore */ }
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
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
