# Mechabellum Mod Manager Implementation Plan

> **说明 / Notice**  
> 本文档整理自本项目维护者在实现 Mechabellum（钢铁指挥官）Mod 管理器过程中的任务计划，供有意复现或参考本方案的开发者使用。文中路径与环境均为**示例**，请按本机调整；不构成官方承诺或完整运维规范。  
> **This document is the implementation plan for the Mechabellum Mod Manager, shared as a reference for developers who wish to reproduce or learn from it. Paths and environment details are examples only. This is not an official commitment or a complete operations manual.**


> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 8 WPF MelonLoader mod manager for Mechabellum that detects the game, manages an external mod library with profiles, file-copy deploys (flattened) to `Mods`/`Plugins`/`UserLibs`/`UserData`, and supports apply + Steam/exe launch.

**Architecture:** Single WPF app with thin Views, ViewModels (`CommunityToolkit.Mvvm`), and testable Services. Library + profiles live under `%AppData%\MechabellumModManager` (portable `data/` optional). Deploy uses a gamePath-bound manifest; conflict/UserData/`Loader.cfg` rules match the design spec.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, System.Text.Json, xUnit, Mono.Cecil (assembly inspect without loading MelonLoader), FluentAssertions (optional).

**Spec:** `docs/superpowers/specs/2026-09-02-mechabellum-mod-manager-design.md`

## Global Constraints

- Target: Windows x64; language UI default Chinese
- Game default path: `C:\Program Files (x86)\Steam\steamapps\common\Mechabellum`; Steam AppID `669330`
- Do **not** install MelonLoader/BepInEx; require Ready (game + `MelonLoader/` + `version.dll` or `winhttp.dll`) before deploy/launch
- Deploy = file **copy** only; flatten to folder roots for Mods/Plugins/UserLibs (no per-mod subfolders in game)
- Never delete/overwrite `UserData/Loader.cfg`
- Never delete non-manifest (unmanaged) files unless user confirms overwrite-and-takeover for same-name conflict
- One library entry = one type; mixed zips split into multiple entries
- Checkbox saves profile immediately; Apply deploys; Apply+Launch deploys then launches
- No cheat features; RiskGate is warning + manual high-risk flag only
- Working tree root: `<repo-root>`
- Sample mod: `_samples/QuickCamera/QuickCamera.dll`

---

## File Structure

```
<repo-root>\
  MechabellumModManager.sln
  src\
    MechabellumModManager\
      MechabellumModManager.csproj
      App.xaml
      App.xaml.cs
      MainWindow.xaml
      MainWindow.xaml.cs
      Models\
        AppConfig.cs
        ModPackage.cs
        ModPackageType.cs
        Profile.cs
        DeployManifest.cs
        GameStatus.cs
        DeployPlan.cs
      Services\
        PathsService.cs
        JsonStore.cs
        GameDetector.cs
        AssemblyInspector.cs
        ModLibraryService.cs
        ProfileService.cs
        DeployPlanner.cs
        DeployService.cs
        GameLauncher.cs
        ProcessProbe.cs
        RiskGate.cs
      ViewModels\
        MainViewModel.cs
        ModItemViewModel.cs
        ProfileItemViewModel.cs
      Converters\          # if needed for status brushes
  tests\
    MechabellumModManager.Tests\
      MechabellumModManager.Tests.csproj
      GameDetectorTests.cs
      DeployPlannerTests.cs
      ModLibraryImportTests.cs
      ProfileServiceTests.cs
      DeployServiceTests.cs
      AssemblyInspectorTests.cs
  docs\superpowers\specs\...
  docs\superpowers\plans\...
  _samples\QuickCamera\QuickCamera.dll
```

**Responsibility notes:**
- `DeployPlanner` = pure plan calculation (unit-tested heavily)
- `DeployService` = filesystem execute + rollback
- `AssemblyInspector` = Mono.Cecil only (no MelonLoader reference required)

---

### Task 1: Solution scaffold + core models + Paths/Json

**Files:**
- Create: `MechabellumModManager.sln`
- Create: `src/MechabellumModManager/MechabellumModManager.csproj`
- Create: `tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`
- Create: `src/MechabellumModManager/Models/*.cs` (listed above)
- Create: `src/MechabellumModManager/Services/PathsService.cs`
- Create: `src/MechabellumModManager/Services/JsonStore.cs`
- Create: `tests/MechabellumModManager.Tests/JsonStoreTests.cs`
- Create: stub `App.xaml`, `MainWindow.xaml` so WPF project builds

