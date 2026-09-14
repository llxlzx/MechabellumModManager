# Settle confirm gate chain (logic)

Date: 2026-09-05

## Pipeline when user clicks「Steam 已切换到…，继续」

```
ConfirmManualBeta
  ├─ Game running? → warn + confirm; cancel keeps settle
  ├─ Steam running only? → log OK (do not block) — snapshot is read-only
  └─ DeployBoundProfileAndClearSettle
       ├─ Detect Ready? → else keep settle ("路径未就绪/仍在下载")
       ├─ ApplyProfile → else keep settle (deploy failed)
       └─ TrySnapshotSettledAcf
            ├─ Game running → keep settle (distinct notify)
            ├─ ACF not settled → keep settle ("清单未稳定，稍等再点")
            ├─ other snapshot fail → clear settle + deferred snapshot warn
            └─ success → clear settle
```

## Why Steam-open used to look like「未下完」

`TrySnapshotSettledAcf` refused when Steam was running, then UI mapped **all** snapshot failures to `NotifySettleBlockedAcfNotSettled`.

## Still requires Steam exit

Junction swap, write live ACF (`TrySilentSetBeta` / restore snapshot), folder move — disk writers.
