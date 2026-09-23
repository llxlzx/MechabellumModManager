namespace MechabellumModManager.Services;

public static class LogicFrameGate
{
    public static bool NeedsConfirm(LogicFrameGrade grade) =>
        grade is LogicFrameGrade.Medium or LogicFrameGrade.High;

    public static bool CanEnable(
        LogicFrameGrade grade,
        string displayName,
        Func<string, string> translate,
        Func<string, bool> confirm)
    {
        if (!NeedsConfirm(grade))
            return true;

        var key = grade == LogicFrameGrade.High
            ? "ConfirmLogicFrameHigh"
            : "ConfirmLogicFrameMedium";
        var template = translate(key);
        if (string.IsNullOrEmpty(template))
            return false;
        var message = template.Replace("{0}", displayName ?? "");
        return confirm(message);
    }
}
