using System.Diagnostics;
using FluentAssertions;
using MechabellumModManager.Services;

public class InboxCoscliSessionTests
{
    static readonly Lazy<string> StubExe = new(CompileStub);

    [Fact]
    public void Write_does_not_modify_config_it_was_not_given()
    {
        // Stand-in for %USERPROFILE%\.cos.yaml. This file lives under temp and is never the live profile.
        var root = NewRoot();
        var keepPath = Path.Combine(root, "stand-in-userprofile", ".cos.yaml");
        var sessionPath = Path.Combine(root, "session", "inbox-cos.yaml");
        var liveProfile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cos.yaml");
        try
        {
            keepPath.Should().NotBe(liveProfile);
            sessionPath.Should().NotBe(liveProfile);
            Directory.CreateDirectory(Path.GetDirectoryName(keepPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
            const string keep = "secretid: \"KEEP\"";
            File.WriteAllText(keepPath, keep);
            File.WriteAllText(sessionPath, "secretid: \"OLD\"\nkeep-merge: \"NO\"\n");

            InboxCoscliSession.Write(sessionPath, "SID", "SKEY", "TOKEN", "mmm-inbox", "ap-shanghai");

            File.ReadAllText(keepPath).Should().Be(keep);
            var written = File.ReadAllText(sessionPath);
            written.Should().Contain("sessiontoken: \"TOKEN\"");
            written.Should().Contain("secretid: \"SID\"");
            written.Should().Contain("secretkey: \"SKEY\"");
            written.Should().NotContain("KEEP");
            written.Should().NotContain("OLD");
            written.Should().NotContain("keep-merge");
        }
        finally
        {
            TryDeleteDir(root);
        }
    }

    [Fact]
    public async Task RunAsync_uses_c_flag_and_deletes_temp_config()
    {
        var root = NewRoot();
        var configPath = Path.Combine(root, "session.yaml");
        var argsFile = Path.Combine(root, "args.txt");
        var localPath = Path.Combine(root, "QuickMark.dll");
        const string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        try
        {
            Directory.CreateDirectory(root);
            InboxCoscliSession.Write(configPath, "SID", "SKEY", "TOKEN", "mmm-inbox", "ap-shanghai");
            File.WriteAllText(localPath, "dll");
            Environment.SetEnvironmentVariable("COSCLI_STUB_ARGS_FILE", argsFile);
            Environment.SetEnvironmentVariable("COSCLI_STUB_EXIT", "0");
            Environment.SetEnvironmentVariable("COSCLI_STUB_STDERR", "");

            await InboxCoscliSession.RunAsync(
                StubExe.Value,
                configPath,
                localPath,
                "mmm-inbox",
                "inbox/author1/quick-mark/QuickMark.dll",
                sha);

            File.ReadAllLines(argsFile).Should().Equal(
                "-c",
                configPath,
                "cp",
                localPath,
                "cos://mmm-inbox/inbox/author1/quick-mark/QuickMark.dll",
                "--meta",
                "x-cos-meta-sha256:" + sha);
            File.Exists(configPath).Should().BeFalse();
        }
        finally
        {
            ClearStubEnv();
            TryDeleteDir(root);
        }
    }

    [Fact]
    public async Task RunAsync_nonzero_exit_strips_sessiontoken_and_deletes_config()
    {
        var root = NewRoot();
        var configPath = Path.Combine(root, "session.yaml");
        try
        {
            Directory.CreateDirectory(root);
            InboxCoscliSession.Write(configPath, "SID", "SKEY", "TOKEN", "mmm-inbox", "ap-shanghai");
            Environment.SetEnvironmentVariable("COSCLI_STUB_ARGS_FILE", Path.Combine(root, "args.txt"));
            Environment.SetEnvironmentVariable("COSCLI_STUB_EXIT", "3");
            Environment.SetEnvironmentVariable("COSCLI_STUB_STDERR", "upload failed\nsessiontoken: \"TOKEN\"\nbucket denied\n");

            var act = () => InboxCoscliSession.RunAsync(
                StubExe.Value,
                configPath,
                Path.Combine(root, "QuickMark.dll"),
                "mmm-inbox",
                "inbox/author1/quick-mark/QuickMark.dll",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            var thrown = await act.Should().ThrowAsync<MirrorPublishException>();
            thrown.Which.Code.Should().Be(DirectUploadErrorCode.Network);
            thrown.Which.Message.Should().Contain("upload failed");
            thrown.Which.Message.Should().Contain("bucket denied");
            thrown.Which.Message.Should().NotContain("sessiontoken");
            thrown.Which.Message.Should().NotContain("TOKEN");
            File.Exists(configPath).Should().BeFalse();
        }
        finally
        {
            ClearStubEnv();
            TryDeleteDir(root);
        }
    }

    static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "mmm-inbox-session-" + Guid.NewGuid().ToString("N"));

    static void ClearStubEnv()
    {
        Environment.SetEnvironmentVariable("COSCLI_STUB_ARGS_FILE", null);
        Environment.SetEnvironmentVariable("COSCLI_STUB_EXIT", null);
        Environment.SetEnvironmentVariable("COSCLI_STUB_STDERR", null);
    }

    static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* ignore */ }
    }

    static string CompileStub()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-inbox-coscli-stub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cs = Path.Combine(dir, "stub.cs");
        var exe = Path.Combine(dir, "coscli.exe");
        var ps1 = Path.Combine(dir, "build.ps1");
        File.WriteAllText(cs, """
            using System;
            using System.IO;
            public static class InboxCoscliStub {
                public static int Main(string[] args) {
                    var argsFile = Environment.GetEnvironmentVariable("COSCLI_STUB_ARGS_FILE");
                    if (!string.IsNullOrEmpty(argsFile))
                        File.WriteAllLines(argsFile, args);
                    var stderr = Environment.GetEnvironmentVariable("COSCLI_STUB_STDERR");
                    if (!string.IsNullOrEmpty(stderr))
                        Console.Error.Write(stderr);
                    var codeText = Environment.GetEnvironmentVariable("COSCLI_STUB_EXIT");
                    if (string.IsNullOrEmpty(codeText)) return 0;
                    return int.Parse(codeText);
                }
            }
            """);
        File.WriteAllText(ps1, "Add-Type -TypeDefinition ([System.IO.File]::ReadAllText('" + cs + "')) -OutputAssembly '" + exe + "' -OutputType ConsoleApplication\r\n");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(ps1);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("powershell");
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!File.Exists(exe))
            throw new InvalidOperationException(stderr);
        return exe;
    }
}
