using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Copies staged portable update files over the live app directory, then removes staging.
/// Never touches a sibling <c>data</c> folder (portable user data).
/// </summary>
public static class PortableUpdateApplier
{
    public const string StagingFolderName = "_update_staging";
    public const string DataFolderName = "data";

    public static void ApplyStagedFiles(string stagingDir, string appBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
        if (!Directory.Exists(stagingDir))
            throw new DirectoryNotFoundException(stagingDir);

        appBaseDirectory = Path.GetFullPath(appBaseDirectory);
        stagingDir = Path.GetFullPath(stagingDir);

        foreach (var file in Directory.EnumerateFiles(stagingDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(stagingDir, file);
            if (IsUnderDataFolder(relative))
                continue;

            var dest = Path.Combine(appBaseDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        try
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        catch
        {
            /* best effort */
        }
    }

    static bool IsUnderDataFolder(string relative)
    {
        var first = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return first.Length > 0
               && string.Equals(first[0], DataFolderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes a cmd helper that waits for <paramref name="pid"/>, applies staging, relaunches, and exits.
    /// </summary>
    public static string WriteReplaceHelper(
        string tempDir,
        int pid,
        string stagingDir,
        string appBaseDirectory,
        string exeName = "MechabellumModManager.exe")
    {
        Directory.CreateDirectory(tempDir);
        var helperPath = Path.Combine(tempDir, "mmm-portable-update.cmd");
        var stagingEsc = stagingDir.Replace("\"", "");
        var appEsc = appBaseDirectory.Replace("\"", "");
        var exeEsc = Path.Combine(appEsc, exeName).Replace("\"", "");

        // Use robocopy for robust tree copy; /XD data skips portable user data if present in staging.
        var script =
            $"""
            @echo off
            setlocal
            :wait
            tasklist /FI "PID eq {pid}" 2>NUL | find "{pid}" >NUL
            if not errorlevel 1 (
              timeout /t 1 /nobreak >NUL
              goto wait
            )
            robocopy "{stagingEsc}" "{appEsc}" /E /IS /IT /XD data _update_staging >NUL
            rmdir /s /q "{stagingEsc}" 2>NUL
            start "" "{exeEsc}"
            del "%~f0"
            """;
        File.WriteAllText(helperPath, script);
        return helperPath;
    }
}
