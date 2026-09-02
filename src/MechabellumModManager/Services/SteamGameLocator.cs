using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MechabellumModManager.Services;

/// <summary>
/// Locates Mechabellum installs across common Steam library layouts (not just one machine's D: drive).
/// </summary>
public sealed class SteamGameLocator
{
    public const string GameFolderName = "Mechabellum";
    public const string GameExeName = "Mechabellum.exe";
    public const string GameAssemblyName = "GameAssembly.dll";

    public string? TryFind()
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (LooksLikeGameRoot(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static bool LooksLikeGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            return File.Exists(Path.Combine(path, GameExeName))
                   && File.Exists(Path.Combine(path, GameAssemblyName));
        }
        catch
        {
            return false;
        }
    }

    IEnumerable<string> EnumerateCandidates()
    {
        foreach (var steamRoot in EnumerateSteamRoots())
        {
            foreach (var library in EnumerateSteamLibraries(steamRoot))
            {
                yield return Path.Combine(library, "steamapps", "common", GameFolderName);
            }
        }

        // Fallbacks when Steam registry / VDF is missing.
        foreach (var drive in EnumerateFixedDriveLetters())
        {
            yield return Path.Combine(drive + @":\Steam\steamapps\common", GameFolderName);
            yield return Path.Combine(drive + @":\steam\steamapps\common", GameFolderName);
            yield return Path.Combine(drive + @":\Program Files (x86)\Steam\steamapps\common", GameFolderName);
            yield return Path.Combine(drive + @":\Program Files\Steam\steamapps\common", GameFolderName);
            yield return Path.Combine(drive + @":\SteamLibrary\steamapps\common", GameFolderName);
            yield return Path.Combine(drive + @":\Games\Steam\steamapps\common", GameFolderName);
        }
    }

    static IEnumerable<string> EnumerateSteamRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in ReadSteamPathFromRegistry())
        {
            if (seen.Add(root))
                yield return root;
        }

        foreach (var drive in EnumerateFixedDriveLetters())
        {
            foreach (var guess in new[]
                     {
                         Path.Combine(drive + @":\Program Files (x86)\Steam"),
                         Path.Combine(drive + @":\Program Files\Steam"),
                         Path.Combine(drive + @":\Steam"),
                         Path.Combine(drive + @":\steam")
                     })
            {
                if (Directory.Exists(guess) && seen.Add(guess))
                    yield return guess;
            }
        }
    }

    static IEnumerable<string> ReadSteamPathFromRegistry()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                string? path = null;
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(@"Software\Valve\Steam");
                    path = key?.GetValue("SteamPath") as string
                           ?? key?.GetValue("InstallPath") as string;
                }
                catch
                {
                    // ignore registry access issues
                }

                if (string.IsNullOrWhiteSpace(path)) continue;
                var normalized = path.Replace('/', Path.DirectorySeparatorChar).TrimEnd('\\', '/');
                if (Directory.Exists(normalized))
                    yield return normalized;
            }
        }
    }

    static IEnumerable<string> EnumerateSteamLibraries(string steamRoot)
    {
        yield return steamRoot;

        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;

        string text;
        try
        {
            text = File.ReadAllText(vdf);
        }
        catch
        {
            yield break;
        }

        // Matches both old ("1" "D:\\SteamLibrary") and new ("path" "D:\\SteamLibrary") styles.
        var matches = Regex.Matches(
            text,
            "\"(?:path|\\d+)\"\\s*\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value;
            if (raw.Contains("depotcache", StringComparison.OrdinalIgnoreCase))
                continue;

            var lib = raw.Replace(@"\\", @"\").Replace('/', Path.DirectorySeparatorChar).TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(lib) || !Directory.Exists(lib))
                continue;
            if (!seen.Add(lib))
                continue;

            yield return lib;
        }
    }

    static IEnumerable<string> EnumerateFixedDriveLetters()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            var letter = drive.Name.TrimEnd('\\', '/');
            if (letter.Length >= 1)
                yield return letter[..1];
        }
    }
}
