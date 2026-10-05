using FluentAssertions;
using MechabellumModManager.Services;

public class CatalogManagerVersionCacheTests
{
    [Fact]
    public void Version_change_deletes_catalog_cache_and_keeps_the_update_stamp()
    {
        var dir = TempDir();
        try
        {
            CatalogCache.Write(dir, "{\"mods\":[]}");
            CatalogCache.WriteEtag(dir, "\"abc\"");
            CatalogCache.WriteSource(dir, "github");
            var updateStamp = Path.Combine(dir, UpdateChecker.QuietStampFileName);
            File.WriteAllText(updateStamp, "keep");

            CatalogCache.DropIfManagerVersionChanged(dir, "1.3.14").Should().BeTrue();

            File.Exists(CatalogCache.GetPath(dir)).Should().BeFalse();
            File.Exists(CatalogCache.GetEtagPath(dir)).Should().BeFalse();
            File.Exists(CatalogCache.GetSourcePath(dir)).Should().BeFalse();
            File.ReadAllText(updateStamp).Should().Be("keep");
            File.ReadAllText(CatalogCache.GetManagerVersionPath(dir)).Trim().Should().Be("1.3.14");
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public void Same_version_keeps_the_catalog_cache()
    {
        var dir = TempDir();
        try
        {
            CatalogCache.Write(dir, "{\"mods\":[1]}");
            CatalogCache.DropIfManagerVersionChanged(dir, "1.3.14").Should().BeTrue();
            CatalogCache.Write(dir, "{\"mods\":[1]}");

            CatalogCache.DropIfManagerVersionChanged(dir, "1.3.14").Should().BeFalse();
            CatalogCache.TryRead(dir, out var json).Should().BeTrue();
            json.Should().Contain("1");
        }
        finally
        {
            TryDelete(dir);
        }
    }

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* cleanup */ }
    }
}
