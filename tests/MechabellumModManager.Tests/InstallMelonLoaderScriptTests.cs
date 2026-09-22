using System.Diagnostics;
using FluentAssertions;

public class InstallMelonLoaderScriptTests
{
    [Fact]
    public void Missing_local_zip_exits_without_github_download()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-melon-ps-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var redist = Path.Combine(root, "redist");
        try
        {
            TouchGame(game);
            Directory.CreateDirectory(redist);

            var (code, output, timedOut) = RunScript(
                "Install-MelonLoader.ps1",
                $"-GamePath \"{game}\" -RedistDir \"{redist}\"",
                timeoutMs: 15000);

            timedOut.Should().BeFalse("missing MelonLoader.x64.zip must not hang on a GitHub download");
            code.Should().Be(2);
            output.Should().NotContain("github.com/LavaGang/MelonLoader/releases/latest");
            output.Should().Contain("MelonLoader.x64.zip");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Restore_skips_melon_when_local_zip_is_not_ready()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-restore-ps-" + Guid.NewGuid().ToString("N"));
        var link = Path.Combine(root, "Mechabellum");
        var official = Path.Combine(root, "Mechabellum_official");
        var beta = Path.Combine(root, "Mechabellum_beta");
        var appData = Path.Combine(root, "appdata");
        var programData = Path.Combine(root, "programdata");
        try
        {
            TouchGame(link);
            TouchGame(official);
            TouchGame(beta);
            var manager = Path.Combine(appData, "MechabellumModManager");
            Directory.CreateDirectory(manager);
            File.WriteAllText(
                Path.Combine(manager, "branch-switch.json"),
                "{\"enabled\":true,\"steamLinkPath\":\"" + link.Replace("\\", "\\\\") +
                "\",\"officialStorePath\":\"" + official.Replace("\\", "\\\\") +
                "\",\"betaStorePath\":\"" + beta.Replace("\\", "\\\\") +
                "\",\"activeBranch\":0}");

            var redist = Path.Combine(root, "redist");
            Directory.CreateDirectory(redist);

            var (code, output, timedOut) = RunScript(
                "Restore-DualFolderConfig.ps1",
                $"-GamePath \"{link}\" -RedistDir \"{redist}\"",
                timeoutMs: 15000,
                env: new Dictionary<string, string>
                {
                    ["APPDATA"] = appData,
                    ["ProgramData"] = programData
                });

            timedOut.Should().BeFalse("config restore must not download MelonLoader before the offline zip exists");
            code.Should().Be(0);
            output.Should().NotContain("github.com/LavaGang/MelonLoader/releases/latest");
            output.Should().Contain("Skip Melon ensure");
            output.Should().Contain("Wrote ProgramData install-defaults.json");
        }
        finally
        {
            TryDelete(root);
        }
    }

    static void TouchGame(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "");
    }

    static (int Code, string Output, bool TimedOut) RunScript(
        string scriptName,
        string args,
        int timeoutMs,
        Dictionary<string, string>? env = null)
    {
        var script = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "packaging", "installer", "scripts", scriptName));
        File.Exists(script).Should().BeTrue(because: script);

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                        script + "\" " + args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (env is not null)
        {
            foreach (var pair in env)
                psi.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                try { process.Kill(); } catch { /* already gone */ }
            }

            return (-1, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult(), true);
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult(), false);
    }

    static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }
}
