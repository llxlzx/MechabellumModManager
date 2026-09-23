# BlackBox 游戏本体黑匣子设计

> **状态**：已确认，按 `docs/dev/superpowers/plans/2026-09-22-blackbox-flight-recorder.md` 实现  
> **日期**：2026-09-19  
> **仓库**：Mechabellum Mod Manager  
> **用户确认**：黑匣子（非实时面板）；文字摘要 + 小型转储；导出诊断自动收录；进程内看门狗；自检修正已接受（进程外转储、20 秒判定、退出不误报、不替换 Melon 异常处理）

---

## 1. 背景与目标

Windows 事件 1002（Application Hang）只说明 `Mechabellum.exe` 停止处理窗口消息后被关闭。它不包含模块、地址或调用栈。钢铁指挥官本体是 IL2CPP，栈在 `GameAssembly.dll` / `UnityPlayer.dll`，托管 `StackTrace` 看不到。

**目标**

- 提供一个 MelonLoader 插件 BlackBox：正常对局几乎不工作；主线程停止心跳约 20 秒后，留下文字摘要和一份小型 minidump。
- 管理器「导出诊断」把这两份记录打进现有 zip，下次诊断包不再只有事件日志。
- 转储用 WinDbg / Visual Studio 能看到 `GameAssembly.dll+偏移`。不承诺 C# 方法名。

**非目标**

- 不自动安装、不默认启用、不进目录上架（本 spec 只做插件源码和导出接入）。
- 不做游戏内面板，不每帧写日志，不做完整内存转储。
- 不在已经卡住的进程内部调用 `MiniDumpWriteDump`。
- 不替换 MelonLoader 的崩溃处理；原生访问冲突、栈溢出、fast-fail 不保证能抓到。
- 不改诊断引擎的判定码，不改纯净清理白名单（`UserData` 仍会随 Melon 目录一起被清理）。
- 不抓插件 `OnApplicationStart` 之前的卡死（例如 Il2Cpp 程序集生成阶段）。

---

## 2. 架构

源码在 `mods/BlackBox/`。`BlackBox.Core`（`net6.0`，不引用 MelonLoader、不引用 Unity）放判定和文本写入，测试项目直接引用它。`BlackBox` 插件只做订阅、线程和启动转储程序。`mods/BlackBox/native/BlackBoxDump.c` 由同目录 `build.ps1` 用 Windows SDK 的 `cl.exe` 编译；没有 `cl.exe` 时脚本失败并说明原因，不把 exe 提交进仓库。

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `BlackBoxPlugin`（MelonPlugin，`net6.0`，对齐 MelonLoader 0.7.3） | 订阅主线程心跳、启动后台线程、启动时释放转储程序 | 调用 `MiniDumpWriteDump`、判定公式、组诊断包 |
| `StallPolicy`（Core） | 纯函数：给定心跳年龄、退出标志和当前阶段，返回是否开始事件、下一阶段、下一档健康计时 | 线程、文件、Unity |
| `IncidentGate`（Core） | `TryBegin`：同一时刻只允许一个调用方进入 `Dumping`。用 `Interlocked` 实现，不把 30 秒等待包在锁里 | 写文件、启动进程 |
| `IncidentWriter`（Core） | 写 `heartbeat.txt` / `summary.txt` | 转储、导出 |
| `BlackBoxDump.exe` | 独立原生 x64 进程，对给定 PID 写小型 minidump 后退出 | 常驻、判断卡死、Unity |
| `DiagnosticsExportService` | 把 `UserData/BlackBox` 下的文本和 `.dmp` 收进 zip | 安装插件、解释栈 |

插件 DLL 放在游戏 `Plugins`。转储程序嵌在 DLL 里，首次启动（或哈希不一致）释放到 `UserData/BlackBox/BlackBoxDump.exe`。分发包是一个 MelonPlugin，不另做一个 UserData 包。

