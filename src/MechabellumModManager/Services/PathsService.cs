using System.IO;

namespace MechabellumModManager.Services;

public sealed class PathsService
{
    public string DataRoot { get; }

    public PathsService(string? overrideRoot = null)
    {
        DataRoot = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MechabellumModManager")
            : overrideRoot;
    }

    public string ConfigPath => Path.Combine(DataRoot, "config.json");
    public string LibraryRoot => Path.Combine(DataRoot, "library");
    public string ProfilesDir => Path.Combine(DataRoot, "profiles");
    public string DeployManifestPath => Path.Combine(DataRoot, "deploy-manifest.json");
    public string DeployManifestPrevPath => Path.Combine(DataRoot, "deploy-manifest.prev.json");
    public string LogsDir => Path.Combine(DataRoot, "logs");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(LibraryRoot);
        foreach (var sub in new[] { "mods", "plugins", "userlibs", "userdata" })
            Directory.CreateDirectory(Path.Combine(LibraryRoot, sub));
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(LogsDir);
    }
}
