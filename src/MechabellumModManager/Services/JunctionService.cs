using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MechabellumModManager.Services;

public interface IJunctionService
{
    bool IsJunction(string path);
    string? ResolveTarget(string path);
    void CreateJunction(string linkPath, string targetPath);
    void DeleteJunction(string linkPath);
}

public sealed class JunctionService : IJunctionService
{
    const uint FileReadAttributes = 0x0080;
    const uint FileShareRead = 0x0001;
    const uint FileShareWrite = 0x0002;
    const uint FileShareDelete = 0x0004;
    const uint OpenExisting = 3;
    const uint FileFlagBackupSemantics = 0x02000000;
    const uint FileFlagOpenReparsePoint = 0x00200000;
    const uint FsctlGetReparsePoint = 0x000900A8;
    const uint IoReparseTagMountPoint = 0xA0000003;
    const uint IoReparseTagSymlink = 0xA000000C;
    const int MaximumReparseDataBufferSize = 16 * 1024;
    const int MklinkTimeoutMs = 15_000;

    static readonly IntPtr InvalidHandleValue = new(-1);

    public bool IsJunction(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !LooksLikeDirectoryReparsePoint(path))
            return false;

        return TryReadReparse(path, out var tag, out _)
               && IsDirectoryLinkTag(tag);
    }

    public string? ResolveTarget(string path)
    {
        if (!IsJunction(path) || !TryReadReparse(path, out _, out var target) || string.IsNullOrWhiteSpace(target))
            return null;

        return NormalizePath(target);
    }

    public void CreateJunction(string linkPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var fullLink = Path.GetFullPath(linkPath);
        var fullTarget = Path.GetFullPath(targetPath);

        EnsureSameNtfsVolume(fullLink, fullTarget);

        if (PathExists(fullLink))
            throw new IOException($"Link path already exists: {fullLink}");

        if (!TryCreateJunctionWithMklink(fullLink, fullTarget))
        {
            throw new InvalidOperationException(
                $"Failed to create directory junction from '{fullLink}' to '{fullTarget}'.");
        }

        if (!TryReadReparse(fullLink, out var tag, out _) || tag != IoReparseTagMountPoint)
            throw new InvalidOperationException($"Created path is not a mount-point junction: {fullLink}");
    }

    public void DeleteJunction(string linkPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);

        var fullLink = Path.GetFullPath(linkPath);
        if (!IsJunction(fullLink))
            throw new InvalidOperationException($"'{fullLink}' is not a directory junction.");

        // RemoveDirectory removes the reparse point only; it must never recurse into the target.
        if (!RemoveDirectoryW(fullLink))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to remove junction '{fullLink}'.");
    }

    static bool LooksLikeDirectoryReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) != 0
                   && (attrs & FileAttributes.Directory) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    static bool IsDirectoryLinkTag(uint tag) =>
        tag == IoReparseTagMountPoint || tag == IoReparseTagSymlink;

    static bool PathExists(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    static void EnsureSameNtfsVolume(string linkPath, string targetPath)
    {
        var linkSerial = QueryNtfsVolumeSerial(linkPath);
        var targetSerial = QueryNtfsVolumeSerial(targetPath);
        if (linkSerial != targetSerial)
        {
            throw new InvalidOperationException(
                $"Junction link and target must be on the same volume ('{linkPath}' vs '{targetPath}').");
        }
    }

    static uint QueryNtfsVolumeSerial(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            throw new InvalidOperationException($"Cannot determine volume for '{path}'.");

        if (!root.EndsWith("\\", StringComparison.Ordinal))
            root += "\\";

        var fsName = new StringBuilder(32);
        if (!GetVolumeInformationW(root, null, 0, out var serial, out _, out _, fsName, fsName.Capacity))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Failed to query file system for '{root}'.");
        }

        if (!string.Equals(fsName.ToString(), "NTFS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Junctions require an NTFS volume (found '{fsName}' on '{root}').");
        }

        return serial;
    }

    static bool TryCreateJunctionWithMklink(string linkPath, string targetPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c mklink /J \"" + linkPath + "\" \"" + targetPath + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(psi);
        if (process is null)
            return false;

        if (!process.WaitForExit(MklinkTimeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort abort if mklink hangs.
            }

            return false;
        }

        return process.ExitCode == 0 && Directory.Exists(linkPath);
    }

    static bool TryReadReparse(string path, out uint tag, out string? target)
    {
        tag = 0;
        target = null;

        var handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);

        if (handle == InvalidHandleValue)
            return false;

        var buffer = new byte[MaximumReparseDataBufferSize];
        try
        {
            if (!DeviceIoControl(
                    handle,
                    FsctlGetReparsePoint,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)buffer.Length,
                    out var bytesReturned,
                    IntPtr.Zero)
                || bytesReturned < 8)
            {
                return false;
            }

            tag = BitConverter.ToUInt32(buffer, 0);
            if (!IsDirectoryLinkTag(tag))
                return false;

            target = ReadReparseTarget(buffer, tag);
            return true;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    static string? ReadReparseTarget(byte[] buffer, uint tag)
    {
        // REPARSE_DATA_BUFFER: tag(4) + dataLength(2) + reserved(2) + name offsets, then PathBuffer.
        // Symlink has an extra Flags DWORD before PathBuffer.
        var pathBufferOffset = tag == IoReparseTagSymlink ? 20 : 16;
        if (buffer.Length < pathBufferOffset)
            return null;

        var substituteOffset = BitConverter.ToUInt16(buffer, 8);
        var substituteLength = BitConverter.ToUInt16(buffer, 10);
        var printOffset = BitConverter.ToUInt16(buffer, 12);
        var printLength = BitConverter.ToUInt16(buffer, 14);

        var printName = DecodeReparseName(buffer, pathBufferOffset, printOffset, printLength);
        if (!string.IsNullOrWhiteSpace(printName))
            return printName;

        var substituteName = DecodeReparseName(buffer, pathBufferOffset, substituteOffset, substituteLength);
        return StripNtPrefix(substituteName);
    }

    static string? DecodeReparseName(byte[] buffer, int pathBufferOffset, ushort nameOffset, ushort nameLength)
    {
        var start = pathBufferOffset + nameOffset;
        if (nameLength == 0 || start < 0 || start + nameLength > buffer.Length)
            return null;

        return Encoding.Unicode.GetString(buffer, start, nameLength);
    }

    static string? StripNtPrefix(string? ntPath)
    {
        if (string.IsNullOrWhiteSpace(ntPath))
            return ntPath;

        const string ntPrefix = @"\??\";
        if (ntPath.StartsWith(ntPrefix, StringComparison.Ordinal))
        {
            var rest = ntPath[ntPrefix.Length..];
            if (rest.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase))
                return @"\\" + rest[4..];
            return rest;
        }

        if (ntPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return ntPath[4..];

        return ntPath;
    }

    static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (root is not null && full.Length == root.Length)
            return full;

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    static extern bool RemoveDirectoryW(string lpPathName);

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
