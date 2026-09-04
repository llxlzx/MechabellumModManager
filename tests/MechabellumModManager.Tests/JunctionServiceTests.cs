using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;

public class JunctionServiceTests
{
    const uint IoReparseTagMountPoint = 0xA0000003;
    const uint IoReparseTagSymlink = 0xA000000C;

    [Fact]
    public void CreateJunction_creates_mount_point_not_directory_symlink()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(root, "Mechabellum");
        var sut = new JunctionService();
        try
        {
            sut.CreateJunction(link, target);
            TryReadReparseTag(link, out var tag).Should().BeTrue();
            tag.Should().Be(IoReparseTagMountPoint);
            tag.Should().NotBe(IoReparseTagSymlink);
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [SkippableFact]
    public void CreateJunction_throws_when_link_and_target_are_on_different_volumes()
    {
        Skip.If(
            !TryCreateCrossVolumeSandbox(out var linkParent, out var target, out var dispose),
            "Need two writable NTFS volumes with different serial numbers.");

        var sut = new JunctionService();
        try
        {
            var link = Path.Combine(linkParent, "Mechabellum");
            var act = () => sut.CreateJunction(link, target);
            act.Should().Throw<InvalidOperationException>();
            Directory.Exists(link).Should().BeFalse();
        }
        finally
        {
            dispose();
        }
    }

    [SkippableFact]
    public void IsJunction_and_DeleteJunction_recognize_directory_symlink()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "Mechabellum.exe");
        File.WriteAllText(marker, "keep");
        var link = Path.Combine(root, "Mechabellum");
        var sut = new JunctionService();
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, target);
            }
            catch (Exception ex)
            {
                throw new SkipException("Directory.CreateSymbolicLink is not permitted: " + ex.Message);
            }

            Skip.If(
                !TryReadReparseTag(link, out var tag) || tag != IoReparseTagSymlink,
                "Created link is not a directory symlink.");

            sut.IsJunction(link).Should().BeTrue();
            sut.DeleteJunction(link);
            Directory.Exists(link).Should().BeFalse();
            File.Exists(marker).Should().BeTrue();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void Create_resolve_delete_preserves_target()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "Mechabellum.exe"), "x");
        var link = Path.Combine(root, "Mechabellum");
        var sut = new JunctionService();
        try
        {
            sut.CreateJunction(link, target);
            sut.IsJunction(link).Should().BeTrue();
            sut.ResolveTarget(link).Should().Be(Path.GetFullPath(target));
            File.Exists(Path.Combine(link, "Mechabellum.exe")).Should().BeTrue();

            sut.DeleteJunction(link);
            Directory.Exists(link).Should().BeFalse();
            File.Exists(Path.Combine(target, "Mechabellum.exe")).Should().BeTrue();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void IsJunction_returns_false_for_regular_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sut = new JunctionService();
        try
        {
            sut.IsJunction(root).Should().BeFalse();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void IsJunction_returns_false_for_missing_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mmm-j-missing-" + Guid.NewGuid().ToString("N"));
        new JunctionService().IsJunction(missing).Should().BeFalse();
    }

    [Fact]
    public void ResolveTarget_returns_null_when_not_junction()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sut = new JunctionService();
        try
        {
            sut.ResolveTarget(root).Should().BeNull();
            sut.ResolveTarget(Path.Combine(root, "nope")).Should().BeNull();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void CreateJunction_throws_when_link_already_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(root, "Mechabellum");
        Directory.CreateDirectory(link);
        var sut = new JunctionService();
        try
        {
            var act = () => sut.CreateJunction(link, target);
            act.Should().Throw<IOException>();
            Directory.Exists(link).Should().BeTrue();
            sut.IsJunction(link).Should().BeFalse();
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    [Fact]
    public void DeleteJunction_does_not_delete_regular_directory_contents()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var realDir = Path.Combine(root, "store");
        Directory.CreateDirectory(realDir);
        var marker = Path.Combine(realDir, "Mechabellum.exe");
        File.WriteAllText(marker, "keep");
        var sut = new JunctionService();
        try
        {
            var act = () => sut.DeleteJunction(realDir);
            act.Should().Throw<InvalidOperationException>();
            Directory.Exists(realDir).Should().BeTrue();
            File.Exists(marker).Should().BeTrue();
            File.ReadAllText(marker).Should().Be("keep");
        }
        finally
        {
            Cleanup(sut, root);
        }
    }

    static void Cleanup(JunctionService sut, string root)
    {
        try
        {
            if (!Directory.Exists(root))
                return;

            foreach (var dir in Directory.GetDirectories(root))
            {
                try
                {
                    if (sut.IsJunction(dir))
                        sut.DeleteJunction(dir);
                }
                catch
                {
                    // Best-effort unlink before recursive delete of the sandbox.
                }
            }

            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Temp sandbox leftover is non-fatal for the test run.
        }
    }

    static bool TryReadReparseTag(string path, out uint tag)
    {
        tag = 0;
        var handle = CreateFileW(
            path,
            0x0080,
            0x0001 | 0x0002 | 0x0004,
            IntPtr.Zero,
            3,
            0x02000000 | 0x00200000,
            IntPtr.Zero);
        if (handle == new IntPtr(-1))
            return false;

        var buffer = new byte[16 * 1024];
        try
        {
            if (!DeviceIoControl(handle, 0x000900A8, IntPtr.Zero, 0, buffer, (uint)buffer.Length, out var bytesReturned, IntPtr.Zero)
                || bytesReturned < 8)
            {
                return false;
            }

            tag = BitConverter.ToUInt32(buffer, 0);
            return true;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    static bool TryCreateCrossVolumeSandbox(out string linkParent, out string targetDir, out Action dispose)
    {
        linkParent = "";
        targetDir = "";
        dispose = () => { };

        var volumes = new List<(string Root, uint Serial)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable))
                continue;

            var root = drive.RootDirectory.FullName;
            if (!root.EndsWith("\\", StringComparison.Ordinal))
                root += "\\";

            var fsName = new StringBuilder(32);
            if (!GetVolumeInformationW(root, null, 0, out var serial, out _, out _, fsName, fsName.Capacity))
                continue;
            if (!string.Equals(fsName.ToString(), "NTFS", StringComparison.OrdinalIgnoreCase))
                continue;

            volumes.Add((root, serial));
        }

        for (var i = 0; i < volumes.Count; i++)
        {
            for (var j = 0; j < volumes.Count; j++)
            {
                if (i == j || volumes[i].Serial == volumes[j].Serial)
                    continue;

                var createdA = TryCreateWritableSandbox(volumes[i].Root, out var a);
                var createdB = TryCreateWritableSandbox(volumes[j].Root, out var b);
                if (!createdA || !createdB)
                {
                    if (createdA)
                        TryDeleteSandbox(a);
                    if (createdB)
                        TryDeleteSandbox(b);
                    continue;
                }

                var target = Path.Combine(b, "target");
                Directory.CreateDirectory(target);
                linkParent = a;
                targetDir = target;
                dispose = () =>
                {
                    TryDeleteSandbox(a);
                    TryDeleteSandbox(b);
                };
                return true;
            }
        }

        return false;
    }

    static bool TryCreateWritableSandbox(string volumeRoot, out string dir)
    {
        dir = "";
        var candidates = new List<string>();
        var temp = Path.GetTempPath();
        var tempRoot = Path.GetPathRoot(temp);
        if (!string.IsNullOrEmpty(tempRoot)
            && string.Equals(
                Path.GetFullPath(tempRoot).TrimEnd('\\') + "\\",
                Path.GetFullPath(volumeRoot).TrimEnd('\\') + "\\",
                StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(temp, "mmm-j-" + Guid.NewGuid().ToString("N")));
        }

        candidates.Add(Path.Combine(volumeRoot, "mmm-j-" + Guid.NewGuid().ToString("N")));

        foreach (var candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, "probe.txt");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                dir = candidate;
                return true;
            }
            catch
            {
                TryDeleteSandbox(candidate);
            }
        }

        return false;
    }

    static void TryDeleteSandbox(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return;

        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of cross-volume sandbox.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    static extern bool GetVolumeInformationW(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder lpFileSystemNameBuffer,
        int nFileSystemNameSize);
}