记录目录：`{游戏根目录}/UserData/BlackBox/`。游戏根目录是与 `Mechabellum.exe` 同级的目录（MelonLoader 0.7.3 的游戏根目录 API）。导出使用的 `GamePath` 与 `MelonLoader/Latest.log` 相同，因此换服后拿到的是当前这一边的记录。

---

## 3. 运行时行为

### 3.1 主线程

`OnApplicationStart`：

1. 立刻写一次心跳时间戳（场景名记为 `unknown`）。这是「见过心跳」的起点。
2. 订阅 `MelonEvents.OnUpdate` 与退出事件。
3. 启动后台线程，`IsBackground = true`，名称 `BlackBox.Watchdog`。
4. 列出 `Plugins` 与 `Mods` 顶层 `*.dll` 文件名，缓存进内存。列目录失败则摘要里写 `unknown`，不影响心跳。
5. 用 MelonLogger 打一行 `BlackBox armed`。此后正常对局不再打日志。

`OnUpdate` 每帧只做一次时间比较。距上次心跳不足 1 秒则立即返回。满 1 秒后：**先**用 `Volatile.Write` 写下 `Stopwatch.GetTimestamp()`，**再**尝试读取 Unity 版本和当前场景名。读取失败只保留上一次的字符串，不得跳过时间戳。看门狗用同一个 `GetTimestamp` / `Frequency` 算年龄。各自 `new Stopwatch()` 会对不上。`silentSeconds` 在 `TryBegin` 成功的那一刻取样，不把后面等待转储的时间算进去。

退出分两种。MelonLoader 0.7.3 的 `OnApplicationQuit` 只是退出请求，这次请求可以取消；`OnApplicationDefiniteQuit` 才是已经关不掉的关闭。

- `OnApplicationDefiniteQuit`：置粘性的「确定退出」。之后不再开始新事件。已经在等转储程序时，让这次等待走完，仍受 30 秒上限。
- `OnApplicationQuit`：只置「已请求退出」。之后若主线程又成功写下 3 次心跳，视为请求已取消，清掉该标志。不清的话，一次被取消的退出会让后面的真卡死永远不记。
- 确定退出，或退出请求还没被这 3 次心跳取消时，不开始新事件。

主线程不写磁盘，不调用转储 API。

### 3.2 后台线程

循环：睡眠 1 秒。线程启动后立刻写一次 `heartbeat.txt`，之后每 30 秒再覆盖一次。不得把「写心跳文件」和「判断卡住」合成同一次睡眠。

阶段只有 `Armed`、`Dumping`、`Disarmed`，初始为 `Armed`。每秒把「是否有过心跳、心跳年龄、是否正在退出、当前阶段、已连续健康多久、本次转储是否已结束」交给 `StallPolicy`。正在退出指：已经确定退出，或退出请求还没被 3 次心跳取消。策略返回「要不要开始事件」「下一阶段」和「下一档健康计时」，不写文件。规则：

- 阶段是 `Armed`，且没有心跳、或正在退出、或年龄小于 20 秒：不开始事件，保持 `Armed`。
- 有心跳、未退出、年龄大于等于 20 秒、阶段是 `Armed`：开始事件，下一阶段 `Dumping`。
- `Dumping` 且转储尚未结束：保持 `Dumping`，不开始第二次。
- `Dumping` 且转储已结束：下一阶段 `Disarmed`，健康计时从 0 起。
- `Disarmed` 且心跳年龄小于 3 秒并连续满 60 秒：下一阶段 `Armed`，不开始事件。健康计时的增加和清零都由策略返回，后台线程只保存返回值。
- `Disarmed` 且心跳年龄达到 3 秒：健康计时清零，保持 `Disarmed`。

看门狗和异常处理在策略返回「开始事件」之后都要过 `IncidentGate.TryBegin`。它只在阶段仍是 `Armed` 时用 `Interlocked` 改成 `Dumping` 并返回 true；`Dumping` 或 `Disarmed` 时返回 false。重新回到 `Armed` 之后可以再次成功。只有返回 true 的一方做下面的事。等转储的 30 秒不占着这把门。另一方拿到 false 就不再写摘要、不再启动第二个转储程序。阶段从 `Armed` 到 `Dumping` 只由 `TryBegin` 改，避免策略和门各写一次。

