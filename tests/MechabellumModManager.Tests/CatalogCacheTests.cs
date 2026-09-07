using FluentAssertions;
using MechabellumModManager.Services;

public class CatalogCacheTests
{
    [Fact]
    public void Write_then_TryRead_roundtrips_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-cc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            CatalogCache.Write(dir, "{\"mods\":[]}");
            CatalogCache.TryRead(dir, out var json).Should().BeTrue();
            json.Should().Contain("mods");
            File.Exists(CatalogCache.GetPath(dir)).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public void TryRead_missing_file_returns_false()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-cc-missing-" + Guid.NewGuid().ToString("N"));
        CatalogCache.TryRead(dir, out var json).Should().BeFalse();
        json.Should().BeEmpty();
    }
}
