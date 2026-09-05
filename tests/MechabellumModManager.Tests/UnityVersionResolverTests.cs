using FluentAssertions;
using MechabellumModManager.Services;

public class UnityVersionResolverTests
{
    [Fact]
    public void TryResolve_reads_version_from_globalgamemanagers()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-uv-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = Path.Combine(root, "Mechabellum_Data");
            Directory.CreateDirectory(data);
            File.WriteAllBytes(Path.Combine(data, "globalgamemanagers"),
                System.Text.Encoding.ASCII.GetBytes("xxxx2022.3.62f3yyyy"));

            new UnityVersionResolver().TryResolve(root, out var v).Should().BeTrue();
            v.Should().Be("2022.3.62");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TryResolve_false_when_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-uv-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            new UnityVersionResolver().TryResolve(root, out var v).Should().BeFalse();
            v.Should().BeNull();
        }
        finally { Directory.Delete(root, true); }
    }
}
