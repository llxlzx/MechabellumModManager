# 单实例 Mutex — 设计规格

**日期：** 2026-09-06  
**状态：** 已实现（计划执行中）  
**背景：** 双开管理器会与游戏冷启动叠加重 I/O，且共用 AppData；用户要求第二开提示并退出。

## 目标

- UI 进程全局单实例（同用户会话）。
- 第二开：`MessageBox` 说明已在运行 → 确定 → 退出。
- **不**激活 / 置顶已有窗口。

## 非目标

- 跨用户 / 跨会话互斥。
- 把已有窗口拉到前台。
- 限制无界面 CLI（安装器依赖并行调用）。

## 方案

命名 `Mutex`（推荐；相对按进程名计数更稳，相对锁文件无崩溃残留）。

建议名称：`Local\MechabellumModManager.SingleInstance`（`Local\` = 当前会话）。

## 行为

1. `OnStartup` 先处理 seed / Melon CLI / sanitize 等参数；这些路径**不**抢 Mutex。
2. 进入 UI 路径后、创建 `MainWindow` / 组装 VM **之前**尝试创建 Mutex。
3. 若已存在：显示本地化文案 → `Shutdown(0)`（或非零均可，约定 `0`）→ return；不加载主界面。
4. 若获得所有权：持有 Mutex 至进程退出（字段持有，防 GC）；正常退出由 OS 释放。

## 文案

键名示例：`NotifyManagerAlreadyRunning`  
中文：`管理器已在运行。请使用已打开的窗口。`  
同步 en / ja / ru / de。

第二开时 Localization 可能尚未完全初始化：允许用固定中英 fallback，或在抢锁前用系统语言读 resx；实现计划里选一种并测。

## 测试

- 单元：Mutex 辅助「首次成功 / 二次失败」可测（不启 WPF）。
- CLI 参数路径：不创建 Mutex（现有 CLI 解析测试或轻量断言）。
- 手工：开两个 exe → 第二开弹窗后退出；第一个仍可用。

## 验收

- [ ] 已有 UI 实例时，再开 Setup/便携/Debug exe 均弹窗并退出，且不出现第二个主窗。
- [ ] `--seed-config` / Melon 安装 CLI / sanitize 可在 UI 已开时仍执行。
- [ ] 无「把第一个窗口置顶」逻辑。
