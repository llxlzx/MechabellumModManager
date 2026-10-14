using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

/// <summary>
/// Catalog summaries published before 2026-10-14 open with a manager-version sentence. The browse
/// list already shows minManagerVersion, so that sentence is dropped from the displayed text only.
/// </summary>
public static class SummaryDisplay
{
    static readonly Regex ManagerFloorPrefix = new(
        @"^\s*(?:" +
        @"从管理器安装或更新需要 [0-9]+\.[0-9]+\.[0-9]+ 或更高版本。" +
        @"|Installing or updating through the manager requires [0-9]+\.[0-9]+\.[0-9]+ or newer\." +
        @"|Installation oder Aktualisierung über den Manager erfordert [0-9]+\.[0-9]+\.[0-9]+ oder neuer\." +
        @"|マネージャーからのインストールと更新には [0-9]+\.[0-9]+\.[0-9]+ 以降が必要です。" +
        @"|Установка и обновление через менеджер требуют версию [0-9]+\.[0-9]+\.[0-9]+ или новее[.。]" +
        @")\s*",
        RegexOptions.CultureInvariant);

    public static string? StripManagerFloorPrefix(string? summary)
    {
        if (summary is null)
            return null;
        var match = ManagerFloorPrefix.Match(summary);
        return match.Success ? summary[match.Length..] : summary;
    }
}
