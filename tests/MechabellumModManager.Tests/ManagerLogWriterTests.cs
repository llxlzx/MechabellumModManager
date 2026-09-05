using FluentAssertions;
using MechabellumModManager.Services;

public class ManagerLogWriterTests
{
    [Fact]
    public void Append_creates_daily_file_and_allows_concurrent_read()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-logw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var writer = new ManagerLogWriter(dir);
            writer.Append("[12:00:00] hello");

            var files = writer.ListRecentLogFiles();
            files.Should().ContainSingle();
            File.ReadAllText(files[0]).Should().Contain("hello");

            writer.Append("[12:00:01] world");
            using (var fs = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                sr.ReadToEnd().Should().Contain("world");
            }
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void PurgeOldFiles_removes_beyond_max_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-logw-purge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var day = DateTime.Today.AddDays(-i);
                File.WriteAllText(Path.Combine(dir, $"manager-{day:yyyy-MM-dd}.log"), "x");
            }

            var writer = new ManagerLogWriter(dir, retainDays: 14, maxFiles: 3);
            writer.PurgeOldFiles();

            Directory.GetFiles(dir, "manager-*.log").Should().HaveCount(3);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Append_does_not_throw_when_directory_missing_until_create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-logw-miss-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new ManagerLogWriter(dir);
            var act = () => writer.Append("line");
            act.Should().NotThrow();
            Directory.Exists(dir).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