只有 `TryBegin` 成功时才做下面的事。

1. 阶段已由 `TryBegin` 写成 `Dumping`。
2. 整份重写 `summary.txt`，`dump` 为 `pending`。
3. 启动 `BlackBoxDump.exe`。
4. 最多等 30 秒。退出码 `0` 且临时文件非空：替换为 `blackbox.dmp`，再整份重写摘要，`dump` 为 `written`。失败、超时或程序不存在：`failed` / `timed-out` / `helper-missing`，带一行原因。超时则结束转储进程，不结束游戏。空的 `.tmp` 删除。
5. 下一秒把「转储已结束」交给策略，阶段变为 `Disarmed`。

健康指心跳年龄小于 3 秒。主线程约 1 秒写一次，后台约 1 秒读一次；若把健康定在 1.5 秒，调度一抖就永远无法重新武装。

### 3.3 未处理托管异常

订阅 `AppDomain.UnhandledException`。另外保存并链入已有的 `SetUnhandledExceptionFilter`：先完成本节的记录，再调用原来的过滤器。不得只装自己的过滤器。

异常路径同样先 `TryBegin`。失败则不再写文件、不再启动转储程序。原生过滤器无论成败都再调用保存下来的上一个过滤器。`AppDomain` 没有这个过滤器：失败时直接返回，成功时等转储结束后返回。不要调用空委托。确定退出期间 `TryBegin` 失败，因此不记录。

异常路径同样禁止进程内 `MiniDumpWriteDump`，禁止调用 Unity API。`TryBegin` 成功时的顺序是：写摘要、启动转储程序、最多等 30 秒。只有原生过滤器在这之后调用保存下来的上一个过滤器。`AppDomain` 路径没有上一个过滤器，等完或 `TryBegin` 失败后直接返回。这会推迟 Melon 自己的崩溃日志，换的是转储程序还看得到活着的进程。

原生崩溃、栈溢出、fast-fail 经常到不了这里。摘要里的 `trigger` 只有两种：`hang` 与 `unhandled-exception`。

### 3.4 转储程序

`BlackBoxDump.exe` 是不依赖 .NET 的 Windows x64 原生程序。只在已经判定卡住或异常之后由插件启动，正常对局不存在这个进程。

命令行由插件经 `CreateProcess` 传入。路径必须是单独的一段参数：游戏装在 `Program Files` 时，不引用会被空格切开。转储程序用 `CommandLineToArgvW` 解析，禁止按空格手切。

```text
BlackBoxDump.exe --pid <pid> --started <进程创建 FILETIME> --out <绝对路径\blackbox.dmp.tmp>
```

`--started` 是游戏进程的创建时间。`OpenProcess` 之后用 `GetProcessTimes` 核对，不一致则退出码 `2`，避免 PID 被重用后转储到别的进程。

退出码：`0` 成功，`1` 参数错误，`2` 打不开进程或创建时间不符，`3` 写转储失败。

`MiniDumpWriteDump` 的标志只有：

- `MiniDumpNormal`
- `MiniDumpWithThreadInfo`
- `MiniDumpWithUnloadedModules`

不用 `MiniDumpWithFullMemory`，不用 `MiniDumpWithIndirectlyReferencedMemory`，不用 `MiniDumpWithDataSegs`。目标是模块 + 偏移的栈，文件保持在几 MB 这一档。

进程句柄权限：`PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_DUP_HANDLE`。不带最后一项时，DbgHelp 复制线程句柄会失败，栈是空的。不请求 `PROCESS_ALL_ACCESS`。

---

## 4. 文件

目录：`UserData/BlackBox/`。

