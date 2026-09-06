# 双服向导 WaitingDownloadB 零点击自动继续 — 设计规格

**日期：** 2026-09-06  
**状态：** 已确认；与空壳 Ready 硬门配套  
**策略：** 去掉「是/否」；等待期不占 Busy；就绪后自动 ArchiveB

## 自动继续条件（同时）

1. Steam 联接路径 `LooksLikeGameRoot`
2. ACF `LooksSettledForSnapshot`
3. Steam 客户端与游戏均未运行

## 结构

ArchiveA → WaitingDownloadB → 提示手动开 Steam → **释放 Busy** → 后台轮询 → 短 Busy：ArchiveB → Link → Settle  

teardown / CancelBusyWork / 离开 WaitingDownloadB → 取消轮询 CTS；单飞 ArchiveB。
