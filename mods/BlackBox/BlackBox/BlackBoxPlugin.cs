using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BlackBox.Core;
using MelonLoader;
using MelonLoader.InternalUtils;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(BlackBoxPlugin), "BlackBox", "0.1.0", "llxmod")]
[assembly: MelonGame("GameRiver", "Mechabellum")]

public class BlackBoxPlugin : MelonPlugin
{
    const uint CreateNoWindow = 0x08000000;
    const uint WaitTimeout = 258;
    const long HeartbeatFileSeconds = 30;

    int _phase = StallPhaseCodes.Armed;
    bool _dumpFinished;
    QuitRequestState _quit;
    long _lastBeat;
    long _started;
    string _scene = "unknown";
    long _published;
    double _healthySeconds;
    string _unityVersion = "unknown";
    string _plugins = "unknown";
    string _mods = "unknown";
    string _gameDir = "";
    string _recordDir = "";
    int _watchdogLogged;
    readonly IDumpRunner _runner = new Win32DumpRunner();

    static BlackBoxPlugin? _self;
    static NativeExceptionFilter? _nativeFilter;
    static IntPtr _previousFilter;
    static MethodInfo? _getActiveScene;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int NativeExceptionFilter(IntPtr exceptionPointers);

#pragma warning disable CS0672
    public override void OnApplicationStart()
#pragma warning restore CS0672
    {
        var now = Stopwatch.GetTimestamp();
        _started = now;
        _lastBeat = now;
        Volatile.Write(ref _published, now);

        MelonEvents.OnUpdate.Subscribe(OnHeartbeat, Priority, false);
        MelonEvents.OnApplicationQuit.Subscribe(OnQuitRequested, Priority, false);
        MelonEvents.OnApplicationDefiniteQuit.Subscribe(OnApplicationDefiniteQuit, Priority, false);

        _self = this;
        _nativeFilter = NativeFilter;
        _previousFilter = SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(_nativeFilter));
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;

        _gameDir = MelonEnvironment.GameRootDirectory ?? "";
        _recordDir = Path.Combine(_gameDir, "UserData", "BlackBox");

        var thread = new Thread(Watchdog) { IsBackground = true, Name = "BlackBox.Watchdog" };
        thread.Start();

        _plugins = ListTopDlls(MelonEnvironment.PluginsDirectory);
        _mods = ListTopDlls(MelonEnvironment.ModsDirectory);
        try { ExtractHelper(); }
        catch { /* DumpSession reports helper-missing. */ }

