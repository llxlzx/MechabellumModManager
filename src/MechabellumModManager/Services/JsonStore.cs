using System.IO;
using System.Text.Json;

namespace MechabellumModManager.Services;

public sealed class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Set when a load fell back to defaults because the file was unreadable.</summary>
    public string? LastRecoveredBackupPath { get; private set; }

    public T LoadOrDefault<T>(string path, Func<T> factory)
    {
        if (!File.Exists(path))
            return factory();

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return factory();

            return JsonSerializer.Deserialize<T>(json, Options) ?? factory();
        }
        catch (JsonException)
        {
            // Silently resetting would drop the game path and launch stamps with no way back.
            LastRecoveredBackupPath = TryBackupCorruptFile(path);
            return factory();
        }
        catch (IOException)
        {
            return factory();
        }
    }

    public void Save<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(value, Options);

        // A direct write leaves a truncated file if the machine dies mid-save.
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException)
        {
            File.Copy(temp, path, overwrite: true);
            try { File.Delete(temp); }
            catch (IOException) { /* leftover temp is harmless */ }
        }
    }

    static string? TryBackupCorruptFile(string path)
    {
        try
        {
            var backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
            File.Copy(path, backup, overwrite: true);
            return backup;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
