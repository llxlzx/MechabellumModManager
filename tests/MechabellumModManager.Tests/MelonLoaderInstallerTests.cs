using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class MelonLoaderInstallerTests
{
    [Fact]
    public async Task Install_from_local_zip_makes_detector_Ready()
    {
        var game = CreateTempGameRoot();
        var zip = CreateFakeMelonZip();
        try
        {
            var installer = new MelonLoaderInstaller(
                resolveZipUrlAsync: _ => Task.FromResult(new Uri(zip).AbsoluteUri),
                http: new HttpClient(new FileHttpHandler()),
                probe: new ProcessProbe());

            var result = await installer.InstallAsync(game);
            result.Success.Should().BeTrue(result.Message);
            Directory.Exists(Path.Combine(game, "MelonLoader")).Should().BeTrue();
            File.Exists(Path.Combine(game, "version.dll")).Should().BeTrue();
            new GameDetector().Detect(game).Kind.Should().Be(GameStatusKind.Ready);
        }
        finally
        {
            TryDelete(game);
            TryDelete(Path.GetDirectoryName(zip)!);
        }
    }

    [Fact]
    public async Task Install_rejects_invalid_game_path()
    {
        var installer = new MelonLoaderInstaller(
            resolveZipUrlAsync: _ => Task.FromResult("http://example.invalid/x.zip"));
        var result = await installer.InstallAsync(Path.Combine(Path.GetTempPath(), "no-such-game-" + Guid.NewGuid().ToString("N")));
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("游戏路径无效");
    }

    static string CreateTempGameRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        return root;
    }

    static string CreateFakeMelonZip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-ml-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var zipPath = Path.Combine(dir, "MelonLoader.x64.zip");
        var stage = Path.Combine(dir, "stage");
        Directory.CreateDirectory(Path.Combine(stage, "MelonLoader"));
        File.WriteAllText(Path.Combine(stage, "MelonLoader", "placeholder.txt"), "ml");
        File.WriteAllText(Path.Combine(stage, "version.dll"), "");
        File.WriteAllText(Path.Combine(stage, "dobby.dll"), "");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(stage, zipPath);
        return zipPath;
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            // ignore
        }
    }

    sealed class FileHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || !request.RequestUri.IsFile)
                throw new InvalidOperationException("Expected file URI for test download.");

            var local = request.RequestUri.LocalPath;
            var bytes = File.ReadAllBytes(local);
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            return Task.FromResult(resp);
        }
    }
}
