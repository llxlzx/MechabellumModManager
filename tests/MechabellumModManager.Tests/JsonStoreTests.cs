using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class JsonStoreTests
{
    [Fact]
    public void Save_then_Load_roundtrips_AppConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
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
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadOrDefault_returns_factory_on_corrupt_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "broken.json");
            File.WriteAllText(path, "{ not-json");
            var store = new JsonStore();
            var loaded = store.LoadOrDefault(path, () => new AppConfig { ActiveProfileId = "fallback" });
            loaded.ActiveProfileId.Should().Be("fallback");
        }
        finally { Directory.Delete(dir, true); }
    }
}
