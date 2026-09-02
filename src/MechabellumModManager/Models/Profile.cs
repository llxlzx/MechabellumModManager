namespace MechabellumModManager.Models;

public sealed class Profile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> EnabledPackageIds { get; set; } = new();
}
