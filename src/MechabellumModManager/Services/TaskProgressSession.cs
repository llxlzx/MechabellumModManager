namespace MechabellumModManager.Services;

public enum ManagerTaskKind
{
    None = 0,
    BranchWizard,
    BranchSwitch,
    BranchSettle,
    BranchRepair,
    MelonInstall,
    Deploy,
    CatalogRefresh,
    CatalogDownload,
    CheckUpdate,
    DiagnosticsExport,
    GenericBusy
}

/// <summary>
/// Single current-task progress for UI strip + session lock.
/// Nested Begin/Complete (e.g. Busy inside sticky wizard) only adjusts depth/message.
/// </summary>
public sealed class TaskProgressSession
{
    int _nestDepth;

    public ManagerTaskKind Kind { get; private set; }
    public string Title { get; private set; } = "";
    public string Message { get; private set; } = "";
    public double? Percent { get; private set; }
    public bool SessionLock { get; private set; }
    public bool IsSticky { get; private set; }

    public bool HasTask => Kind != ManagerTaskKind.None;
    public bool IsIndeterminate => HasTask && Percent is null;

    public void Begin(
        ManagerTaskKind kind,
        string title,
        string message,
        bool sessionLock,
        bool sticky = false)
    {
        if (kind == ManagerTaskKind.None)
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (HasTask && IsSticky && kind != Kind)
        {
            _nestDepth++;
            if (!string.IsNullOrWhiteSpace(message))
                Message = message;
            return;
        }

        Kind = kind;
        Title = title ?? "";
        Message = message ?? "";
        Percent = null;
        SessionLock = sessionLock;
        IsSticky = sticky;
        _nestDepth = 1;
    }

    public void Report(string? message = null, double? percent = null)
    {
        if (!HasTask)
            return;
        if (message is not null)
            Message = message;
        if (percent is not null)
            Percent = Math.Clamp(percent.Value, 0, 100);
    }

    /// <summary>
    /// Completes one nest level. Sticky outer task stays until <see cref="Clear"/>.
    /// </summary>
    public void Complete(string? message = null)
    {
        if (!HasTask)
            return;

        if (_nestDepth > 1)
        {
            _nestDepth--;
            if (message is not null)
                Message = message;
            return;
        }

        if (IsSticky)
        {
            if (message is not null)
                Message = message;
            Percent = null;
            return;
        }

        Clear();
    }

    public void Fail(string? message = null)
    {
        if (!HasTask)
            return;

        if (_nestDepth > 1)
        {
            _nestDepth--;
            if (message is not null)
                Message = message;
            return;
        }

        if (IsSticky)
        {
            if (message is not null)
                Message = message;
            Percent = null;
            return;
        }

        Clear();
    }

    public void Clear()
    {
        Kind = ManagerTaskKind.None;
        Title = "";
        Message = "";
        Percent = null;
        SessionLock = false;
        IsSticky = false;
        _nestDepth = 0;
    }
}