**Interfaces:**
- Consumes: none
- Produces:
  - `enum ModPackageType { MelonMod, MelonPlugin, MelonUserLibs, MelonUserData }`
  - `enum GameStatusKind { GameMissing, GameOkLoaderMissing, LoaderPartial, Ready }`
  - `sealed class PathsService` → `string DataRoot`, `string ConfigPath`, `string LibraryRoot`, `string ProfilesDir`, `string DeployManifestPath`, `string DeployManifestPrevPath`, `string LogsDir`; ctor `(string? overrideRoot = null)`
  - `sealed class JsonStore` → `T LoadOrDefault<T>(string path, Func<T> factory)`, `void Save<T>(string path, T value)`

- [ ] **Step 1: Create solution and projects**

```powershell
cd "<repo-root>"
dotnet new sln -n MechabellumModManager
dotnet new wpf -n MechabellumModManager -o src/MechabellumModManager -f net8.0-windows
dotnet new xunit -n MechabellumModManager.Tests -o tests/MechabellumModManager.Tests -f net8.0
dotnet sln add src/MechabellumModManager/MechabellumModManager.csproj
dotnet sln add tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj
dotnet add tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj reference src/MechabellumModManager/MechabellumModManager.csproj
dotnet add src/MechabellumModManager package CommunityToolkit.Mvvm
dotnet add src/MechabellumModManager package Mono.Cecil
dotnet add tests/MechabellumModManager.Tests package FluentAssertions
```

Edit `MechabellumModManager.Tests.csproj` to `net8.0-windows` (match WPF TFM) if reference fails.

- [ ] **Step 2: Write failing test for JsonStore round-trip**

```csharp
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class JsonStoreTests
{
    [Fact]
    public void Save_then_Load_roundtrips_AppConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            var store = new JsonStore();
            var cfg = new AppConfig
            {
                GamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Mechabellum",
                LaunchMode = LaunchMode.SteamThenExe,
                ActiveProfileId = "default",
                DataRoot = dir
            };
            store.Save(path, cfg);
            var loaded = store.LoadOrDefault(path, () => new AppConfig());
            loaded.GamePath.Should().Be(cfg.GamePath);
            loaded.ActiveProfileId.Should().Be("default");
            loaded.LaunchMode.Should().Be(LaunchMode.SteamThenExe);
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 3: Run test — expect fail**

Run: `dotnet test tests/MechabellumModManager.Tests --filter JsonStoreTests -v n`  
Expected: FAIL (types/store missing)

- [ ] **Step 4: Implement models + JsonStore + PathsService**

`AppConfig.cs`:

```csharp
namespace MechabellumModManager.Models;

public enum LaunchMode { SteamThenExe, SteamOnly, ExeOnly }

public sealed class AppConfig
{
    public string GamePath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\Mechabellum";
    public LaunchMode LaunchMode { get; set; } = LaunchMode.SteamThenExe;
    public string ActiveProfileId { get; set; } = "default";
    public string? DataRoot { get; set; }
}
```

`ModPackageType.cs` / `GameStatus.cs` / `ModPackage.cs` / `Profile.cs` / `DeployManifest.cs` / `DeployPlan.cs` — define as:

```csharp
namespace MechabellumModManager.Models;

public enum ModPackageType { MelonMod, MelonPlugin, MelonUserLibs, MelonUserData }
public enum GameStatusKind { GameMissing, GameOkLoaderMissing, LoaderPartial, Ready }

public sealed class GameStatus
{
    public GameStatusKind Kind { get; init; }
    public string GamePath { get; init; } = "";
    public string Message { get; init; } = "";
    public string? MelonLoaderVersion { get; init; }
}

public sealed class DeployableFile
{
    public string RelativePathInPackage { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class ModPackage
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Version { get; set; }
    public string? Author { get; set; }
    public ModPackageType Type { get; set; }
    public bool HighRisk { get; set; }
    public string? RequiredMelonLoaderVersion { get; set; }
    public List<DeployableFile> Files { get; set; } = new();
    public string PackageDirectory { get; set; } = ""; // absolute under library
}

public sealed class Profile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> EnabledPackageIds { get; set; } = new();
}

