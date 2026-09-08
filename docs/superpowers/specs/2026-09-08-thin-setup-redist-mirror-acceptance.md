# Acceptance — thin Setup + MechabellumRedist (v1.2.0)

## Automated

```powershell
dotnet test -c Release
```

Expect RedistEnsureTests (candidate order, hash mismatch→origin, opt-out skips mirror, local skip) green.

## Ops before shipping

1. Stage files under `installer/redist\` (see `installer/redist/README.md`).
2. `.\tools\sync-mirror.ps1 -Bucket … -ModsRepo … -ReleaseDir .\release\v1.2.0` (redist ON by default).
3. `.\tools\verify-mirror.ps1 -BaseUrl https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com -ExpectVersion 1.2.0`
4. `.\installer\build-installer.ps1` → Setup should be **much smaller** than ~90 MB (no embedded Melon/.NET).

## Manual

| Case | Expect |
|------|--------|
| CN no VPN, fresh thin Setup, Melon+.NET checked | Post-install downloads from COS; Melon installs; log/sources show `mirror` |
| COS down / blocked | Falls back to GitHub / Microsoft; Melon or .NET still installs if origin reachable |
| Settings mirror cleared (`""`) | In-app Melon ensure does not hit COS host |
| Mod browse + download | Unchanged mirror→GitHub behavior |
| Portable zip | Still no redist; in-app Melon uses ensure |

## Do not claim done without

- `verify-mirror` green on `MechabellumRedist/manifest.json` + artifacts
- At least one real thin Setup post-install Melon path on a machine without prior fat redist
