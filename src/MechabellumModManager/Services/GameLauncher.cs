using System.Diagnostics;
using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public interface IProcessStarter
{
    void StartShell(string uriOrPath);
}

public sealed class ShellProcessStarter : IProcessStarter
{
    public void StartShell(string uriOrPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uriOrPath,
            UseShellExecute = true
        });
    }
}

public sealed class LaunchResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

public sealed class GameLauncher
{
    public const string SteamUri = "steam://rungameid/669330";
    public const string GameExeName = "Mechabellum.exe";

    private readonly IProcessStarter _starter;
    private readonly Func<bool> _isGameRunning;

    public GameLauncher(IProcessStarter starter, ProcessProbe processProbe)
        : this(starter, processProbe.IsGameRunning)
    {
    }

    public GameLauncher(IProcessStarter starter, Func<bool> isGameRunning)
    {
        _starter = starter;
        _isGameRunning = isGameRunning;
    }

    public LaunchResult Launch(AppConfig cfg)
    {
        if (_isGameRunning())
            return Fail("游戏已在运行。");

        var exePath = Path.Combine(cfg.GamePath, GameExeName);

        try
        {
            switch (cfg.LaunchMode)
            {
                case LaunchMode.SteamOnly:
                    _starter.StartShell(SteamUri);
                    break;
                case LaunchMode.ExeOnly:
                    _starter.StartShell(exePath);
                    break;
                case LaunchMode.SteamThenExe:
                default:
                    try
                    {
                        _starter.StartShell(SteamUri);
                    }
                    catch
                    {
                        _starter.StartShell(exePath);
                    }
                    break;
            }

            return new LaunchResult { Success = true, Message = "" };
        }
        catch (Exception ex)
        {
            return Fail($"启动失败：{ex.Message}");
        }
    }

    static LaunchResult Fail(string message) => new() { Success = false, Message = message };
}
