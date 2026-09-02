using Mono.Cecil;

namespace MechabellumModManager.Services;

public sealed class AssemblyInspectResult
{
    public bool ReferencesMelonLoader { get; set; }
    public bool LooksLikeMelonMod { get; set; }
    public bool LooksLikeMelonPlugin { get; set; }
    public string? MelonName { get; set; }
    public string? MelonVersion { get; set; }
    public string? MelonAuthor { get; set; }
}

public sealed class AssemblyInspector
{
    public AssemblyInspectResult Inspect(string dllPath)
    {
        var result = new AssemblyInspectResult();
        using var module = ModuleDefinition.ReadModule(dllPath);

        result.ReferencesMelonLoader = module.AssemblyReferences
            .Any(r => string.Equals(r.Name, "MelonLoader", StringComparison.OrdinalIgnoreCase));

        foreach (var type in module.Types)
            ScanType(type, result);

        foreach (var attr in module.Assembly.CustomAttributes)
            TryReadMelonInfo(attr, result);

        return result;
    }

    static void ScanType(TypeDefinition type, AssemblyInspectResult result)
    {
        var baseName = type.BaseType?.FullName ?? "";
        if (baseName.EndsWith("MelonMod", StringComparison.Ordinal))
            result.LooksLikeMelonMod = true;
        if (baseName.EndsWith("MelonPlugin", StringComparison.Ordinal))
            result.LooksLikeMelonPlugin = true;

        foreach (var attr in type.CustomAttributes)
        {
            var attrName = attr.AttributeType.FullName ?? attr.AttributeType.Name ?? "";
            if (attrName.Contains("MelonInfo", StringComparison.Ordinal))
            {
                if (!result.LooksLikeMelonMod && !result.LooksLikeMelonPlugin)
                    result.LooksLikeMelonMod = true;
                TryReadMelonInfo(attr, result);
            }
            if (attrName.Contains("MelonMod", StringComparison.Ordinal) && attrName.Contains("Attribute", StringComparison.Ordinal))
                result.LooksLikeMelonMod = true;
            if (attrName.Contains("MelonPlugin", StringComparison.Ordinal) && attrName.Contains("Attribute", StringComparison.Ordinal))
                result.LooksLikeMelonPlugin = true;
        }

        foreach (var nested in type.NestedTypes)
            ScanType(nested, result);
    }

    static void TryReadMelonInfo(CustomAttribute attr, AssemblyInspectResult result)
    {
        var attrName = attr.AttributeType.FullName ?? attr.AttributeType.Name ?? "";
        if (!attrName.Contains("MelonInfo", StringComparison.Ordinal))
            return;

        // MelonInfo(Type type, string name, string version, string author, ...)
        var args = attr.ConstructorArguments;
        if (args.Count >= 2 && args[1].Value is string name)
            result.MelonName ??= name;
        if (args.Count >= 3 && args[2].Value is string version)
            result.MelonVersion ??= version;
        if (args.Count >= 4 && args[3].Value is string author)
            result.MelonAuthor ??= author;
    }
}
