# v1.1.8 — Mod 更新检测与大文件下载 / Mod updates and large downloads

相对 GitHub 上的 **v1.1.7**。请用本 Setup 覆盖。  
Since GitHub **v1.1.7**. Overwrite with this Setup.

build=`v1.1.8-mod-download-scale`

## 中文

### 本版

- **Mod 更新检测。** 已安装版本落后于目录时标「可更新」，可一键覆盖安装。
- **大文件下载。** 去掉 80 MB 硬上限；按 `catalog.json` 的 `size` 做体积校验；空闲超时代替总时长超时；断点续传（Range + `.part`）；下载进度条与字节数。
- **全来源强制 sha256。** 取消 GitHub 域名豁免；镜像 / Release / raw 一律校验。
- **下载候选顺序：** 国内镜像 → `originUrl`（GitHub Release 全量）→ raw.githubusercontent.com。
- **支持 gzip 的 catalog。** 镜像上超 1 MB 的目录可带 `Content-Encoding: gzip`。
- **镜像工具：** `publish-mods-release.ps1`、增量 `sync-mirror.ps1`（热门子集 + 元数据跳过）、冷门 404 可容忍的 `verify-mirror.ps1`；托管安装包时改写 `setupUrl`。

### 使用注意

- 当前全量 Mod 仍很小；`mirror-hot.txt` 已列入全部条目，同步后等于全量镜像。
- 未在 GitHub 发 Release 前，`latest.json` 里的 `setupUrl` 指向未来的 `v1.1.8` 资源地址，检查更新在发版前不会生效。

## English

### This release

- **Outdated-mod detection** with one-click update.
- **Large downloads:** no 80 MB cap; catalog `size` bounds; idle timeout; resume; progress UI.
- **sha256 required for every source** (GitHub no longer exempt).
- **Candidates:** mirror → `originUrl` → raw.
- **Gzip catalogs** supported.
- **Mirror tooling:** publish Release assets, incremental hot-subset sync, partial verify; rewrite `setupUrl` when hosting Setup.

### Notes

- Until this tag is published on GitHub, in-app update checks will not see `v1.1.8`.