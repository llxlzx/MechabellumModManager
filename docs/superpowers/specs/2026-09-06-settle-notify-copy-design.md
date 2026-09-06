# Steam 结算弹窗文案澄清 — 设计

**日期：** 2026-09-06  
**状态：** 已确认（用户「继续」）

## 问题

`NotifyOpenSteamManuallyForSettle` 等提示写「挡住继续 / 点结算继续」，但弹窗只有「确定」，用户以为点确定即结算。

## 改动

1. 弹窗文案：说明「确定」只关提示；结算要去设置页点金色「…继续」。
2. 同步 `NotifyWizardDoneWaitingSteam`、`LogSettleSnapshotNoAutoSteam` 同类表述。
3. 不改结算逻辑 / Phase0 不自动开 Steam。
