using System.Text.Json.Nodes;
using FluentAssertions;
using MechabellumModManager.Services;

public class MirrorCatalogPublisherTests
{
    [Fact]
    public void Bucket_comes_from_the_mirror_host()
    {
        MirrorCatalogPublisher.BucketFromMirrorUrl(
                "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com")
            .Should().Be("mmm-mirror-1312774738");
        MirrorCatalogPublisher.BucketFromMirrorUrl("").Should().BeNull();
        MirrorCatalogPublisher.BucketFromMirrorUrl("https://example.com/mirror").Should().BeNull();
        MirrorCatalogPublisher.RegionFromMirrorUrl(
                "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com")
            .Should().Be("ap-shanghai");
    }

    [Fact]
    public void Plan_appends_a_new_mod_and_puts_catalog_after_the_files()
    {
        var dll = WriteTemp(new byte[] { 1, 2, 3, 4 });
        var preview = WriteTemp(new byte[] { 9, 9 });
        try
        {
            var plan = MirrorCatalogPublisher.Plan(
                """{"updatedAt":"old","mods":[{"id":"keep","name":"Keep","file":"mods/keep/a.dll","extra":1}]}""",
                new MirrorPublishInput
                {
                    Id = "NewMod",
                    Name = "New",
                    Summary = "hello",
                    Version = "1.2",
                    DllPath = dll,
                    PreviewPath = preview,
                    UpdatedAt = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero)
                });

            plan.Size.Should().Be(4);
            plan.Sha256.Should().HaveLength(64);
            plan.Uploads.Select(u => u.RemoteKey).Should().Equal(
                "MechabellumMods/mods/NewMod/" + Path.GetFileName(dll),
                "MechabellumMods/mods/NewMod/preview.png");
            plan.Uploads.Should().NotContain(u => u.RemoteKey.EndsWith("catalog.json", StringComparison.Ordinal));
            plan.EntryKey.Should().Be("MechabellumMods/mods/NewMod/entry.json");
            var entry = JsonNode.Parse(plan.EntryJson)!.AsObject();
            entry["id"]!.GetValue<string>().Should().Be("NewMod");
            entry.ContainsKey("mods").Should().BeFalse();

            var root = JsonNode.Parse(plan.CatalogJson)!.AsObject();
            root["updatedAt"]!.GetValue<string>().Should().Be("2026-09-21T08:00:00Z");
            var mods = root["mods"]!.AsArray();
            mods.Should().HaveCount(2);
            mods[0]!["extra"]!.GetValue<int>().Should().Be(1);
            var added = mods[1]!.AsObject();
            added["id"]!.GetValue<string>().Should().Be("NewMod");
            added["sha256"]!.GetValue<string>().Should().Be(plan.Sha256);
            added["size"]!.GetValue<long>().Should().Be(4);
            added["preview"]!.GetValue<string>().Should().Be("mods/NewMod/preview.png");
        }
        finally
        {
            File.Delete(dll);
            File.Delete(preview);
        }
    }

    [Fact]
    public void Plan_updates_the_matching_id_and_keeps_fields_it_does_not_edit()
    {
        var dll = WriteTemp(new byte[] { 7 });
        try
        {
            var plan = MirrorCatalogPublisher.Plan(
                """{"mods":[{"id":"cam","name":"Old","file":"mods/cam/old.dll","author":"Ada","tags":["hud"]}]}""",
                new MirrorPublishInput
                {
                    Id = "cam",
                    Name = "Cam",
                    Version = "2",
                    DllPath = dll
                });

            var mod = JsonNode.Parse(plan.CatalogJson)!["mods"]![0]!.AsObject();
            mod["name"]!.GetValue<string>().Should().Be("Cam");
            mod["version"]!.GetValue<string>().Should().Be("2");
            mod["author"]!.GetValue<string>().Should().Be("Ada");
            mod["tags"]![0]!.GetValue<string>().Should().Be("hud");
            mod["file"]!.GetValue<string>().Should().Be("mods/cam/" + Path.GetFileName(dll));
            plan.Uploads.Should().ContainSingle();
        }
        finally
        {
            File.Delete(dll);
        }
    }

    [Fact]
    public void MergeListedEntry_adds_one_mod_and_leaves_the_rest_of_the_catalog()
    {
        var sha = new string('a', 64);
        var merged = MirrorCatalogPublisher.MergeListedEntry(
            """{"mods":[{"id":"keep","name":"Keep","file":"mods/keep/a.dll","author":"Ada"}]}""",
            $$"""{"id":"NewMod","name":"New","summary":"s","version":"1","file":"mods/NewMod/a.dll","sha256":"{{sha}}","size":4,"preview":"mods/NewMod/preview.png","originUrl":"https://evil.example/x.dll","updatedAt":"2026-09-21T08:00:00Z"}""",
            "NewMod");

        var mods = JsonNode.Parse(merged)!["mods"]!.AsArray();
        mods.Should().HaveCount(2);
        mods[0]!["author"]!.GetValue<string>().Should().Be("Ada");
        var added = mods[1]!.AsObject();
        added["name"]!.GetValue<string>().Should().Be("New");
        added["file"]!.GetValue<string>().Should().Be("mods/NewMod/a.dll");
        added.ContainsKey("originUrl").Should().BeFalse();
    }

    [Fact]
    public void MergeListedEntry_keeps_existing_fields_the_uploader_does_not_own()
    {
        var sha = new string('b', 64);
        var merged = MirrorCatalogPublisher.MergeListedEntry(
            """{"mods":[{"id":"cam","name":"Old","file":"mods/cam/old.dll","author":"Ada","tags":["hud"],"originUrl":"https://keep.example/a.dll"}]}""",
            $$"""{"id":"cam","name":"Cam","file":"mods/cam/new.dll","sha256":"{{sha}}","size":8,"author":"Mallory","originUrl":"https://evil.example/x.dll"}""",
            "cam");

        var mod = JsonNode.Parse(merged)!["mods"]![0]!.AsObject();
        mod["name"]!.GetValue<string>().Should().Be("Cam");
        mod["file"]!.GetValue<string>().Should().Be("mods/cam/new.dll");
        mod["author"]!.GetValue<string>().Should().Be("Ada");
        mod["originUrl"]!.GetValue<string>().Should().Be("https://keep.example/a.dll");
        mod["tags"]![0]!.GetValue<string>().Should().Be("hud");
    }

    [Theory]
    [InlineData("""{"id":"NewMod","name":"New","file":"mods/Other/a.dll","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":4}""")]
    [InlineData("""{"id":"Other","name":"New","file":"mods/NewMod/a.dll","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":4}""")]
    [InlineData("""{"id":"NewMod","name":"New","file":"mods/NewMod/../Other/a.dll","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":4}""")]
    public void MergeListedEntry_rejects_an_entry_that_points_outside_its_folder(string entryJson)
    {
        var act = () => MirrorCatalogPublisher.MergeListedEntry("""{"mods":[]}""", entryJson, "NewMod");
        act.Should().Throw<MirrorPublishException>()
            .Which.Code.Should().Be(DirectUploadErrorCode.ValidationFailed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("has space")]
    public void Plan_rejects_an_unsafe_id(string id)
    {
        var dll = WriteTemp(new byte[] { 1 });
        try
        {
            var act = () => MirrorCatalogPublisher.Plan(null, new MirrorPublishInput
            {
                Id = id,
                Name = "N",
                DllPath = dll
            });
            act.Should().Throw<MirrorPublishException>()
                .Which.Code.Should().Be(DirectUploadErrorCode.ValidationFailed);
        }
        finally
        {
            File.Delete(dll);
        }
    }

    static string WriteTemp(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), "mmm-mirror-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
