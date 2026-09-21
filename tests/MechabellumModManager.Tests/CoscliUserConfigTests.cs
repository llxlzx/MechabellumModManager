using FluentAssertions;
using MechabellumModManager.Services;

public class CoscliUserConfigTests
{
    [Fact]
    public void Save_writes_a_new_file_and_reads_the_id_back()
    {
        var path = Path.Combine(Path.GetTempPath(), "mmm-cos-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            CoscliUserConfig.Save(path, "AKIDexample", "secret/with:colon", "mmm-mirror-1312774738", "ap-shanghai");

            CoscliUserConfig.ReadSecretId(path).Should().Be("AKIDexample");
            CoscliUserConfig.HasSecret(path).Should().BeTrue();
            var text = File.ReadAllText(path);
            text.Should().Contain("mmm-mirror-1312774738");
            text.Should().Contain("ap-shanghai");
            text.Should().Contain("secret/with:colon");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_replaces_keys_and_keeps_another_bucket()
    {
        var path = Path.Combine(Path.GetTempPath(), "mmm-cos-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            File.WriteAllText(path,
                "secretid: \"old-id\"\nsecretkey: \"old-key\"\nbuckets:\n- name: \"other-bucket\"\n  region: \"ap-beijing\"\n");

            CoscliUserConfig.Save(path, "new-id", "new-key", "mmm-mirror-1312774738", "ap-shanghai");

            var text = File.ReadAllText(path);
            text.Should().Contain("new-id");
            text.Should().Contain("new-key");
            text.Should().NotContain("old-key");
            text.Should().Contain("other-bucket");
            text.Should().Contain("mmm-mirror-1312774738");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
