using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Builds machine-readable probe + findings for diagnostics packs (Track A/B).
/// </summary>
public static class DiagnosticsProbeBuilder
{
    static readonly Regex MelonTimestamp = new(
        @"^\[(?<ts>\d{2}:\d{2}:\d{2}(?:\.\d+)?)\]",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static Dictionary<string, object?> Build(DiagnosticsExportRequest request)
    {
        var gamePath = request.GamePath ?? "";
        var exportedAt = DateTimeOffset.Now;
        var detector = new GameDetector();
        var status = string.IsNullOrWhiteSpace(gamePath)
            ? null
            : detector.Detect(gamePath);

        var melonLogPath = string.IsNullOrWhiteSpace(gamePath)
            ? null
            : Path.Combine(gamePath, "MelonLoader", "Latest.log");
        var melon = InspectMelonLog(melonLogPath, exportedAt);

        var branchCfg = TryLoadBranchConfig(request.Paths.BranchSwitchConfigPath);
        var junctions = new JunctionService();
        var branch = InspectBranch(branchCfg, junctions, request);

        var files = InspectGameFiles(gamePath);
        var manifests = InspectDeployManifests(request.Paths);

        var probe = new Dictionary<string, object?>
        {
            ["exportedAt"] = exportedAt.ToString("o"),
            ["launchMode"] = request.LaunchMode,
            ["gameRunning"] = request.GameRunning,
            ["steamRunning"] = request.SteamRunning,
            ["isAwaitingSteamSettle"] = request.IsAwaitingSteamSettle,
            ["gameStatusKind"] = status?.Kind.ToString() ?? request.GameStatusKind,
            ["gameFiles"] = files,
            ["melonLatestLog"] = melon,
            ["branch"] = branch,
            ["deployManifests"] = manifests
        };

        var findings = BuildFindings(status?.Kind.ToString() ?? request.GameStatusKind, files, melon, branch, manifests, request);
        return new Dictionary<string, object?>
        {
            ["probe"] = probe,
            ["findings"] = findings
        };
    }

    static Dictionary<string, object?> InspectGameFiles(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return new Dictionary<string, object?>
            {
                ["pathPresent"] = false
            };
        }

        return new Dictionary<string, object?>
        {
            ["pathPresent"] = Directory.Exists(gamePath),
            ["hasMechabellumExe"] = File.Exists(Path.Combine(gamePath, "Mechabellum.exe")),
            ["hasGameAssembly"] = File.Exists(Path.Combine(gamePath, "GameAssembly.dll")),
            ["hasVersionDll"] = File.Exists(Path.Combine(gamePath, "version.dll")),
            ["hasWinHttpDll"] = File.Exists(Path.Combine(gamePath, "winhttp.dll")),
            ["hasMelonLoaderDir"] = Directory.Exists(Path.Combine(gamePath, "MelonLoader")),
            ["hasIl2CppAssemblies"] = GameDetector.HasIl2CppAssemblies(gamePath)
        };
    }

    internal static Dictionary<string, object?> InspectMelonLog(string? melonLogPath, DateTimeOffset exportedAt)
    {
        if (string.IsNullOrWhiteSpace(melonLogPath) || !File.Exists(melonLogPath))
        {
            return new Dictionary<string, object?>
            {
                ["exists"] = false
            };
        }

        string text;
        try { text = File.ReadAllText(melonLogPath); }
        catch
        {
            return new Dictionary<string, object?>
            {
                ["exists"] = true,
                ["unreadable"] = true
            };
        }

        var info = new FileInfo(melonLogPath);
        var ageSeconds = Math.Max(0, (exportedAt - info.LastWriteTime).TotalSeconds);
        var lines = text.Split('\n').Length;
        var hasPreferences = text.Contains("Preferences Loaded", StringComparison.OrdinalIgnoreCase);
        var hasModsSection = text.Contains("Loading Mods", StringComparison.OrdinalIgnoreCase);
        var firstTs = MelonTimestamp.Match(text).Success ? MelonTimestamp.Match(text).Groups["ts"].Value : null;
        var staleOrIncomplete = ageSeconds > 300 || (!hasPreferences && lines < 80);

        return new Dictionary<string, object?>
        {
            ["exists"] = true,
            ["lastWriteTimeLocal"] = info.LastWriteTime.ToString("o"),
            ["ageSeconds"] = Math.Round(ageSeconds, 1),
            ["lineCount"] = lines,
            ["firstTimestamp"] = firstTs,
            ["hasPreferencesLoaded"] = hasPreferences,
            ["hasLoadingModsSection"] = hasModsSection,
            ["staleOrIncomplete"] = staleOrIncomplete
        };
    }

