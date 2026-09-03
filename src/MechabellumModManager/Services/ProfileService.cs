using System.IO;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class ProfileService
{
    readonly PathsService _paths;
    readonly JsonStore _store;

    public ProfileService(PathsService paths, JsonStore store)
    {
        _paths = paths;
        _store = store;
    }

    public void EnsureDefaults()
    {
        Directory.CreateDirectory(_paths.ProfilesDir);
        var path = ProfilePath("default");
        const string defaultDisplayName = "默认";

        if (File.Exists(path))
        {
            var existing = _store.LoadOrDefault(path, () => (Profile?)null);
            if (existing is not null
                && string.Equals(existing.Id, "default", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existing.Name, defaultDisplayName, StringComparison.Ordinal))
            {
                existing.Name = defaultDisplayName;
                SaveProfile(existing);
            }

            return;
        }

        SaveProfile(new Profile
        {
            Id = "default",
            Name = defaultDisplayName,
            EnabledPackageIds = new List<string>()
        });

        var config = LoadConfig();
        if (string.IsNullOrWhiteSpace(config.ActiveProfileId))
            config.ActiveProfileId = "default";
        SaveConfig(config);
    }

    public IReadOnlyList<Profile> List()
    {
        Directory.CreateDirectory(_paths.ProfilesDir);
        var list = new List<Profile>();
        foreach (var file in Directory.GetFiles(_paths.ProfilesDir, "*.json"))
        {
            var profile = _store.LoadOrDefault(file, () => (Profile?)null);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Id))
                continue;
            list.Add(profile);
        }

        return list
            .OrderBy(p => p.Id == "default" ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Profile Get(string id)
    {
        var path = ProfilePath(id);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Profile not found: {id}", path);

        var profile = _store.LoadOrDefault(path, () => (Profile?)null);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id))
            throw new InvalidDataException($"Invalid profile file: {path}");

        return profile;
    }

    public Profile Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name is required.", nameof(name));

        var id = NewUniqueId(name);
        var profile = new Profile
        {
            Id = id,
            Name = name.Trim(),
            EnabledPackageIds = new List<string>()
        };
        SaveProfile(profile);
        return profile;
    }

    public void Rename(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name is required.", nameof(name));

        var profile = Get(id);
        profile.Name = name.Trim();
        SaveProfile(profile);
    }

    public Profile Duplicate(string id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Profile name is required.", nameof(newName));

        var source = Get(id);
        var copy = new Profile
        {
            Id = NewUniqueId(newName),
            Name = newName.Trim(),
            EnabledPackageIds = new List<string>(source.EnabledPackageIds)
        };
        SaveProfile(copy);
        return copy;
    }

    public void Delete(string id)
    {
        var profiles = List();
        if (profiles.Count <= 1)
            throw new InvalidOperationException("Cannot delete the last profile.");

        var target = profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Profile not found: {id}");

        var path = ProfilePath(target.Id);
        if (File.Exists(path))
            File.Delete(path);

        var remaining = List();
        var config = LoadConfig();
        if (string.Equals(config.ActiveProfileId, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            config.ActiveProfileId = remaining[0].Id;
            SaveConfig(config);
        }
    }

    public void SetEnabled(string profileId, string packageId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Package id is required.", nameof(packageId));

        var profile = Get(profileId);
        var ids = profile.EnabledPackageIds;
        var idx = ids.FindIndex(x => string.Equals(x, packageId, StringComparison.OrdinalIgnoreCase));

        if (enabled)
        {
            if (idx < 0)
                ids.Add(packageId);
        }
        else if (idx >= 0)
        {
            ids.RemoveAt(idx);
        }

        SaveProfile(profile);
    }

    public void RemovePackageFromAllProfiles(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        foreach (var profile in List())
        {
            var removed = profile.EnabledPackageIds.RemoveAll(x =>
                string.Equals(x, packageId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                SaveProfile(profile);
        }
    }

    string ProfilePath(string id) => Path.Combine(_paths.ProfilesDir, id + ".json");

    void SaveProfile(Profile profile)
    {
        Directory.CreateDirectory(_paths.ProfilesDir);
        _store.Save(ProfilePath(profile.Id), profile);
    }

    AppConfig LoadConfig() =>
        _store.LoadOrDefault(_paths.ConfigPath, () => new AppConfig());

    void SaveConfig(AppConfig config) => _store.Save(_paths.ConfigPath, config);

    string NewUniqueId(string name)
    {
        var baseId = Slug(name);
        if (baseId == "default" || File.Exists(ProfilePath(baseId)))
            baseId = baseId + "-" + Guid.NewGuid().ToString("N")[..8];

        while (File.Exists(ProfilePath(baseId)))
            baseId = Slug(name) + "-" + Guid.NewGuid().ToString("N")[..8];

        return baseId;
    }

    static string Slug(string name)
    {
        var s = name.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        s = Regex.Replace(s, @"-+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "profile" : s;
    }
}
