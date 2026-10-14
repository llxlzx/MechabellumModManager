using System.Text.Json.Nodes;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class CatalogRemovalParseTests
{
    static string CatalogWithSignedList(TestRemovalSigner signer, out CatalogSignedBlob blob)
    {
        blob = signer.Sign(
        [
            new CatalogRemoval
            {
                Id = "opponent-economy",
                Name = "Opponent Economy",
                Sha256 = ["AAAA", "bbbb"],
                Reason = "影响对局公平",
                Since = "2026-10-14",
                Locales = new() { ["en"] = new CatalogRemovalLocale { Reason = "Affects match fairness" } }
            }
        ]);
        return $$"""
            {
              "updatedAt": "2026-10-14T00:00:00Z",
              "removedSigned": { "payload": "{{blob.Payload}}", "signature": "{{blob.Signature}}" },
              "mods": [ { "id": "keep", "name": "Keep", "file": "mods/keep/Keep.dll" } ]
            }
            """;
    }

    [Fact]
    public void Signed_list_is_read_from_the_catalog_and_verifies()
    {
        using var signer = new TestRemovalSigner();
        var root = ModCatalogService.DeserializeCatalog(CatalogWithSignedList(signer, out _));

        RemovalListVerifier.TryVerify(root.RemovedSigned, signer.PublicKey, out var list, out var error)
            .Should().BeTrue(error);
        var removal = list!.Removed.Should().ContainSingle().Subject;
        removal.Id.Should().Be("opponent-economy");
        removal.Name.Should().Be("Opponent Economy");
        removal.Sha256.Should().Equal("AAAA", "bbbb");
        removal.Since.Should().Be("2026-10-14");
        root.Mods.Should().ContainSingle(m => m.Id == "keep");
    }

    [Fact]
    public void Reason_follows_the_language_and_falls_back_to_the_default()
    {
        using var signer = new TestRemovalSigner();
        var root = ModCatalogService.DeserializeCatalog(CatalogWithSignedList(signer, out _));
        RemovalListVerifier.TryVerify(root.RemovedSigned, signer.PublicKey, out var list, out _).Should().BeTrue();
        var removal = list!.Removed[0];

        removal.ResolveReason("en").Should().Be("Affects match fairness");
        removal.ResolveReason("zh-CN").Should().Be("影响对局公平");
        removal.ResolveReason("de").Should().Be("影响对局公平");
    }

    [Fact]
    public void A_catalog_without_the_field_still_parses()
    {
        var root = ModCatalogService.DeserializeCatalog("""{"mods":[{"id":"a","name":"A","file":"mods/a/A.dll"}]}""");

        root.RemovedSigned.Should().BeNull();
        root.Mods.Should().ContainSingle();
    }

    [Fact]
    public void Direct_upload_publishing_keeps_the_signed_list_byte_for_byte()
    {
        using var signer = new TestRemovalSigner();
        var catalog = CatalogWithSignedList(signer, out var blob);
        var sha = new string('a', 64);

        var merged = MirrorCatalogPublisher.MergeListedEntry(
            catalog,
            $$"""{"id":"keep","name":"Keep","file":"mods/keep/Keep.dll","sha256":"{{sha}}","size":4}""",
            "keep");

        var kept = JsonNode.Parse(merged)!["removedSigned"]!;
        kept["payload"]!.GetValue<string>().Should().Be(blob.Payload);
        kept["signature"]!.GetValue<string>().Should().Be(blob.Signature);
    }
}