    static Dictionary<string, object?> InspectBranch(
        BranchSwitchConfig? cfg,
        JunctionService junctions,
        DiagnosticsExportRequest request)
    {
        var enabled = cfg?.Enabled == true || request.BranchSwitchEnabled;
        var link = cfg?.SteamLinkPath ?? "";
        var official = cfg?.OfficialStorePath ?? "";
        var beta = cfg?.BetaStorePath ?? "";
        var isJunction = !string.IsNullOrWhiteSpace(link) && junctions.IsJunction(link);
        string? target = null;
        if (isJunction)
        {
            try { target = junctions.ResolveTarget(link); }
            catch { /* ignore */ }
        }

        string? acfPath = null;
        Dictionary<string, object?>? acf = null;
        if (!string.IsNullOrWhiteSpace(link))
        {
            try
            {
                acfPath = SteamBetaKeyEditor.FindAppManifestPath(link);
                if (File.Exists(acfPath))
                {
                    var text = File.ReadAllText(acfPath);
                    acf = new Dictionary<string, object?>
                    {
                        ["exists"] = true,
                        ["settled"] = SteamBetaKeyEditor.LooksSettledForSnapshot(text),
                        ["StateFlags"] = ReadAcfValue(text, "StateFlags"),
                        ["buildid"] = ReadAcfValue(text, "buildid"),
                        ["TargetBuildID"] = ReadAcfValue(text, "TargetBuildID"),
                        ["BytesToDownload"] = ReadAcfValue(text, "BytesToDownload"),
                        ["BytesDownloaded"] = ReadAcfValue(text, "BytesDownloaded"),
                        ["BytesToStage"] = ReadAcfValue(text, "BytesToStage"),
                        ["BytesStaged"] = ReadAcfValue(text, "BytesStaged"),
                        ["BetaKey"] = ReadAcfValueFromUserConfig(text),
                        ["MountedBetaKey"] = ReadAcfMountedBetaKey(text)
                    };
                }
                else
                {
                    acf = new Dictionary<string, object?> { ["exists"] = false };
                }
            }
            catch
            {
                acf = new Dictionary<string, object?> { ["exists"] = false, ["error"] = true };
            }
        }

        return new Dictionary<string, object?>
        {
            ["enabled"] = enabled,
            ["wizardStep"] = cfg?.WizardStep.ToString() ?? request.BranchWizardStep,
            ["activeBranch"] = cfg?.ActiveBranch.ToString() ?? request.ActiveGameBranch,
            ["steamLinkPath"] = link,
            ["steamLinkIsJunction"] = isJunction,
            ["junctionTarget"] = target,
            ["linkLooksLikeGameRoot"] = LooksLikeGameRoot(link),
            ["officialLooksLikeGameRoot"] = LooksLikeGameRoot(official),
            ["betaLooksLikeGameRoot"] = LooksLikeGameRoot(beta),
            ["acfPath"] = acfPath,
            ["acf"] = acf
        };
    }

