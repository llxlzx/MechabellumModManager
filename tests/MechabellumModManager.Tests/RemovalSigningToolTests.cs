using System.Diagnostics;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

/// <summary>
/// tools/sign-removed.ps1 runs on Windows PowerShell (.NET Framework); the manager verifies on
/// .NET 8. These run the real script so a format drift between the two fails here, not on players.
/// </summary>
public sealed class RemovalSigningToolTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "mmm-signtool-" + Guid.NewGuid().ToString("N"));
    readonly string _mods;
    readonly string _key;

    public RemovalSigningToolTests()
    {
        _mods = Path.Combine(_root, "mods-repo");
        _key = Path.Combine(_root, "keys", "removal-key.json");
        Directory.CreateDirectory(_mods);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* cleanup */ }
    }

    static string Tool()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "tools", "sign-removed.ps1")))
            dir = Path.GetDirectoryName(dir);
        dir.Should().NotBeNull();
        return Path.Combine(dir!, "tools", "sign-removed.ps1");
    }

    static (int Code, string Output) RunTool(params string[] args)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Tool() }.Concat(args))
            psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(60_000).Should().BeTrue();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    static (int Code, string Output) RunToolInCulture(string culture, params string[] args)
    {
        static string Quote(string s) => "'" + s.Replace("'", "''") + "'";
        var command = "& { [Threading.Thread]::CurrentThread.CurrentCulture = " + Quote(culture) + "; & "
            + Quote(Tool()) + " " + string.Join(" ", args.Select(a => a.StartsWith('-') ? a : Quote(a)))
            + "; exit $LASTEXITCODE }";
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command })
            psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(60_000).Should().BeTrue();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    string NewKey()
    {
        var (code, output) = RunTool("-NewKey", "-KeyFile", _key);
        code.Should().Be(0, output);
        var line = output.Split('\n').Single(l => l.StartsWith("PublicKey: ", StringComparison.Ordinal));
        return line["PublicKey: ".Length..].Trim();
    }

    [Fact]
    public void A_list_signed_by_the_script_verifies_in_the_manager()
    {
        var publicKey = NewKey();
        var hash = new string('A', 64);
        File.WriteAllText(
            Path.Combine(_mods, "removed.json"),
            $$"""[ { "id": "opponent-economy", "sha256": ["{{hash}}"], "reason": "影响对局公平" } ]""",
            new UTF8Encoding(true));
        var catalogBefore = "{\r\n  \"updatedAt\": \"2026-10-14\",\r\n  \"mods\": [ { \"id\": \"keep\", \"name\": \"保留\", \"file\": \"mods/keep/Keep.dll\" } ]\r\n}\r\n";
        File.WriteAllText(Path.Combine(_mods, "catalog.json"), catalogBefore, new UTF8Encoding(false));

        var (code, output) = RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-01T01:00:00Z");
        code.Should().Be(0, output);

        var text = File.ReadAllText(Path.Combine(_mods, "catalog.json"));
        var root = ModCatalogService.DeserializeCatalog(text);
        RemovalListVerifier.TryVerify(root.RemovedSigned, publicKey, out var list, out var error).Should().BeTrue(error);
        list!.IssuedAt.Should().Be(new DateTimeOffset(2026, 10, 1, 1, 0, 0, TimeSpan.Zero));
        var removal = list.Removed.Should().ContainSingle().Subject;
        removal.Id.Should().Be("opponent-economy");
        removal.Reason.Should().Be("影响对局公平");
        removal.Sha256.Should().Equal(hash);
        root.Mods.Should().ContainSingle(m => m.Name == "保留");
        text.Should().EndWith("\"mods\": [ { \"id\": \"keep\", \"name\": \"保留\", \"file\": \"mods/keep/Keep.dll\" } ]\r\n}\r\n",
            "the rest of the catalog stays as the maintainer wrote it");
    }

    [Fact]
    public void Signing_again_replaces_the_previous_list()
    {
        var publicKey = NewKey();
        File.WriteAllText(Path.Combine(_mods, "catalog.json"), """{ "mods": [] }""");
        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "first" } ]""");
        RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-01T01:00:00Z").Code.Should().Be(0);
        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "first" }, { "id": "second" } ]""");

        var (code, output) = RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-02T01:00:00Z");
        code.Should().Be(0, output);

        var text = File.ReadAllText(Path.Combine(_mods, "catalog.json"));
        text.Split("removedSigned").Should().HaveCount(2, "exactly one removedSigned object");
        RemovalListVerifier.TryVerify(ModCatalogService.DeserializeCatalog(text).RemovedSigned, publicKey, out var list, out _)
            .Should().BeTrue();
        list!.Removed.Select(r => r.Id).Should().Equal("first", "second");
    }

    [Fact]
    public void The_script_refuses_a_key_file_inside_any_git_working_tree()
    {
        Directory.CreateDirectory(Path.Combine(_mods, ".git"));
        var keyInRepo = Path.Combine(_mods, "secrets", "key.json");

        var (code, output) = RunTool("-NewKey", "-KeyFile", keyInRepo);

        code.Should().NotBe(0);
        output.Should().Contain("outside every repository");
        File.Exists(keyInRepo).Should().BeFalse();
    }

    [Fact]
    public void The_script_refuses_a_key_file_inside_the_manager_repo()
    {
        var (code, output) = RunTool("-NewKey", "-KeyFile", Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Tool()))!, "key.json"));

        code.Should().NotBe(0);
        output.Should().Contain("outside every repository");
    }

    /// <summary>Finnish formats times as 09.30.05; the default time must not follow the culture.</summary>
    [Fact]
    public void The_default_signing_time_verifies_under_any_culture()
    {
        var publicKey = NewKey();
        File.WriteAllText(Path.Combine(_mods, "catalog.json"), """{ "mods": [] }""");
        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "x" } ]""");

        var (code, output) = RunToolInCulture("fi-FI", "-ModsRepo", _mods, "-KeyFile", _key);
        code.Should().Be(0, output);

        var root = ModCatalogService.DeserializeCatalog(File.ReadAllText(Path.Combine(_mods, "catalog.json")));
        RemovalListVerifier.TryVerify(root.RemovedSigned, publicKey, out var list, out var error).Should().BeTrue(error);
        list!.IssuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void The_script_refuses_a_time_that_does_not_move_forward()
    {
        NewKey();
        File.WriteAllText(Path.Combine(_mods, "catalog.json"), """{ "mods": [] }""");
        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "x" } ]""");
        RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-02T00:00:00Z").Code.Should().Be(0);

        var older = RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-01T00:00:00Z");
        var future = RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2099-01-01T00:00:00Z");
        var malformed = RunTool("-ModsRepo", _mods, "-KeyFile", _key, "-IssuedAt", "2026-10-03 00:00");

        older.Code.Should().NotBe(0);
        older.Output.Should().Contain("not later than");
        future.Code.Should().NotBe(0);
        future.Output.Should().Contain("future");
        malformed.Code.Should().NotBe(0);
    }

    [Fact]
    public void The_script_refuses_to_take_down_what_the_catalog_still_lists()
    {
        NewKey();
        var shipped = new string('c', 64);
        File.WriteAllText(
            Path.Combine(_mods, "catalog.json"),
            $$"""{ "mods": [ { "id": "econ-v2", "name": "E", "file": "mods/e/E.dll", "sha256": "{{shipped}}" } ] }""");

        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "econ-v2" } ]""");
        var listedId = RunTool("-ModsRepo", _mods, "-KeyFile", _key);
        File.WriteAllText(Path.Combine(_mods, "removed.json"), $$"""[ { "id": "econ-v1", "sha256": ["{{shipped}}"] } ]""");
        var shippedHash = RunTool("-ModsRepo", _mods, "-KeyFile", _key);
        File.WriteAllText(
            Path.Combine(_mods, "removed.json"),
            """[ { "id": "x", "sha256": ["e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"] } ]""");
        var emptyFile = RunTool("-ModsRepo", _mods, "-KeyFile", _key);

        listedId.Output.Should().Contain("still listed");
        shippedHash.Output.Should().Contain("still ships");
        emptyFile.Output.Should().Contain("empty-file");
        new[] { listedId.Code, shippedHash.Code, emptyFile.Code }.Should().OnlyContain(c => c != 0);
        File.ReadAllText(Path.Combine(_mods, "catalog.json")).Should().NotContain("removedSigned");
    }

    [Fact]
    public void The_script_rejects_a_malformed_hash()
    {
        NewKey();
        File.WriteAllText(Path.Combine(_mods, "catalog.json"), """{ "mods": [] }""");
        File.WriteAllText(Path.Combine(_mods, "removed.json"), """[ { "id": "x", "sha256": ["aabbccdd"] } ]""");

        var (code, output) = RunTool("-ModsRepo", _mods, "-KeyFile", _key);

        code.Should().NotBe(0);
        output.Should().Contain("not a sha256");
        File.ReadAllText(Path.Combine(_mods, "catalog.json")).Should().NotContain("removedSigned");
    }
}
