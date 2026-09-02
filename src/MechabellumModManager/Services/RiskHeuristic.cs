using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class RiskHeuristicResult
{
    public bool HighRisk { get; init; }
    public string? MatchedKeyword { get; init; }
}

public sealed class RiskHeuristic
{
    public static readonly IReadOnlyList<string> Keywords = new[]
    {
        "cheat", "hack", "unlock", "damage", "economy", "godmode", "trainer",
        "aimbot", "esp", "infinite", "unlimited", "winhack", "alwayswin",
        "作弊", "外挂", "解锁", "伤害", "经济", "无敌", "秒杀", "修改器", "战斗", "数值", "必胜"
    };

    public RiskHeuristicResult Evaluate(ModPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        foreach (var part in EnumerateParts(package))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            foreach (var keyword in Keywords)
            {
                if (part.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return new RiskHeuristicResult { HighRisk = true, MatchedKeyword = keyword };
            }
        }

        return new RiskHeuristicResult { HighRisk = false };
    }

    static IEnumerable<string> EnumerateParts(ModPackage package)
    {
        yield return package.DisplayName;
        yield return package.Id;
        if (!string.IsNullOrWhiteSpace(package.Author))
            yield return package.Author;

        if (!string.IsNullOrWhiteSpace(package.PackageDirectory))
            yield return Path.GetFileName(package.PackageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (var file in package.Files)
        {
            if (string.IsNullOrWhiteSpace(file.RelativePathInPackage)) continue;
            var name = Path.GetFileName(file.RelativePathInPackage.Replace('/', Path.DirectorySeparatorChar));
            yield return name;
            yield return Path.GetFileNameWithoutExtension(name);
        }
    }
}
