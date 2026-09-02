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
                GamePath = @"D:\steam\steamapps\common\Mechabellum",
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
}
