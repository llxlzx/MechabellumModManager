# Single-Instance Mutex Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Second UI launch shows “already running” and exits; CLI helpers stay unrestricted; never activate the first window.

**Architecture:** Extract `SingleInstanceGate` (named Mutex). Call it from `App.OnStartup` only on the UI path after CLI arg handlers. Hold the Mutex for process lifetime. Localized MessageBox; no Win32 focus APIs.

**Tech Stack:** .NET 8 WPF, `System.Threading.Mutex`, xUnit + FluentAssertions, existing `LocalizationService` + `Strings*.resx`.

**Spec:** `docs/superpowers/specs/2026-09-06-single-instance-mutex-design.md`

## Global Constraints

- Mutex name: `Local\MechabellumModManager.SingleInstance`
- UI only; seed / Melon CLI / sanitize do **not** acquire
- No activate / bring-to-front of existing window
- MessageBox then `Shutdown(0)`; do not create `MainWindow` on failure
- Prefer resx key `NotifyManagerAlreadyRunning` in zh + en + ja + ru + de

## File map

| File | Responsibility |
|------|----------------|
| Create `Services/SingleInstanceGate.cs` | TryAcquire named Mutex; disposable hold |
| Modify `App.xaml.cs` | UI-path gate + MessageBox + Shutdown |
| Modify `Resources/Strings*.resx` | Notify string |
| Create `tests/.../SingleInstanceGateTests.cs` | First acquire / second fail / dispose releases |

---

### Task 1: `SingleInstanceGate` (TDD)

**Files:**
- Create: `src/MechabellumModManager/Services/SingleInstanceGate.cs`
- Create: `tests/MechabellumModManager.Tests/SingleInstanceGateTests.cs`

**Interfaces:**
- Produces: `public static class SingleInstanceGate`
- Produces: `public const string DefaultName = @"Local\MechabellumModManager.SingleInstance";`
- Produces: `public static bool TryAcquire(string name, out Mutex? held)` — true + non-null held when this process owns the mutex; false + null when another owner exists
- Produces: `public static bool TryAcquire(out Mutex? held)` → `TryAcquire(DefaultName, out held)`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Threading;
using FluentAssertions;
using MechabellumModManager.Services;

public class SingleInstanceGateTests
{
    [Fact]
    public void TryAcquire_first_succeeds_second_fails_until_dispose()
    {
        var name = @"Local\MechabellumModManager.SingleInstance.Test." + Guid.NewGuid().ToString("N");
        SingleInstanceGate.TryAcquire(name, out var first).Should().BeTrue();
        first.Should().NotBeNull();
        try
        {
            SingleInstanceGate.TryAcquire(name, out var second).Should().BeFalse();
            second.Should().BeNull();
        }
        finally
        {
            first!.Dispose();
        }

        SingleInstanceGate.TryAcquire(name, out var again).Should().BeTrue();
        again!.Dispose();
    }
}
```

- [ ] **Step 2: Run test — expect FAIL (type missing)**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~SingleInstanceGateTests" -v q
```

- [ ] **Step 3: Implement gate**

```csharp
using System.Threading;

namespace MechabellumModManager.Services;

public static class SingleInstanceGate
{
    public const string DefaultName = @"Local\MechabellumModManager.SingleInstance";

    public static bool TryAcquire(out Mutex? held) => TryAcquire(DefaultName, out held);

    public static bool TryAcquire(string name, out Mutex? held)
    {
        held = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        held = mutex;
        return true;
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```powershell
git add src/MechabellumModManager/Services/SingleInstanceGate.cs tests/MechabellumModManager.Tests/SingleInstanceGateTests.cs
git commit -m "feat: SingleInstanceGate named Mutex helper"
```

---

### Task 2: Wire `App.OnStartup` + i18n

**Files:**
- Modify: `src/MechabellumModManager/App.xaml.cs`
- Modify: `src/MechabellumModManager/Resources/Strings.resx` (+ en/ja/ru/de)

**Interfaces:**
- Consumes: `SingleInstanceGate.TryAcquire(out Mutex? held)`
- Field on `App`: `Mutex? _singleInstanceMutex;` keep reference until process exit

- [ ] **Step 1: Add strings**

`NotifyManagerAlreadyRunning`:
- zh: `管理器已在运行。请使用已打开的窗口。`
- en: `The manager is already running. Use the existing window.`
- ja / ru / de: equivalent short copy

- [ ] **Step 2: Gate UI path in `OnStartup`**

After the three CLI `TryParseArgs` blocks (and their early returns), **before** creating `MainWindow`:

```csharp
// After CLI handlers; before MainWindow
LocalizationService.Apply(LocalizationService.ResolveSystemLanguage());
if (!SingleInstanceGate.TryAcquire(out _singleInstanceMutex))
{
    MessageBox.Show(
        LocalizationService.T("NotifyManagerAlreadyRunning"),
        "Mechabellum Mod Manager",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    Shutdown(0);
    return;
}

base.OnStartup(e);
// ... existing MainWindow path unchanged
```

Notes:
- Move `base.OnStartup(e)` so it still runs once on success (today it is after exception handler setup — keep exception handler registration, then mutex, then `base.OnStartup` if not already called).
- Do **not** call any activate/focus API.
- CLI paths must remain above this block unchanged.

- [ ] **Step 3: Build + unit tests**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~SingleInstanceGateTests" -v q
dotnet build src/MechabellumModManager/MechabellumModManager.csproj -c Release -v q
```

- [ ] **Step 4: Commit**

```powershell
git add src/MechabellumModManager/App.xaml.cs src/MechabellumModManager/Resources/Strings*.resx
git commit -m "feat: block second UI instance with Mutex prompt"
```

---

### Task 3: Verification

- [ ] **Step 1: Confirm CLI still parses without mutex** — no code change if handlers remain above gate; optional smoke: run existing InstallMelonLoaderCli / Sanitize tests if present.

- [ ] **Step 2: Manual** — start one Release/Debug exe; start second; expect one MessageBox and one main window.

- [ ] **Step 3: Spec checkbox update** — mark acceptance items in design spec when verified.

---

## Spec coverage self-check

| Spec item | Task |
|-----------|------|
| Named Mutex `Local\…` | Task 1 |
| UI-only; CLI exempt | Task 2 order |
| MessageBox + exit; no MainWindow | Task 2 |
| No activate | Task 2 (omission) |
| i18n | Task 2 |
| Unit test first/second | Task 1 |
