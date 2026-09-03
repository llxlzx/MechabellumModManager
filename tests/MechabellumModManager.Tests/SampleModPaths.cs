internal static class SampleModPaths
{
    /// <summary>
    /// Resolves <c>_samples/QuickCamera/QuickCamera.dll</c> by walking up from
    /// <see cref="AppContext.BaseDirectory"/>. Returns null if not found.
    /// </summary>
    public static string? FindQuickCameraDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "_samples", "QuickCamera", "QuickCamera.dll");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Returns the sample DLL path, or skips the current test if it is missing.
    /// Call only from <c>[SkippableFact]</c> methods.
    /// </summary>
    public static string RequireQuickCameraDll()
    {
        var path = FindQuickCameraDll();
        Skip.If(
            path is null,
            "Missing _samples/QuickCamera/QuickCamera.dll (searched upward from AppContext.BaseDirectory).");
        return path!;
    }
}
