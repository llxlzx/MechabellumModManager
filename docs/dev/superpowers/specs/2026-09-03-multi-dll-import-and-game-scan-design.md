# 设计：多 DLL 分条导入 + 从游戏扫描 Mod

日期：2026-09-03  
状态：已批准（用户同意）

## A. 导入文件夹/Zip 多 DLL

- `Mods`/`Plugins`/无前缀 分组后，再按**每个入口 DLL**拆成独立库包（MelonMod / MelonPlugin，以 AssemblyInspector 为准；前缀可作 hint）。
- 同目录旁路非 DLL 文件（配置等）并入该 DLL 包；无法归类的依赖 DLL 并入同组第一个入口包，若无入口则整组保持原行为或按 UserLibs。
- `UserLibs`/`UserData` 仍按类型整包，不按 DLL 拆。

## B. 从游戏扫描

- 扫描 `{Game}/Mods`、`{Game}/Plugins` 下 `*.dll`，对每个调用 `ImportDll`（已存在相同内容哈希则跳过）。
- **不**自动勾进当前方案。
- 启动且游戏路径有效时自动扫描一次（库可已有内容，只补缺）；设置区提供「从游戏导入」按钮可手动再扫。
- 不扫描 UserLibs/UserData（避免噪音）；高风险仍走现有启发式。

## 非目标

- 不删除游戏内未托管文件。
- 不把「已初始化 MelonLoader」整目录当 Mod 导入。
