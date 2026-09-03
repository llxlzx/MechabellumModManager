using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ProfileServiceTests
{
    [Fact]
    public void EnsureDefaults_creates_default_profile_named_默认()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var svc = new ProfileService(paths, new JsonStore());
            svc.EnsureDefaults();

            var list = svc.List();
            list.Should().ContainSingle();
            list[0].Id.Should().Be("default");
            list[0].Name.Should().Be("默认");
            list[0].EnabledPackageIds.Should().BeEmpty();

            // Idempotent
            svc.EnsureDefaults();
            svc.List().Should().ContainSingle(p => p.Id == "default");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void EnsureDefaults_repairs_default_profile_name()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            store.Save(Path.Combine(paths.ProfilesDir, "default.json"), new Profile
            {
                Id = "default",
                Name = "Default",
                EnabledPackageIds = new List<string> { "keep-me" }
            });

            var svc = new ProfileService(paths, store);
            svc.EnsureDefaults();

            var p = svc.Get("default");
            p.Name.Should().Be("默认");
            p.EnabledPackageIds.Should().Contain("keep-me");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void EnsureDefaults_recreates_corrupt_default_profile()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            File.WriteAllText(Path.Combine(paths.ProfilesDir, "default.json"), "{ not-json");

            var svc = new ProfileService(paths, new JsonStore());
            svc.EnsureDefaults();

            var list = svc.List();
            list.Should().ContainSingle(p => p.Id == "default");
            list[0].Name.Should().Be("默认");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Create_adds_named_profile()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var svc = new ProfileService(paths, new JsonStore());
            svc.EnsureDefaults();

            var created = svc.Create("比赛用");
            created.Name.Should().Be("比赛用");
            created.Id.Should().NotBeNullOrWhiteSpace();
            created.Id.Should().NotBe("default");
            created.EnabledPackageIds.Should().BeEmpty();

            svc.List().Should().HaveCount(2);
            svc.Get(created.Id).Name.Should().Be("比赛用");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void SetEnabled_persists_immediately()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            var svc = new ProfileService(paths, store);
            svc.EnsureDefaults();
            var p = svc.List().Single();
            svc.SetEnabled(p.Id, "pkg1", true);
            var again = new ProfileService(paths, store).Get(p.Id);
            again.EnabledPackageIds.Should().Contain("pkg1");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void SetEnabled_false_removes_package_and_persists()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            var svc = new ProfileService(paths, store);
            svc.EnsureDefaults();
            var p = svc.List().Single();
            svc.SetEnabled(p.Id, "pkg1", true);
            svc.SetEnabled(p.Id, "pkg1", false);
            new ProfileService(paths, store).Get(p.Id).EnabledPackageIds.Should().NotContain("pkg1");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void RemovePackageFromAllProfiles_clears_id_everywhere()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            var svc = new ProfileService(paths, store);
            svc.EnsureDefaults();
            var a = svc.List().Single();
            var b = svc.Create("开发用");
            svc.SetEnabled(a.Id, "pkg-x", true);
            svc.SetEnabled(b.Id, "pkg-x", true);
            svc.SetEnabled(b.Id, "pkg-y", true);

            svc.RemovePackageFromAllProfiles("pkg-x");

            var reloaded = new ProfileService(paths, store);
            reloaded.Get(a.Id).EnabledPackageIds.Should().NotContain("pkg-x");
            reloaded.Get(b.Id).EnabledPackageIds.Should().NotContain("pkg-x");
            reloaded.Get(b.Id).EnabledPackageIds.Should().Contain("pkg-y");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Rename_and_Duplicate_work()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var svc = new ProfileService(paths, new JsonStore());
            svc.EnsureDefaults();
            var src = svc.List().Single();
            svc.SetEnabled(src.Id, "pkg1", true);

            svc.Rename(src.Id, "仅相机");
            svc.Get(src.Id).Name.Should().Be("仅相机");

            var copy = svc.Duplicate(src.Id, "仅相机-副本");
            copy.Id.Should().NotBe(src.Id);
            copy.Name.Should().Be("仅相机-副本");
            copy.EnabledPackageIds.Should().Contain("pkg1");
            svc.List().Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Delete_cannot_remove_last_profile_and_switches_active()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            var svc = new ProfileService(paths, store);
            svc.EnsureDefaults();
            var extra = svc.Create("临时");

            // Active is default; deleting default should switch active to remaining
            store.Save(paths.ConfigPath, new AppConfig { ActiveProfileId = "default", DataRoot = data });
            svc.Delete("default");
            svc.List().Should().ContainSingle(p => p.Id == extra.Id);
            store.LoadOrDefault(paths.ConfigPath, () => new AppConfig()).ActiveProfileId.Should().Be(extra.Id);

            var act = () => svc.Delete(extra.Id);
            act.Should().Throw<InvalidOperationException>();
            svc.List().Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }
}
