using FluentAssertions;
using MechabellumModManager.Services;

public class CatalogCacheEtagTests
{
    [Fact]
    public void WriteEtag_round_trips_and_empty_clears()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-etag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CatalogCache.WriteEtag(root, "\"abc\"");
            CatalogCache.TryReadEtag(root, out var etag).Should().BeTrue();
            etag.Should().Be("\"abc\"");

            CatalogCache.WriteEtag(root, null);
            CatalogCache.TryReadEtag(root, out _).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ok */ }
        }
    }
}
