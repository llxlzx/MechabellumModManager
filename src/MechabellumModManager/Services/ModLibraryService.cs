using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class ImportNeedsTypeException : Exception
{
    public string StagingPath { get; }

    public ImportNeedsTypeException(string stagingPath)
        : base("无法识别 Mod 类型，需要用户指定。")
        => StagingPath = stagingPath;
}

public sealed class ModLibraryService
{
    static readonly JsonSerializerOptions PackageJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new ModPackageTypeSnakeCaseConverter(),
            new NullableModPackageTypeSnakeCaseConverter()
        }
    };

    readonly PathsService _paths;
    readonly AssemblyInspector _inspector;
    readonly JsonStore _store;

    public ModLibraryService(PathsService paths, AssemblyInspector inspector, JsonStore store)
    {
        _paths = paths;
        _inspector = inspector;
        _store = store;
    }

    string IndexPath => Path.Combine(_paths.LibraryRoot, "index.json");

    public ModPackage ImportDll(string dllPath, ModPackageType? forceType = null)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException("DLL not found", dllPath);

        var staging = Path.Combine(Path.GetTempPath(), "mmm-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var fileName = Path.GetFileName(dllPath);
            var stagedDll = Path.Combine(staging, fileName);
            File.Copy(dllPath, stagedDll, overwrite: true);

            var type = ResolveType(staging, stagedDll, pathHint: null, forceType);
            return CommitPackage(staging, type, primaryFileName: fileName);
        }
        catch (ImportNeedsTypeException)
        {
            throw; // keep StagingPath for UI to choose type
        }
        catch
        {
            TryDeleteDir(staging);
            throw;
        }
    }

    public IReadOnlyList<ModPackage> ImportZip(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Zip not found", zipPath);

        var extractRoot = Path.Combine(Path.GetTempPath(), "mmm-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractRoot);
        var prepared = new List<PreparedImport>();
        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractRoot);

            var groups = GroupExtractedFiles(extractRoot);
            if (groups.Count == 0)
                throw new InvalidOperationException("Zip 中没有可导入的文件。");

            // Phase 1: stage + resolve all groups before any library commit (atomic).
            foreach (var group in groups)
            {
                var staging = Path.Combine(Path.GetTempPath(), "mmm-stage-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    string? primaryDll = null;
                    foreach (var (relative, absolute) in group.Files)
                    {
                        var dest = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(absolute, dest, overwrite: true);
                        if (primaryDll is null && relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            primaryDll = dest;
                    }

                    var type = ResolveType(staging, primaryDll, group.PathHint, forceType: null);
                    var primaryFileName = primaryDll is null
                        ? Path.GetFileName(group.Files.First(f => !IsSkippedMeta(f.Relative)).Relative)
                        : Path.GetFileName(primaryDll);
                    prepared.Add(new PreparedImport(staging, type, primaryFileName));
                }
                catch (ImportNeedsTypeException)
                {
                    throw; // keep StagingPath; other prepared dirs cleaned below
                }
                catch
                {
                    TryDeleteDir(staging);
                    throw;
                }
            }

            // Phase 2: commit all; roll back already-committed packages on failure.
            var results = new List<ModPackage>();
            try
            {
                foreach (var item in prepared)
                {
                    results.Add(CommitPackage(item.StagingPath, item.Type, item.PrimaryFileName));
                    item.Committed = true;
                }
                return results;
            }
            catch
            {
                foreach (var pkg in results)
                {
                    try { Delete(pkg.Id); }
                    catch { /* best-effort rollback */ }
                }

                foreach (var item in prepared.Where(p => !p.Committed))
                    TryDeleteDir(item.StagingPath);
                throw;
            }
        }
        catch (ImportNeedsTypeException)
        {
            foreach (var item in prepared)
                TryDeleteDir(item.StagingPath);
            throw;
        }
        catch
        {
            foreach (var item in prepared)
                TryDeleteDir(item.StagingPath);
            throw;
        }
        finally
        {
            TryDeleteDir(extractRoot);
        }
    }

    public IReadOnlyList<ModPackage> List()
    {
        var index = LoadIndex();
        var list = new List<ModPackage>();
        foreach (var id in index.PackageIds)
        {
            var pkg = TryLoadPackage(id);
            if (pkg is not null)
                list.Add(pkg);
        }
        return list;
    }

    public void Delete(string packageId)
    {
        var index = LoadIndex();
        var pkg = TryLoadPackage(packageId);
        if (pkg is not null && Directory.Exists(pkg.PackageDirectory))
            Directory.Delete(pkg.PackageDirectory, recursive: true);

        index.PackageIds.RemoveAll(id => string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase));
        SaveIndex(index);
    }

    ModPackageType ResolveType(string stagingPath, string? primaryDll, ModPackageType? pathHint, ModPackageType? forceType)
    {
        var packageJsonPath = Path.Combine(stagingPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<PackageMeta>(File.ReadAllText(packageJsonPath), PackageJsonOptions);
                // Only trust type when the JSON property is present (nullable).
                if (meta?.Type is { } declared && Enum.IsDefined(typeof(ModPackageType), declared))
                    return declared;
            }
            catch
            {
                // fall through
            }
        }

        if (primaryDll is not null && File.Exists(primaryDll))
        {
            try
            {
                var inspect = _inspector.Inspect(primaryDll);
                if (inspect.LooksLikeMelonMod)
                    return ModPackageType.MelonMod;
                if (inspect.LooksLikeMelonPlugin)
                    return ModPackageType.MelonPlugin;
            }
            catch
            {
                // non-assemblies / stub dlls
            }
        }

        if (pathHint.HasValue)
            return pathHint.Value;

        if (forceType.HasValue)
            return forceType.Value;

        throw new ImportNeedsTypeException(stagingPath);
    }

    ModPackage CommitPackage(string stagingPath, ModPackageType type, string primaryFileName)
    {
        var primaryPath = Directory.GetFiles(stagingPath, "*", SearchOption.AllDirectories)
            .FirstOrDefault(f => Path.GetFileName(f).Equals(primaryFileName, StringComparison.OrdinalIgnoreCase))
            ?? Directory.GetFiles(stagingPath, "*.dll", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.GetFiles(stagingPath, "*", SearchOption.AllDirectories)
                .First(f => !IsSkippedMeta(Path.GetRelativePath(stagingPath, f)));

        string? melonName = null, melonVersion = null, melonAuthor = null;
        if (primaryPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var inspect = _inspector.Inspect(primaryPath);
                melonName = inspect.MelonName;
                melonVersion = inspect.MelonVersion;
                melonAuthor = inspect.MelonAuthor;
            }
            catch
            {
                // ignore
            }
        }

        var displayName = !string.IsNullOrWhiteSpace(melonName)
            ? melonName!
            : Path.GetFileNameWithoutExtension(primaryFileName);

        var hash = Sha256Hex(primaryPath);
        var id = Slug(displayName) + "-" + hash[..8];

        var packageDir = Path.Combine(TypeDirectory(type), id);
        if (Directory.Exists(packageDir))
            Directory.Delete(packageDir, recursive: true);
        Directory.CreateDirectory(packageDir);

        var files = new List<DeployableFile>();
        foreach (var src in Directory.GetFiles(stagingPath, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stagingPath, src).Replace('\\', '/');
            if (IsSkippedMeta(rel))
                continue;

            var dest = Path.Combine(packageDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, overwrite: true);
            files.Add(new DeployableFile
            {
                RelativePathInPackage = rel,
                Sha256 = Sha256Hex(dest)
            });
        }

        var pkg = new ModPackage
        {
            Id = id,
            DisplayName = displayName,
            Version = melonVersion,
            Author = melonAuthor,
            Type = type,
            Files = files,
            PackageDirectory = packageDir
        };

        WritePackageJson(pkg);
        var index = LoadIndex();
        if (!index.PackageIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            index.PackageIds.Add(id);
        SaveIndex(index);

        TryDeleteDir(stagingPath);
        return pkg;
    }

    void WritePackageJson(ModPackage pkg)
    {
        var meta = new PackageMeta
        {
            Id = pkg.Id,
            DisplayName = pkg.DisplayName,
            Version = pkg.Version,
            Author = pkg.Author,
            Type = pkg.Type,
            HighRisk = pkg.HighRisk,
            RequiredMelonLoaderVersion = pkg.RequiredMelonLoaderVersion,
            Files = pkg.Files
        };
        var json = JsonSerializer.Serialize(meta, PackageJsonOptions);
        File.WriteAllText(Path.Combine(pkg.PackageDirectory, "package.json"), json);
    }

    ModPackage? TryLoadPackage(string packageId)
    {
        foreach (ModPackageType type in Enum.GetValues<ModPackageType>())
        {
            var dir = Path.Combine(TypeDirectory(type), packageId);
            var metaPath = Path.Combine(dir, "package.json");
            if (!File.Exists(metaPath))
                continue;

            var meta = JsonSerializer.Deserialize<PackageMeta>(File.ReadAllText(metaPath), PackageJsonOptions);
            if (meta is null || meta.Type is null)
                continue;

            return new ModPackage
            {
                Id = meta.Id,
                DisplayName = meta.DisplayName,
                Version = meta.Version,
                Author = meta.Author,
                Type = meta.Type.Value,
                HighRisk = meta.HighRisk,
                RequiredMelonLoaderVersion = meta.RequiredMelonLoaderVersion,
                Files = meta.Files ?? new List<DeployableFile>(),
                PackageDirectory = dir
            };
        }

        return null;
    }

    string TypeDirectory(ModPackageType type) => type switch
    {
        ModPackageType.MelonMod => Path.Combine(_paths.LibraryRoot, "mods"),
        ModPackageType.MelonPlugin => Path.Combine(_paths.LibraryRoot, "plugins"),
        ModPackageType.MelonUserLibs => Path.Combine(_paths.LibraryRoot, "userlibs"),
        ModPackageType.MelonUserData => Path.Combine(_paths.LibraryRoot, "userdata"),
        _ => Path.Combine(_paths.LibraryRoot, "mods")
    };

    LibraryIndex LoadIndex() =>
        _store.LoadOrDefault(IndexPath, () => new LibraryIndex());

    void SaveIndex(LibraryIndex index) => _store.Save(IndexPath, index);

    static List<FileGroup> GroupExtractedFiles(string extractRoot)
    {
        var all = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories)
            .Select(abs =>
            {
                var rel = Path.GetRelativePath(extractRoot, abs).Replace('\\', '/');
                return (Relative: rel, Absolute: abs);
            })
            // Keep package.json for type detection; still excluded from deployable Files later.
            .Where(x => !IsSkippedImportSource(x.Relative))
            .ToList();

        var byHint = new Dictionary<ModPackageType, List<(string Relative, string Absolute)>>();
        var unprefixed = new List<(string Relative, string Absolute)>();

        foreach (var item in all)
        {
            if (TryStripPrefix(item.Relative, out var hint, out var stripped))
            {
                if (!byHint.TryGetValue(hint, out var list))
                {
                    list = new List<(string, string)>();
                    byHint[hint] = list;
                }
                list.Add((stripped, item.Absolute));
            }
            else
            {
                unprefixed.Add(item);
            }
        }

        var groups = new List<FileGroup>();
        foreach (var (hint, files) in byHint)
            groups.Add(new FileGroup(hint, files));

        if (unprefixed.Count > 0)
            groups.Add(new FileGroup(null, unprefixed));

        return groups;
    }

    static bool TryStripPrefix(string relative, out ModPackageType hint, out string stripped)
    {
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var top = parts[0];
            if (top.Equals("Mods", StringComparison.OrdinalIgnoreCase))
            {
                hint = ModPackageType.MelonMod;
                stripped = string.Join('/', parts.Skip(1));
                return true;
            }
            if (top.Equals("Plugins", StringComparison.OrdinalIgnoreCase))
            {
                hint = ModPackageType.MelonPlugin;
                stripped = string.Join('/', parts.Skip(1));
                return true;
            }
            if (top.Equals("UserLibs", StringComparison.OrdinalIgnoreCase))
            {
                hint = ModPackageType.MelonUserLibs;
                stripped = string.Join('/', parts.Skip(1));
                return true;
            }
            if (top.Equals("UserData", StringComparison.OrdinalIgnoreCase))
            {
                hint = ModPackageType.MelonUserData;
                stripped = string.Join('/', parts.Skip(1));
                return true;
            }
        }

        hint = default;
        stripped = relative;
        return false;
    }

    /// <summary>Entries skipped when building import groups (package.json is kept for type meta).</summary>
    static bool IsSkippedImportSource(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            return false;
        return IsSkippedMeta(relativePath);
    }

    static bool IsSkippedMeta(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.StartsWith("README", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            return true;
        var parts = relativePath.Replace('\\', '/').Split('/');
        if (parts.Any(p => p.StartsWith(".git", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    static string Slug(string displayName)
    {
        var s = displayName.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        s = Regex.Replace(s, @"-+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "mod" : s;
    }

    static string Sha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    sealed class LibraryIndex
    {
        public List<string> PackageIds { get; set; } = new();
    }

    sealed class PackageMeta
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Version { get; set; }
        public string? Author { get; set; }
        /// <summary>Null when package.json omits type — must not default to MelonMod.</summary>
        public ModPackageType? Type { get; set; }
        public bool HighRisk { get; set; }
        public string? RequiredMelonLoaderVersion { get; set; }
        public List<DeployableFile>? Files { get; set; }
    }

    sealed class FileGroup
    {
        public ModPackageType? PathHint { get; }
        public List<(string Relative, string Absolute)> Files { get; }

        public FileGroup(ModPackageType? pathHint, List<(string Relative, string Absolute)> files)
        {
            PathHint = pathHint;
            Files = files;
        }
    }

    sealed class PreparedImport
    {
        public string StagingPath { get; }
        public ModPackageType Type { get; }
        public string PrimaryFileName { get; }
        public bool Committed { get; set; }

        public PreparedImport(string stagingPath, ModPackageType type, string primaryFileName)
        {
            StagingPath = stagingPath;
            Type = type;
            PrimaryFileName = primaryFileName;
        }
    }

    /// <summary>Wire format: melon_mod / melon_plugin / melon_userlibs / melon_userdata.</summary>
    sealed class ModPackageTypeSnakeCaseConverter : JsonConverter<ModPackageType>
    {
        public override ModPackageType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            if (s is null)
                throw new JsonException("Mod package type string expected.");

            return s switch
            {
                "melon_mod" or "melonMod" => ModPackageType.MelonMod,
                "melon_plugin" or "melonPlugin" => ModPackageType.MelonPlugin,
                "melon_userlibs" or "melonUserLibs" => ModPackageType.MelonUserLibs,
                "melon_userdata" or "melonUserData" => ModPackageType.MelonUserData,
                _ => throw new JsonException($"Unknown mod package type: {s}")
            };
        }

        public override void Write(Utf8JsonWriter writer, ModPackageType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value switch
            {
                ModPackageType.MelonMod => "melon_mod",
                ModPackageType.MelonPlugin => "melon_plugin",
                ModPackageType.MelonUserLibs => "melon_userlibs",
                ModPackageType.MelonUserData => "melon_userdata",
                _ => throw new JsonException($"Unknown mod package type: {value}")
            });
        }
    }

    sealed class NullableModPackageTypeSnakeCaseConverter : JsonConverter<ModPackageType?>
    {
        readonly ModPackageTypeSnakeCaseConverter _inner = new();

        public override ModPackageType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            return _inner.Read(ref reader, typeof(ModPackageType), options);
        }

        public override void Write(Utf8JsonWriter writer, ModPackageType? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }
            _inner.Write(writer, value.Value, options);
        }
    }
}