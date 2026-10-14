using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public sealed class ModRemovalEnforcerTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "mmm-removal-" + Guid.NewGuid().ToString("N"));
    readonly PathsService _paths;
    readonly JsonStore _store = new();
    readonly ProfileService _profiles;
    readonly ModLibraryService _library;
    readonly FakeProcessProbe _probe = new();
    readonly string _game;

    public ModRemovalEnforcerTests()
    {
        _paths = new PathsService(Path.Combine(_root, "data"));
        _paths.EnsureCreated();
        _profiles = new ProfileService(_paths, _store);
        _profiles.EnsureDefaults();
        _library = new ModLibraryService(_paths, new AssemblyInspector(), _store, _profiles);
        _game = Path.Combine(_root, "game");
        Directory.CreateDirectory(Path.Combine(_game, "Mods"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* cleanup */ }
    }

    ModRemovalEnforcer Enforcer() => new(_library, _store, _probe);

    /// <summary>Single-folder layout: the plain manifest at its own recorded root, fallback in the game folder.</summary>
    ModRemovalResult Run(IReadOnlyList<CatalogRemoval>? removed) =>
        Enforcer().Run(removed, [new DeployTarget(_paths.DeployManifestPath, null)], [_game]);

    static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Library package with one Mods DLL, enabled in the default profile, optionally deployed.</summary>
    string AddPackage(string id, string dll, string content, string? catalogId = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var dir = Path.Combine(_paths.LibraryRoot, "mods", id);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, dll), bytes);
        var catalogJson = catalogId is null ? "null" : $"\"{catalogId}\"";
        File.WriteAllText(
            Path.Combine(dir, "package.json"),
            $$"""
            {
              "id": "{{id}}",
              "catalogId": {{catalogJson}},
              "displayName": "{{Path.GetFileNameWithoutExtension(dll)}}",
              "type": "melon_mod",
              "files": [ { "relativePathInPackage": "{{dll}}", "sha256": "{{Sha(bytes)}}" } ]
            }
            """);
        var indexPath = Path.Combine(_paths.LibraryRoot, "index.json");
        var ids = _library.List().Select(p => p.Id).Append(id).ToArray();
        _store.Save(indexPath, new { packageIds = ids });
        _profiles.SetEnabled("default", id, true);
        return Sha(bytes);
    }

    void Deploy(string manifestPath, string gameRoot, params (string Rel, string PackageId, string Content)[] files)
    {
        var manifest = new DeployManifest { GamePath = gameRoot, ProfileId = "default" };
        foreach (var (rel, packageId, content) in files)
        {
            var abs = Path.Combine(gameRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            var bytes = Encoding.UTF8.GetBytes(content);
            File.WriteAllBytes(abs, bytes);
            manifest.Files.Add(new ManifestFileEntry { RelativePath = rel, PackageId = packageId, Sha256 = Sha(bytes) });
        }

        _store.Save(manifestPath, manifest);
    }

    DeployManifest Manifest(string path) => _store.LoadOrDefault(path, () => new DeployManifest());

    static CatalogRemoval Removal(string id, string? reason = null, params string[] hashes) =>
        new() { Id = id, Reason = reason, Sha256 = hashes.ToList() };

    [Fact]
    public void Removes_library_package_profile_entry_and_deployed_copy()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        AddPackage("grid-1", "Grid.dll", "grid", catalogId: "show-grid");
        Deploy(_paths.DeployManifestPath, _game, ("Mods/Econ.dll", "econ-1", "econ"), ("Mods/Grid.dll", "grid-1", "grid"));

        var result = Run([Removal("opponent-economy", "unfair")]);

        result.Removed.Should().ContainSingle(r => r.Name == "Econ" && r.Reason == "unfair");
        _library.List().Select(p => p.Id).Should().Equal("grid-1");
        Directory.Exists(Path.Combine(_paths.LibraryRoot, "mods", "econ-1")).Should().BeFalse();
        _profiles.Get("default").EnabledPackageIds.Should().Equal("grid-1");
        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeFalse();
        File.Exists(Path.Combine(_game, "Mods", "Grid.dll")).Should().BeTrue();
        Manifest(_paths.DeployManifestPath).Files.Select(f => f.RelativePath).Should().Equal("Mods/Grid.dll");
    }

    [Fact]
    public void A_same_named_file_with_other_bytes_is_left_alone()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        Deploy(_paths.DeployManifestPath, _game, ("Mods/Econ.dll", "econ-1", "econ"));
        File.WriteAllText(Path.Combine(_game, "Mods", "Econ.dll"), "player rebuilt this");

        Run([Removal("opponent-economy")]);

        File.ReadAllText(Path.Combine(_game, "Mods", "Econ.dll")).Should().Be("player rebuilt this");
        Manifest(_paths.DeployManifestPath).Files.Should().BeEmpty();
        _library.List().Should().BeEmpty();
    }

    [Fact]
    public void Nothing_is_touched_while_the_game_runs_and_the_next_run_finishes()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        Deploy(_paths.DeployManifestPath, _game, ("Mods/Econ.dll", "econ-1", "econ"));
        _probe.GameRunning = true;

        var deferred = Run([Removal("opponent-economy")]);

        deferred.Removed.Should().BeEmpty();
        deferred.Deferred.Should().Equal("Econ");
        _library.List().Should().ContainSingle();
        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeTrue();

        _probe.GameRunning = false;
        var done = Run([Removal("opponent-economy")]);

        done.Removed.Should().ContainSingle();
        _library.List().Should().BeEmpty();
        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeFalse();
    }

    [Fact]
    public void An_orphan_deployed_copy_is_found_by_hash()
    {
        Deploy(_paths.DeployManifestPath, _game, ("Mods/Econ.dll", "deleted-earlier", "econ"));
        var hash = Sha(Encoding.UTF8.GetBytes("econ"));

        Run([Removal("opponent-economy", null, hash.ToUpperInvariant())]);

        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeFalse();
        Manifest(_paths.DeployManifestPath).Files.Should().BeEmpty();
    }

    /// <summary>
    /// Dual-folder layout: both branch manifests record the Steam link, which only reaches the
    /// mounted store. The enforcer must clean each physical store, not resolve through the link.
    /// </summary>
    [Fact]
    public void Both_dual_folder_stores_are_cleaned_even_though_manifests_record_the_link()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        var link = Path.Combine(_root, "Mechabellum");
        var official = Path.Combine(_root, "Mechabellum_official");
        var beta = Path.Combine(_root, "Mechabellum_beta");
        var officialManifest = _paths.GetDeployManifestPath(GameBranch.Official, enabled: true);
        var betaManifest = _paths.GetDeployManifestPath(GameBranch.Beta, enabled: true);
        Deploy(officialManifest, official, ("Mods/Econ.dll", "econ-1", "econ"));
        Deploy(betaManifest, beta, ("Mods/Econ.dll", "econ-1", "econ"));
        foreach (var path in new[] { officialManifest, betaManifest })
        {
            var m = Manifest(path);
            m.GamePath = link;
            _store.Save(path, m);
        }

        var result = Enforcer().Run(
            [Removal("opponent-economy")],
            [new DeployTarget(officialManifest, official), new DeployTarget(betaManifest, beta)],
            [official, beta]);

        result.Removed.Should().ContainSingle();
        File.Exists(Path.Combine(official, "Mods", "Econ.dll")).Should().BeFalse();
        File.Exists(Path.Combine(beta, "Mods", "Econ.dll")).Should().BeFalse();
        Manifest(officialManifest).Files.Should().BeEmpty();
        Manifest(betaManifest).Files.Should().BeEmpty();
    }

    [Fact]
    public void An_unreachable_store_keeps_its_entries_and_defers_the_package()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        var official = Path.Combine(_root, "Mechabellum_official");
        var missing = Path.Combine(_root, "Mechabellum_beta_unplugged");
        var officialManifest = _paths.GetDeployManifestPath(GameBranch.Official, enabled: true);
        var betaManifest = _paths.GetDeployManifestPath(GameBranch.Beta, enabled: true);
        Deploy(officialManifest, official, ("Mods/Econ.dll", "econ-1", "econ"));
        _store.Save(betaManifest, new DeployManifest
        {
            GamePath = missing,
            Files = [new ManifestFileEntry { RelativePath = "Mods/Econ.dll", PackageId = "econ-1", Sha256 = Sha(Encoding.UTF8.GetBytes("econ")) }]
        });

        var result = Enforcer().Run(
            [Removal("opponent-economy")],
            [new DeployTarget(officialManifest, official), new DeployTarget(betaManifest, missing)],
            [official, missing]);

        result.Deferred.Should().Equal("Econ");
        _library.List().Should().ContainSingle();
        Manifest(betaManifest).Files.Should().ContainSingle();
    }

    [Fact]
    public void A_package_without_records_waits_while_a_store_is_unreachable()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        var missing = Path.Combine(_root, "Mechabellum_beta_unplugged");

        var result = Enforcer().Run([Removal("opponent-economy")], [], [_game, missing]);

        result.Deferred.Should().Equal("Econ");
        _library.List().Should().ContainSingle();
    }

    [Fact]
    public void Fallback_keeps_a_same_named_file_with_other_bytes()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        File.WriteAllText(Path.Combine(_game, "Mods", "Econ.dll"), "player's own build");

        var result = Run([Removal("opponent-economy")]);

        result.Removed.Should().ContainSingle();
        File.ReadAllText(Path.Combine(_game, "Mods", "Econ.dll")).Should().Be("player's own build");
        _library.List().Should().BeEmpty();
    }

    [Fact]
    public void Fallback_deletes_bytes_on_the_takedown_list_even_from_another_build()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        var older = Encoding.UTF8.GetBytes("econ v0");
        File.WriteAllBytes(Path.Combine(_game, "Mods", "Econ.dll"), older);

        Run([Removal("opponent-economy", null, Sha(older))]);

        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeFalse();
    }

    [Fact]
    public void An_entry_that_escapes_the_game_folder_is_dropped_without_deleting()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        var outside = Path.Combine(_root, "outside.dll");
        File.WriteAllText(outside, "econ");
        _store.Save(_paths.DeployManifestPath, new DeployManifest
        {
            GamePath = _game,
            Files = [new ManifestFileEntry { RelativePath = "../outside.dll", PackageId = "econ-1", Sha256 = Sha(Encoding.UTF8.GetBytes("econ")) }]
        });

        Run([Removal("opponent-economy")]);

        File.Exists(outside).Should().BeTrue();
        Manifest(_paths.DeployManifestPath).Files.Should().BeEmpty();
    }

    [Fact]
    public void A_locked_deployed_file_defers_the_whole_package()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        Deploy(_paths.DeployManifestPath, _game, ("Mods/Econ.dll", "econ-1", "econ"));

        ModRemovalResult result;
        using (File.Open(Path.Combine(_game, "Mods", "Econ.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            result = Run([Removal("opponent-economy")]);

        result.Deferred.Should().Equal("Econ");
        _library.List().Should().ContainSingle();
        Manifest(_paths.DeployManifestPath).Files.Should().ContainSingle();
    }

    [Fact]
    public void A_package_without_a_deploy_record_still_loses_its_game_copy()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");
        File.WriteAllText(Path.Combine(_game, "Mods", "Econ.dll"), "econ");

        Run([Removal("opponent-economy")]);

        File.Exists(Path.Combine(_game, "Mods", "Econ.dll")).Should().BeFalse();
        _library.List().Should().BeEmpty();
    }

    [Fact]
    public void No_list_means_no_work()
    {
        AddPackage("econ-1", "Econ.dll", "econ", catalogId: "opponent-economy");

        var result = Run(null);

        result.HasWork.Should().BeFalse();
        _library.List().Should().ContainSingle();
    }
}
