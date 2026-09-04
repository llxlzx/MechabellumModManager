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
}
