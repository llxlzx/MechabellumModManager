# v1.2.1

- Installed-mods page silently refetches the catalog (60s throttle) so updates show without opening the workshop
- Catalog fetch is mirror + GitHub in parallel; freshest updatedAt wins (stale mirror can no longer hide updates)
- Manager update check also reads elease/latest.json from the repo tree (raw), in parallel with mirror and Release
- Thin Setup / MechabellumRedist path unchanged from 1.2.0

Build: `v1.2.1-library-autodetect-raw-pointer`