        MelonLogger.Msg("BlackBox armed");
    }

    void OnHeartbeat()
    {
        try
        {
            var next = HeartbeatPulse.Apply(
                Stopwatch.GetTimestamp(),
                _lastBeat,
                Stopwatch.Frequency,
                ReadScene,
                ref _scene,
                ref _published);
            if (next != _lastBeat)
            {
                _lastBeat = next;
                _quit.OnHeartbeatWritten();
            }
        }
        catch
        {
            // A heartbeat failure must not escape the frame.
        }
    }

    void OnQuitRequested() => _quit.OnQuitRequested();

    void OnApplicationDefiniteQuit() => _quit.OnDefiniteQuit();

    string? ReadScene()
    {
        try
        {
            var version = EngineVersionText();
            if (!string.IsNullOrEmpty(version))
                _unityVersion = version;
        }
        catch
        {
            // Keep the previous version string.
        }

        try
        {
            return ReadActiveSceneName();
        }
        catch
        {
            return null;
        }
    }

    static string? ReadActiveSceneName()
    {
        var getActive = _getActiveScene;
        if (getActive == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? type;
                try { type = assembly.GetType("UnityEngine.SceneManagement.SceneManager", false); }
                catch { continue; }
                if (type == null)
                    continue;
                getActive = type.GetMethod("GetActiveScene", BindingFlags.Public | BindingFlags.Static);
                if (getActive != null)
                {
                    _getActiveScene = getActive;
                    break;
                }
            }
        }

        if (getActive == null)
            return null;
        var scene = getActive.Invoke(null, null);
        if (scene == null)
            return null;
        var sceneType = scene.GetType();
        if (sceneType.GetProperty("name")?.GetValue(scene) is string name && name.Length > 0)
            return name;
        return sceneType.GetMethod("get_name")?.Invoke(scene, null) as string;
    }

    void Watchdog()
    {
        long lastFile = 0;
        try
        {
            WriteHeartbeatFile();
            lastFile = Stopwatch.GetTimestamp();
        }
        catch (Exception ex)
        {
            LogWatchdogOnce(ex);
        }

        while (true)
        {
            try
            {
                Thread.Sleep(1000);
                var freq = Frequency();
                var now = Stopwatch.GetTimestamp();
                if (now - lastFile >= HeartbeatFileSeconds * freq)
                {
                    WriteHeartbeatFile();
                    lastFile = Stopwatch.GetTimestamp();
                }

                EvaluateStall();
            }
            catch (Exception ex)
            {
                LogWatchdogOnce(ex);
            }
        }
    }

    void EvaluateStall()
    {
        var freq = Frequency();
        var now = Stopwatch.GetTimestamp();
        var observed = _phase;
        var beat = Volatile.Read(ref _published);
        var hasHeartbeat = beat != 0;
        var age = hasHeartbeat ? (now - beat) / (double)freq : 0d;
        var decision = StallPolicy.Evaluate(new StallInput(
            hasHeartbeat,
            age,
            _quit.DefiniteQuit,
            _quit.Requested,
            _quit.HeartbeatsSinceRequest,
            ToPhase(observed),
            _healthySeconds,
            _dumpFinished));
        _healthySeconds = decision.NextHealthySeconds;
        if (decision.BeginIncident)
        {
            Record("hang", 0);
            return;
        }

        var next = ToCode(decision.NextPhase);
        if (Interlocked.CompareExchange(ref _phase, next, observed) == observed
            && decision.NextPhase == StallPhase.Disarmed)
            _dumpFinished = false;
    }

    void Record(string trigger, long silentSeconds)
    {
        if (_quit.DefiniteQuit)
            return;

        if (!IncidentGate.TryBegin(ref _phase))
            return;

        try
        {
            if (trigger == "hang")
                silentSeconds = (Stopwatch.GetTimestamp() - _lastBeat) / Frequency();

            var pending = Summary(trigger, silentSeconds, "pending", "");
            IncidentWriter.WriteSummary(_recordDir, IncidentWriter.FormatSummary(pending));
            var outcome = Dump();
            var done = Summary(trigger, silentSeconds, outcome.Status, outcome.Error);
            IncidentWriter.WriteSummary(_recordDir, IncidentWriter.FormatSummary(done));
            MelonLogger.Msg("trigger: " + trigger + " dump: " + outcome.Status);
        }
        catch (Exception ex)
        {
            try
            {
                var failed = Summary(trigger, silentSeconds, "failed", ex.Message);
                IncidentWriter.WriteSummary(_recordDir, IncidentWriter.FormatSummary(failed));
            }
            catch
            {
                // Do not let the failure summary escape Record.
            }

            try
            {
                MelonLogger.Msg("trigger: " + trigger + " dump: failed");
            }
            catch
            {
                // Do not let the log itself throw out of Record.
            }
        }
        finally
        {
            _dumpFinished = true;
        }
    }

    DumpOutcome Dump()
    {
        var tmp = Path.Combine(_recordDir, "blackbox.dmp.tmp");
        var finalPath = Path.Combine(_recordDir, "blackbox.dmp");
        var helper = Path.Combine(_recordDir, "BlackBoxDump.exe");
        var arguments = DumpArguments.Format(Environment.ProcessId, CreationFileTime(), tmp);
        return DumpSession.Complete(helper, arguments, tmp, finalPath, _runner, 30000);
    }

    void WriteHeartbeatFile()
    {
        var text = IncidentWriter.FormatHeartbeat(
            DateTimeOffset.UtcNow,
            Environment.ProcessId,
            (Stopwatch.GetTimestamp() - _started) / Frequency(),
            _unityVersion,
            _scene);
        IncidentWriter.WriteHeartbeat(_recordDir, text);
    }

    SummaryFields Summary(string trigger, long silentSeconds, string dump, string dumpError) =>
        new(
            trigger,
            DateTimeOffset.UtcNow.ToString("o"),
            Environment.ProcessId,
            (Stopwatch.GetTimestamp() - _started) / Frequency(),
            silentSeconds,
            _gameDir,
            _unityVersion,
            _scene,
            WorkingSetMb(),
            _plugins,
            _mods,
            dump,
            dumpError);

    static string WorkingSetMb()
    {
        try
        {
            return (Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)).ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    static ulong CreationFileTime()
    {
        try
        {
            if (GetProcessTimes(Process.GetCurrentProcess().Handle, out var creation, out _, out _, out _))
                return (ulong)creation;
        }
        catch
        {
            // Fall through to the managed start time.
        }

        try
        {
            return (ulong)Process.GetCurrentProcess().StartTime.ToFileTimeUtc();
        }
        catch
        {
            return 0;
        }
    }

    void ExtractHelper()
    {
        using var stream = typeof(BlackBoxPlugin).Assembly.GetManifestResourceStream("BlackBoxDump.exe");
        if (stream == null)
            return;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var payload = buffer.ToArray();
        var dest = Path.Combine(_recordDir, "BlackBoxDump.exe");
        if (!HelperRelease.NeedsWrite(dest, payload))
            return;
        Directory.CreateDirectory(_recordDir);
        File.WriteAllBytes(dest, payload);
    }

    static string ListTopDlls(string? directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory))
                return "unknown";
            var names = Directory.EnumerateFiles(directory, "*.dll")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name));
            return string.Join(";", names!);
        }
        catch
        {
            return "unknown";
        }
    }

    void LogWatchdogOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _watchdogLogged, 1) != 0)
            return;
        try { MelonLogger.Error("BlackBox watchdog: " + ex.Message); }
        catch { }
    }

    static string? EngineVersionText()
    {
        var value = typeof(UnityInformationHandler)
            .GetProperty("EngineVersion", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        var text = value?.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    static long Frequency()
    {
        var freq = Stopwatch.Frequency;
        return freq <= 0 ? 1 : freq;
    }

    static StallPhase ToPhase(int code) => code switch
    {
        StallPhaseCodes.Dumping => StallPhase.Dumping,
        StallPhaseCodes.Disarmed => StallPhase.Disarmed,
        _ => StallPhase.Armed
    };

    static int ToCode(StallPhase phase) => phase switch
    {
        StallPhase.Dumping => StallPhaseCodes.Dumping,
        StallPhase.Disarmed => StallPhaseCodes.Disarmed,
        _ => StallPhaseCodes.Armed
    };

    static int NativeFilter(IntPtr info)
    {
        try { _self?.Record("unhandled-exception", 0); }
        catch { }

        if (_previousFilter != IntPtr.Zero)
        {
            var previous = Marshal.GetDelegateForFunctionPointer<NativeExceptionFilter>(_previousFilter);
            return previous(info);
        }

        return 0;
    }

    static void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs args)
    {
        try { _self?.Record("unhandled-exception", 0); }
        catch { }
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr SetUnhandledExceptionFilter(IntPtr lpTopLevelExceptionFilter);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetProcessTimes(
        IntPtr process,
        out long creation,
        out long exit,
        out long kernel,
        out long user);

    sealed class Win32DumpRunner : IDumpRunner
    {
        public DumpRunResult Run(string exePath, string arguments, int timeoutMs)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return new DumpRunResult(true, null, false);

            var command = "\"" + exePath + "\" " + arguments;
            var commandLine = new StringBuilder(command, command.Length + 1);
            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
            if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateNoWindow, IntPtr.Zero, null, ref startup, out var process))
                return new DumpRunResult(false, null, false, false);

            try
            {
                var wait = WaitForSingleObject(process.hProcess, (uint)timeoutMs);
                if (wait != 0)
                {
                    TerminateProcess(process.hProcess, 1);
                    return new DumpRunResult(false, null, true, true);
                }

                if (!GetExitCodeProcess(process.hProcess, out var code))
                    return new DumpRunResult(false, null, false, true);
                return new DumpRunResult(false, code, false, true);
            }
            finally
            {
                if (process.hThread != IntPtr.Zero)
                    CloseHandle(process.hThread);
                if (process.hProcess != IntPtr.Zero)
                    CloseHandle(process.hProcess);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcess(
            string? applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(IntPtr process, out int exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        struct StartupInfo
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
    }
}