public sealed class ManifestFileEntry
{
    public string RelativePath { get; set; } = ""; // relative GameRoot
    public string PackageId { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class DeployManifest
{
    public string GamePath { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public List<ManifestFileEntry> Files { get; set; } = new();
}

public sealed class PlannedCopy
{
    public string SourceAbsolute { get; init; } = "";
    public string DestAbsolute { get; init; } = "";
    public string RelativeGamePath { get; init; } = "";
    public string PackageId { get; init; } = "";
    public string Sha256 { get; init; } = "";
}

public sealed class DeployPlan
{
    public List<string> Deletes { get; init; } = new(); // absolute paths
    public List<PlannedCopy> Copies { get; init; } = new();
    public List<string> ConflictsUnmanaged { get; init; } = new(); // absolute
    public List<string> IntraProfileNameCollisions { get; init; } = new();
    public bool ManifestInvalidDueToGamePath { get; init; }
}
```

`JsonStore.cs`: use `System.Text.Json` with `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

`PathsService.cs`:

```csharp
public sealed class PathsService
{
    public string DataRoot { get; }
    public PathsService(string? overrideRoot = null)
    {
        DataRoot = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MechabellumModManager")
            : overrideRoot;
    }
    public string ConfigPath => Path.Combine(DataRoot, "config.json");
    public string LibraryRoot => Path.Combine(DataRoot, "library");
    public string ProfilesDir => Path.Combine(DataRoot, "profiles");
    public string DeployManifestPath => Path.Combine(DataRoot, "deploy-manifest.json");
    public string DeployManifestPrevPath => Path.Combine(DataRoot, "deploy-manifest.prev.json");
    public string LogsDir => Path.Combine(DataRoot, "logs");
    public void EnsureCreated()
    {
        Directory.CreateDirectory(LibraryRoot);
        foreach (var sub in new[] { "mods", "plugins", "userlibs", "userdata" })
            Directory.CreateDirectory(Path.Combine(LibraryRoot, sub));
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(LogsDir);
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

Run: `dotnet test tests/MechabellumModManager.Tests --filter JsonStoreTests -v n`  
Expected: PASS

- [ ] **Step 6: Commit**

```powershell
cd "<repo-root>"
git init  # only if not already a repo
git add MechabellumModManager.sln src tests
git commit -m "chore: scaffold WPF solution, models, JsonStore, PathsService"
```

---

### Task 2: GameDetector

**Files:**
- Create: `src/MechabellumModManager/Services/GameDetector.cs`
- Create: `tests/MechabellumModManager.Tests/GameDetectorTests.cs`

**Interfaces:**
- Consumes: `GameStatus`, `GameStatusKind`
- Produces: `GameDetector.Detect(string gamePath) -> GameStatus`

- [ ] **Step 1: Write failing tests**

```csharp
public class GameDetectorTests
{
    [Fact]
    public void Missing_exe_is_GameMissing()
    {
        var root = CreateTempGame(exe: false, ga: false, melonDir: false, proxy: false);
        try
        {
            var s = new GameDetector().Detect(root);
            s.Kind.Should().Be(GameStatusKind.GameMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Game_without_loader_is_GameOkLoaderMissing()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: false, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.GameOkLoaderMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Melon_dir_without_proxy_is_LoaderPartial()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.LoaderPartial);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Full_install_is_Ready()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.Ready);
        }
        finally { Directory.Delete(root, true); }
    }

    static string CreateTempGame(bool exe, bool ga, bool melonDir, bool proxy)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (exe) File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        if (ga) File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        if (melonDir) Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        if (proxy) File.WriteAllText(Path.Combine(root, "version.dll"), "");
        return root;
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test --filter GameDetectorTests -v n`  
Expected: FAIL

- [ ] **Step 3: Implement GameDetector**

```csharp
public sealed class GameDetector
{
    public GameStatus Detect(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) ||
            !File.Exists(Path.Combine(gamePath, "Mechabellum.exe")) ||
            !File.Exists(Path.Combine(gamePath, "GameAssembly.dll")))
        {
            return new GameStatus
            {
                Kind = GameStatusKind.GameMissing,
                GamePath = gamePath ?? "",
                Message = "未找到有效的 Mechabellum 安装（需要 Mechabellum.exe 与 GameAssembly.dll）。"
            };
        }

        var melon = Directory.Exists(Path.Combine(gamePath, "MelonLoader"));
        var proxy = File.Exists(Path.Combine(gamePath, "version.dll"))
                    || File.Exists(Path.Combine(gamePath, "winhttp.dll"));

        if (!melon && !proxy)
            return new GameStatus
            {
                Kind = GameStatusKind.GameOkLoaderMissing,
                GamePath = gamePath,
                Message = "已找到游戏，但未安装 MelonLoader。请自行安装后再部署。"
            };

        if (!(melon && proxy))
            return new GameStatus
            {
                Kind = GameStatusKind.LoaderPartial,
                GamePath = gamePath,
                Message = "MelonLoader 安装不完整（需要 MelonLoader 目录以及 version.dll 或 winhttp.dll）。"
            };

        return new GameStatus
        {
            Kind = GameStatusKind.Ready,
            GamePath = gamePath,
            Message = "游戏与 MelonLoader 已就绪。",
            MelonLoaderVersion = TryReadMelonVersion(gamePath)
        };
    }

    static string? TryReadMelonVersion(string gamePath)
    {
        // Best-effort: MelonLoader/Documentation/MelonLoader.xml or version on MelonLoader.dll if present
        var dll = Directory.GetFiles(Path.Combine(gamePath, "MelonLoader"), "MelonLoader.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (dll is null) return null;
        try { return System.Diagnostics.FileVersionInfo.GetVersionInfo(dll).FileVersion; }
        catch { return null; }
    }
}
```

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test --filter GameDetectorTests -v n`

- [ ] **Step 5: Commit**

```powershell
git add src/MechabellumModManager/Services/GameDetector.cs tests/MechabellumModManager.Tests/GameDetectorTests.cs
git commit -m "feat: detect Mechabellum and MelonLoader readiness"
```

---

### Task 3: AssemblyInspector + ModLibraryService import

**Files:**
- Create: `src/MechabellumModManager/Services/AssemblyInspector.cs`
- Create: `src/MechabellumModManager/Services/ModLibraryService.cs`
- Create: `tests/MechabellumModManager.Tests/AssemblyInspectorTests.cs`
- Create: `tests/MechabellumModManager.Tests/ModLibraryImportTests.cs`

**Interfaces:**
- Consumes: `PathsService`, `ModPackage`, `ModPackageType`
- Produces:
  - `AssemblyInspector.Inspect(string dllPath) -> AssemblyInspectResult`
  - `AssemblyInspectResult { bool ReferencesMelonLoader; bool LooksLikeMelonMod; bool LooksLikeMelonPlugin; string? MelonName; string? MelonVersion; string? MelonAuthor; }`
  - `ModLibraryService.ImportDll(string dllPath, ModPackageType? forceType = null) -> ModPackage`
  - `ModLibraryService.ImportZip(string zipPath) -> IReadOnlyList<ModPackage>`
  - `ModLibraryService.List() -> IReadOnlyList<ModPackage>`
  - `ModLibraryService.Delete(string packageId)`
  - Library index file: `{LibraryRoot}/index.json` listing packages (or scan directories + `package.json`)

- [ ] **Step 1: Write failing AssemblyInspector test against sample**

```csharp
[Fact]
public void QuickCamera_looks_like_MelonMod()
{
    var dll = @"<repo-root>\_samples\QuickCamera\QuickCamera.dll";
    File.Exists(dll).Should().BeTrue();
    var r = new AssemblyInspector().Inspect(dll);
    r.ReferencesMelonLoader.Should().BeTrue();
    r.LooksLikeMelonMod.Should().BeTrue();
    r.LooksLikeMelonPlugin.Should().BeFalse();
}
```

Use Mono.Cecil: load module, check assembly refs for name `MelonLoader`; scan types for `BaseType.FullName` ending with `MelonMod` / `MelonPlugin`, or custom attrs containing `MelonInfo`.

- [ ] **Step 2: Run — FAIL then implement AssemblyInspector — PASS**

- [ ] **Step 3: Write ModLibraryImportTests**

```csharp
[Fact]
public void Import_QuickCamera_dll_creates_melon_mod_package_flat_files()
{
    var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
    var paths = new PathsService(data);
    paths.EnsureCreated();
    var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore());
    var pkg = lib.ImportDll(@"<repo-root>\_samples\QuickCamera\QuickCamera.dll");
    pkg.Type.Should().Be(ModPackageType.MelonMod);
    pkg.Files.Should().ContainSingle(f => f.RelativePathInPackage.Equals("QuickCamera.dll", StringComparison.OrdinalIgnoreCase));
    File.Exists(Path.Combine(pkg.PackageDirectory, "QuickCamera.dll")).Should().BeTrue();
    Directory.Delete(data, true);
}

[Fact]
public void Import_zip_with_Mods_prefix_strips_prefix()
{
    // Create temp zip containing Mods/Foo.dll (empty stub file renamed .dll is fine for path logic;
    // if inspector fails, pass forceType via ImportZip internals — prefer real QuickCamera bytes named under Mods/)
}
```

For zip test: build zip in temp with entry `Mods/QuickCamera.dll` copying sample bytes; import; expect one MelonMod with file `QuickCamera.dll` not `Mods/QuickCamera.dll`.

Also test mixed zip `Mods/a.dll` + `UserLibs/b.dll` → two packages.

- [ ] **Step 4: Implement ModLibraryService**

Rules from spec:
- Id = slug(displayName) + "-" + first 8 of sha256 of primary file
- Skip deploying metadata names when building file list: `package.json`, `README*`, `*.pdb`, `.git*`
- Write `package.json` into package dir
- Type detection order: package.json → Cecil MelonMod/Plugin → path prefix → require force / throw asking UI to choose (`ImportNeedsTypeException`)

```csharp
public sealed class ImportNeedsTypeException : Exception
{
    public string StagingPath { get; }
    public ImportNeedsTypeException(string stagingPath) : base("无法识别 Mod 类型，需要用户指定。")
        => StagingPath = stagingPath;
}
```

- [ ] **Step 5: `dotnet test --filter "AssemblyInspectorTests|ModLibraryImportTests"` — PASS**

- [ ] **Step 6: Commit**

```powershell
git add src/MechabellumModManager/Services/AssemblyInspector.cs src/MechabellumModManager/Services/ModLibraryService.cs tests/MechabellumModManager.Tests/*.cs
git commit -m "feat: import MelonLoader packages into external library"
```

---

### Task 4: ProfileService

**Files:**
- Create: `src/MechabellumModManager/Services/ProfileService.cs`
- Create: `tests/MechabellumModManager.Tests/ProfileServiceTests.cs`

**Interfaces:**
- Consumes: `PathsService`, `JsonStore`, `AppConfig`
- Produces:
  - `EnsureDefaults()` creates profile `default` named `默认` if missing
  - `IReadOnlyList<Profile> List()`
  - `Profile Get(string id)`
  - `Profile Create(string name)`
  - `void Rename(string id, string name)`
  - `Profile Duplicate(string id, string newName)`
  - `void Delete(string id)` — cannot delete last profile; if deleting active, switch to another
  - `void SetEnabled(string profileId, string packageId, bool enabled)` — immediate save
  - `void RemovePackageFromAllProfiles(string packageId)`

- [ ] **Step 1: Failing tests for create / toggle / remove from all**

```csharp
[Fact]
public void SetEnabled_persists_immediately()
{
    var data = Path.Combine(Path.GetTempPath(), "mmm-prof-" + Guid.NewGuid().ToString("N"));
    var paths = new PathsService(data);
    paths.EnsureCreated();
    var store = new JsonStore();
    var svc = new ProfileService(paths, store);
    svc.EnsureDefaults();
    var p = svc.List().Single();
    svc.SetEnabled(p.Id, "pkg1", true);
    var again = new ProfileService(paths, store).Get(p.Id);
    again.EnabledPackageIds.Should().Contain("pkg1");
    Directory.Delete(data, true);
}
```

- [ ] **Step 2: Implement + test PASS + commit**

```powershell
git commit -m "feat: profile CRUD and immediate enable toggles"
```

---

### Task 5: DeployPlanner (pure logic)

**Files:**
- Create: `src/MechabellumModManager/Services/DeployPlanner.cs`
- Create: `tests/MechabellumModManager.Tests/DeployPlannerTests.cs`

**Interfaces:**
- Consumes: `ModPackage`, `Profile`, `DeployManifest`, `DeployPlan`
- Produces: `DeployPlanner.Build(...) -> DeployPlan`

```csharp
public sealed class DeployPlanner
{
    public DeployPlan Build(
        string gamePath,
        Profile profile,
        IReadOnlyDictionary<string, ModPackage> packagesById,
        DeployManifest? existingManifest,
        bool allowOverwriteUnmanaged);
}
```

Mapping:
- MelonMod → `Mods/{fileName}`
- MelonPlugin → `Plugins/{fileName}`
- MelonUserLibs → `UserLibs/{fileName}`
- MelonUserData → `UserData/{relativePathInPackage}` — **reject** if relative is `Loader.cfg` (case-insensitive)

Logic:
1. If `existingManifest != null` && !PathsEqual(manifest.GamePath, gamePath) → `ManifestInvalidDueToGamePath=true`, treat existing as empty for deletes (no deletes from old path)
2. Desired targets from enabled packages (skip missing package ids); build map relativePath → (packageId, source, sha)
3. If two packages map to same relativePath → add to `IntraProfileNameCollisions` and return plan with empty execute lists (caller aborts)
4. Deletes = manifest files whose relativePath not in desired
5. For each desired: if dest exists and not in manifest and not allowOverwriteUnmanaged → ConflictsUnmanaged
6. Copies = all desired (even if hash matches — simpler OK; optional skip same hash)

- [ ] **Step 1: Tests**

```csharp
[Fact]
public void Flatten_mod_dll_to_Mods_root()
{
    var pkg = new ModPackage
    {
        Id = "qc",
        Type = ModPackageType.MelonMod,
        PackageDirectory = @"C:\lib\mods\qc",
        Files = { new DeployableFile { RelativePathInPackage = "QuickCamera.dll", Sha256 = "a" } }
    };
    var profile = new Profile { Id = "p", EnabledPackageIds = { "qc" } };
    var plan = new DeployPlanner().Build(@"G:\Game", profile, new Dictionary<string, ModPackage> { ["qc"] = pkg }, null, false);
    plan.Copies.Single().RelativeGamePath.Replace('\\','/').Should().Be("Mods/QuickCamera.dll");
}

[Fact]
public void Does_not_delete_unmanaged_files()
{
    // manifest empty; game has Mods/Other.dll unmanaged — Deletes must not include Other.dll
}

[Fact]
public void Deletes_only_manifest_entries_not_in_new_profile()
{
    var manifest = new DeployManifest
    {
        GamePath = @"G:\Game",
        ProfileId = "old",
        Files = { new ManifestFileEntry { RelativePath = "Mods/Old.dll", PackageId = "old", Sha256 = "1" } }
    };
    var plan = new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p" }, new Dictionary<string, ModPackage>(), manifest, false);
    plan.Deletes.Should().Contain(d => d.EndsWith("Old.dll", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void Rejects_Loader_cfg_in_userdata_package()
{
    var pkg = new ModPackage
    {
        Id = "u",
        Type = ModPackageType.MelonUserData,
        PackageDirectory = @"C:\lib\userdata\u",
        Files = { new DeployableFile { RelativePathInPackage = "Loader.cfg", Sha256 = "x" } }
    };
    var act = () => new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p", EnabledPackageIds = { "u" } },
        new Dictionary<string, ModPackage> { ["u"] = pkg }, null, false);
    act.Should().Throw<InvalidOperationException>();
}

[Fact]
public void GamePath_mismatch_skips_deletes()
{
    var manifest = new DeployManifest
    {
        GamePath = @"G:\Old",
        Files = { new ManifestFileEntry { RelativePath = "Mods/A.dll", PackageId = "a", Sha256 = "1" } }
    };
    var plan = new DeployPlanner().Build(@"G:\New", new Profile { Id = "p" }, new Dictionary<string, ModPackage>(), manifest, false);
    plan.ManifestInvalidDueToGamePath.Should().BeTrue();
    plan.Deletes.Should().BeEmpty();
}

[Fact]
public void Intra_profile_name_collision_detected()
{
    var a = new ModPackage { Id = "a", Type = ModPackageType.MelonMod, PackageDirectory = @"C:\a", Files = { new DeployableFile { RelativePathInPackage = "Same.dll", Sha256 = "1" } } };
    var b = new ModPackage { Id = "b", Type = ModPackageType.MelonMod, PackageDirectory = @"C:\b", Files = { new DeployableFile { RelativePathInPackage = "Same.dll", Sha256 = "2" } } };
    var plan = new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p", EnabledPackageIds = { "a", "b" } },
        new Dictionary<string, ModPackage> { ["a"] = a, ["b"] = b }, null, false);
    plan.IntraProfileNameCollisions.Should().NotBeEmpty();
}
```

- [ ] **Step 2: Implement DeployPlanner until all PASS**

- [ ] **Step 3: Commit**

```powershell
git commit -m "feat: deploy plan calculation with flatten and safety rules"
```

---

### Task 6: DeployService + ProcessProbe

**Files:**
- Create: `src/MechabellumModManager/Services/ProcessProbe.cs`
- Create: `src/MechabellumModManager/Services/DeployService.cs`
- Create: `tests/MechabellumModManager.Tests/DeployServiceTests.cs`

**Interfaces:**
- Consumes: `DeployPlanner`, `PathsService`, `JsonStore`, `GameDetector`
- Produces:
  - `ProcessProbe.IsGameRunning() -> bool` (process name `Mechabellum`)
  - `DeployService.Apply(profile, packages, gamePath, allowOverwriteUnmanaged) -> DeployResult`
  - `DeployResult { bool Success; string Message; DeployPlan Plan; }`

Apply algorithm:
1. If process running → fail
2. If `GameDetector.Detect` != Ready → fail
3. Load manifest; `plan = planner.Build(...)`
4. If collisions non-empty → fail
5. If unmanaged conflicts and not allow → fail
6. Save current manifest to prev (or empty manifest)
7. Ensure dirs Mods/Plugins/UserLibs/UserData
8. Execute deletes then copies; track `writtenThisAttempt`
9. On failure: rollback per spec (prev non-empty → resync from library using prev file list; prev empty → delete writtenThisAttempt); return failure
10. On success: write new manifest with gamePath, profileId, files

Integration-style test using temp game root + temp library (create fake Ready layout with exe/ga/melon/proxy + empty Mods).

```csharp
[Fact]
public void Apply_copies_and_second_empty_profile_removes_managed_only()
{
    // arrange temp game Ready + import fake dll bytes as package
    // apply profile with pkg → Mods/X.dll exists
    // place unmanaged Mods/Hand.dll
    // apply empty profile → X gone, Hand remains
}
```

- [ ] **Step 1–4: TDD DeployService + commit**

```powershell
git commit -m "feat: execute deploy with manifest and rollback"
```

---

### Task 7: GameLauncher + RiskGate

**Files:**
- Create: `src/MechabellumModManager/Services/GameLauncher.cs`
- Create: `src/MechabellumModManager/Services/RiskGate.cs`
- Create: `tests/MechabellumModManager.Tests/RiskGateTests.cs`
- Create: `tests/MechabellumModManager.Tests/GameLauncherTests.cs` (optional; can mock Process.Start via wrapper)

**Interfaces:**
- `RiskGate.ConfirmEnableHighRisk(bool packageHighRisk) -> bool` — for unit test, inject `Func<string,bool> confirm`
- `RiskGate.BannerText` Chinese constant from spec §9
- `IProcessStarter { void StartShell(string uriOrPath); }`
- `GameLauncher.Launch(AppConfig cfg)`:
  - SteamThenExe: try `steam://rungameid/669330`, on failure start `Path.Combine(gamePath, "Mechabellum.exe")`
  - If already running → throw/return friendly message

```csharp
public sealed class RiskGate
{
    public const string BannerText =
        "本工具仅用于客户端 QoL Mod。修改战斗逻辑可能导致 Data Error 与处罚；官方未支持 Mod，风险自负。";

    public bool CanEnable(bool highRisk, Func<string, bool> confirm)
    {
        if (!highRisk) return true;
        return confirm("该条目被标记为高风险，确定加入当前方案吗？");
    }
}
```

- [ ] **Step 1: RiskGate tests PASS**
- [ ] **Step 2: GameLauncher with `IProcessStarter` fake — verify Steam URI first**
- [ ] **Step 3: Commit**

```powershell
git commit -m "feat: Steam/exe launcher and risk banner gate"
```

---

### Task 8: MainViewModel wiring

**Files:**
- Create: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Create: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- Create: `src/MechabellumModManager/ViewModels/ProfileItemViewModel.cs`
- Create: `tests/MechabellumModManager.Tests/MainViewModelTests.cs` (smoke: toggle marks dirty; apply clears dirty when fake deploy succeeds)

**Interfaces:**
- Commands: `RefreshStatus`, `BrowseGamePath`, `ImportDll`, `ImportZip`, `ApplyProfile`, `ApplyAndLaunch`, `CreateProfile`, `DeleteProfile`, `SelectProfile`, `ToggleHighRisk`
- Properties: `GameStatus`, `Profiles`, `Mods`, `SelectedProfile`, `LogText`, `IsDirty`, `RiskBanner`, `LoaderVersionWarning` (non-empty when package RequiredMelonLoaderVersion mismatches detected loader version)
- On enable checkbox: call `RiskGate` then `ProfileService.SetEnabled`; set `IsDirty = true`
- `ApplyProfile`: call `DeployService.Apply`; update dirty from comparing manifest profileId+files vs desired
- Settings-bound: `GamePath`, `LaunchMode`, optional `UsePortableDataRoot` (when true, `PathsService` root = `Path.Combine(AppContext.BaseDirectory, "data")` and persist in config)

- [ ] **Step 1: Write one ViewModel smoke test with temp PathsService**
- [ ] **Step 2: Implement ViewModel**
- [ ] **Step 3: Commit**

```powershell
git commit -m "feat: main ViewModel for library, profiles, apply flow"
```

---

### Task 9: WPF MainWindow UI

**Files:**
- Modify: `src/MechabellumModManager/App.xaml.cs` — compose services, set `MainWindow.DataContext`
- Modify: `src/MechabellumModManager/MainWindow.xaml` — layout per spec §6
- Modify: `src/MechabellumModManager/MainWindow.xaml.cs` — file dialogs hooks if not in VM

UI checklist:
- Top: status text, profile combo, Apply, Apply+Launch, Settings (game path, launch mode, portable data root)
- Left: profile list + new/rename/duplicate/delete
- Center: mod list with type, checkbox, high-risk, import buttons, version warning column/tooltip
- Bottom: log + risk banner always visible
- Disable Apply/Launch when status != Ready

Chinese labels throughout.

- [ ] **Step 1: Build UI XAML binding to MainViewModel**
- [ ] **Step 2: `dotnet build` — expect success**
- [ ] **Step 3: Manual run**

```powershell
dotnet run --project src/MechabellumModManager
```

Manual script:
1. Point game path to real Mechabellum (or temp Ready stub)
2. Import `_samples/QuickCamera/QuickCamera.dll`
3. Enable in profile → Apply → confirm `{Game}\Mods\QuickCamera.dll` exists as file in root
4. Create second empty profile → switch → Apply → dll removed if managed
5. Without MelonLoader → Apply disabled/fails with message

- [ ] **Step 4: Commit**

```powershell
git commit -m "feat: WPF UI for Mechabellum mod manager"
```

---

### Task 10: Spec acceptance checklist + docs touch-up

**Files:**
- Modify: `docs/superpowers/specs/2026-09-02-mechabellum-mod-manager-design.md` — set status to `已实现计划就绪/实现中` only if desired; optional
- Create: `README.md` at repo root — how to build/run, MelonLoader prerequisite, risk note

- [ ] **Step 1: Run full test suite**

```powershell
cd "<repo-root>"
dotnet test
```

Expected: all PASS

- [ ] **Step 2: Walk success criteria §12 of spec manually; note results in commit message or README Testing section**

- [ ] **Step 3: Commit README**

```powershell
git add README.md
git commit -m "docs: add build/run instructions and risk notice"
```

---

## Spec coverage map

| Spec section | Task(s) |
|---|---|
| Game detection + Loader Ready | Task 2 |
| Types Mod/Plugin/UserLibs/UserData | Tasks 3, 5 |
| External library + import zip/dll | Task 3 |
| Profiles + checkbox vs apply | Tasks 4, 8 |
| Flatten deploy + manifest + conflicts + Loader.cfg | Tasks 5–6 |
| Rollback | Task 6 |
| Launch Steam/exe | Task 7 |
| Risk banner / high-risk confirm | Tasks 7–9 |
| WPF UI Chinese | Task 9 |
| Success criteria | Task 10 |

## Plan self-review notes

- No TBD placeholders in task steps
- `package.json` naming consistent with spec
- UserLibs are separate packages (Task 3 mixed zip)
- DeployPlanner vs DeployService split keeps TDD sharp
- Mono.Cecil avoids needing MelonLoader in the manager process

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-02-mechabellum-mod-manager.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — execute tasks in this session with checkpoints  

Which approach?
