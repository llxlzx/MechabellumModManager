using System.Security.Cryptography;

namespace BlackBox.Core;

public static class HelperRelease
{
    public static bool NeedsWrite(string destPath, byte[] payload)
    {
        if (!File.Exists(destPath))
            return true;
        byte[] existing;
        try { existing = File.ReadAllBytes(destPath); }
        catch { return true; }
        return !SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(payload));
    }
}
