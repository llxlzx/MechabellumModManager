using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class PreviewStoreTests
{
    static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static void WriteBundle(string root, string relative, byte[] bytes)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    [Fact]
    public void Matching_bundle_hash_does_not_need_the_cache()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-bundle-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("poster-a");
        WriteBundle(bundle, "mods/show-dps/preview.png", bytes);

        var hit = PreviewStore.TryLocal(bundle, cacheRoot: null, "mods/show-dps/preview.png", Hash(bytes));

        hit.Should().NotBeNull();
        hit!.Value.Kind.Should().Be(PreviewStore.LocalKind.Bundle);
        hit.Value.Bytes.Should().Equal(bytes);
    }

    [Fact]
    public void Cache_wins_when_the_bundle_hash_differs()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-bundle-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(Path.GetTempPath(), "pv-cache-" + Guid.NewGuid().ToString("N"));
        var oldBytes = Encoding.UTF8.GetBytes("old-poster");
        var newBytes = Encoding.UTF8.GetBytes("new-poster");
        WriteBundle(bundle, "mods/show-dps/preview.png", oldBytes);
        PreviewStore.Save(cache, "mods/show-dps/preview.png", Hash(newBytes), newBytes);

        var hit = PreviewStore.TryLocal(bundle, cache, "mods/show-dps/preview.png", Hash(newBytes));

        hit!.Value.Kind.Should().Be(PreviewStore.LocalKind.Cache);
        hit.Value.Bytes.Should().Equal(newBytes);
    }

    [Fact]
    public void Hash_mismatch_is_a_miss_and_stale_bundle_remains_available()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-bundle-" + Guid.NewGuid().ToString("N"));
        var oldBytes = Encoding.UTF8.GetBytes("old-poster");
        WriteBundle(bundle, "mods/cut-guide/preview.png", oldBytes);
        var wanted = Hash(Encoding.UTF8.GetBytes("not-shipped"));

        PreviewStore.TryLocal(bundle, cacheRoot: null, "mods/cut-guide/preview.png", wanted)
            .Should().BeNull();
        var stale = PreviewStore.TryStaleBundle(bundle, "mods/cut-guide/preview.png");
        stale!.Value.Bytes.Should().Equal(oldBytes);
    }

    [Fact]
    public void Absent_hash_uses_the_bundled_file()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-bundle-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("bundled");
        WriteBundle(bundle, "mods/quick-mark/preview.png", bytes);

        var hit = PreviewStore.TryLocal(bundle, cacheRoot: null, "mods/quick-mark/preview.png", sha256: null);

        hit!.Value.Kind.Should().Be(PreviewStore.LocalKind.Bundle);
        hit.Value.Bytes.Should().Equal(bytes);
    }

    [Fact]
    public void Absent_hash_uses_a_previously_saved_path_entry()
    {
        var cache = Path.Combine(Path.GetTempPath(), "pv-cache-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("downloaded-once");
        PreviewStore.Save(cache, "mods/new-mod/preview.png", Hash(bytes), bytes);

        var hit = PreviewStore.TryLocal(bundleRoot: Path.Combine(Path.GetTempPath(), "missing"), cache, "mods/new-mod/preview.png", sha256: null);

        hit!.Value.Kind.Should().Be(PreviewStore.LocalKind.Cache);
        hit.Value.Bytes.Should().Equal(bytes);
    }

    [Fact]
    public void Parent_segments_are_rejected()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bundle);

        PreviewStore.TryLocal(bundle, cacheRoot: null, "../outside.png", sha256: null).Should().BeNull();
        PreviewStore.TryStaleBundle(bundle, "../outside.png").Should().BeNull();
        Directory.GetFiles(bundle, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void Truncated_cache_file_is_a_miss()
    {
        var cache = Path.Combine(Path.GetTempPath(), "pv-cache-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("complete-poster");
        var hash = Hash(bytes);
        Directory.CreateDirectory(cache);
        File.WriteAllBytes(Path.Combine(cache, hash), Encoding.UTF8.GetBytes("nope"));

        PreviewStore.TryLocal(Path.Combine(Path.GetTempPath(), "missing"), cache, "mods/show-dps/preview.png", hash)
            .Should().BeNull();
    }
}