| 文件 | 内容 |
|------|------|
| `heartbeat.txt` | 覆盖写。`utc`、`pid`、`uptimeSeconds`、`unityVersion`、`scene` |
| `summary.txt` | 一次事件一份，覆盖上一份。见下方字段 |
| `blackbox.dmp.tmp` | 转储程序的输出。导出忽略 |
| `blackbox.dmp` | 仅在转储进程退出码为 0 且临时文件非空时，由插件替换得到 |
| `BlackBoxDump.exe` | 释放出的转储程序。导出忽略 |

文本编码 UTF-8。先写同目录临时文件，再替换目标：目标已存在时用 `File.Replace`，第一次写出时用 `File.Move`（`File.Replace` 在目标不存在时会失败）。

`summary.txt` 字段（每行 `key: value`）：

- `trigger`：`hang` 或 `unhandled-exception`
- `utc`
- `pid`
- `uptimeSeconds`
- `silentSeconds`：仅 `hang`；异常写 `0`
- `gamePath`：游戏根目录；强脱敏只按现有规则替换用户目录，不保证盘符路径消失
- `unityVersion`
- `scene`
- `workingSetMb`：读不到写 `unknown`
- `plugins`：启动时缓存的 DLL 文件名，分号分隔
- `mods`：同上
- `dump`：`pending`、`written`、`failed`、`timed-out`、`helper-missing`
- `dumpError`：无则空

强脱敏在导出时处理文本，不回写游戏目录里的原文件。

发生事件时 MelonLogger 再打一行，包含 `trigger` 和 `dump` 的最终值。

---

## 5. 导出诊断

在现有 `DiagnosticsExportService` 里增加只读收集。缺目录、缺文件、单文件失败都不让整包失败。

| Zip 内路径 | 规则 |
|------------|------|
| `blackbox/heartbeat.txt` | 文本复制，沿用现有 8 MB 截断与强脱敏 |
| `blackbox/summary.txt` | 同上 |
| `blackbox/blackbox.dmp` | 二进制原样复制。大于 64 MB 则不收入，`missing` 写 `blackbox/blackbox.dmp (over 64MB)` |
| 不收入 | `*.tmp`、`BlackBoxDump.exe`、其他文件 |

`environment.json` 增加 `blackbox`，取值：

- `absent`：目录不存在
- `empty`：目录在，但没有 `heartbeat.txt`，也没有 `summary.txt`
- `heartbeat-only`：有心跳、无摘要
- `summary`：有摘要、无转储（或转储被跳过、或转储超过 64 MB）
- `dump-only`：没有摘要，但有未超限的转储并已收入包内
- `summary-and-dump`：摘要和未超限的转储都在包内

未超限的 `.dmp` 一律复制，不要求同时有摘要。目录不存在时 `missing` 增加 `blackbox/`。没有摘要时增加 `blackbox/summary.txt`（含 `empty`、`heartbeat-only`、`dump-only`）。

`summary.md` 增加 `## BlackBox`，两到四行，说明上述状态。强脱敏时明确写：`blackbox.dmp` 未脱敏，可能含本机路径和内存片段。`README.txt` 在包内确有 `.dmp` 时加同一句。

不把 BlackBox 状态送进 `DiagnosisEngine`。

---

## 6. 错误处理

- 场景名 / Unity 版本读取失败：心跳时间戳仍然更新。
- 释放转储程序失败：看门狗继续写心跳和摘要，`dump` 为 `helper-missing`。
- 转储程序超时：结束转储进程，游戏进程继续；摘要 `dump` 为 `timed-out`。
- 转储进程非 0 退出：保留原因，删除空的 `.tmp`，不替换 `blackbox.dmp`。
- 导出时文件被占用：该文件记入 `missing`，继续打包。
- 插件内任何异常不得逃出 `OnUpdate`。后台线程的异常只写一次 MelonLogger，然后继续循环。

已知且接受的残余：

