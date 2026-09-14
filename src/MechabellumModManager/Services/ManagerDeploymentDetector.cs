using System.IO;

namespace MechabellumModManager.Services;

public enum ManagerDeploymentKind
{
    Installed,
    Portable
}

public static class ManagerDeploymentDetector
{
    public const string UninstallerFileName = "unins000.exe";

    public static ManagerDeploymentKind Detect(string? appBaseDirectory = null)
    {
        var root = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory;
        var unins = Path.Combine(root, UninstallerFileName);
        return File.Exists(unins) ? ManagerDeploymentKind.Installed : ManagerDeploymentKind.Portable;
    }
}
