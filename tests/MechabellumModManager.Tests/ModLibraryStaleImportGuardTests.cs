using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModLibraryStaleImportGuardTests
{
    [Fact]
    public void ImportFromGame_skips_same_filename_when_library_already_owns_slot_without_sample_dll()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(Path.GetTempPath(), "mmm-game-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", "friendoverlay-new");
            Directory.CreateDirectory(pkgDir);
            var libBytes = Encoding.UTF8.GetBytes("library-current-bytes");
            File.WriteAllBytes(Path.Combine(pkgDir, "FriendOverlay.dll"), libBytes);
            var libHash = Convert.ToHexString(SHA256.HashData(libBytes)).ToLowerInvariant();
            // Matches ModLibraryService PackageJsonOptions (snake_case type).
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), $$"""
                {
                  "id": "friendoverlay-new",
                  "displayName": "FriendOverlay",
                  "type": "melon_mod",
                  "files": [
                    { "relativePathInPackage": "FriendOverlay.dll", "sha256": "{{libHash}}" }
                  ]
                }
                """);

            var store = new JsonStore();
            store.Save(
                Path.Combine(paths.LibraryRoot, "index.json"),
                new { PackageIds = new List<string> { "friendoverlay-new" } });

            Directory.CreateDirectory(game);
            File.WriteAllBytes(Path.Combine(game, "Mechabellum.exe"), new byte[] { 0x4D, 0x5A });
            Directory.CreateDirectory(Path.Combine(game, "Mods"));
            File.WriteAllBytes(
                Path.Combine(game, "Mods", "FriendOverlay.dll"),
                Encoding.UTF8.GetBytes("stale-game-folder-bytes"));

            var lib = new ModLibraryService(
                paths, new AssemblyInspector(), store, new ProfileService(paths, store));
            lib.LibraryOwnsDeployFileName("FriendOverlay.dll", ModPackageType.MelonMod).Should().BeTrue();

            var result = lib.ImportFromGame(game);
            result.Imported.Should().Be(0);
            result.Skipped.Should().BeGreaterThanOrEqualTo(1);
            result.Messages.Should().Contain(m => m.Contains("同名已在库中", StringComparison.Ordinal));
            lib.List().Should().ContainSingle(p => p.Id == "friendoverlay-new");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }

    [Fact]
    public void TryRemoveDeployedPrimaryDll_deletes_hash_matched_file()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(Path.GetTempPath(), "mmm-game-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", "friendoverlay-old");
            Directory.CreateDirectory(pkgDir);
            var bytes = Encoding.UTF8.GetBytes("delete-me-bytes");
            File.WriteAllBytes(Path.Combine(pkgDir, "FriendOverlay.dll"), bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), $$"""
                {
                  "id": "friendoverlay-old",
                  "displayName": "FriendOverlay",
                  "type": "melon_mod",
                  "files": [
                    { "relativePathInPackage": "FriendOverlay.dll", "sha256": "{{hash}}" }
                  ]
                }
                """);

            var store = new JsonStore();
            store.Save(
                Path.Combine(paths.LibraryRoot, "index.json"),
                new { PackageIds = new List<string> { "friendoverlay-old" } });

            Directory.CreateDirectory(Path.Combine(game, "Mods"));
            var dest = Path.Combine(game, "Mods", "FriendOverlay.dll");
            File.WriteAllBytes(dest, bytes);

            var lib = new ModLibraryService(
                paths, new AssemblyInspector(), store, new ProfileService(paths, store));
            var loaded = lib.List().Should().ContainSingle().Subject;
            lib.TryRemoveDeployedPrimaryDll(game, loaded).Should().BeTrue();
            File.Exists(dest).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }
}
