# 发布产物说明

本目录按版本存放本地整理好的发布文件，便于对照与上传 GitHub Releases。

## 目录结构（以 v1.0.2 为例（当前最新；v1.0.0 仍保留作历史对照））

```
release/
  README.md
  v1.0.2/
    安装包/
      MechabellumModManager_Setup_v1.0.2.exe   # 推荐：完整安装程序
      README.txt                               # 给最终用户：运行 Setup
    本体/
      MechabellumModManager.exe                # 便携版程序
      Assets/
      README.txt                               # 需 .NET 8 Desktop，双击 exe
    latest.json                                # 更新检查元数据（上传 Releases 时一并附上）
```

## 安装包 vs 本体

| 目录 | 给谁用 | 怎么用 |
|------|--------|--------|
| **安装包/** | 绝大多数最终用户 | 运行 `MechabellumModManager_Setup_*.exe`；内嵌 MelonLoader 离线包，并会提示缺少的 .NET 运行时 |
| **本体/** | 便携/开发对照 | 需已安装 **.NET 8 Desktop** Runtime；保持 `Assets` 与 exe 同目录，双击 `MechabellumModManager.exe` |

建议：普通用户只分发/下载 **安装包**；**本体** 仅作便携运行或排查对照。

## latest.json

- 可放在版本目录根（如 `v1.0.2/latest.json`）方便本地对照。
- 上传到 GitHub Release 时，资源文件名仍须为 `latest.json`（程序会按 `.../releases/latest/download/latest.json` 拉取）。
- 字段说明见 `docs/releasing.md` 与 `docs/latest.example.json`。
