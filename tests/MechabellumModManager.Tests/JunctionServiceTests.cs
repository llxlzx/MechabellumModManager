using FluentAssertions;
using MechabellumModManager.Services;

public class JunctionServiceTests
{
    [Fact]
    public void Create_resolve_delete_preserves_target()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "Mechabellum.exe"), "x");
        var link = Path.Combine(root, "Mechabellum");
        var sut = new JunctionService();
        try
        {
            sut.CreateJunction(link, target);
            sut.IsJunction(link).Should().BeTrue();
            sut.ResolveTarget(link).Should().Be(Path.GetFullPath(target));
            File.Exists(Path.Combine(link, "Mechabellum.exe")).Should().BeTrue();

            sut.DeleteJunction(link);
            Directory.Exists(link).Should().BeFalse();
            File.Exists(Path.Combine(target, "Mechabellum.exe")).Should().BeTrue();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void IsJunction_returns_false_for_regular_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sut = new JunctionService();
        try
        {
            sut.IsJunction(root).Should().BeFalse();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void IsJunction_returns_false_for_missing_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mmm-j-missing-" + Guid.NewGuid().ToString("N"));
        new JunctionService().IsJunction(missing).Should().BeFalse();
    }

    [Fact]
    public void ResolveTarget_returns_null_when_not_junction()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sut = new JunctionService();
        try
        {
            sut.ResolveTarget(root).Should().BeNull();
            sut.ResolveTarget(Path.Combine(root, "nope")).Should().BeNull();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void CreateJunction_throws_when_link_already_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(root, "Mechabellum");
        Directory.CreateDirectory(link);
        var sut = new JunctionService();
        try
        {
            var act = () => sut.CreateJunction(link, target);
            act.Should().Throw<IOException>();
            Directory.Exists(link).Should().BeTrue();
            sut.IsJunction(link).Should().BeFalse();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void DeleteJunction_does_not_delete_regular_directory_contents()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var realDir = Path.Combine(root, "store");
        Directory.CreateDirectory(realDir);
        var marker = Path.Combine(realDir, "Mechabellum.exe");
        File.WriteAllText(marker, "keep");
        var sut = new JunctionService();
        try
        {
            var act = () => sut.DeleteJunction(realDir);
            act.Should().Throw<InvalidOperationException>();
            Directory.Exists(realDir).Should().BeTrue();
            File.Exists(marker).Should().BeTrue();
            File.ReadAllText(marker).Should().Be("keep");
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    static void Cleanup(JunctionService sut, string root)
    {
        try
        {
            if (!Directory.Exists(root))
                return;

            foreach (var dir in Directory.GetDirectories(root))
            {
                try
                {
                    if (sut.IsJunction(dir))
                        sut.DeleteJunction(dir);
                }
                catch
                {
                    // Best-effort unlink before recursive delete of the sandbox.
                }
            }

            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Temp sandbox leftover is non-fatal for the test run.
        }
    }
}
