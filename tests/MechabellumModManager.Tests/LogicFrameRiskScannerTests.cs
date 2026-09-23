using FluentAssertions;
using MechabellumModManager.Services;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class LogicFrameRiskScannerTests
{
    [Fact]
    public void ReduceLife_void_prefix_is_medium_sim_observe()
    {
        var scan = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: false, attributeOnMethod: true, methodName: "ReduceLife"));
        scan.Grade.Should().Be(LogicFrameGrade.Medium);
        scan.ReasonCode.Should().Be("sim-observe");
    }

    [Fact]
    public void ReduceLife_bool_prefix_is_high_sim_replace()
    {
        var scan = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "ReduceLife"));
        scan.Grade.Should().Be(LogicFrameGrade.High);
        scan.ReasonCode.Should().Be("sim-replace");
    }

    [Fact]
    public void ReduceLife_void_postfix_stays_medium_sim_observe()
    {
        var scan = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Postfix", returnsBool: false, attributeOnMethod: true, methodName: "ReduceLife"));
        scan.Grade.Should().Be(LogicFrameGrade.Medium);
        scan.ReasonCode.Should().Be("sim-observe");
    }

    [Fact]
    public void GetMaxStateTime_void_postfix_is_high_sim_replace()
    {
        var scan = Scan(m => AddHook(m, "ProcessController", "GetMaxStateTime", "Postfix", returnsBool: false, attributeOnMethod: true, methodName: "GetMaxStateTime"));
        scan.Grade.Should().Be(LogicFrameGrade.High);
        scan.ReasonCode.Should().Be("sim-replace");
    }

    [Fact]
    public void OnClickUndoButton_bool_prefix_stays_low()
    {
        var scan = Scan(m => AddHook(m, "MainUI", "OnClickUndoButton", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "OnClickUndoButton"));
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void Class_level_HarmonyPatch_matches_method_level_grade()
    {
        var methodLevel = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: false, attributeOnMethod: true, methodName: "ReduceLife"));
        var classLevel = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: false, attributeOnMethod: false, methodName: "ReduceLife"));

        classLevel.Grade.Should().Be(methodLevel.Grade);
        classLevel.ReasonCode.Should().Be(methodLevel.ReasonCode);
        classLevel.Grade.Should().Be(LogicFrameGrade.Medium);
        classLevel.ReasonCode.Should().Be("sim-observe");
    }

    [Fact]
    public void Exact_Patch_call_without_HarmonyPatch_is_unchecked_manual_patch()
    {
        var scan = Scan(m => AddHarmonyCall(m, "Patch"));
        scan.Grade.Should().Be(LogicFrameGrade.Unchecked);
        scan.ReasonCode.Should().Be("manual-patch");
    }

    [Fact]
    public void PatchAll_call_without_exact_Patch_stays_low()
    {
        var scan = Scan(m => AddHarmonyCall(m, "PatchAll"));
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void Unpatch_call_without_exact_Patch_stays_low()
    {
        var scan = Scan(m => AddHarmonyCall(m, "Unpatch"));
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void HarmonyPatch_type_without_method_name_is_unchecked_manual_patch()
    {
        var scan = Scan(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: false, attributeOnMethod: true, methodName: null));
        scan.Grade.Should().Be(LogicFrameGrade.Unchecked);
        scan.ReasonCode.Should().Be("manual-patch");
    }

    [Fact]
    public void Prefix_without_HarmonyPatch_stays_low()
    {
        var scan = Scan(m => AddBarePrefix(m));
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void Empty_module_is_low()
    {
        var scan = Scan(_ => { });
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void Path_that_is_not_a_dll_is_unchecked_unreadable()
    {
        var path = TempPath(".txt");
        File.WriteAllText(path, "not a dll");
        try
        {
            var scan = LogicFrameRiskScanner.ScanFile(path);
            scan.Grade.Should().Be(LogicFrameGrade.Unchecked);
            scan.ReasonCode.Should().Be("unreadable");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ScanPaths_low_and_medium_is_medium()
    {
        var low = WriteDll(m => AddHook(m, "MainUI", "OnClickUndoButton", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "OnClickUndoButton"));
        var medium = WriteDll(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: false, attributeOnMethod: true, methodName: "ReduceLife"));
        try
        {
            var scan = LogicFrameRiskScanner.ScanPaths(new[] { low, medium });
            scan.Grade.Should().Be(LogicFrameGrade.Medium);
            scan.ReasonCode.Should().Be("sim-observe");
        }
        finally
        {
            File.Delete(low);
            File.Delete(medium);
        }
    }

    [Fact]
    public void ScanPaths_high_and_unreadable_is_high()
    {
        var unreadable = TempPath(".dll");
        File.WriteAllText(unreadable, "not an assembly");
        var high = WriteDll(m => AddHook(m, "FightActor", "ReduceLife", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "ReduceLife"));
        try
        {
            var scan = LogicFrameRiskScanner.ScanPaths(new[] { unreadable, high });
            scan.Grade.Should().Be(LogicFrameGrade.High);
            scan.ReasonCode.Should().Be("sim-replace");
        }
        finally
        {
            File.Delete(unreadable);
            File.Delete(high);
        }
    }

    [Fact]
    public void ScanPaths_of_no_paths_is_low()
    {
        var scan = LogicFrameRiskScanner.ScanPaths(Array.Empty<string>());
        scan.Grade.Should().Be(LogicFrameGrade.Low);
        scan.ReasonCode.Should().Be("none");
    }

    [Fact]
    public void ScanPaths_unreadable_dll_beats_low()
    {
        var low = WriteDll(m => AddHook(m, "MainUI", "OnClickUndoButton", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "OnClickUndoButton"));
        var unreadable = TempPath(".dll");
        File.WriteAllText(unreadable, "not an assembly");
        try
        {
            var scan = LogicFrameRiskScanner.ScanPaths(new[] { low, unreadable });
            scan.Grade.Should().Be(LogicFrameGrade.Unchecked);
            scan.ReasonCode.Should().Be("unreadable");
        }
        finally
        {
            File.Delete(low);
            File.Delete(unreadable);
        }
    }

    [Fact]
    public void ScanPaths_ignores_non_dll_paths()
    {
        var low = WriteDll(m => AddHook(m, "MainUI", "OnClickUndoButton", "Prefix", returnsBool: true, attributeOnMethod: true, methodName: "OnClickUndoButton"));
        var text = TempPath(".txt");
        File.WriteAllText(text, "not a dll");
        try
        {
            var scan = LogicFrameRiskScanner.ScanPaths(new[] { low, text });
            scan.Grade.Should().Be(LogicFrameGrade.Low);
            scan.ReasonCode.Should().Be("none");
        }
        finally
        {
            File.Delete(low);
            File.Delete(text);
        }
    }

    static LogicFrameScan Scan(Action<ModuleDefinition> build)
    {
        var path = WriteDll(build);
        try
        {
            return LogicFrameRiskScanner.ScanFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    static string WriteDll(Action<ModuleDefinition> build)
    {
        var path = TempPath(".dll");
        using var module = ModuleDefinition.CreateModule("LogicFrameFixture", ModuleKind.Dll);
        build(module);
        module.Write(path);
        return path;
    }

    static string TempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), "logic-frame-" + Guid.NewGuid().ToString("N") + extension);
    }

    static void AddHook(
        ModuleDefinition module,
        string targetTypeName,
        string targetMethodName,
        string hookName,
        bool returnsBool,
        bool attributeOnMethod,
        string? methodName)
    {
        var target = new TypeDefinition("Sim", targetTypeName, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var gameMethod = new MethodDefinition(targetMethodName, MethodAttributes.Public, module.TypeSystem.Void);
        gameMethod.Body.GetILProcessor().Emit(OpCodes.Ret);
        target.Methods.Add(gameMethod);
        module.Types.Add(target);

        var nested = AddNested(module, "Hooks");
        var hook = AddMethod(nested, hookName, returnsBool ? module.TypeSystem.Boolean : module.TypeSystem.Void, returnsBool);
        var attribute = BuildHarmonyPatch(module, target, methodName);
        if (attributeOnMethod)
            hook.CustomAttributes.Add(attribute);
        else
            nested.CustomAttributes.Add(attribute);
    }

    static void AddBarePrefix(ModuleDefinition module)
    {
        var nested = AddNested(module, "Hooks");
        AddMethod(nested, "Prefix", module.TypeSystem.Void, returnsBool: false);
    }

    static void AddHarmonyCall(ModuleDefinition module, string methodName)
    {
        var harmony = new TypeDefinition("", "Harmony", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var callee = new MethodDefinition(methodName, MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        callee.Body.GetILProcessor().Emit(OpCodes.Ret);
        harmony.Methods.Add(callee);
        module.Types.Add(harmony);

        var nested = AddNested(module, "Bootstrap");
        var run = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        var il = run.Body.GetILProcessor();
        il.Emit(OpCodes.Call, callee);
        il.Emit(OpCodes.Ret);
        nested.Methods.Add(run);
    }

    static TypeDefinition AddNested(ModuleDefinition module, string nestedName)
    {
        var outer = new TypeDefinition("", "Mod", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var nested = new TypeDefinition("", nestedName, TypeAttributes.NestedPublic | TypeAttributes.Class, module.TypeSystem.Object);
        outer.NestedTypes.Add(nested);
        module.Types.Add(outer);
        return nested;
    }

    static MethodDefinition AddMethod(TypeDefinition type, string name, TypeReference returnType, bool returnsBool)
    {
        var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static, returnType);
        var il = method.Body.GetILProcessor();
        if (returnsBool)
            il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        type.Methods.Add(method);
        return method;
    }

    static CustomAttribute BuildHarmonyPatch(ModuleDefinition module, TypeReference targetType, string? methodName)
    {
        var ctor = HarmonyPatchCtor(module, methodName != null);
        var attribute = new CustomAttribute(ctor);
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(typeof(Type)), targetType));
        if (methodName != null)
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, methodName));
        return attribute;
    }

    static MethodDefinition HarmonyPatchCtor(ModuleDefinition module, bool withMethodName)
    {
        var attr = module.Types.FirstOrDefault(t => t.Name == "HarmonyPatchAttribute");
        if (attr == null)
        {
            attr = new TypeDefinition(
                "",
                "HarmonyPatchAttribute",
                TypeAttributes.Public | TypeAttributes.Class,
                module.TypeSystem.Object);
            attr.Methods.Add(CreateCtor(module, withMethodName: false));
            attr.Methods.Add(CreateCtor(module, withMethodName: true));
            module.Types.Add(attr);
        }

        var count = withMethodName ? 2 : 1;
        return attr.Methods.First(m => m.Name == ".ctor" && m.Parameters.Count == count);
    }

    static MethodDefinition CreateCtor(ModuleDefinition module, bool withMethodName)
    {
        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        ctor.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        if (withMethodName)
            ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        ctor.Body.GetILProcessor().Emit(OpCodes.Ret);
        return ctor;
    }
}
