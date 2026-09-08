using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class RedistEnsureTests
{
    const string SampleManifest = """
        {
          "schemaVersion": 1,
          "melonTag": "v0.7.3",
          "artifacts": [
            {
              "id": "melonloader-x64",
              "path": "melonloader/MelonLoader.x64.zip",
              "sha256": "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae",
              "size": 3,
              "originUrl": "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
            }
          ]
        }
        """;

    // sha256("foo")
    static readonly byte[] FooBytes = Encoding.UTF8.GetBytes("foo");

    [Fact]
    public void Parse_rejects_missing_sha256()
    {
        var act = () => RedistEnsureService.ParseManifest("""
            {"schemaVersion":1,"artifacts":[{"id":"x","path":"a.bin","sha256":""}]}
            """);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void BuildCandidates_mirror_before_origin_when_mirror_set()
    {
        var artifact = new RedistArtifact
        {
            Id = "melonloader-x64",
            Path = "melonloader/MelonLoader.x64.zip",
            Sha256 = "aa",
            OriginUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
        };

        var list = RedistEnsureService.BuildArtifactCandidates(
            "https://cdn.example",
            artifact);

        list.Should().HaveCount(2);
        list[0].ToString().Should().Be("https://cdn.example/MechabellumRedist/melonloader/MelonLoader.x64.zip");
        list[1].Host.Should().Contain("github");
    }

    [Fact]
    public void BuildCandidates_opt_out_skips_mirror()
    {
        var artifact = new RedistArtifact
        {
            Id = "melonloader-x64",
            Path = "melonloader/MelonLoader.x64.zip",
            Sha256 = "aa",
            OriginUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.16/windowsdesktop-runtime-8.0.16-win-x64.exe"
        };

        var list = RedistEnsureService.BuildArtifactCandidates("", artifact);
        list.Should().ContainSingle();
        list[0].Host.Should().Contain("microsoft");
    }

    [Fact]
    public async Task Ensure_uses_mirror_bytes_and_records_mirror_source()
    {
        var mirrorManifest = new Uri("https://cdn.example/MechabellumRedist/manifest.json");
        var mirrorZip = new Uri("https://cdn.example/MechabellumRedist/melonloader/MelonLoader.x64.zip");
        var originZip = new Uri("https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip");

        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri == mirrorManifest)
                return ScriptedHttpHandler.Json(HttpStatusCode.OK, SampleManifest);
            if (req.RequestUri == mirrorZip)
                return Bytes(HttpStatusCode.OK, FooBytes);
            if (req.RequestUri == originZip)
                return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "no");
            return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "miss");
        });
        using var http = new HttpClient(handler);
        using var dir = new TempDir();
        var svc = new RedistEnsureService(http);

        var result = await svc.EnsureAsync(
            dir.Path,
            mirrorBaseUrl: "https://cdn.example",
            ids: ["melonloader-x64"],
            bundledManifestJson: null);

        result.Success.Should().BeTrue();
        result.SourceById["melonloader-x64"].Should().Be(RedistEnsureService.MirrorSource);
        var dest = Path.Combine(dir.Path, "melonloader", "MelonLoader.x64.zip");
        File.ReadAllBytes(dest).Should().Equal(FooBytes);
    }

    [Fact]
    public async Task Ensure_rejects_hash_mismatch_and_falls_through_to_origin()
    {
        var mirrorManifest = new Uri("https://cdn.example/MechabellumRedist/manifest.json");
        var mirrorZip = new Uri("https://cdn.example/MechabellumRedist/melonloader/MelonLoader.x64.zip");
        var originZip = new Uri("https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip");

        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri == mirrorManifest)
                return ScriptedHttpHandler.Json(HttpStatusCode.OK, SampleManifest);
            if (req.RequestUri == mirrorZip)
                return Bytes(HttpStatusCode.OK, Encoding.UTF8.GetBytes("bad"));
            if (req.RequestUri == originZip)
                return Bytes(HttpStatusCode.OK, FooBytes);
            return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "miss");
        });
        using var http = new HttpClient(handler);
        using var dir = new TempDir();
        var svc = new RedistEnsureService(http);

        var result = await svc.EnsureAsync(
            dir.Path,
            mirrorBaseUrl: "https://cdn.example",
            ids: ["melonloader-x64"]);

        result.Success.Should().BeTrue();
        result.SourceById["melonloader-x64"].Should().Be(RedistEnsureService.OriginSource);
    }

    [Fact]
    public async Task Ensure_opt_out_never_hits_mirror_host()
    {
        var originZip = new Uri("https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip");
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("cdn.example", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("mirror must not be contacted");
            if (req.RequestUri == originZip)
                return Bytes(HttpStatusCode.OK, FooBytes);
            return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "miss");
        });
        using var http = new HttpClient(handler);
        using var dir = new TempDir();
        var svc = new RedistEnsureService(http);

        var result = await svc.EnsureAsync(
            dir.Path,
            mirrorBaseUrl: "",
            ids: ["melonloader-x64"],
            bundledManifestJson: SampleManifest);

        result.Success.Should().BeTrue();
        result.SourceById["melonloader-x64"].Should().Be(RedistEnsureService.OriginSource);
        handler.Requests.Should().OnlyContain(u => !u.Host.Contains("cdn.example"));
    }

    [Fact]
    public async Task Ensure_skips_download_when_local_hash_matches()
    {
        using var dir = new TempDir();
        var dest = Path.Combine(dir.Path, "melonloader", "MelonLoader.x64.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        await File.WriteAllBytesAsync(dest, FooBytes);

        var handler = new ScriptedHttpHandler(_ =>
            throw new InvalidOperationException("should not download"));
        using var http = new HttpClient(handler);
        var svc = new RedistEnsureService(http);

        var result = await svc.EnsureAsync(
            dir.Path,
            mirrorBaseUrl: "",
            ids: ["melonloader-x64"],
            bundledManifestJson: SampleManifest);

        result.Success.Should().BeTrue();
        result.SourceById["melonloader-x64"].Should().Be(RedistEnsureService.LocalSource);
        handler.Requests.Should().BeEmpty();
    }

    static HttpResponseMessage Bytes(HttpStatusCode code, byte[] body)
    {
        var resp = new HttpResponseMessage(code)
        {
            Content = new ByteArrayContent(body)
        };
        resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return resp;
    }

    sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "mmm-redist-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
