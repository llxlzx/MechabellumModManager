namespace MechabellumModManager.Services;

public sealed record ManagerUpdatePrompt(
    string LocalVersion,
    string RemoteVersion,
    string Notes,
    string? SetupUrl,
    string? BrowseUrl);

public enum ManagerUpdateUiAction
{
    Skip,
    LaunchSetup,
    OpenBrowseUrl,
    StayAfterFailure
}

public sealed record ManagerUpdateUiResult(
    ManagerUpdateUiAction Action,
    string? LocalSetupPath = null);
