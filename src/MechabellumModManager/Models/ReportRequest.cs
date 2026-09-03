namespace MechabellumModManager.Models;

public sealed class ReportRequest
{
    public const int MaxNotesLength = 1500;

    public string ModId { get; set; } = "";
    public string? ModName { get; set; }
    public string? Source { get; set; }
    public ReportCategory Category { get; set; }
    public string? Notes { get; set; }
    public string? AppVersion { get; set; }

    public static bool TryValidate(ReportRequest request, out string? error)
    {
        error = null;
        if (request is null)
        {
            error = "request is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.ModId))
        {
            error = "modId is required";
            return false;
        }

        if (request.Category == ReportCategory.Other &&
            string.IsNullOrWhiteSpace(request.Notes))
        {
            error = "notes required for Other";
            return false;
        }

        if (request.Notes is { Length: > MaxNotesLength })
        {
            error = $"notes must be <= {MaxNotesLength} characters";
            return false;
        }

        return true;
    }
}
