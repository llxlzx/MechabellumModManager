# BlackBox Flight Recorder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a MelonLoader plugin that writes a hang/exception summary and a small out-of-process minidump, and make the manager diagnostics zip pick those files up.

**Architecture:** `BlackBox.Core` (`net6.0`) holds the stall rules, the single-incident gate, text files, and dump-session completion. The Melon plugin only subscribes, heartbeats, and starts `BlackBoxDump.exe`. The manager copies `UserData/BlackBox` into the existing diagnostics zip without changing diagnosis codes.

**Tech Stack:** C# / xUnit / FluentAssertions / net6.0 class library referenced by the net8.0 manager tests; MelonLoader 0.7.3 `MelonPlugin` (`net6.0`); native x64 `BlackBoxDump.exe` built with the Windows SDK `cl.exe`.

**Working branch:** `blackbox-flight-recorder`

## Global Constraints

- Melon plugin, `net6.0`, MelonLoader 0.7.3 callback names: `OnApplicationStart`, `MelonEvents.OnUpdate`, `OnApplicationQuit`, `OnApplicationDefiniteQuit`.
- Hang threshold is 20 seconds. Healthy beat is under 3 seconds. Rearm after 60 healthy seconds. Quit-request cancel is 3 successful heartbeats. Dump wait cap is 30 seconds.
- Do not call `MiniDumpWriteDump` inside the game process. Do not replace Melon's unhandled-exception filter; chain it.
- Minidump flags are only `MiniDumpNormal | MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules`. Process rights are only `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE`.
- Do not auto-install, do not enable by default, do not add a catalog entry, do not change `DiagnosisEngine` or the pure-cleanup whitelist.
- Do not commit `BlackBoxDump.exe`. Text files are UTF-8. Atomic replace: `File.Replace` when the target exists, otherwise `File.Move`.
- Diagnostics: text cap stays 8 MB with existing strong redaction; `.dmp` is copied raw, skipped above 64 MB; `*.tmp` and `BlackBoxDump.exe` are never zipped.
- Tests do not launch the game and do not call a real `MiniDumpWriteDump`.

---

## File map

| File | Responsibility |
|------|----------------|
| `mods/BlackBox/BlackBox.Core/BlackBox.Core.csproj` | net6.0 library, no Melon, no Unity |
| `mods/BlackBox/BlackBox.Core/StallPolicy.cs` | Pure hang / rearm decision |
| `mods/BlackBox/BlackBox.Core/IncidentGate.cs` | One caller enters `Dumping` |
| `mods/BlackBox/BlackBox.Core/QuitRequestState.cs` | Cancellable quit vs definite quit |
| `mods/BlackBox/BlackBox.Core/HeartbeatPulse.cs` | Timestamp first, scene read second |
| `mods/BlackBox/BlackBox.Core/IncidentWriter.cs` | `heartbeat.txt` / `summary.txt` |
| `mods/BlackBox/BlackBox.Core/DumpSession.cs` | Helper outcome, no process spawn of its own |
| `mods/BlackBox/native/BlackBoxDump.c` | Out-of-process minidump |
| `mods/BlackBox/native/build.ps1` | `cl.exe` build; fails with a clear message if missing |
| `mods/BlackBox/BlackBox/BlackBox.csproj` | MelonPlugin; not added to the solution build |
| `mods/BlackBox/BlackBox/BlackBoxPlugin.cs` | Subscriptions, watchdog thread, helper extract |
| `src/MechabellumModManager/Services/BlackBoxExport.cs` | Read-only zip collection |
| `tests/MechabellumModManager.Tests/BlackBox*.cs` | Core and export tests |

---

### Task 1: Stall policy

**Files:**
- Create: `mods/BlackBox/BlackBox.Core/BlackBox.Core.csproj`
- Create: `mods/BlackBox/BlackBox.Core/StallPolicy.cs`
- Create: `tests/MechabellumModManager.Tests/BlackBoxStallPolicyTests.cs`
- Modify: `tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj` (project reference)
- Modify: `MechabellumModManager.sln` (via `dotnet sln add`)

**Interfaces:**
- Consumes: nothing
- Produces:
  - `enum StallPhase { Armed, Dumping, Disarmed }`
  - `readonly record struct StallInput(bool HasHeartbeat, double HeartbeatAgeSeconds, bool DefiniteQuit, bool QuitRequested, int HeartbeatsSinceQuitRequest, StallPhase Phase, double HealthySeconds, bool DumpFinished)`
  - `readonly record struct StallDecision(bool BeginIncident, StallPhase NextPhase, double NextHealthySeconds)`
  - `static StallDecision StallPolicy.Evaluate(StallInput input)`
  - Constants: `HangSeconds = 20`, `HealthyBeatSeconds = 3`, `RearmSeconds = 60`, `QuitCancelHeartbeats = 3`

- [ ] **Step 1: Create the branch and the failing tests**

```powershell
git checkout -b blackbox-flight-recorder
```

`mods/BlackBox/BlackBox.Core/BlackBox.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>BlackBox.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

```powershell
dotnet sln MechabellumModManager.sln add mods/BlackBox/BlackBox.Core/BlackBox.Core.csproj
dotnet add tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj reference mods/BlackBox/BlackBox.Core/BlackBox.Core.csproj
```

`tests/MechabellumModManager.Tests/BlackBoxStallPolicyTests.cs`:

```csharp
using BlackBox.Core;
using FluentAssertions;

public class BlackBoxStallPolicyTests
{
    static StallInput Sample(
        bool hasHeartbeat = true,
        double age = 0,
        bool definiteQuit = false,
        bool quitRequested = false,
        int heartbeatsSinceQuit = 0,
        StallPhase phase = StallPhase.Armed,
        double healthy = 0,
        bool dumpFinished = false) =>
        new(hasHeartbeat, age, definiteQuit, quitRequested, heartbeatsSinceQuit, phase, healthy, dumpFinished);

