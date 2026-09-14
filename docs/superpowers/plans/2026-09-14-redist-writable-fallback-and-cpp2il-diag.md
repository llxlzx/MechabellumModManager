# Redist Writable Fallback + Cpp2IL Failure Diagnostics

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When Program Files `installer-redist` is not writable, fall back to LocalAppData so offline cpp2il/unity-deps can seed; when Il2Cpp generation fails, surface Melon `Failed to Download Cpp2IL` instead of a silent 180s timeout.

**Architecture:** Add `RedistDirectoryResolver` that probes write access and picks LocalAppData fallback. Wire Melon install to that path. Add `MelonGenerationFailureReader` (or static helpers on the generator) that parses `MelonLoader\Latest.log` and fail-fast during poll / enrich timeout messages.

**Tech Stack:** C# / .NET 8 WPF, xUnit + FluentAssertions

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Diagnosis-only root cause already accepted: Program Files Access Denied → no offline seed → GitHub Cpp2IL timeout.
- No commits unless user asks.
- TDD: failing test before production code for each behavior.
- Match existing Chinese hardcoded messages in `MelonLoaderAssemblyGenerator` (no new resx required for generator strings).

## File map

| File | Role |
|------|------|
| `src/.../Services/RedistDirectoryResolver.cs` | Resolve writable installer-redist path |
| `tests/.../RedistDirectoryResolverTests.cs` | Writability + fallback tests |
| `src/.../Services/MelonGenerationFailureReader.cs` | Parse Latest.log for Cpp2IL / INTERNAL FAILURE |
| `src/.../Services/MelonLoaderAssemblyGenerator.cs` | Fail-fast + enriched timeout |
| `tests/.../MelonLoaderAssemblyGeneratorTests.cs` | Timeout + early fail cases |
| `src/.../ViewModels/MainViewModel.cs` | Use resolver for Melon install EnsureAsync |

---

### Task 1: RedistDirectoryResolver

**Files:**
- Create: `src/MechabellumModManager/Services/RedistDirectoryResolver.cs`
- Test: `tests/MechabellumModManager.Tests/RedistDirectoryResolverTests.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (~2506)

**Interfaces:**
- Produces: `RedistDirectoryResolver.Resolve(string? preferredBaseDir = null, string? localAppDataRoot = null, Func<string, bool>? canWrite = null)` → absolute path under `installer-redist`
- Fallback: `{LocalAppData}\MechabellumModManager\installer-redist`

- [ ] **Step 1: Write failing tests** — preferred writable → primary; preferred not writable → LocalAppData fallback; creates fallback dir.

- [ ] **Step 2: Run tests — expect FAIL** (type missing)

- [ ] **Step 3: Implement resolver** — probe write via create+delete temp file; on false use LocalAppData; `Directory.CreateDirectory` on chosen path.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Wire MainViewModel** — replace `Path.Combine(AppContext.BaseDirectory, "installer-redist")` with `RedistDirectoryResolver.Resolve()`; log when fallback used (`AppendLog` Chinese one-liner).

---

### Task 2: MelonGenerationFailureReader + generator fail-fast

**Files:**
- Create: `src/MechabellumModManager/Services/MelonGenerationFailureReader.cs`
- Modify: `src/MechabellumModManager/Services/MelonLoaderAssemblyGenerator.cs`
- Test: `tests/MechabellumModManager.Tests/MelonLoaderAssemblyGeneratorTests.cs`

**Interfaces:**
- Produces: `MelonGenerationFailureReader.TryDescribe(string gamePath)` → `string?` human message
- Detect: `Failed to Download Cpp2IL` / `INTERNAL FAILURE` lines in `MelonLoader\Latest.log`

- [ ] **Step 1: Write failing tests** — reader maps Cpp2IL download failure; generator fails early when Latest.log has INTERNAL FAILURE before timeout; timeout message includes Cpp2IL hint when log has it.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement reader + poll check in EnsureAssembliesAsync** — each poll cycle call TryDescribe; on hit kill process and Fail with description; on timeout append description if present.

- [ ] **Step 4: Run tests — expect PASS**

---

### Task 3: Verify

- [ ] Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~RedistDirectoryResolver|FullyQualifiedName~MelonLoaderAssemblyGenerator" --no-restore` (restore if needed)
- [ ] Expected: all matching tests pass
