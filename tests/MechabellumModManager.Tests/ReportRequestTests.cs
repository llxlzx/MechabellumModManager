using FluentAssertions;
using MechabellumModManager.Models;

public class ReportRequestTests
{
    [Fact]
    public void TryValidate_Other_requires_notes()
    {
        var request = new ReportRequest
        {
            ModId = "demo",
            Category = ReportCategory.Other,
            Notes = "  "
        };

        ReportRequest.TryValidate(request, out var error).Should().BeFalse();
        error.Should().Contain("notes");
    }

    [Fact]
    public void TryValidate_Cheat_allows_empty_notes()
    {
        var request = new ReportRequest
        {
            ModId = "demo",
            Category = ReportCategory.Cheat
        };

        ReportRequest.TryValidate(request, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_requires_modId()
    {
        var request = new ReportRequest { Category = ReportCategory.Virus };
        ReportRequest.TryValidate(request, out _).Should().BeFalse();
    }
}
