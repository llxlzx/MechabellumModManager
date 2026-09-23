using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MechabellumModManager.Services;

public enum LogicFrameGrade
{
    Unchecked,
    Low,
    Medium,
    High,
}

public sealed class LogicFrameScan
{
    public LogicFrameGrade Grade { get; }
    public string ReasonCode { get; }

    public LogicFrameScan(LogicFrameGrade grade, string reasonCode)
    {
        Grade = grade;
        ReasonCode = reasonCode;
    }
}

public static class LogicFrameRiskScanner
{
    public static LogicFrameScan ScanFile(string dllPath)
    {
        try
        {
            var parameters = new ReaderParameters { ReadSymbols = false, InMemory = true };
            using var module = ModuleDefinition.ReadModule(dllPath, parameters);
            return GradeModule(module);
        }
        catch (Exception)
        {
            return new LogicFrameScan(LogicFrameGrade.Unchecked, "unreadable");
        }
    }

    public static LogicFrameScan ScanPaths(IEnumerable<string> dllPaths)
    {
        LogicFrameScan? best = null;
        foreach (var path in dllPaths)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            var scan = ScanFile(path);
            if (best == null || Rank(scan.Grade) > Rank(best.Grade))
                best = scan;
        }

        return best ?? new LogicFrameScan(LogicFrameGrade.Low, "none");
    }

    static LogicFrameScan GradeModule(ModuleDefinition module)
    {
        var state = new ScanState();
        foreach (var type in module.Types)
        {
            try
            {
                Walk(type, state);
            }
            catch
            {
                // A later unreadable type must not erase a Medium or High hook already found.
                state.Unresolved = true;
            }
        }

        if (state.Best == LogicFrameGrade.High)
            return new LogicFrameScan(LogicFrameGrade.High, "sim-replace");
        if (state.Best == LogicFrameGrade.Medium)
            return new LogicFrameScan(LogicFrameGrade.Medium, "sim-observe");
        if (state.Unresolved || (state.ManualPatchCall && !state.YieldedTarget))
            return new LogicFrameScan(LogicFrameGrade.Unchecked, "manual-patch");
        return new LogicFrameScan(LogicFrameGrade.Low, "none");
    }

    static void Walk(TypeDefinition type, ScanState state)
    {
        NoteAttributes(type.CustomAttributes, state);
        foreach (var method in type.Methods)
        {
            try
            {
                NoteAttributes(method.CustomAttributes, state);
                NoteManualPatchCall(method, state);
                if (!IsHook(type, method))
                    continue;

                if (TryResolveTarget(method, type, out var typeName, out var methodName))
                    state.Raise(GradeHook(method, typeName, methodName));
                else if (HasTypeWithoutMethodName(method, type))
                    state.Unresolved = true;
            }
            catch
            {
                // One unreadable method floors the file at Unchecked unless a hook is already Medium or High.
                state.Unresolved = true;
            }
        }

        foreach (var nested in type.NestedTypes)
            Walk(nested, state);
    }

    static bool IsHook(TypeDefinition type, MethodDefinition method)
    {
        if (!IsPrefix(method) && !IsPostfix(method))
            return false;
        return HasAttribute(method.CustomAttributes, "HarmonyPatch")
            || HasAttribute(type.CustomAttributes, "HarmonyPatch");
    }

    static bool IsPrefix(MethodDefinition method)
    {
        return method.Name.Equals("Prefix", StringComparison.Ordinal)
            || HasAttribute(method.CustomAttributes, "HarmonyPrefix");
    }

    static bool IsPostfix(MethodDefinition method)
    {
        return method.Name.Equals("Postfix", StringComparison.Ordinal)
            || HasAttribute(method.CustomAttributes, "HarmonyPostfix");
    }

    static LogicFrameGrade GradeHook(MethodDefinition method, string typeName, string methodName)
    {
        var simulation = IsObserve(typeName, methodName) || IsResult(typeName, methodName);
        var returnsBool = method.ReturnType.MetadataType == MetadataType.Boolean
            || method.ReturnType.FullName.Equals("System.Boolean", StringComparison.Ordinal);

        if (IsPrefix(method) && simulation && returnsBool)
            return LogicFrameGrade.High;
        if (IsPostfix(method) && IsResult(typeName, methodName))
            return LogicFrameGrade.High;
        if (simulation)
            return LogicFrameGrade.Medium;
        return LogicFrameGrade.Low;
    }

    static bool IsObserve(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            ("FightActor", "ReduceLife") => true,
            ("DamagePerformer", "Perform") => true,
            ("Player", "AddSupply") => true,
            ("Player", "AddRoundSupply") => true,
            ("Player", "ReduceSupply") => true,
            ("Player", "SetSupply") => true,
            ("BattleStatisticManager", "OnFightEnd") => true,
            ("BattleSystem", "OnFightOver") => true,
            _ => false,
        };
    }

    static bool IsResult(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            ("ProcessController", "IsStateTimesUp") => true,
            ("ProcessController", "GetMaxStateTime") => true,
            ("ProcessController", "GetDeployTime") => true,
            _ => false,
        };
    }

    static bool TryResolveTarget(MethodDefinition method, TypeDefinition declaringType, out string typeName, out string methodName)
    {
        if (TryPair(method.CustomAttributes, out typeName, out methodName))
            return true;
        return TryPair(declaringType.CustomAttributes, out typeName, out methodName);
    }

    static bool TryPair(IEnumerable<CustomAttribute> attributes, out string typeName, out string methodName)
    {
        foreach (var attr in attributes)
        {
            if (!AttributeNameContains(attr, "HarmonyPatch"))
                continue;
            if (TryReadPair(attr, out typeName, out methodName))
                return true;
        }

        typeName = "";
        methodName = "";
        return false;
    }

    static bool TryReadPair(CustomAttribute attr, out string typeName, out string methodName)
    {
        typeName = "";
        methodName = "";
        var sawType = false;
        var sawName = false;
        foreach (var arg in attr.ConstructorArguments)
        {
            if (!sawType && arg.Value is TypeReference type && !string.IsNullOrEmpty(type.Name))
            {
                typeName = type.Name;
                sawType = true;
            }
            else if (!sawName && arg.Value is string name)
            {
                methodName = name;
                sawName = true;
            }
        }

        return sawType && sawName;
    }

    static bool HasTypeWithoutMethodName(MethodDefinition method, TypeDefinition declaringType)
    {
        return HasTypeOnlyPatch(method.CustomAttributes) || HasTypeOnlyPatch(declaringType.CustomAttributes);
    }

    static bool HasTypeOnlyPatch(IEnumerable<CustomAttribute> attributes)
    {
        foreach (var attr in attributes)
        {
            if (!AttributeNameContains(attr, "HarmonyPatch"))
                continue;
            var sawType = false;
            var sawName = false;
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Value is TypeReference)
                    sawType = true;
                else if (arg.Value is string)
                    sawName = true;
            }

            if (sawType && !sawName)
                return true;
        }

        return false;
    }

    static void NoteAttributes(IEnumerable<CustomAttribute> attributes, ScanState state)
    {
        if (TryPair(attributes, out _, out _))
            state.YieldedTarget = true;
    }

    static void NoteManualPatchCall(MethodDefinition method, ScanState state)
    {
        if (!method.HasBody)
            return;

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code is not (Code.Call or Code.Callvirt))
                continue;
            if (instruction.Operand is not MethodReference called)
                continue;
            if (!called.Name.Equals("Patch", StringComparison.Ordinal))
                continue;

            var declaringName = called.DeclaringType?.Name;
            if (declaringName != null && declaringName.Contains("Harmony", StringComparison.Ordinal))
                state.ManualPatchCall = true;
        }
    }

    static bool HasAttribute(IEnumerable<CustomAttribute> attributes, string token)
    {
        foreach (var attr in attributes)
        {
            if (AttributeNameContains(attr, token))
                return true;
        }

        return false;
    }

    static bool AttributeNameContains(CustomAttribute attr, string token)
    {
        var name = attr.AttributeType.Name;
        if (name != null && name.Contains(token, StringComparison.Ordinal))
            return true;
        var fullName = attr.AttributeType.FullName;
        return fullName != null && fullName.Contains(token, StringComparison.Ordinal);
    }

    static int Rank(LogicFrameGrade grade)
    {
        return grade switch
        {
            LogicFrameGrade.High => 4,
            LogicFrameGrade.Medium => 3,
            LogicFrameGrade.Unchecked => 2,
            LogicFrameGrade.Low => 1,
            _ => 0,
        };
    }

    sealed class ScanState
    {
        public LogicFrameGrade Best { get; private set; } = LogicFrameGrade.Low;
        public bool Unresolved { get; set; }
        public bool ManualPatchCall { get; set; }
        public bool YieldedTarget { get; set; }

        public void Raise(LogicFrameGrade grade)
        {
            if (grade is LogicFrameGrade.High or LogicFrameGrade.Medium && Rank(grade) > Rank(Best))
                Best = grade;
        }
    }
}
