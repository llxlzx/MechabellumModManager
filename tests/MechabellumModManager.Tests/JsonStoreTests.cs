using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class JsonStoreTests
{
    [Fact]
    public void Save_then_Load_roundtrips_AppConfig()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, "config.json");
            var store = new JsonStore();
            var cfg = new AppConfig
            {
                GamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Mechabellum",
                LaunchMode = LaunchMode.SteamThenExe,
                ActiveProfileId = "default",
                DataRoot = dir
            };
            store.Save(path, cfg);

            var loaded = store.LoadOrDefault(path, () => new AppConfig());
            loaded.GamePath.Should().Be(cfg.GamePath);
            loaded.ActiveProfileId.Should().Be("default");
            loaded.LaunchMode.Should().Be(LaunchMode.SteamThenExe);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, "config.json");
            new JsonStore().Save(path, new AppConfig { GamePath = @"D:\steam\Mechabellum" });

            File.Exists(path).Should().BeTrue();
            File.Exists(path + ".tmp").Should().BeFalse();
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Save_replaces_an_existing_file_atomically()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, "config.json");
            var store = new JsonStore();
            store.Save(path, new AppConfig { GamePath = "first" });
            store.Save(path, new AppConfig { GamePath = "second" });

            store.LoadOrDefault(path, () => new AppConfig()).GamePath.Should().Be("second");
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Corrupt_json_is_backed_up_instead_of_silently_dropped()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"gamePath\": \"D:\\\\steam\\\\Mecha");

            var store = new JsonStore();
            var loaded = store.LoadOrDefault(path, () => new AppConfig());

            loaded.GamePath.Should().BeEmpty();
            store.LastRecoveredBackupPath.Should().NotBeNull();
            File.Exists(store.LastRecoveredBackupPath!).Should().BeTrue();
            File.ReadAllText(store.LastRecoveredBackupPath!).Should().Contain("steam");
        }
        finally { Cleanup(dir); }
    }

    static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { /* temp leftover is non-fatal */ }
    }
}
