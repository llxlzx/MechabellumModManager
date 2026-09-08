namespace MechabellumModManager.Services;

/// <summary>Human-readable byte counts for progress text.</summary>
public static class ByteSize
{
    const double Kb = 1024;
    const double Mb = Kb * 1024;
    const double Gb = Mb * 1024;

    public static string Format(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes < Kb) return $"{bytes} B";
        if (bytes < Mb) return $"{bytes / Kb:0.#} KB";
        if (bytes < Gb) return $"{bytes / Mb:0.00} MB";
        return $"{bytes / Gb:0.00} GB";
    }
}
