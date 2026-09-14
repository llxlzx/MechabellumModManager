# China Offline Melon Fat Setup (Success Level B)

**Date:** 2026-09-05  
**Status:** Approved  
**Approach:** Fat Setup — embed MelonLoader + matching `UnityDependencies_*.zip` (+ .NET Desktop offline installers)

## Goal

Domestic players **without VPN / GitHub** can, after obtaining the Setup by any channel:

1. Install the manager  
2. Have MelonLoader written into the game folder  
3. Complete **first-run Il2Cpp assembly generation** offline  
4. Install / enable **local** mods  

Online workshop catalog and manager auto-update without VPN are **out of scope**.

## Non-goals

- Domestic CDN / Gitee auto-download mirrors  
- Pre-baking `Il2CppAssemblies` into the installer  
- Guaranteeing GitHub Release download without VPN (ops/docs only)  
- Changing overseas update channels  

## Problem

Melon’s official `MelonLoader.x64.zip` does **not** include `UnityDependencies_*.zip`.  
Il2CppAssemblyGenerator expects:

`{game}\MelonLoader\Dependencies\Il2CppAssemblyGenerator\UnityDependencies_{major.minor.patch}.zip`

(e.g. Unity `2022.3.62f3` → `UnityDependencies_2022.3.62.zip`)

Previously the manager forced `force_offline_generation = true`, so missing zips caused `INTERNAL FAILURE` / `UnityDependencies_*.zip does not Exist`.  
Wildcard “any UnityDependencies_*.zip ⇒ offline” is unsafe when the game Unity patch bumps.

## Design invariants

1. Set `force_offline_generation = true` only when assemblies already exist **or** the **exact** matching `UnityDependencies_{version}.zip` is on disk.  
2. Every Melon install/ensure path must attempt to **seed** that zip from Setup redist.  
3. Seed failure must be visible (log / UI); never silently claim offline-ready.  
4. Release `build-installer` hard-fails if Melon zip, at least one correctly named UnityDependencies zip, or .NET 8 offline installer is missing.  
5. Overseas behavior must not regress: no matching zip ⇒ allow online generation; do not lock offline on a wrong-version zip.

## Redist layout

```
installer/redist/
  melonloader/MelonLoader.x64.zip
  unity-deps/UnityDependencies_2022.3.62.zip   # Melon filename; may keep multiple versions
  dotnet8/windowsdesktop-runtime-8.*-win-x64.exe
  dotnet6/windowsdesktop-runtime-6.*-win-x64.exe  # required if offline acceptance needs it
```

- Not committed to git (same as Melon zip).  
- Copied into Setup as `{app}\installer-redist\` (existing Inno `redist\*` rule).  
- Upstream `LavaGang/Unity-Runtime-Libraries` files named `2022.3.62.zip` **must be renamed** before placing in `unity-deps/`.

## Runtime components

### Unity version resolve

Normalize `2022.3.62f3` → `2022.3.62`.  
Resolve from game files when possible; if resolve fails and redist contains **exactly one** `UnityDependencies_*.zip`, use that version with a warning log; otherwise do not seed / do not force offline.

### SeedUnityDependencies

After Melon framework is on a game root, copy the matching zip into `Il2CppAssemblyGenerator\`.  
Shared by: `InstallMelonLoaderCli`, in-app Install MelonLoader, `Install-MelonLoader.ps1`, dual-store ensure (both stores).

### MelonLoaderConfigOptimizer

`CanForceOfflineGeneration` must check **exact** zip name for resolved version (or assemblies present). Remove “any `UnityDependencies_*.zip`” wildcard.

## Release / ops

- Build gate: Melon + unity-deps + dotnet8 (and dotnet6 if required by acceptance).  
- Document: Setup may still need a domestic mirror for acquisition; **post-install** path is offline to B.  
- When Mechabellum Unity version bumps, refresh `unity-deps` and ship a new Setup.  
- Acceptance case F: disconnected first-run after seeding — if Melon still needs Cpp2IL (or other) packages, add them to redist and to the build gate before calling B done.

## Automated test coverage (unit / integration)

These xUnit suites exercise **logic and wiring only** — no real game launch, no disconnected network, no Il2Cpp assembly generation. They do **not** replace manual acceptance A–F below.

| Area | Tests | Covers (partial) |
|------|-------|------------------|
| Version normalize / zip naming | `UnityVersionNormalizerTests` | Redist rename rule (`2022.3.62f3` → `UnityDependencies_2022.3.62.zip`) |
| Version resolve from game | `UnityVersionResolverTests` | Read Unity string from `globalgamemanagers` |
| Seed from redist | `UnityDependenciesSeederTests` | Copy exact zip; skip when present; fail on mismatch / missing redist; single-zip fallback |
| Exact offline gate | `MelonLoaderConfigOptimizerTests` | **C** — wrong zip ⇒ `force_offline=false`; exact zip or assemblies ⇒ true |
| Install + seed wiring | `MelonLoaderDualStoreSyncTests`, `InstallMelonLoaderCliTests` | **B** (seed + cfg only) — zip copied into AG folder and `force_offline_generation=true` after install |
| Dual-store framework sync | `MelonLoaderDualStoreSyncTests.EnsureOnBothStores_*` | Loader presence on both folders (not seeding both / not first-run gen) |
| Build gate | Manual script check only | Melon + `unity-deps` + dotnet8 required at `build-installer` time |

**Not covered by automated tests:** disconnected first-run Il2Cpp generation (**A**, **B** end-to-end), dual-store seed + branch switch + gen (**D**), overseas online generation (**E**), Cpp2IL / extra AG package probe (**F**).

## Acceptance (manual checklist)

Run on a machine with Mechabellum installed. Do **not** mark done without executing the scenario.

| ID | Scenario | Expect | Auto-assisted |
|----|----------|--------|---------------|
| A | Offline + fat Setup + clean game | First launch generates assemblies; no UnityDependencies dialog | — |
| B | Offline + Melon present, zip missing; in-app Install MelonLoader | Seeds from `installer-redist`; generation succeeds | Seed + offline cfg only (`InstallMelonLoaderCliTests`, `MelonLoaderDualStoreSyncTests`) |
| C | Disk has `…62.zip`, game is `63` | `force_offline=false`; not stuck offline | Optimizer + seeder mismatch tests |
| D | Dual-folder | Both stores seeded; switch branch first gen OK | Framework sync only; seed both + gen **manual** |
| E | Online overseas, no/wrong local zip | Online generation still works; manager update unchanged | Optimizer allows online when no exact zip — **manual** verify |
| F | Offline probe for extra Melon AG downloads (Cpp2IL, etc.) | Document or bundle missing deps; extend build gate before calling B done | — |

### Residual probe (F) — procedure

1. Place all release redist files; build fat Setup; install on a clean or reset game folder.  
2. Seed UnityDependencies via Install Melon (or fresh Setup install).  
3. **Disconnect network** (airplane mode or firewall block on game + Melon).  
4. Launch game once; watch `MelonLoader/Latest.log` and Il2CppAssemblyGenerator output.  
5. If Melon requests another missing package under `{game}\MelonLoader\Dependencies\Il2CppAssemblyGenerator\` (commonly Cpp2IL-related), download/add to `installer/redist/`, extend `build-installer` gate, rebuild Setup, and re-run A–F as needed.  
6. Only call success level **B** done when step 4 completes generation with no network and no missing-package prompt.

## Overseas impact

Shared fat Setup: larger download only. Auto-update and Steam updates unchanged. Correct version matching protects overseas players after Unity patch bumps.
