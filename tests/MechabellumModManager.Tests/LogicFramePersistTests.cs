using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class LogicFramePersistTests
{
    [Fact]
    public void ImportDll_bool_ReduceLife_prefix_is_high_sim_replace_and_not_high_risk()
    {
        var data = TempDir();
        var dll = WriteHighDll();
        try
        {
            var lib = CreateLibrary(data);
            var pkg = lib.ImportDll(dll, ModPackageType.MelonMod);

            pkg.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.High));
            pkg.LogicFrameReason.Should().Be("sim-replace");
            pkg.HighRisk.Should().BeFalse();
            RiskHeuristic.DetectionEnabled.Should().BeFalse();

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.High));
            loaded.LogicFrameReason.Should().Be("sim-replace");
            loaded.HighRisk.Should().BeFalse();
        }
        finally
        {
            TryDelete(data);
            TryDeleteFile(dll);
        }
    }

    [Fact]
    public void SavePackageMeta_round_trips_high_sim_replace()
    {
        var data = TempDir();
        try
        {
            var paths = new PathsService(data);
            paths.EnsureCreated();
            var store = new JsonStore();
            var lib = CreateLibrary(paths, store);
            var pkgId = "frame-aaaaaaaa";
            SeedPackage(paths, store, pkgId, "Frame", includeGrade: false, catalogId: "show-grid");

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.LogicFrameGrade = nameof(LogicFrameGrade.High);
            loaded.LogicFrameReason = "sim-replace";
            lib.SavePackageMeta(loaded);

            var again = lib.List().Should().ContainSingle().Subject;
            again.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.High));
            again.LogicFrameReason.Should().Be("sim-replace");
            again.CatalogId.Should().Be("show-grid");
            again.HighRisk.Should().BeFalse();
        }
        finally
        {
            TryDelete(data);
        }
    }

    [Fact]
    public void Package_json_without_grade_fields_loads_unchecked_unreadable()
    {
        var data = TempDir();
        try
        {
            var paths = new PathsService(data);
            paths.EnsureCreated();
            var store = new JsonStore();
            var lib = CreateLibrary(paths, store);
            SeedPackage(paths, store, "old-aaaaaaaa", "Old", includeGrade: false, catalogId: null);

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.Unchecked));
            loaded.LogicFrameReason.Should().Be("unreadable");
        }
        finally
        {
            TryDelete(data);
        }
    }

    [Fact]
    public void Reload_rescans_package_dll_and_keeps_catalog_id_and_enabled_state()
    {
        using var fx = Fixture.CreateReady();
        var dll = WriteHighDll();
        try
        {
            const string pkgId = "cam-aaaaaaaa";
            var pkgDir = Path.Combine(fx.Paths.LibraryRoot, "mods", pkgId);
            File.Copy(dll, Path.Combine(pkgDir, "Cam.dll"), overwrite: true);
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                PackageJson(pkgId, "Cam", "Cam.dll", includeGrade: true, grade: "Unchecked", reason: "unreadable", catalogId: "show-grid"));

            fx.Profiles.SetEnabled("default", pkgId, true);
            var confirms = 0;
            var vm = fx.CreateVm(confirmHighRisk: _ =>
            {
                confirms++;
                return true;
            });

            var loaded = fx.Library.List().Should().ContainSingle(p => p.Id == pkgId).Subject;
            loaded.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.High));
            loaded.LogicFrameReason.Should().Be("sim-replace");
            loaded.HighRisk.Should().BeFalse();
            loaded.CatalogId.Should().Be("show-grid");

            var json = File.ReadAllText(Path.Combine(pkgDir, "package.json"));
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("logicFrameGrade").GetString().Should().Be(nameof(LogicFrameGrade.High));
            doc.RootElement.GetProperty("logicFrameReason").GetString().Should().Be("sim-replace");
            doc.RootElement.GetProperty("catalogId").GetString().Should().Be("show-grid");

            fx.Profiles.Get("default").EnabledPackageIds.Should().Contain(pkgId);
            vm.Mods.Should().Contain(m => m.Package.Id == pkgId && m.IsEnabled);
            confirms.Should().Be(0);
        }
        finally
        {
            TryDeleteFile(dll);
        }
    }

    [Fact]
    public void Reload_without_dll_becomes_low_none()
    {
        using var fx = Fixture.CreateReady();
        const string emptyId = "nodll-bbbbbbbb";
        var emptyDir = Path.Combine(fx.Paths.LibraryRoot, "mods", emptyId);
        Directory.CreateDirectory(emptyDir);
        File.WriteAllText(
            Path.Combine(emptyDir, "package.json"),
            PackageJson(emptyId, "NoDll", "missing.dll", includeGrade: true, grade: "Unchecked", reason: "unreadable", catalogId: null));
        fx.Store.Save(
            Path.Combine(fx.Paths.LibraryRoot, "index.json"),
            new { packageIds = new[] { "cam-aaaaaaaa", emptyId } });

        fx.CreateVm(confirmHighRisk: _ => true);

        var empty = fx.Library.List().Should().ContainSingle(p => p.Id == emptyId).Subject;
        empty.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.Low));
        empty.LogicFrameReason.Should().Be("none");
        empty.HighRisk.Should().BeFalse();
    }

    static ModLibraryService CreateLibrary(string data)
    {
        var paths = new PathsService(data);
        paths.EnsureCreated();
        return CreateLibrary(paths, new JsonStore());
    }

    static ModLibraryService CreateLibrary(PathsService paths, JsonStore store)
    {
        var profiles = new ProfileService(paths, store);
        profiles.EnsureDefaults();
        return new ModLibraryService(paths, new AssemblyInspector(), store, profiles);
    }

    static void SeedPackage(PathsService paths, JsonStore store, string pkgId, string displayName, bool includeGrade, string? catalogId)
    {
        var pkgDir = Path.Combine(paths.LibraryRoot, "mods", pkgId);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllBytes(Path.Combine(pkgDir, displayName + ".dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
        File.WriteAllText(
            Path.Combine(pkgDir, "package.json"),
            PackageJson(pkgId, displayName, displayName + ".dll", includeGrade, "Unchecked", "unreadable", catalogId));
        store.Save(Path.Combine(paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });
    }

    static string PackageJson(
        string pkgId,
        string displayName,
        string dllName,
        bool includeGrade,
        string grade,
        string reason,
        string? catalogId)
    {
        var gradeLines = includeGrade
            ? $"""
                  "logicFrameGrade": "{grade}",
                  "logicFrameReason": "{reason}",
              """
            : "";
        var catalogLine = catalogId is null
            ? ""
            : $"""
                  "catalogId": "{catalogId}",
              """;
        return $$"""
            {
              "id": "{{pkgId}}",
              "displayName": "{{displayName}}",
              "type": "melon_mod",
              "highRisk": false,
            {{catalogLine}}
            {{gradeLines}}
              "files": [ { "relativePathInPackage": "{{dllName}}", "sha256": "aabb" } ]
            }
            """;
    }

    static string TempDir() => Path.Combine(Path.GetTempPath(), "mmm-frame-" + Guid.NewGuid().ToString("N"));

    static void TryDelete(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    static string WriteHighDll()
    {
        var path = Path.Combine(Path.GetTempPath(), "logic-frame-" + Guid.NewGuid().ToString("N") + ".dll");
        using var module = ModuleDefinition.CreateModule("LogicFrameFixture", ModuleKind.Dll);
        AddHook(module, "FightActor", "ReduceLife", "Prefix", returnsBool: true, methodName: "ReduceLife");
        module.Write(path);
        return path;
    }

    static void AddHook(ModuleDefinition module, string targetTypeName, string targetMethodName, string hookName, bool returnsBool, string methodName)
    {
        var target = new TypeDefinition("Sim", targetTypeName, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var gameMethod = new MethodDefinition(targetMethodName, MethodAttributes.Public, module.TypeSystem.Void);
        gameMethod.Body.GetILProcessor().Emit(OpCodes.Ret);
        target.Methods.Add(gameMethod);
        module.Types.Add(target);

        var outer = new TypeDefinition("", "Mod", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var nested = new TypeDefinition("", "Hooks", TypeAttributes.NestedPublic | TypeAttributes.Class, module.TypeSystem.Object);
        outer.NestedTypes.Add(nested);
        module.Types.Add(outer);

        var hook = new MethodDefinition(hookName, MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Boolean);
        var il = hook.Body.GetILProcessor();
        if (returnsBool)
            il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        nested.Methods.Add(hook);

        var attr = new TypeDefinition("", "HarmonyPatchAttribute", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        ctor.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        ctor.Body.GetILProcessor().Emit(OpCodes.Ret);
        attr.Methods.Add(ctor);
        module.Types.Add(attr);

        var attribute = new CustomAttribute(ctor);
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(typeof(Type)), target));
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, methodName));
        hook.CustomAttributes.Add(attribute);
    }
}
