namespace MechabellumModManager.Models;

public sealed class SubmitModRequest
{
    public string Name { get; set; } = "";
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? Summary { get; set; }
    public string Sha256 { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string? AppVersion { get; set; }
}
