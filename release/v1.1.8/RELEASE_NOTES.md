# v1.1.8

- Mod update detection UI: library outdated badge, status column, per-row Update button, optional startup check
- Factory-default domestic COS mirror (`null` → COS; cleared `""` → GitHub only)
- Branch-switch harden: Steam-busy rollback keeps SessionOwned; Archive A volume/writable preflight; partial copy-delete residue journal
- Resumable large downloads; sha256 on all sources; mirror → originUrl → raw
- Fat Setup still embeds MelonLoader + UnityDependencies + Cpp2IL + .NET 8 offline

Build: `v1.1.8-mirror-default-branch-harden`
