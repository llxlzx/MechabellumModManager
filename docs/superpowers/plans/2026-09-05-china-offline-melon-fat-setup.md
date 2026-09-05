# China Offline Melon Fat Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fat Setup seeds the exact Melon `UnityDependencies_{version}.zip` into the game folder so first Il2Cpp generation works offline (success level B), without locking overseas players into wrong offline mode.

**Architecture:** Add a small Unity version normalizer/resolver and a `UnityDependenciesSeeder` that copies matching zips from `installer-redist\unity-deps` (or repo `installer\redist\unity-deps`). Call the seeder from every Melon install/ensure path, then tighten `MelonLoaderConfigOptimizer.CanForceOfflineGeneration` to require an exact version match. Harden `build-installer` to require Melon + unity-deps + dotnet8 offline packages.

**Tech Stack:** C# / .NET 8 WPF, xUnit + FluentAssertions, PowerShell installer scripts, Inno Setup.

## Global Constraints

- Success level **B only** (no workshop/CDN mirror work in this plan).
- Exact zip name: `UnityDependencies_{major}.{minor}.{patch}.zip` (strip trailing `fN` from Unity strings).
- Never force offline on a non-matching zip.
- Do not commit large redist binaries to git; only enforce their presence at release build time.
- Keep changes focused; reuse existing Melon install / dual-store patterns.
- Tests first (TDD) for resolver, seeder, and optimizer offline rules.

## File map

| File | Responsibility |
|------|----------------|
| `src/.../Services/UnityVersionNormalizer.cs` | Parse/normalize Unity version strings |
| `src/.../Services/UnityVersionResolver.cs` | Resolve version from game root (scan known data files) |
| `src/.../Services/UnityDependenciesSeeder.cs` | Find redist zip + copy into Melon AG folder |
| `src/.../Services/MelonLoaderConfigOptimizer.cs` | Exact-match `CanForceOfflineGeneration` |
| `src/.../Services/MelonLoaderDualStoreSync.cs` | Seed after zip install / sibling copy |
| `src/.../Services/InstallMelonLoaderCli.cs` | Pass redist dir into seed |
| `src/.../Services/MelonLoaderInstaller.cs` | Seed if this path still installs Melon |
| `installer/scripts/Install-MelonLoader.ps1` | Seed + exact-match offline flag |
| `installer/build-installer.ps1` / `.bat` | Hard-check unity-deps + dotnet8 |
| `installer/redist/README.md` | Document unity-deps + rename rule |
| `tests/.../UnityVersionNormalizerTests.cs` | Normalization cases |
| `tests/.../UnityDependenciesSeederTests.cs` | Seed / skip / missing |
| `tests/.../MelonLoaderConfigOptimizerTests.cs` | Exact offline rules |

---

### Task 1: Unity version normalizer

**Files:**
- Create: `src/MechabellumModManager/Services/UnityVersionNormalizer.cs`
- Test: `tests/MechabellumModManager.Tests/UnityVersionNormalizerTests.cs`

**Interfaces:**
- Produces: `UnityVersionNormalizer.TryNormalize(string? raw, out string? majorMinorPatch)` → `bool`; also `ExpectedZipFileName(string majorMinorPatch)` → `string`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MechabellumModManager.Services;

public class UnityVersionNormalizerTests
{
    [Theory]
    [InlineData("2022.3.62f3", "2022.3.62")]
    [InlineData("2022.3.62", "2022.3.62")]
    [InlineData(" 2022.3.62f1 ", "2022.3.62")]
    public void TryNormalize_strips_suffix(string raw, string expected)
    {
        UnityVersionNormalizer.TryNormalize(raw, out var v).Should().BeTrue();
        v.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("2022.3")]
    public void TryNormalize_rejects_invalid(string? raw)
    {
        UnityVersionNormalizer.TryNormalize(raw, out var v).Should().BeFalse();
        v.Should().BeNull();
    }

