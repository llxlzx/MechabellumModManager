# Plan: Installer ACF sanitize + Melon gate

## Files

| File | Role |
|------|------|
| `Services/SteamAcfSnapshotSanitizer.cs` | Validate + delete dirty snapshots |
| `Services/SanitizeAcfSnapshotsCli.cs` | `--sanitize-acf-snapshots` |
| `App.xaml.cs` | Hook CLI |
| `tests/.../SteamAcfSnapshotSanitizerTests.cs` | TDD |
| `installer/scripts/Sanitize-AcfSnapshots.ps1` | Fallback |
| `installer/MechabellumModManager.iss` | Wire + Melon gate/strings |

## Tasks

1. RED/GREEN sanitizer tests + implementation  
2. CLI + App hook  
3. PS fallback + ISS post-install  
4. Melon `LooksLikeMelon` ≥0.7.3 + 5-lang strings  
5. `dotnet test` filter sanitizer (+ gate if touched)
