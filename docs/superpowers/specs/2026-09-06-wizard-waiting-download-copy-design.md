# WaitingDownloadB 文案 / 状态条澄清 — 设计

**日期：** 2026-09-06  
**状态：** 已确认（用户批准）

## 问题

零点击改造后，提示仍写「无需点『是』」，但 UI 只有「确定」，用户不敢点、也不知下一步。

## 改动

1. `NotifyWizardWaitingDownloadAuto` / `TaskMessageWizardWaitingDownload`：去掉「是」；说明「确定」只关提示；强调退出 Steam。
2. 新增 `TaskMessageWizardWaitingExitSteam`：磁盘已完整但 Steam/游戏仍在跑时，任务条显示「下载已完成，请退出 Steam…」。
3. 轮询循环内刷新 sticky 任务条，使下完后文案即时切换。
4. 自动继续条件不变。