    [Fact]
    public void ExpectedZipFileName_matches_Melon_convention()
    {
        UnityVersionNormalizer.ExpectedZipFileName("2022.3.62")
            .Should().Be("UnityDependencies_2022.3.62.zip");
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UnityVersionNormalizerTests"`

Expected: FAIL (type not found)

- [ ] **Step 3: Minimal implementation**

```csharp
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public static class UnityVersionNormalizer
{
    static readonly Regex VersionRx = new(
        @"^\s*(?<maj>\d+)\.(?<min>\d+)\.(?<patch>\d+)([a-zA-Z]\d+)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryNormalize(string? raw, out string? majorMinorPatch)
    {
        majorMinorPatch = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var m = VersionRx.Match(raw);
        if (!m.Success) return false;
        majorMinorPatch = $"{m.Groups["maj"].Value}.{m.Groups["min"].Value}.{m.Groups["patch"].Value}";
        return true;
    }

    public static string ExpectedZipFileName(string majorMinorPatch) =>
        $"UnityDependencies_{majorMinorPatch}.zip";
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/UnityVersionNormalizer.cs tests/MechabellumModManager.Tests/UnityVersionNormalizerTests.cs
git commit -m "feat: add Unity version normalizer for Melon dependency zips"
```

---

### Task 2: Unity version resolver (game folder)

**Files:**
- Create: `src/MechabellumModManager/Services/UnityVersionResolver.cs`
- Test: `tests/MechabellumModManager.Tests/UnityVersionResolverTests.cs`

**Interfaces:**
- Consumes: `UnityVersionNormalizer.TryNormalize`
- Produces: `UnityVersionResolver.TryResolve(string gamePath, out string? majorMinorPatch)` → `bool`

Strategy (keep simple, no Unity API):
1. Prefer `Mechabellum_Data\globalgamemanagers` if present; else any `*_Data\globalgamemanagers`.
2. Read file as bytes; search ASCII/UTF8 for pattern `\d+\.\d+\.\d+[a-zA-Z]\d+` (first plausible Unity editor version near typical markers is OK — take first match that normalizes and looks like 20xx).
3. Optional second source: if Melon `MelonLoader\Config.cfg` or console-less metadata exists with a version field, use only if primary fails (skip if not already present in codebase — YAGNI unless needed for tests).

- [ ] **Step 1: Write failing tests** with a temp game root containing a tiny fake `Mechabellum_Data\globalgamemanagers` file whose bytes include the ASCII string `2022.3.62f3`.

```csharp
[Fact]
public void TryResolve_reads_version_from_globalgamemanagers()
{
    var root = Path.Combine(Path.GetTempPath(), "mmm-uv-" + Guid.NewGuid().ToString("N"));
    try
    {
        var data = Path.Combine(root, "Mechabellum_Data");
        Directory.CreateDirectory(data);
        // Minimal payload containing a Unity version string Melon would report
        File.WriteAllBytes(Path.Combine(data, "globalgamemanagers"),
            System.Text.Encoding.ASCII.GetBytes("xxxx2022.3.62f3yyyy"));

        new UnityVersionResolver().TryResolve(root, out var v).Should().BeTrue();
        v.Should().Be("2022.3.62");
    }
    finally { Directory.Delete(root, true); }
}

[Fact]
public void TryResolve_false_when_missing()
{
    var root = Path.Combine(Path.GetTempPath(), "mmm-uv-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(root);
        new UnityVersionResolver().TryResolve(root, out var v).Should().BeFalse();
        v.Should().BeNull();
    }
    finally { Directory.Delete(root, true); }
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement resolver** — scan up to first ~4MB of `globalgamemanagers` for regex `(20\d{2}\.\d+\.\d+[a-zA-Z]\d+)`; normalize first match.

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: resolve Unity version from game globalgamemanagers"
```

---

### Task 3: UnityDependenciesSeeder

**Files:**
- Create: `src/MechabellumModManager/Services/UnityDependenciesSeeder.cs`
- Test: `tests/MechabellumModManager.Tests/UnityDependenciesSeederTests.cs`

**Interfaces:**
- Consumes: `UnityVersionNormalizer`, `UnityVersionResolver`
- Produces:

```csharp
public sealed class UnityDependenciesSeedResult
{
    public bool Success { get; init; }           // true if matching zip is present after call
    public bool Copied { get; init; }
    public string? Version { get; init; }
    public string Message { get; init; } = "";
}

public sealed class UnityDependenciesSeeder
{
    public UnityDependenciesSeedResult Seed(string gamePath, string? redistDir = null, string? versionOverride = null);
    public static string? ResolveUnityDepsRedistDir(string? preferredRedistDir = null);
    public static string? FindMatchingZipInRedist(string unityDepsDir, string majorMinorPatch);
}
```

Redist lookup order for zip directory:
1. `{redistDir}/unity-deps`
2. `{AppBase}/installer-redist/unity-deps`
3. `{AppBase}/redist/unity-deps`
4. Walk parents for `installer/redist/unity-deps` (dev)

Fallback when `TryResolve` fails: if redist folder has **exactly one** `UnityDependencies_*.zip`, parse version from filename `UnityDependencies_(.+)\.zip`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Seed_copies_matching_zip_into_Il2CppAssemblyGenerator()
{
    var game = CreateGameRoot();
    var redist = CreateRedistWithZip("2022.3.62");
    WriteFakeGlobalgamemanagers(game, "2022.3.62f3");

    var result = new UnityDependenciesSeeder().Seed(game, redist);
    result.Success.Should().BeTrue();
    result.Copied.Should().BeTrue();
    File.Exists(Path.Combine(game, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator",
        "UnityDependencies_2022.3.62.zip")).Should().BeTrue();
}

[Fact]
public void Seed_skips_when_already_present()
{
    // pre-create destination zip; expect Copied=false, Success=true
}

[Fact]
public void Seed_fails_when_redist_missing_match()
{
    var game = CreateGameRoot();
    WriteFakeGlobalgamemanagers(game, "2022.3.62f3");
    var redist = CreateEmptyUnityDepsRedist();
    var result = new UnityDependenciesSeeder().Seed(game, redist);
    result.Success.Should().BeFalse();
}

[Fact]
public void Seed_does_not_treat_wrong_version_zip_as_success()
{
    var game = CreateGameRoot();
    WriteFakeGlobalgamemanagers(game, "2022.3.63f1");
    // only 62 in redist and only 62 already in game AG folder
    // Success must be false for 63
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement seeder** (create destination dirs; `File.Copy` overwrite false if same length optional — prefer overwrite if source newer/different length)

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: seed Melon UnityDependencies zip from installer redist"
```

---

### Task 4: Exact-match force_offline in optimizer

**Files:**
- Modify: `src/MechabellumModManager/Services/MelonLoaderConfigOptimizer.cs`
- Modify: `tests/MechabellumModManager.Tests/MelonLoaderConfigOptimizerTests.cs`
- Modify: `installer/scripts/Install-MelonLoader.ps1` (`Test-CanForceOfflineGeneration`)

**Interfaces:**
- Change `CanForceOfflineGeneration(string gamePath)` to require exact zip for resolved version (inject/use `UnityVersionResolver`).
- Optional overload `CanForceOfflineGeneration(string gamePath, string? resolvedVersion)` for tests.

Rules:
1. Assemblies present → true  
2. Else resolve version; if fail → false (unless exact zip path passed in tests)  
3. Else true only if `UnityDependencies_{version}.zip` exists under AG folder  

Update tests that currently expect offline=true when assemblies missing and no zip — they must expect **false**. Keep tests with assemblies or exact zip expecting true. Add test: wrong zip version ⇒ false / cfg written false.

- [ ] **Step 1: Update/add failing tests** reflecting exact-match rules (TDD: change expectations first, run FAIL on old wildcard impl)

- [ ] **Step 2: Implement optimizer + PS1 parity**

PS1: resolve version via same regex on `Mechabellum_Data\globalgamemanagers` if feasible; else require exact filename from env/redist single zip. Prefer calling into the app path for Melon install (CLI already preferred); PS1 path still must not use wildcard.

- [ ] **Step 3: `dotnet test` filter MelonLoaderConfigOptimizer + Unity* — all PASS**

- [ ] **Step 4: Commit**

```bash
git commit -m "fix: force Melon offline generation only with exact UnityDependencies zip"
```

---

### Task 5: Wire seeder into all Melon install paths

**Files:**
- Modify: `src/MechabellumModManager/Services/MelonLoaderDualStoreSync.cs` — after successful `InstallFromZip` and after `CopyFrameworkFromSibling`, call seeder (pass `localZipPath`’s parent redist dir when available: zip at `...\melonloader\MelonLoader.x64.zip` ⇒ redist root is parent of `melonloader`)
- Modify: `src/MechabellumModManager/Services/InstallMelonLoaderCli.cs` — after install, `Seed(gamePath, redistDir)`; include seed message in log; if Melon OK but seed failed, still return 0 only if Melon detect OK **but** log loud warning (Setup should have redist; don’t fail entire install if Melon present — surface in Message). Prefer: exit 0 when Melon ready; seed failure appended to log for Case B repair via re-run.
- Modify: `src/MechabellumModManager/Services/MelonLoaderInstaller.cs` if it installs independently
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` `InstallMelonLoaderAsync` — append seed result to user-visible log line
- Modify: `installer/scripts/Install-MelonLoader.ps1` — after extract, copy matching zip from `$RedistDir\unity-deps`

**Interfaces:**
- Consumes: `UnityDependenciesSeeder.Seed`
- Dual-store both targets must be seeded in `EnsureOnBothStores`

- [ ] **Step 1: Add/adjust unit tests** around DualStoreSync with temp redist + fake game (may need to extract a tiny fake Melon zip or stub — prefer testing seeder call via DualStoreSync InstallFromZip with minimal zip that satisfies `MelonLoaderInstaller.CopyExtractedPayloadForSync` / detector; if too heavy, test a package-internal helper `TrySeedAfterInstall(gamePath, zipPath)` extracted for testability)

Minimal approach if full zip install is heavy: add internal/public method on DualStoreSync:

```csharp
public UnityDependenciesSeedResult SeedDependencies(string gamePath, string? redistDir = null)
    => new UnityDependenciesSeeder().Seed(gamePath, redistDir);
```

and call it from InstallFromZip; unit-test SeedDependencies wiring separately already covered by Task 3 — for Task 5 add one integration-style test that InstallFromZip invokes seed when redist provided (optional if hard).

- [ ] **Step 2: Implement wiring**

- [ ] **Step 3: Run full test suite**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`  
Expected: all PASS

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: seed UnityDependencies on every Melon install path"
```

---

### Task 6: Build gate + redist docs

**Files:**
- Modify: `installer/build-installer.ps1`
- Modify: `installer/build-installer.bat` (mirror checks or call ps1 only)
- Modify: `installer/redist/README.md`
- Create empty: `installer/redist/unity-deps/.gitkeep`
- Ensure `.gitignore` ignores `installer/redist/unity-deps/*.zip` (and keeps `.gitkeep`)

Build checks (when not `-SkipMelonRedistCheck`):
1. Melon zip present (existing)
2. At least one file matching `installer/redist/unity-deps/UnityDependencies_*.zip` with size > 0
3. At least one `installer/redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` with size > 0

Allow `-SkipMelonRedistCheck` to skip all three for local debug (rename switch later only if needed; keep name for less churn, document it skips all release redist checks).

- [ ] **Step 1: Update README** with rename rule + Unity-Runtime-Libraries URL  
- [ ] **Step 2: Add checks to `build-installer.ps1`**  
- [ ] **Step 3: Dry-run check logic** — without zips, script exits non-zero; with Skip switch, continues  
- [ ] **Step 4: Commit**

```bash
git commit -m "build: require UnityDependencies and .NET 8 redist for release Setup"
```

---

### Task 7: Manual acceptance notes + residual Cpp2IL probe

**Files:**
- Modify: `docs/superpowers/specs/2026-09-05-china-offline-melon-fat-setup-design.md` — check off what automated tests cover; leave A–F as manual
- Modify: `docs/releasing.md` or `docs/GitHub-Release更新说明.md` — one short section: place `UnityDependencies_*.zip`, domestic Setup mirror, offline B expectation

- [x] **Step 1: Document release steps for obtaining the zip** (download from `https://github.com/LavaGang/Unity-Runtime-Libraries`, rename, place under `unity-deps/`)
- [ ] **Step 2: On a machine with game installed: place redist files; run Install Melon; disconnect network; launch game once; confirm generation  
- [ ] **Step 3: If Melon requests another missing package under AG folder, add to redist + build gate before release  
- [x] **Step 4: Commit docs only**

```bash
git commit -m "docs: release notes for China-offline Melon fat Setup"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Exact offline match | Task 4 |
| Seed all Melon paths | Task 5 |
| Redist layout + rename | Task 3, 6 |
| Version resolve + normalize | Task 1–2 |
| Build hard-fail Melon + unity-deps + dotnet8 | Task 6 |
| Overseas not locked on wrong zip | Task 4 tests |
| Dual-store both seeded | Task 5 |
| Acceptance F / Cpp2IL residual | Task 7 |
| No workshop/CDN scope creep | Global constraints |

No TBD placeholders remain in tasks. Types aligned: `UnityDependenciesSeedResult`, `Seed(gamePath, redistDir)`, `ExpectedZipFileName`.
