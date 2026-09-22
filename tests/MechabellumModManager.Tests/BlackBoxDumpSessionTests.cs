using BlackBox.Core;
using FluentAssertions;

public class BlackBoxDumpSessionTests
{
    [Fact]
    public void Arguments_keep_a_spaced_path_as_one_token()
    {
        var args = DumpArguments.Format(12, 99, @"C:\Program Files\Mechabellum\UserData\BlackBox\blackbox.dmp.tmp");
        args.Should().Be("--pid 12 --started 99 --out \"C:\\Program Files\\Mechabellum\\UserData\\BlackBox\\blackbox.dmp.tmp\"");
    }

    [Fact]
    public void Missing_helper_does_not_run()
    {
        var runner = new Fake();
        var outcome = DumpSession.Complete("missing.exe", "--pid 1", "a.tmp", "a.dmp", runner);
        outcome.Status.Should().Be("helper-missing");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public void Timeout_is_timed_out()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var helper = Path.Combine(dir, "helper.exe");
        File.WriteAllText(helper, "x");

        var outcome = DumpSession.Complete(helper, "--pid 1", "a.tmp", "a.dmp",
            new Fake(new DumpRunResult(false, null, true)));
        outcome.Status.Should().Be("timed-out");
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Nonzero_exit_deletes_empty_tmp_and_does_not_replace_dump()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var helper = Path.Combine(dir, "helper.exe");
        var tmp = Path.Combine(dir, "blackbox.dmp.tmp");
        var finalPath = Path.Combine(dir, "blackbox.dmp");
        File.WriteAllText(helper, "x");
        File.WriteAllBytes(tmp, Array.Empty<byte>());
        File.WriteAllBytes(finalPath, new byte[] { 1, 2 });

        var outcome = DumpSession.Complete(helper, "--pid 1", tmp, finalPath,
            new Fake(new DumpRunResult(false, 3, false)));

        outcome.Status.Should().Be("failed");
        File.Exists(tmp).Should().BeFalse();
        File.ReadAllBytes(finalPath).Should().Equal(1, 2);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Zero_exit_and_nonempty_tmp_replaces_dump()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var helper = Path.Combine(dir, "helper.exe");
        var tmp = Path.Combine(dir, "blackbox.dmp.tmp");
        var finalPath = Path.Combine(dir, "blackbox.dmp");
        File.WriteAllText(helper, "x");
        File.WriteAllBytes(tmp, new byte[] { 9, 8 });

        var outcome = DumpSession.Complete(helper, "--pid 1", tmp, finalPath,
            new Fake(new DumpRunResult(false, 0, false)));

        outcome.Status.Should().Be("written");
        outcome.Error.Should().BeEmpty();
        File.ReadAllBytes(finalPath).Should().Equal(9, 8);
        Directory.Delete(dir, true);
    }

    sealed class Fake : IDumpRunner
    {
        readonly DumpRunResult _result;
        public int Calls;
        public Fake() : this(new DumpRunResult(false, 0, false)) { }
        public Fake(DumpRunResult result) => _result = result;
        public DumpRunResult Run(string exePath, string arguments, int timeoutMs)
        {
            Calls++;
            return _result;
        }
    }
}