    static Dictionary<string, object?> InspectDeployManifests(PathsService paths)
    {
        bool Exists(string name) => File.Exists(Path.Combine(paths.DataRoot, name));
        return new Dictionary<string, object?>
        {
            ["legacy"] = Exists("deploy-manifest.json"),
            ["official"] = Exists("deploy-manifest.official.json"),
            ["beta"] = Exists("deploy-manifest.beta.json")
        };
    }

    static List<string> BuildFindings(
        string? statusKind,
        Dictionary<string, object?> files,
        Dictionary<string, object?> melon,
        Dictionary<string, object?> branch,
        Dictionary<string, object?> manifests,
        DiagnosticsExportRequest request)
    {
        var findings = new List<string>();
        if (string.Equals(statusKind, nameof(GameStatusKind.GameMissing), StringComparison.Ordinal))
            findings.Add("game_missing");

        if (melon.TryGetValue("exists", out var ex) && ex is false)
            findings.Add("melon_log_missing");
        else if (melon.TryGetValue("staleOrIncomplete", out var stale) && stale is true)
            findings.Add("melon_log_stale_or_incomplete");

        if (files.TryGetValue("hasVersionDll", out var ver) && ver is false
            && files.TryGetValue("hasWinHttpDll", out var win) && win is false
            && files.TryGetValue("pathPresent", out var present) && present is true)
            findings.Add("melon_proxy_dll_missing");

        var enabled = branch.TryGetValue("enabled", out var en) && en is true;
        if (enabled)
        {
            if (branch.TryGetValue("steamLinkIsJunction", out var j) && j is false)
                findings.Add("junction_missing_while_branch_enabled");
            if (branch.TryGetValue("linkLooksLikeGameRoot", out var linkOk) && linkOk is false
                && ((branch.TryGetValue("officialLooksLikeGameRoot", out var o) && o is true)
                    || (branch.TryGetValue("betaLooksLikeGameRoot", out var b) && b is true)))
                findings.Add("link_incomplete_but_store_valid");

            if (branch.TryGetValue("acf", out var acfObj) && acfObj is Dictionary<string, object?> acf)
            {
                if (acf.TryGetValue("exists", out var acfEx) && acfEx is true
                    && acf.TryGetValue("settled", out var settled) && settled is false)
                    findings.Add("acf_not_settled");
            }

            if (request.IsAwaitingSteamSettle)
                findings.Add("awaiting_steam_settle");

            var hasOfficial = manifests.TryGetValue("official", out var mo) && mo is true;
            var hasBeta = manifests.TryGetValue("beta", out var mb) && mb is true;
            var hasLegacy = manifests.TryGetValue("legacy", out var ml) && ml is true;
            if (!hasOfficial && !hasBeta && !hasLegacy
                && string.Equals(request.BranchWizardStep, nameof(BranchWizardStep.Ready), StringComparison.Ordinal))
                findings.Add("ready_without_branch_deploy_manifest");
        }

        return findings;
    }

    static BranchSwitchConfig? TryLoadBranchConfig(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<BranchSwitchConfig>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    static bool LooksLikeGameRoot(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && File.Exists(Path.Combine(path, "Mechabellum.exe"))
        && File.Exists(Path.Combine(path, "GameAssembly.dll"));

    static string? ReadAcfValue(string text, string key)
    {
        var m = Regex.Match(text, $@"\""{Regex.Escape(key)}\""\s+\""([^\""]*)\""", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : null;
    }

    static string? ReadAcfValueFromUserConfig(string text)
    {
        var block = Regex.Match(text, @"\""UserConfig\""\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!block.Success) return null;
        var m = Regex.Match(block.Groups["body"].Value, @"\""BetaKey\""\s+\""([^\""]*)\""", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : null;
    }

    static string? ReadAcfMountedBetaKey(string text)
    {
        var block = Regex.Match(text, @"\""MountedConfig\""\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!block.Success) return null;
        var m = Regex.Match(block.Groups["body"].Value, @"\""BetaKey\""\s+\""([^\""]*)\""", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : null;
    }
}
