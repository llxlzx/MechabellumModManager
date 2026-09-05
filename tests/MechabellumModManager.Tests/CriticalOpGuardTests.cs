using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class CriticalOpGuardTests
{
    sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mmm-critical-op-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    [Fact]
    public void Begin_writes_marker_and_sets_IsRunning()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);

        guard.Begin(CriticalOpKind.BranchDiskWrite, "SwitchToBeta");

        guard.IsRunning.Should().BeTrue();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeTrue();
        var json = File.ReadAllText(paths.CriticalOpMarkerPath);
        json.Should().Contain("BranchDiskWrite");
        json.Should().Contain("SwitchToBeta");
    }

    [Fact]
    public void Complete_deletes_marker_and_clears_IsRunning()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);
        guard.Begin(CriticalOpKind.MelonInstall, "Install");
        guard.Complete();

        guard.IsRunning.Should().BeFalse();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public void TryLoadInterrupted_true_when_leftover_file_and_not_running()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        Directory.CreateDirectory(paths.DataRoot);
        File.WriteAllText(paths.CriticalOpMarkerPath, """
            {"kind":"BranchDiskWrite","startedUtc":"2026-09-05T12:00:00+00:00","detail":"x","pid":1}
            """);

        var guard = new CriticalOpGuard(paths);
        guard.TryLoadInterrupted(out var marker).Should().BeTrue();
        marker!.Kind.Should().Be(CriticalOpKind.BranchDiskWrite);
    }

    [Fact]
    public void ClearInterrupted_removes_stale_file()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);
        guard.Begin(CriticalOpKind.OrphanRepair, "repair");
        guard.Complete();
        File.WriteAllText(paths.CriticalOpMarkerPath, """{"kind":"OrphanRepair","startedUtc":"2026-09-05T12:00:00+00:00","detail":"stale","pid":1}""");

        guard.ClearInterrupted();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeFalse();
    }
}