    [Fact]
    public void Never_heartbeat_does_not_hang()
    {
        var d = StallPolicy.Evaluate(Sample(hasHeartbeat: false, age: 100));
        d.BeginIncident.Should().BeFalse();
        d.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Nineteen_seconds_does_not_hang()
    {
        var d = StallPolicy.Evaluate(Sample(age: 19));
        d.BeginIncident.Should().BeFalse();
        d.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Twenty_seconds_armed_not_quitting_begins()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20));
        d.BeginIncident.Should().BeTrue();
        d.NextPhase.Should().Be(StallPhase.Dumping);
    }

    [Fact]
    public void Definite_quit_does_not_begin()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20, definiteQuit: true));
        d.BeginIncident.Should().BeFalse();
    }

    [Fact]
    public void Quit_request_before_three_heartbeats_does_not_begin()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20, quitRequested: true, heartbeatsSinceQuit: 2));
        d.BeginIncident.Should().BeFalse();
    }

    [Fact]
    public void Dumping_does_not_begin_again_until_finished_then_disarms()
    {
        var waiting = StallPolicy.Evaluate(Sample(age: 30, phase: StallPhase.Dumping, dumpFinished: false));
        waiting.BeginIncident.Should().BeFalse();
        waiting.NextPhase.Should().Be(StallPhase.Dumping);

        var done = StallPolicy.Evaluate(Sample(age: 30, phase: StallPhase.Dumping, dumpFinished: true));
        done.BeginIncident.Should().BeFalse();
        done.NextPhase.Should().Be(StallPhase.Disarmed);
        done.NextHealthySeconds.Should().Be(0);
    }

    [Fact]
    public void Disarmed_rearms_only_from_policy_healthy_timer()
    {
        double healthy = 0;
        var phase = StallPhase.Disarmed;
        for (var i = 0; i < 59; i++)
        {
            var d = StallPolicy.Evaluate(Sample(age: 1, phase: phase, healthy: healthy));
            d.BeginIncident.Should().BeFalse();
            d.NextPhase.Should().Be(StallPhase.Disarmed);
            healthy = d.NextHealthySeconds;
            phase = d.NextPhase;
        }

        var armed = StallPolicy.Evaluate(Sample(age: 1, phase: phase, healthy: healthy));
        armed.BeginIncident.Should().BeFalse();
        armed.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Healthy_timer_resets_when_age_reaches_three_seconds()
    {
        var d = StallPolicy.Evaluate(Sample(age: 3, phase: StallPhase.Disarmed, healthy: 40));
        d.NextPhase.Should().Be(StallPhase.Disarmed);
        d.NextHealthySeconds.Should().Be(0);
        d.BeginIncident.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxStallPolicyTests -v q --nologo`

Expected: FAIL because `StallPolicy` does not exist.

- [ ] **Step 3: Implement the policy**

`mods/BlackBox/BlackBox.Core/StallPolicy.cs`:

```csharp
namespace BlackBox.Core;

public enum StallPhase
{
    Armed,
    Dumping,
    Disarmed
}

public readonly record struct StallInput(
    bool HasHeartbeat,
    double HeartbeatAgeSeconds,
    bool DefiniteQuit,
    bool QuitRequested,
    int HeartbeatsSinceQuitRequest,
    StallPhase Phase,
    double HealthySeconds,
    bool DumpFinished);

public readonly record struct StallDecision(
    bool BeginIncident,
    StallPhase NextPhase,
    double NextHealthySeconds);

public static class StallPolicy
{
    public const double HangSeconds = 20;
    public const double HealthyBeatSeconds = 3;
    public const double RearmSeconds = 60;
    public const int QuitCancelHeartbeats = 3;

    public static StallDecision Evaluate(StallInput input)
    {
        var exiting = input.DefiniteQuit
            || (input.QuitRequested && input.HeartbeatsSinceQuitRequest < QuitCancelHeartbeats);

        if (input.Phase == StallPhase.Armed)
        {
            if (!input.HasHeartbeat || exiting || input.HeartbeatAgeSeconds < HangSeconds)
                return new StallDecision(false, StallPhase.Armed, input.HealthySeconds);
            return new StallDecision(true, StallPhase.Dumping, input.HealthySeconds);
        }

        if (input.Phase == StallPhase.Dumping)
        {
            if (!input.DumpFinished)
                return new StallDecision(false, StallPhase.Dumping, input.HealthySeconds);
            return new StallDecision(false, StallPhase.Disarmed, 0);
        }

        if (input.HeartbeatAgeSeconds >= HealthyBeatSeconds)
            return new StallDecision(false, StallPhase.Disarmed, 0);

        var nextHealthy = input.HealthySeconds + 1;
        if (nextHealthy >= RearmSeconds)
            return new StallDecision(false, StallPhase.Armed, 0);
        return new StallDecision(false, StallPhase.Disarmed, nextHealthy);
    }
}
```

The caller stores `NextHealthySeconds` and `NextPhase` except when `BeginIncident` is true: that transition to `Dumping` is performed only by `IncidentGate.TryBegin` in Task 2. If `TryBegin` returns false, the caller leaves the phase unchanged.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxStallPolicyTests -v q --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add mods/BlackBox/BlackBox.Core MechabellumModManager.sln tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj tests/MechabellumModManager.Tests/BlackBoxStallPolicyTests.cs
git commit -m "feat: add BlackBox stall policy"
```

---

### Task 2: Incident gate and quit request

**Files:**
- Create: `mods/BlackBox/BlackBox.Core/IncidentGate.cs`
- Create: `mods/BlackBox/BlackBox.Core/QuitRequestState.cs`
- Create: `tests/MechabellumModManager.Tests/BlackBoxIncidentGateTests.cs`

**Interfaces:**
- Consumes: `StallPhase` from Task 1
- Produces:
  - `static class StallPhaseCodes` with `Armed = 0`, `Dumping = 1`, `Disarmed = 2`
  - `static bool IncidentGate.TryBegin(ref int phase)` — `Interlocked.CompareExchange` from `Armed` to `Dumping`; true only for the winner
  - `struct QuitRequestState` with `bool DefiniteQuit`, `bool Requested`, `int HeartbeatsSinceRequest`, `void OnQuitRequested()`, `void OnDefiniteQuit()`, `void OnHeartbeatWritten()`, `bool IsExiting`

- [ ] **Step 1: Write the failing tests**

```csharp
using BlackBox.Core;
using FluentAssertions;

public class BlackBoxIncidentGateTests
{
    [Fact]
    public void TryBegin_allows_one_caller()
    {
        var phase = StallPhaseCodes.Armed;
        IncidentGate.TryBegin(ref phase).Should().BeTrue();
        phase.Should().Be(StallPhaseCodes.Dumping);
        IncidentGate.TryBegin(ref phase).Should().BeFalse();
    }

    [Fact]
    public void TryBegin_fails_while_disarmed_and_succeeds_again_after_rearm()
    {
        var phase = StallPhaseCodes.Disarmed;
        IncidentGate.TryBegin(ref phase).Should().BeFalse();
        phase = StallPhaseCodes.Armed;
        IncidentGate.TryBegin(ref phase).Should().BeTrue();
    }

    [Fact]
    public void Three_heartbeats_clear_a_quit_request_and_a_later_hang_can_begin()
    {
        var quit = new QuitRequestState();
        quit.OnQuitRequested();
        quit.IsExiting.Should().BeTrue();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeTrue();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeFalse();

        var d = StallPolicy.Evaluate(new StallInput(
            true, 20, quit.DefiniteQuit, quit.Requested, quit.HeartbeatsSinceRequest,
            StallPhase.Armed, 0, false));
        d.BeginIncident.Should().BeTrue();
    }

    [Fact]
    public void Definite_quit_stays_exiting()
    {
        var quit = new QuitRequestState();
        quit.OnDefiniteQuit();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxIncidentGateTests -v q --nologo`

Expected: FAIL because `IncidentGate` does not exist.

- [ ] **Step 3: Implement the gate and quit state**

`IncidentGate.cs`:

```csharp
namespace BlackBox.Core;

public static class StallPhaseCodes
{
    public const int Armed = 0;
    public const int Dumping = 1;
    public const int Disarmed = 2;
}

public static class IncidentGate
{
    public static bool TryBegin(ref int phase) =>
        Interlocked.CompareExchange(ref phase, StallPhaseCodes.Dumping, StallPhaseCodes.Armed)
        == StallPhaseCodes.Armed;
}
```

`QuitRequestState.cs`:

```csharp
namespace BlackBox.Core;

public struct QuitRequestState
{
    public bool DefiniteQuit;
    public bool Requested;
    public int HeartbeatsSinceRequest;

    public void OnQuitRequested()
    {
        Requested = true;
        HeartbeatsSinceRequest = 0;
    }

    public void OnDefiniteQuit() => DefiniteQuit = true;

    public void OnHeartbeatWritten()
    {
        if (!Requested || DefiniteQuit)
            return;
        HeartbeatsSinceRequest++;
        if (HeartbeatsSinceRequest >= StallPolicy.QuitCancelHeartbeats)
        {
            Requested = false;
            HeartbeatsSinceRequest = 0;
        }
    }

    public bool IsExiting =>
        DefiniteQuit || (Requested && HeartbeatsSinceRequest < StallPolicy.QuitCancelHeartbeats);
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxIncidentGateTests -v q --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add mods/BlackBox/BlackBox.Core/IncidentGate.cs mods/BlackBox/BlackBox.Core/QuitRequestState.cs tests/MechabellumModManager.Tests/BlackBoxIncidentGateTests.cs
git commit -m "feat: add BlackBox incident gate and quit latch"
```

---

### Task 3: Heartbeat pulse and incident text

**Files:**
- Create: `mods/BlackBox/BlackBox.Core/HeartbeatPulse.cs`
- Create: `mods/BlackBox/BlackBox.Core/IncidentWriter.cs`
- Create: `tests/MechabellumModManager.Tests/BlackBoxIncidentWriterTests.cs`

**Interfaces:**
- Consumes: nothing from Tasks 1–2
- Produces:
  - `static long HeartbeatPulse.Apply(long now, long last, long frequency, Func<string?> readScene, ref string scene)` — if `now - last < frequency`, return `last` and do not call `readScene`; otherwise return `now` even when `readScene` throws, and keep the previous scene on throw or empty
  - `static void IncidentWriter.WriteAtomic(string path, string text)`
  - `static string IncidentWriter.FormatHeartbeat(DateTimeOffset utc, int pid, long uptimeSeconds, string unityVersion, string scene)`
  - `static string IncidentWriter.FormatSummary(SummaryFields fields)` where `SummaryFields` is a `readonly record struct` with the keys in spec section 4: `Trigger`, `Utc`, `Pid`, `UptimeSeconds`, `SilentSeconds`, `GamePath`, `UnityVersion`, `Scene`, `WorkingSetMb`, `Plugins`, `Mods`, `Dump`, `DumpError`
  - `static void IncidentWriter.WriteHeartbeat(string directory, string text)` writes `heartbeat.txt`
  - `static void IncidentWriter.WriteSummary(string directory, string text)` writes `summary.txt`

- [ ] **Step 1: Write the failing tests**

```csharp
using BlackBox.Core;
using FluentAssertions;

public class BlackBoxIncidentWriterTests
{
    [Fact]
    public void Scene_read_failure_still_advances_the_timestamp()
    {
        var scene = "old";
        var called = false;
        var next = HeartbeatPulse.Apply(2_000_0000, 0, 10_000_000, () =>
        {
            called = true;
            throw new InvalidOperationException("scene");
        }, ref scene);

        called.Should().BeTrue();
        next.Should().Be(2_000_0000);
        scene.Should().Be("old");
    }

    [Fact]
    public void Pulse_inside_one_second_does_not_read_the_scene()
    {
        var scene = "keep";
        var next = HeartbeatPulse.Apply(10_000_000, 5_000_000, 10_000_000, () =>
        {
            throw new InvalidOperationException("should not read");
        }, ref scene);
        next.Should().Be(5_000_000);
        scene.Should().Be("keep");
    }

    [Fact]
    public void Summary_and_heartbeat_round_trip_keys()
    {
        using var dir = new TempDir();
        var heartbeat = IncidentWriter.FormatHeartbeat(
            DateTimeOffset.Parse("2026-09-22T12:00:00Z"), 10, 15, "2022.3.62", "unknown");
        IncidentWriter.WriteHeartbeat(dir.Path, heartbeat);
        File.ReadAllText(Path.Combine(dir.Path, "heartbeat.txt")).Should().Contain("scene: unknown");

        var summary = IncidentWriter.FormatSummary(new SummaryFields(
            "hang", "2026-09-22T12:00:20Z", 10, 35, 20, @"C:\Games\Mechabellum",
            "2022.3.62", "battle", "unknown", "A.dll", "B.dll", "pending", ""));
        IncidentWriter.WriteSummary(dir.Path, summary);
        var text = File.ReadAllText(Path.Combine(dir.Path, "summary.txt"));
        text.Should().Contain("trigger: hang");
        text.Should().Contain("silentSeconds: 20");
        text.Should().Contain("dump: pending");
        text.Should().Contain("workingSetMb: unknown");
    }

    sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bb-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxIncidentWriterTests -v q --nologo`

Expected: FAIL because `HeartbeatPulse` does not exist.

- [ ] **Step 3: Implement pulse and writer**

`HeartbeatPulse.cs`:

```csharp
namespace BlackBox.Core;

public static class HeartbeatPulse
{
    public static long Apply(long now, long last, long frequency, Func<string?> readScene, ref string scene)
    {
        if (frequency <= 0)
            frequency = 1;
        if (last != 0 && now - last < frequency)
            return last;

        try
        {
            var next = readScene();
            if (!string.IsNullOrEmpty(next))
                scene = next;
        }
        catch
        {
            // Timestamp already committed by returning now.
        }

        return now;
    }
}
```

`IncidentWriter.cs`:

```csharp
using System.Text;

namespace BlackBox.Core;

public readonly record struct SummaryFields(
    string Trigger,
    string Utc,
    int Pid,
    long UptimeSeconds,
    long SilentSeconds,
    string GamePath,
    string UnityVersion,
    string Scene,
    string WorkingSetMb,
    string Plugins,
    string Mods,
    string Dump,
    string DumpError);

public static class IncidentWriter
{
    static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string FormatHeartbeat(DateTimeOffset utc, int pid, long uptimeSeconds, string unityVersion, string scene) =>
        "utc: " + utc.ToUniversalTime().ToString("o") + "\n" +
        "pid: " + pid + "\n" +
        "uptimeSeconds: " + uptimeSeconds + "\n" +
        "unityVersion: " + unityVersion + "\n" +
        "scene: " + scene + "\n";

    public static string FormatSummary(SummaryFields fields) =>
        "trigger: " + fields.Trigger + "\n" +
        "utc: " + fields.Utc + "\n" +
        "pid: " + fields.Pid + "\n" +
        "uptimeSeconds: " + fields.UptimeSeconds + "\n" +
        "silentSeconds: " + fields.SilentSeconds + "\n" +
        "gamePath: " + fields.GamePath + "\n" +
        "unityVersion: " + fields.UnityVersion + "\n" +
        "scene: " + fields.Scene + "\n" +
        "workingSetMb: " + fields.WorkingSetMb + "\n" +
        "plugins: " + fields.Plugins + "\n" +
        "mods: " + fields.Mods + "\n" +
        "dump: " + fields.Dump + "\n" +
        "dumpError: " + fields.DumpError + "\n";

    public static void WriteHeartbeat(string directory, string text) =>
        WriteAtomic(Path.Combine(directory, "heartbeat.txt"), text);

    public static void WriteSummary(string directory, string text) =>
        WriteAtomic(Path.Combine(directory, "summary.txt"), text);

    public static void WriteAtomic(string path, string text)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = path + ".writing";
        File.WriteAllText(tmp, text, Utf8);
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxIncidentWriterTests -v q --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add mods/BlackBox/BlackBox.Core/HeartbeatPulse.cs mods/BlackBox/BlackBox.Core/IncidentWriter.cs tests/MechabellumModManager.Tests/BlackBoxIncidentWriterTests.cs
git commit -m "feat: write BlackBox heartbeat and summary atomically"
```

---

### Task 4: Dump session completion

**Files:**
- Create: `mods/BlackBox/BlackBox.Core/DumpSession.cs`
- Create: `tests/MechabellumModManager.Tests/BlackBoxDumpSessionTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `readonly record struct DumpRunResult(bool ExeMissing, int? ExitCode, bool TimedOut)`
  - `interface IDumpRunner { DumpRunResult Run(string exePath, string arguments, int timeoutMs); }`
  - `readonly record struct DumpOutcome(string Status, string Error)` with status strings `written`, `failed`, `timed-out`, `helper-missing`
  - `static string DumpArguments.Format(int pid, ulong startedFileTime, string outputTmpPath)` → `--pid <pid> --started <filetime> --out "<path>"`
  - `static DumpOutcome DumpSession.Complete(string helperPath, string arguments, string tmpPath, string finalPath, IDumpRunner runner, int timeoutMs = 30000)`

`Complete` rules: missing helper → `helper-missing` and do not call the runner; timeout → `timed-out`; exit code other than 0, or empty/missing tmp → `failed`, delete an empty tmp, do not replace `finalPath`; exit 0 and non-empty tmp → move tmp onto `blackbox.dmp` and return `written`.

- [ ] **Step 1: Write the failing tests**

```csharp
using BlackBox.Core;
using FluentAssertions;

public class BlackBoxDumpSessionTests
{
    [Fact]
    public void Arguments_keep_a_spaced_path_as_one_token()
    {
        var args = DumpArguments.Format(12, 99, @"C:\Program Files\Mechabellum\UserData\BlackBox\blackbox.dmp.tmp");
        args.Should().Be("--pid 12 --started 99 --out \"C:\\Program Files\\Mechabellum\\UserData\\BlackBox\\blackbox.dmp.tmp\"");
    }

    [Fact]
    public void Missing_helper_does_not_run()
    {
        var runner = new Fake();
        var outcome = DumpSession.Complete("missing.exe", "--pid 1", "a.tmp", "a.dmp", runner);
        outcome.Status.Should().Be("helper-missing");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public void Timeout_is_timed_out()
    {
        var outcome = DumpSession.Complete("helper.exe", "--pid 1", "a.tmp", "a.dmp",
            new Fake(new DumpRunResult(false, null, true)));
        outcome.Status.Should().Be("timed-out");
    }

    [Fact]
    public void Nonzero_exit_deletes_empty_tmp_and_does_not_replace_dump()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var helper = Path.Combine(dir, "helper.exe");
        var tmp = Path.Combine(dir, "blackbox.dmp.tmp");
        var finalPath = Path.Combine(dir, "blackbox.dmp");
        File.WriteAllText(helper, "x");
        File.WriteAllBytes(tmp, Array.Empty<byte>());
        File.WriteAllBytes(finalPath, new byte[] { 1, 2 });

        var outcome = DumpSession.Complete(helper, "--pid 1", tmp, finalPath,
            new Fake(new DumpRunResult(false, 3, false)));

        outcome.Status.Should().Be("failed");
        File.Exists(tmp).Should().BeFalse();
        File.ReadAllBytes(finalPath).Should().Equal(1, 2);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Zero_exit_and_nonempty_tmp_replaces_dump()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-dump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var helper = Path.Combine(dir, "helper.exe");
        var tmp = Path.Combine(dir, "blackbox.dmp.tmp");
        var finalPath = Path.Combine(dir, "blackbox.dmp");
        File.WriteAllText(helper, "x");
        File.WriteAllBytes(tmp, new byte[] { 9, 8 });

        var outcome = DumpSession.Complete(helper, "--pid 1", tmp, finalPath,
            new Fake(new DumpRunResult(false, 0, false)));

        outcome.Status.Should().Be("written");
        outcome.Error.Should().BeEmpty();
        File.ReadAllBytes(finalPath).Should().Equal(9, 8);
        Directory.Delete(dir, true);
    }

    sealed class Fake : IDumpRunner
    {
        readonly DumpRunResult _result;
        public int Calls;
        public Fake() : this(new DumpRunResult(false, 0, false)) { }
        public Fake(DumpRunResult result) => _result = result;
        public DumpRunResult Run(string exePath, string arguments, int timeoutMs)
        {
            Calls++;
            return _result;
        }
    }
}
```

The missing-helper test uses a path that does not exist, so `Complete` must check `File.Exists` before calling the runner. The other tests create a real `helper.exe` text file so existence passes.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxDumpSessionTests -v q --nologo`

Expected: FAIL because `DumpSession` does not exist.

- [ ] **Step 3: Implement argument formatting and completion**

`DumpSession.cs`:

```csharp
namespace BlackBox.Core;

public readonly record struct DumpRunResult(bool ExeMissing, int? ExitCode, bool TimedOut);

public interface IDumpRunner
{
    DumpRunResult Run(string exePath, string arguments, int timeoutMs);
}

public readonly record struct DumpOutcome(string Status, string Error);

public static class DumpArguments
{
    public static string Format(int pid, ulong startedFileTime, string outputTmpPath) =>
        "--pid " + pid + " --started " + startedFileTime + " --out \"" + outputTmpPath + "\"";
}

public static class DumpSession
{
    public static DumpOutcome Complete(
        string helperPath,
        string arguments,
        string tmpPath,
        string finalPath,
        IDumpRunner runner,
        int timeoutMs = 30000)
    {
        if (!File.Exists(helperPath))
            return new DumpOutcome("helper-missing", "helper missing");

        var run = runner.Run(helperPath, arguments, timeoutMs);
        if (run.TimedOut)
            return new DumpOutcome("timed-out", "timed out");
        if (run.ExitCode != 0)
        {
            DeleteIfEmpty(tmpPath);
            return new DumpOutcome("failed", "exit " + run.ExitCode);
        }

        if (!File.Exists(tmpPath) || new FileInfo(tmpPath).Length == 0)
        {
            DeleteIfEmpty(tmpPath);
            return new DumpOutcome("failed", "empty dump");
        }

        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tmpPath, finalPath);
        return new DumpOutcome("written", "");
    }

    static void DeleteIfEmpty(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length == 0)
            File.Delete(path);
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxDumpSessionTests -v q --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add mods/BlackBox/BlackBox.Core/DumpSession.cs tests/MechabellumModManager.Tests/BlackBoxDumpSessionTests.cs
git commit -m "feat: complete BlackBox dump session without spawning a process"
```

---

### Task 5: Native dump helper

**Files:**
- Create: `mods/BlackBox/native/BlackBoxDump.c`
- Create: `mods/BlackBox/native/build.ps1`
- Modify: `.gitignore` — add `mods/BlackBox/**/BlackBoxDump.exe`

**Interfaces:**
- Consumes: the command line from `DumpArguments.Format`
- Produces: `BlackBoxDump.exe` next to the script output, not committed. Exit codes: `0` success, `1` bad args, `2` cannot open the process or creation `FILETIME` mismatches, `3` dump write failed. Flags and rights are the Global Constraints values. Parse with `CommandLineToArgvW` only.

- [ ] **Step 1: Add the ignore rule and the sources**

Append to `.gitignore`:

```
mods/BlackBox/**/BlackBoxDump.exe
```

`mods/BlackBox/native/BlackBoxDump.c`:

```c
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dbghelp.h>

#pragma comment(lib, "dbghelp.lib")

static int fail(int code, HANDLE process, HANDLE file, LPWSTR *argv)
{
    if (file && file != INVALID_HANDLE_VALUE) CloseHandle(file);
    if (process) CloseHandle(process);
    if (argv) LocalFree(argv);
    return code;
}

int wmain(void)
{
    int argc = 0;
    LPWSTR *argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) return 1;

    DWORD pid = 0;
    unsigned long long started = 0;
    const wchar_t *outPath = NULL;
    for (int i = 1; i < argc; i++)
    {
        if (wcscmp(argv[i], L"--pid") == 0 && i + 1 < argc) pid = (DWORD)_wtoi(argv[++i]);
        else if (wcscmp(argv[i], L"--started") == 0 && i + 1 < argc) started = _wcstoui64(argv[++i], NULL, 10);
        else if (wcscmp(argv[i], L"--out") == 0 && i + 1 < argc) outPath = argv[++i];
        else return fail(1, NULL, NULL, argv);
    }
    if (pid == 0 || started == 0 || outPath == NULL || outPath[0] == 0)
        return fail(1, NULL, NULL, argv);

    HANDLE process = OpenProcess(
        PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE, FALSE, pid);
    if (!process) return fail(2, NULL, NULL, argv);

    FILETIME createTime, exitTime, kernel, user;
    if (!GetProcessTimes(process, &createTime, &exitTime, &kernel, &user))
        return fail(2, process, NULL, argv);
    ULARGE_INTEGER got;
    got.LowPart = createTime.dwLowDateTime;
    got.HighPart = createTime.dwHighDateTime;
    if (got.QuadPart != started) return fail(2, process, NULL, argv);

    HANDLE file = CreateFileW(outPath, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return fail(3, process, NULL, argv);

    MINIDUMP_TYPE flags = MiniDumpNormal | MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules;
    BOOL ok = MiniDumpWriteDump(process, pid, file, flags, NULL, NULL, NULL);
    CloseHandle(file);
    CloseHandle(process);
    LocalFree(argv);
    if (!ok)
    {
        DeleteFileW(outPath);
        return 3;
    }
    return 0;
}
```

`build.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$cl = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio","$env:ProgramFiles\Microsoft Visual Studio" -Filter cl.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\cl.exe$' } |
    Select-Object -First 1
if (-not $cl) {
    Write-Error "cl.exe not found. Install the Windows SDK and MSVC x64 tools. BlackBoxDump.exe is not committed."
    exit 1
}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "BlackBoxDump.exe"
Push-Location $here
& $cl.FullName /nologo /O2 /W3 /DUNICODE /D_UNICODE BlackBoxDump.c /Fe:$out /link dbghelp.lib
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) { exit $code }
Write-Output $out
```

- [ ] **Step 2: Run the build**

Run: `powershell -NoProfile -File mods/BlackBox/native/build.ps1`

Expected: either `BlackBoxDump.exe` path printed, or a non-zero exit whose message contains `cl.exe not found`. Do not commit the exe. Do not call it against `Mechabellum.exe`.

- [ ] **Step 3: Commit**

```powershell
git add .gitignore mods/BlackBox/native/BlackBoxDump.c mods/BlackBox/native/build.ps1
git commit -m "feat: add out-of-process BlackBox dump helper"
```

---

### Task 6: Melon plugin

**Files:**
- Create: `mods/BlackBox/BlackBox/BlackBox.csproj`
- Create: `mods/BlackBox/BlackBox/BlackBoxPlugin.cs`
- Create: `mods/BlackBox/BlackBox.Core/HelperRelease.cs`
- Create: `tests/MechabellumModManager.Tests/BlackBoxHelperReleaseTests.cs`

Do not add the plugin project to `MechabellumModManager.sln`. `dotnet test` must stay green on a machine without the game.

**Interfaces:**
- Consumes: `StallPolicy`, `IncidentGate`, `QuitRequestState`, `HeartbeatPulse`, `IncidentWriter`, `DumpArguments`, `DumpSession`, `IDumpRunner`
- Produces:
  - `static bool HelperRelease.NeedsWrite(string destPath, byte[] payload)` — true when the file is missing or its SHA-256 differs
  - Plugin assembly name `BlackBox`, version `0.1.0`, `[assembly: MelonInfo(typeof(BlackBoxPlugin), "BlackBox", "0.1.0", "llxmod")]`, `[assembly: MelonGame("GameRiver", "Mechabellum")]` only if that attribute exists on this Melon build; if the compile fails on `MelonGame`, omit it
  - Record directory `{MelonUtils.GameDirectory}/UserData/BlackBox` or the 0.7.3 game-directory API that is the folder containing `Mechabellum.exe`

Plugin behavior, all in `BlackBoxPlugin.cs`:

1. `OnApplicationStart`: write the in-memory timestamp immediately with scene `unknown`; subscribe update and both quit events; start background thread `BlackBox.Watchdog` (`IsBackground = true`); cache top-level `Plugins\*.dll` and `Mods\*.dll` names, or `unknown` if the directory cannot be listed; extract the embedded helper when `HelperRelease.NeedsWrite` is true; `MelonLogger.Msg("BlackBox armed")` once.
2. `OnUpdate`: catch every exception. Call `HeartbeatPulse.Apply` with `Stopwatch.GetTimestamp()` and `Stopwatch.Frequency`. Scene and Unity version reads happen only inside the `readScene` / version delegates after the timestamp is committed. On a written heartbeat, call `QuitRequestState.OnHeartbeatWritten`.
3. `OnApplicationQuit` calls `OnQuitRequested`. `OnApplicationDefiniteQuit` calls `OnDefiniteQuit`.
4. Watchdog: write `heartbeat.txt` immediately, then every 30 seconds. Every loop sleeps 1 second, then `StallPolicy.Evaluate`. On `BeginIncident`, `IncidentGate.TryBegin`. Only the true result writes `summary.txt` with `dump: pending`, calls `DumpSession.Complete` (30 seconds), rewrites the summary with the outcome, and `MelonLogger.Msg` with `trigger` and the final `dump` value. `silentSeconds` is sampled at `TryBegin` success. Store the policy's `NextHealthySeconds`. When `BeginIncident` is false, store `NextPhase` (`Dumping` stays until `DumpFinished`, then `Disarmed`, then `Armed`). Map `StallPhase` onto `StallPhaseCodes` for the gate only.
5. `IDumpRunner` uses `CreateProcess` with the exe path and the argument string as one command line (the exe path quoted, then a space, then `DumpArguments.Format`). `CREATE_NO_WINDOW`. `WaitForSingleObject` for 30000 ms. On timeout, `TerminateProcess` the helper only, return `TimedOut`. Do not create `cmd.exe`.
6. `AppDomain.CurrentDomain.UnhandledException` and a chained `SetUnhandledExceptionFilter`: both call the same record method (`trigger` `unhandled-exception`, `silentSeconds` 0). The native filter always calls the previous filter afterward, including when `TryBegin` returns false. The `AppDomain` handler returns without calling a null delegate. Neither path calls Unity or `MiniDumpWriteDump`.
7. `OnUpdate` and the watchdog swallow their own exceptions. The watchdog logs once via `MelonLogger` and keeps looping.

`BlackBox.csproj` references only `$(GameRoot)\MelonLoader\net6\MelonLoader.dll` with `Private` false. Default `GameRoot` is `D:\steam\steamapps\common\Mechabellum`. Project-reference `BlackBox.Core`. Embed `mods/BlackBox/native/BlackBoxDump.exe` as `BlackBoxDump.exe` only when that file exists.

- [ ] **Step 1: Write the helper-release test**

```csharp
using BlackBox.Core;
using FluentAssertions;

public class BlackBoxHelperReleaseTests
{
    [Fact]
    public void Missing_file_needs_write_and_same_bytes_do_not()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-rel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "BlackBoxDump.exe");
        var payload = new byte[] { 1, 2, 3, 4 };
        HelperRelease.NeedsWrite(path, payload).Should().BeTrue();
        File.WriteAllBytes(path, payload);
        HelperRelease.NeedsWrite(path, payload).Should().BeFalse();
        HelperRelease.NeedsWrite(path, new byte[] { 1, 2, 3, 5 }).Should().BeTrue();
        Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxHelperReleaseTests -v q --nologo`

Expected: FAIL because `HelperRelease` does not exist.

- [ ] **Step 3: Implement `HelperRelease` and the plugin**

`HelperRelease.cs`:

```csharp
using System.Security.Cryptography;

namespace BlackBox.Core;

public static class HelperRelease
{
    public static bool NeedsWrite(string destPath, byte[] payload)
    {
        if (!File.Exists(destPath))
            return true;
        byte[] existing;
        try { existing = File.ReadAllBytes(destPath); }
        catch { return true; }
        return !SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(payload));
    }
}
```

`BlackBox.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>BlackBox</AssemblyName>
    <Version>0.1.0</Version>
    <GameRoot Condition="'$(GameRoot)' == ''">D:\steam\steamapps\common\Mechabellum</GameRoot>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\BlackBox.Core\BlackBox.Core.csproj" />
    <Reference Include="MelonLoader">
      <HintPath>$(GameRoot)\MelonLoader\net6\MelonLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="..\native\BlackBoxDump.exe" LogicalName="BlackBoxDump.exe"
                      Condition="Exists('..\native\BlackBoxDump.exe')" />
  </ItemGroup>
</Project>
```

`BlackBoxPlugin.cs` uses the behavior list above. Keep one `int _phase = StallPhaseCodes.Armed`, one `bool _dumpFinished`, one `QuitRequestState _quit`, one `long _lastBeat`, one `long _started`, and `string _scene = "unknown"`. `OnApplicationStart` sets `_started` and `_lastBeat` to `Stopwatch.GetTimestamp()` before any scene read. The watchdog writes heartbeat text through `IncidentWriter` immediately and when 30 seconds of `GetTimestamp` have elapsed. On `BeginIncident`, call `TryBegin`; on false, do not write. On true, sample silent seconds as `(GetTimestamp() - _lastBeat) / Frequency`, write summary `dump: pending`, run `DumpSession.Complete`, set `_dumpFinished = true`, rewrite the summary, and log `trigger` plus the outcome status. The next loop passes `_dumpFinished` into `StallPolicy`; when `NextPhase` is `Disarmed`, set `_phase = StallPhaseCodes.Disarmed` and `_dumpFinished = false`. `CreateProcess` command line is `"\"" + exe + "\" " + arguments` with `CREATE_NO_WINDOW` (`0x08000000`). Timeout is `WaitForSingleObject` returning `258`, then `TerminateProcess` on the helper only. The native filter delegate is stored in a static field. `SetUnhandledExceptionFilter` saves the previous pointer and the new filter calls it with `Marshal.GetDelegateForFunctionPointer` only when the pointer is not zero. Extract uses `Assembly.GetManifestResourceStream("BlackBoxDump.exe")`; if the stream is null, skip extract and let `DumpSession` return `helper-missing`.

- [ ] **Step 4: Test and build**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBox -v q --nologo`

Expected: PASS.

Run: `dotnet build mods/BlackBox/BlackBox/BlackBox.csproj -c Release -v q`

Expected: PASS when `D:\steam\steamapps\common\Mechabellum\MelonLoader\net6\MelonLoader.dll` exists. If that file is absent, stop and report that path; do not retarget the plugin to net8.

- [ ] **Step 5: Commit**

```powershell
git add mods/BlackBox/BlackBox.Core/HelperRelease.cs mods/BlackBox/BlackBox tests/MechabellumModManager.Tests/BlackBoxHelperReleaseTests.cs
git commit -m "feat: add BlackBox Melon plugin"
```

---

### Task 7: Diagnostics export

**Files:**
- Create: `src/MechabellumModManager/Services/BlackBoxExport.cs`
- Modify: `src/MechabellumModManager/Services/DiagnosticsExportService.cs` (call collect before the `env` dictionary is serialized; add `blackbox`)
- Modify: `src/MechabellumModManager/Services/DiagnosticsSummaryBuilder.cs` (add `## BlackBox`)
- Create: `tests/MechabellumModManager.Tests/BlackBoxExportTests.cs`

**Interfaces:**
- Consumes: `DiagnosticsExportService.Redact`, `DiagnosticsProbeBuilder.ReadTextShared`, existing `TryCopyText` 8 MB cap behavior by copying text through the same redact path
- Produces: `static BlackBoxCollectResult BlackBoxExport.Collect(string staging, string? gamePath, List<string> missing, bool redact)` where `BlackBoxCollectResult` is `readonly record struct(string Status, bool DumpIncluded)`

Status strings, copied verbatim from the spec: `absent`, `empty`, `heartbeat-only`, `summary`, `dump-only`, `summary-and-dump`.

Rules:

- No `UserData/BlackBox` directory: `absent`, `missing` adds `blackbox/`.
- Directory exists, no `heartbeat.txt` and no `summary.txt`, and no included dump: `empty`, `missing` adds `blackbox/summary.txt`.
- Heartbeat and no summary: `heartbeat-only`, `missing` adds `blackbox/summary.txt`. If an under-64 MB `.dmp` is also present, status is still not `heartbeat-only`; no summary plus included dump is `dump-only`.
- Summary and no included dump: `summary`.
- No summary and included dump: `dump-only`, `missing` adds `blackbox/summary.txt`.
- Summary and included dump: `summary-and-dump`.
- Copy `heartbeat.txt` and `summary.txt` as UTF-8 text, last 8 MB if larger, strong-redact when `redact` is true.
- Copy `blackbox.dmp` bytes unchanged when length is less than or equal to `64L * 1024 * 1024`. Above that, do not copy, `missing` adds `blackbox/blackbox.dmp (over 64MB)`.
- Never copy `blackbox.dmp.tmp`, `*.writing`, or `BlackBoxDump.exe`.
- A sharing violation on one file adds that zip-relative path to `missing` and continues.
- `environment.json` gets `blackbox` set to the status. `summary.md` gets `## BlackBox` with two to four lines: the status, whether a dump was included, and when a dump was included the sentence `blackbox.dmp is not redacted and may contain local paths and memory fragments.`
- `README.txt` gets that same sentence when `DumpIncluded` is true.
- Do not pass the status into `DiagnosisEngine`.

- [ ] **Step 1: Write the failing export tests**

`DiagnosticsExportServiceTests.Fixture` is private. Put a local fixture in `BlackBoxExportTests.cs`.

```csharp
using System.IO.Compression;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class BlackBoxExportTests
{
    [Fact]
    public void Missing_directory_is_absent()
    {
        using var fx = new Fixture();
        var result = Export(fx, Path.Combine(fx.Root, "game"), DiagnosticsRedactionMode.None);
        result.Success.Should().BeTrue();
        result.Missing.Should().Contain("blackbox/");
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"absent\"");
    }

    [Fact]
    public void Summary_is_redacted_and_dump_bytes_are_not()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        var user = Environment.UserName;
        File.WriteAllText(Path.Combine(box, "summary.txt"), "gamePath: C:\\Users\\" + user + "\\Games\n");
        File.WriteAllBytes(Path.Combine(box, "blackbox.dmp"), new byte[] { 9, 8, 7 });
        var result = Export(fx, game, DiagnosticsRedactionMode.Strong);
        result.Success.Should().BeTrue();
        Read(fx, "blackbox/summary.txt").Should().Contain(@"Users\<User>");
        Read(fx, "blackbox/summary.txt").Should().NotContain(user);
        ReadBytes(fx, "blackbox/blackbox.dmp").Should().Equal(9, 8, 7);
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"summary-and-dump\"");
        Read(fx, "README.txt").Should().Contain("not redacted");
        Read(fx, "summary.md").Should().Contain("## BlackBox");
    }

    [Fact]
    public void Oversize_dump_is_skipped()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        File.WriteAllText(Path.Combine(box, "summary.txt"), "dump: written\n");
        using (var stream = new FileStream(Path.Combine(box, "blackbox.dmp"), FileMode.CreateNew))
            stream.SetLength(64L * 1024 * 1024 + 1);
        var result = Export(fx, game, DiagnosticsRedactionMode.None);
        result.Missing.Should().Contain(m => m.Contains("over 64MB"));
        using var archive = ZipFile.OpenRead(Zip(fx));
        archive.GetEntry("blackbox/blackbox.dmp").Should().BeNull();
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"summary\"");
    }

    [Fact]
    public void Tmp_and_helper_are_not_zipped()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        File.WriteAllText(Path.Combine(box, "heartbeat.txt"), "scene: unknown\n");
        File.WriteAllText(Path.Combine(box, "blackbox.dmp.tmp"), "nope");
        File.WriteAllText(Path.Combine(box, "BlackBoxDump.exe"), "nope");
        var result = Export(fx, game, DiagnosticsRedactionMode.None);
        using var archive = ZipFile.OpenRead(Zip(fx));
        archive.GetEntry("blackbox/blackbox.dmp.tmp").Should().BeNull();
        archive.GetEntry("blackbox/BlackBoxDump.exe").Should().BeNull();
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"heartbeat-only\"");
        result.Missing.Should().Contain("blackbox/summary.txt");
    }

    static string Zip(Fixture fx) => Path.Combine(fx.Root, "out.zip");

    static DiagnosticsExportResult Export(Fixture fx, string game, DiagnosticsRedactionMode mode) =>
        new DiagnosticsExportService().ExportToFile(Zip(fx), new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = game,
            SessionLogText = "",
            AppVersion = "1.3.5",
            Redaction = mode,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

    static string Read(Fixture fx, string name)
    {
        using var archive = ZipFile.OpenRead(Zip(fx));
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    static byte[] ReadBytes(Fixture fx, string name)
    {
        using var archive = ZipFile.OpenRead(Zip(fx));
        using var stream = archive.GetEntry(name)!.Open();
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        return mem.ToArray();
    }

    sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "bb-export-" + Guid.NewGuid().ToString("N"));
        public PathsService Paths { get; }
        public Fixture()
        {
            Paths = new PathsService(Root);
            Paths.EnsureCreated();
        }
        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~BlackBoxExportTests -v q --nologo`

Expected: FAIL because `blackbox` is not in `environment.json`.

- [ ] **Step 3: Implement collection and wire it**

`BlackBoxExport.cs`:

```csharp
namespace MechabellumModManager.Services;

public readonly record struct BlackBoxCollectResult(string Status, bool DumpIncluded);

public static class BlackBoxExport
{
    public const long MaxDumpBytes = 64L * 1024 * 1024;
    const string DumpWarning =
        "blackbox.dmp is not redacted and may contain local paths and memory fragments.";

    public static string DumpWarningText => DumpWarning;

    public static BlackBoxCollectResult Collect(string staging, string? gamePath, List<string> missing, bool redact)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            missing.Add("blackbox/");
            return new BlackBoxCollectResult("absent", false);
        }

        var dir = Path.Combine(gamePath, "UserData", "BlackBox");
        if (!Directory.Exists(dir))
        {
            missing.Add("blackbox/");
            return new BlackBoxCollectResult("absent", false);
        }

        var hasHeartbeat = CopyText(staging, dir, "heartbeat.txt", "blackbox/heartbeat.txt", missing, redact);
        var hasSummary = CopyText(staging, dir, "summary.txt", "blackbox/summary.txt", missing, redact);
        var dumpIncluded = CopyDump(staging, dir, missing);

        if (!hasSummary)
            missing.Add("blackbox/summary.txt");

        var status = (hasSummary, dumpIncluded) switch
        {
            (true, true) => "summary-and-dump",
            (false, true) => "dump-only",
            (true, false) => "summary",
            (false, false) when hasHeartbeat => "heartbeat-only",
            _ => "empty"
        };
        return new BlackBoxCollectResult(status, dumpIncluded);
    }

    static bool CopyText(string staging, string dir, string name, string relative, List<string> missing, bool redact)
    {
        var source = Path.Combine(dir, name);
        if (!File.Exists(source))
            return false;
        try
        {
            var text = DiagnosticsProbeBuilder.ReadTextShared(source);
            if (redact)
                text = DiagnosticsExportService.Redact(text);
            var dest = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllText(dest, text);
            return true;
        }
        catch
        {
            missing.Add(relative);
            return false;
        }
    }

    static bool CopyDump(string staging, string dir, List<string> missing)
    {
        var source = Path.Combine(dir, "blackbox.dmp");
        if (!File.Exists(source))
            return false;
        try
        {
            if (new FileInfo(source).Length > MaxDumpBytes)
            {
                missing.Add("blackbox/blackbox.dmp (over 64MB)");
                return false;
            }
            var destDir = Path.Combine(staging, "blackbox");
            Directory.CreateDirectory(destDir);
            File.Copy(source, Path.Combine(destDir, "blackbox.dmp"), overwrite: true);
            return true;
        }
        catch
        {
            missing.Add("blackbox/blackbox.dmp");
            return false;
        }
    }
}
```

In `ExportToFile`, after the Melon log block and before `var env = new Dictionary<string, object?>`:

```csharp
var blackbox = BlackBoxExport.Collect(staging, request.GamePath, missing, redact);
```

Inside the dictionary initializer add `["blackbox"] = blackbox.Status`.

Change `DiagnosticsSummaryBuilder.Build` to add `string? blackboxStatus = null, bool blackboxDumpIncluded = false` after `diagnosis`. After the findings block append:

```csharp
sb.AppendLine("## BlackBox");
sb.AppendLine("- 状态：" + (string.IsNullOrEmpty(blackboxStatus) ? "absent" : blackboxStatus));
sb.AppendLine("- 转储已收入：" + (blackboxDumpIncluded ? "是" : "否"));
if (blackboxDumpIncluded)
    sb.AppendLine("- " + BlackBoxExport.DumpWarningText);
sb.AppendLine();
```

Pass the two arguments at the existing `Build` call. When `blackbox.DumpIncluded` is true, append `BlackBoxExport.DumpWarningText + "\n"` to the README string before it is written. Do not read or write `DiagnosisEngine`.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~BlackBox|FullyQualifiedName~DiagnosticsExportServiceTests" -v q --nologo`

Expected: PASS, including the existing export tests.

- [ ] **Step 5: Commit**

```powershell
git add src/MechabellumModManager/Services/BlackBoxExport.cs src/MechabellumModManager/Services/DiagnosticsExportService.cs src/MechabellumModManager/Services/DiagnosticsSummaryBuilder.cs tests/MechabellumModManager.Tests/BlackBoxExportTests.cs
git commit -m "feat: include BlackBox records in diagnostics export"
```

---

## Spec coverage

| Spec section | Task |
|--------------|------|
| 20 s / 60 s / quitting rules | Task 1 |
| `TryBegin` and 3-heartbeat quit cancel | Task 2 |
| Timestamp-before-scene, heartbeat and summary fields, atomic replace | Task 3 |
| pending / written / failed / timed-out / helper-missing, spaced `--out` | Task 4 |
| External minidump, flags, rights, creation time, no committed exe | Task 5 |
| Plugin subscriptions, watchdog, chained filter, one log line when armed and one when recording | Task 6 |
| Zip paths, 64 MB, redaction, `environment.json`, `summary.md`, `README.txt`, no diagnosis-code change | Task 7 |

Known accepted residuals in spec section 6 stay unhandled: pre-loop false dump, instant terminate leaving only heartbeat, sleep jumping the timestamp, no IL2CPP method names, dumps not redacted.

## Self-review

- No `TBD` / `TODO` / "similar to task N".
- `StallInput` field order is the same in Task 1 and Task 2.
- Dump status strings match spec section 4: `written`, `failed`, `timed-out`, `helper-missing`. `pending` is written by the plugin before `Complete`, not returned by `Complete`.
- Phase `Dumping` is stored by `TryBegin` only when an incident starts. Later phase changes use `StallDecision.NextPhase`.
