using BlackBox.Core;
using FluentAssertions;

public class BlackBoxHelperReleaseTests
{
    [Fact]
    public void Missing_file_needs_write_and_same_bytes_do_not()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-rel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "BlackBoxDump.exe");
        var payload = new byte[] { 1, 2, 3, 4 };
        HelperRelease.NeedsWrite(path, payload).Should().BeTrue();
        File.WriteAllBytes(path, payload);
        HelperRelease.NeedsWrite(path, payload).Should().BeFalse();
        HelperRelease.NeedsWrite(path, new byte[] { 1, 2, 3, 5 }).Should().BeTrue();
        Directory.Delete(dir, true);
    }
}