- 第一次进入主循环前，若主线程超过 20 秒没有回到 `OnUpdate`，可能写出一份转储，并把读条再拖长转储所花费的时间。60 秒健康心跳后才会再武装。
- 进程被瞬间 `TerminateProcess`、且尚未满 20 秒时，只有上一份 `heartbeat.txt`（最多落后 30 秒）。
- 睡眠唤醒后 `GetTimestamp` 可能跳过 20 秒，多写一份转储。不另做睡眠检测。
- 转储没有 IL2CPP 方法名，只有模块和偏移。
- 强脱敏不覆盖 `.dmp`。
- 对方没安装插件时，诊断包里没有 BlackBox 段，导出仍成功。

---

## 7. 测试

不启动游戏，不调用真实的 `MiniDumpWriteDump`。

`StallPolicy`（在 `BlackBox.Core`，由现有测试项目引用）：

- 从未心跳 → 不判定卡住
- 心跳年龄 19 秒 → 不判定
- 心跳年龄 20 秒、Armed、未退出 → 判定
- 确定退出，或退出请求尚未被 3 次心跳取消 → 不判定
- 退出请求之后又写下 3 次心跳 → 请求标志清除，之后仍可判定
- `IncidentGate.TryBegin` 连续两次：第一次 true，第二次 false
- Dumping / 刚结束 → 不再判定
- Disarmed 后心跳年龄一直小于 3 秒、满 60 秒 → 再次 Armed；调用方不得自己改健康计时
- 健康计时期间心跳年龄达到 3 秒 → 策略把健康计时归零

`DiagnosticsExportService`：

- 无 `UserData/BlackBox`：成功，`blackbox` 为 `absent`，`missing` 含 `blackbox/`
- 有摘要和小于 64 MB 的 `.dmp`：zip 内字节与源文件一致；强脱敏会改摘要中的 `C:\Users\<Name>\`，不改 `.dmp` 字节
- `.dmp` 大于 64 MB：包内无该文件，`missing` 含大小说明
- 存在 `.tmp` 与 `BlackBoxDump.exe`：两者都不在 zip 里

心跳「先写时间戳、后读场景」用一个可替换的场景读取委托测：读取抛异常时，时间戳仍前进。

---

## 8. 自检记录

| 项 | 处理 |
|----|------|
| 进程内 `MiniDumpWriteDump` 会死锁 | 只由外部 `BlackBoxDump.exe` 调用 |
| 8 秒误报读条，且转储会加长卡顿 | 改为 20 秒；一次事件一份；健康 60 秒后才再武装 |
| 看门狗若睡 30 秒会漏判 | 每秒判断；30 秒只用于覆盖 `heartbeat.txt` |
| 正常退出被当成卡死 | 只有确定退出是粘性的；可取消的退出请求在恢复 3 次心跳后清除 |
| 看门狗与异常线程同时开始转储 | `IncidentGate.TryBegin` 只放行一个；30 秒等待不占着这把门 |
| 两套 Stopwatch 把 20 秒算错 | 只用 `GetTimestamp` / `Frequency` |
| `silentSeconds` 含等待转储的时间 | 在 `TryBegin` 成功时取样 |
| 路径含空格被切碎；PID 重用 | 参数整段传递，`CommandLineToArgvW`，核对进程创建时间 |
| 复制线程句柄失败导致空栈 | 增加 `PROCESS_DUP_HANDLE` |
| 只有转储程序的目录被写成 absent | 记为 `empty` |
| 场景读取失败吞掉心跳 | 时间戳先写 |
| 替换 Melon 异常过滤 | 链入原过滤器，先记录再调用 |
| 转储过大被 64 MB 上限丢弃 | 不用间接内存 / 数据段标志 |
| 强脱敏与 `.dmp` 矛盾 | 明确不脱敏，摘要和 README 写明 |
| 方法名期望过高 | 只承诺 `GameAssembly.dll+偏移` |
| 导出路径与双服 | 与 `Latest.log` 同一 `GamePath` |
| 未安装插件 | 导出成功并记 `blackbox/` 缺失 |
| 程序集生成阶段卡死 | 非目标 |

无未决项。范围是一个插件加一处导出接入，可以单独做实现计划。
