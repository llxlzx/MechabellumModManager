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

    public T LoadOrDefault<T>(string path, Func<T> factory)
    {
        if (!File.Exists(path))
            return factory();

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return factory();

        return JsonSerializer.Deserialize<T>(json, Options) ?? factory();
    }

    public void Save<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(path, json);
    }
}
