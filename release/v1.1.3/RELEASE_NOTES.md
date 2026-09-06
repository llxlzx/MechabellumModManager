# v1.1.3（预览）— 双服假死 / 解除 / Steam 结算 / 诊断 + Phase0 + 空壳假 Ready 硬门 + 向导零点击

## 本包增量（相对此前同号 1.1.3 预览包）

- **空壳仓 / 假 Ready 硬门**：激活仓缺游戏根（exe+GameAssembly）时自动退出 Ready、进待修/settle；ACF Corrupt/Missing/未结算或快照失败时**绝不**清 settle 升 Ready；settle 中仍可切到另一完整仓 / 解除双服。
- **Phase0 Steam 生命周期**：切服/结算不再自动 `steam://open`；退出时仅对客户端发 `steam://exit`，无效则强制结束客户端（含账户选择器）；忽略后台 `steamservice`，避免误拉起启动器。
- **双服向导 WaitingDownloadB 零点击**：ArchiveA 后不再弹「是/否」；等待期不占 Busy；联接路径完整 + ACF 已结算 + Steam/游戏已退出后自动 ArchiveB。
- 构建自本地 `master`（含 `BranchStoreHealth` / `LooksAcfHardFault` / 向导自动继续等提交）。

## 修复要点（本号累计）

1. **解除双服**：测服态会先切正式服并显示 Busy，再解除；避免 Steam `sharedassets` 损毁。
2. **Busy 关窗**：Busy / 切服中关窗改为确认后取消任务并退出进程，减少僵尸占用。
3. **Melon 程序集生成**：保留超时；增加约 30s 心跳日志；关窗可取消生成。
4. **Steam 结算门禁**：识别更新失败/卡住（如 `StateFlags=518`），硬提示先验证/等更新成功，禁止硬继续部署；Corrupt/Missing 等 HardFault 与快照失败同样保持结算。
5. **诊断包**：新增 `summary.md`（人话结论 + 建议下一步）与 `timeline.jsonl`；把整个 zip 发给维护者即可。
6. **切服 Steam 启停**（Phase0）：见上方「本包增量」。
7. **空壳假 Ready**（本包）：见上方「本包增量」。
8. **向导下载等待零点击**（本包）：见上方「本包增量」。

## UX（解除双服确认）

- 「是否删除另一服」默认「否」时，**否**为金色主按钮（与回车一致），避免误删。
- 解除/保留文案一次说清两种选择后果；保留后的提示改为中性说明，不再「先劝留再骂残留」。
