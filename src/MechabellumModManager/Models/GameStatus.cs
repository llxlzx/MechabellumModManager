namespace MechabellumModManager.Models;

public enum GameStatusKind
{
    GameMissing,
    GameOkLoaderMissing,
    LoaderPartial,
    LoaderPresentAssembliesMissing,
    Ready
}

public sealed class GameStatus
{
    public GameStatusKind Kind { get; init; }
    public string GamePath { get; init; } = "";
    public string Message { get; init; } = "";
    public string? MelonLoaderVersion { get; init; }
    public TimeSpan? LatestLogAge { get; init; }
    /// <summary>
    /// Null when no launch has been recorded yet, or the settle window has not elapsed.
    /// False when Latest.log was not updated after the last launch request.
    /// </summary>
    public bool? LoaderInjected { get; init; }
}
