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
                    var stateFlags = ReadAcfValue(text, "StateFlags");
                    acf = new Dictionary<string, object?>
                    {
                        ["exists"] = true,
                        ["settled"] = SteamBetaKeyEditor.LooksSettledForSnapshot(text),
                        ["updateUnhealthy"] = SteamBetaKeyEditor.LooksUpdateUnhealthy(text),
                        ["StateFlags"] = stateFlags,
                        ["StateFlagsDecoded"] = SteamBetaKeyEditor.DecodeStateFlags(stateFlags),
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
            ["officialStorePath"] = official,
            ["betaStorePath"] = beta,
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

        if (SteamBranchLayout.IsBranchStorePath(request.GamePath))
            findings.Add("game_path_is_branch_store");

        if (melon.TryGetValue("exists", out var ex) && ex is false)
            findings.Add("melon_log_missing");
        else if (melon.TryGetValue("staleOrIncomplete", out var stale) && stale is true)
            findings.Add("melon_log_stale_or_incomplete");

        if (files.TryGetValue("hasVersionDll", out var ver) && ver is false
            && files.TryGetValue("hasWinHttpDll", out var win) && win is false
            && files.TryGetValue("pathPresent", out var present) && present is true)
            findings.Add("melon_proxy_dll_missing");

        var enabled = branch.TryGetValue("enabled", out var en) && en is true;
        if (!enabled)
        {
            if (SteamBranchLayout.EnumerateExistingStoreFolders(request.GamePath).Any(LooksLikeGameRoot)
                || (branch.TryGetValue("steamLinkIsJunction", out var junc) && junc is true))
                findings.Add("orphan_dual_layout");

            if (SteamBranchLayout.EnumerateExistingStoreFolders(request.GamePath).Any())
                findings.Add("leftover_dual_store_folders");
        }

        if (enabled)
        {
            if (branch.TryGetValue("steamLinkIsJunction", out var j) && j is false)
                findings.Add("junction_missing_while_branch_enabled");
            var linkOk = branch.TryGetValue("linkLooksLikeGameRoot", out var linkOkObj) && linkOkObj is true;
            var officialOk = branch.TryGetValue("officialLooksLikeGameRoot", out var o) && o is true;
            var betaOk = branch.TryGetValue("betaLooksLikeGameRoot", out var b) && b is true;
            if (!linkOk && (officialOk || betaOk))
            {
                findings.Add("link_incomplete_but_store_valid");
                findings.Add("active_store_incomplete");
            }

            var isJunc = branch.TryGetValue("steamLinkIsJunction", out var juncObj) && juncObj is true;
            var target = branch.TryGetValue("junctionTarget", out var tObj) ? tObj as string : null;
            var officialPath = branch.TryGetValue("officialStorePath", out var op) ? op as string : null;
            var betaPath = branch.TryGetValue("betaStorePath", out var bp) ? bp as string : null;
            if (IsJunctionDesync(isJunc, target, officialPath, betaPath, linkOk, officialOk, betaOk))
                findings.Add("junction_desync");

            if (branch.TryGetValue("acf", out var acfObj) && acfObj is Dictionary<string, object?> acf)
            {
                if (acf.TryGetValue("exists", out var acfEx) && acfEx is true)
                {
                    if (acf.TryGetValue("updateUnhealthy", out var unhealthy) && unhealthy is true)
                        findings.Add("acf_update_unhealthy");
                    else if (acf.TryGetValue("settled", out var settled) && settled is false)
                        findings.Add("acf_not_settled");
                }
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

    public static bool IsJunctionDesync(
        bool isJunction,
        string? target,
        string? official,
        string? beta,
        bool linkOk,
        bool officialOk,
        bool betaOk)
    {
        if (!isJunction || linkOk)
            return false;
        if (SamePath(target, official) && officialOk)
            return true;
        if (SamePath(target, beta) && betaOk)
            return true;
        return false;
    }

    static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(a.Trim()),
                Path.GetFullPath(b.Trim()),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
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
