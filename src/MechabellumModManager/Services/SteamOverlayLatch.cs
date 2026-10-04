using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MechabellumModManager.Services;

/// <summary>
/// Steam's in-game overlay restarts after Mechabellum exits and keeps the
/// library Stop button up. Turning the overlay off, while Steam itself is
/// closed, is what lets Steam drop the game from its running list.
/// </summary>
public static class SteamOverlayLatch
{
    public static void EnsureDisabledWhileSteamIsClosed()
    {
        try
        {
            if (Process.GetProcessesByName("steam").Length > 0)
                return;
            if (Process.GetProcessesByName("Mechabellum").Length > 0)
                return;

            var config = FindActiveLocalConfig();
            if (config != null)
                WriteOverlayOff(config);

            ClearStaleRunningFlag();
        }
        catch
        {
            // A missing Steam install must not stop the manager from opening.
        }
    }

    static string? FindActiveLocalConfig()
    {
        string? steamPath = null;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            steamPath = key?.GetValue("SteamPath") as string;
        }

        if (string.IsNullOrWhiteSpace(steamPath))
            return null;

        var userdata = Path.Combine(steamPath.Replace('/', Path.DirectorySeparatorChar), "userdata");
        if (!Directory.Exists(userdata))
            return null;

        string? newest = null;
        DateTime newestWrite = DateTime.MinValue;
        foreach (var account in Directory.EnumerateDirectories(userdata))
        {
            var candidate = Path.Combine(account, "config", "localconfig.vdf");
            if (!File.Exists(candidate))
                continue;
            var write = File.GetLastWriteTimeUtc(candidate);
            if (write < newestWrite)
                continue;
            newest = candidate;
            newestWrite = write;
        }

        return newest;
    }

    static void WriteOverlayOff(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains('\0'))
            return;

        var updated = Regex.Replace(
            text,
            "(\"EnableGameOverlay\"\\s+\")\\d+(\")",
            "${1}0$2",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (updated == text && text.Contains("\"EnableGameOverlay\""))
            return;

        if (!text.Contains("\"EnableGameOverlay\""))
        {
            var marker = "\"OverlayLogCleanupDone\"";
            var at = text.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                return;
            var lineStart = text.LastIndexOf('\n', at) + 1;
            var indent = text[lineStart..at];
            var newline = text.Contains("\r\n") ? "\r\n" : "\n";
            updated = text.Insert(lineStart, indent + "\"EnableGameOverlay\"\t\t\"0\"" + newline);
        }

        if (updated == text)
            return;

        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(updated));
    }

    static void ClearStaleRunningFlag()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Valve\Steam\Apps\" + SteamAppRunningState.MechabellumAppId,
            writable: true);
        key?.SetValue("Running", 0, RegistryValueKind.DWord);
    }
}
