using BlackBox.Core;
using FluentAssertions;

public class BlackBoxIncidentWriterTests
{
    [Fact]
    public void Scene_read_failure_still_advances_the_timestamp()
    {
        var scene = "old";
        var published = 0L;
        var called = false;
        var next = HeartbeatPulse.Apply(2_000_0000, 0, 10_000_000, () =>
        {
            called = true;
            throw new InvalidOperationException("scene");
        }, ref scene, ref published);

        called.Should().BeTrue();
        next.Should().Be(2_000_0000);
        scene.Should().Be("old");
    }

    [Fact]
    public void Heartbeat_is_published_before_scene_read()
    {
        var scene = "old";
        var published = 0L;
        const long now = 2_000_0000;
        long seen = 0;
        var next = HeartbeatPulse.Apply(now, 0, 10_000_000, () =>
        {
            seen = Volatile.Read(ref published);
            throw new InvalidOperationException("scene");
        }, ref scene, ref published);

        seen.Should().Be(now);
        next.Should().Be(now);
        scene.Should().Be("old");
    }

    [Fact]
    public void Pulse_inside_one_second_does_not_read_the_scene()
    {
        var scene = "keep";
        var published = 0L;
        var next = HeartbeatPulse.Apply(10_000_000, 5_000_000, 10_000_000, () =>
        {
            throw new InvalidOperationException("should not read");
        }, ref scene, ref published);
        next.Should().Be(5_000_000);
        scene.Should().Be("keep");
    }

    [Fact]
    public void Summary_and_heartbeat_round_trip_keys()
    {
        using var dir = new TempDir();
        var heartbeat = IncidentWriter.FormatHeartbeat(
            DateTimeOffset.Parse("2026-09-22T12:00:00Z"), 10, 15, "2022.3.62", "unknown");
        IncidentWriter.WriteHeartbeat(dir.Path, heartbeat);
        File.ReadAllText(Path.Combine(dir.Path, "heartbeat.txt")).Should().Contain("scene: unknown");

        var summary = IncidentWriter.FormatSummary(new SummaryFields(
            "hang", "2026-09-22T12:00:20Z", 10, 35, 20, @"C:\Games\Mechabellum",
            "2022.3.62", "battle", "unknown", "A.dll", "B.dll", "pending", ""));
        IncidentWriter.WriteSummary(dir.Path, summary);
        var text = File.ReadAllText(Path.Combine(dir.Path, "summary.txt"));
        text.Should().Contain("trigger: hang");
        text.Should().Contain("silentSeconds: 20");
        text.Should().Contain("dump: pending");
        text.Should().Contain("workingSetMb: unknown");
    }

    sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bb-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
