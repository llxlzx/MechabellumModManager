using FluentAssertions;
using MechabellumModManager.Services;

public class TaskProgressSessionTests
{
    [Fact]
    public void Begin_sets_indeterminate_session_lock()
    {
        var s = new TaskProgressSession();
        s.Begin(ManagerTaskKind.Deploy, "Deploy", "Copying…", sessionLock: true);
        s.HasTask.Should().BeTrue();
        s.IsIndeterminate.Should().BeTrue();
        s.SessionLock.Should().BeTrue();
        s.Title.Should().Be("Deploy");
    }

    [Fact]
    public void Report_percent_makes_determinate()
    {
        var s = new TaskProgressSession();
        s.Begin(ManagerTaskKind.MelonInstall, "Melon", "Download", sessionLock: true);
        s.Report("Downloading", 42);
        s.Percent.Should().Be(42);
        s.IsIndeterminate.Should().BeFalse();
        s.Message.Should().Be("Downloading");
    }

    [Fact]
    public void Complete_clears_ephemeral_task()
    {
        var s = new TaskProgressSession();
        s.Begin(ManagerTaskKind.CheckUpdate, "Update", "Checking", sessionLock: false);
        s.Complete("Done");
        s.HasTask.Should().BeFalse();
    }

    [Fact]
    public void Sticky_survives_nested_busy_complete()
    {
        var s = new TaskProgressSession();
        s.Begin(ManagerTaskKind.BranchWizard, "Wizard", "Waiting download", sessionLock: true, sticky: true);
        s.Begin(ManagerTaskKind.GenericBusy, "Busy", "Moving folder", sessionLock: true);
        s.Message.Should().Be("Moving folder");
        s.Kind.Should().Be(ManagerTaskKind.BranchWizard);
        s.Complete();
        s.HasTask.Should().BeTrue();
        s.Kind.Should().Be(ManagerTaskKind.BranchWizard);
        s.Clear();
        s.HasTask.Should().BeFalse();
    }

    [Fact]
    public void Sticky_complete_without_nest_keeps_task()
    {
        var s = new TaskProgressSession();
        s.Begin(ManagerTaskKind.BranchSettle, "Settle", "Waiting Steam", sessionLock: true, sticky: true);
        s.Complete("Still waiting confirm");
        s.HasTask.Should().BeTrue();
        s.Message.Should().Be("Still waiting confirm");
    }
}
