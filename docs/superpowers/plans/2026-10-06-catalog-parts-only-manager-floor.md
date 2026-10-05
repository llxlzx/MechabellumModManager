# 目录只按分块发布 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 现有目录 Mod 通过管理器安装或更新时，1.3.12 及更早会在下载整包时失败；1.3.13 及以后仍按分块下载成功；手动放入游戏的 DLL 不变。

**Architecture:** 第一段只改 MechabellumMods 的校验脚本和目录内容：每个小于 100MB 的条目增加 `mods/<id>/parts/0000`，去掉 `originUrl`，简介加版本前缀，整包 DLL 先留在仓库里。本仓库的直传改为只上传分块并在合并目录时保留 `parts`。第二段删除整包是 24 小时之后的人工步骤，本计划的代码任务不执行它。

**Tech Stack:** Python 3 目录脚本，C# / xUnit 直传计划。

**Working branch:** none (working tree only, no commits)

## Global Constraints

- 分块路径不得等于条目的 `file`。
- 不改条目 `sha256`，不删本地库已匹配的副本。
- 简介前缀接在现有文字前，后面一个空格。默认：`从管理器安装或更新需要 1.3.13 或更高版本。` en：`Installing or updating through the manager requires 1.3.13 or newer.` de：`Installation oder Aktualisierung über den Manager erfordert 1.3.13 oder neuer.` ja：`マネージャーからのインストールと更新には 1.3.13 以降が必要です。` ru：`Установка и обновление через менеджер требуют версию 1.3.13 или новее。`
- 第一段不删除仓库、Release、镜像上的整包。
- 带 `parts` 的条目不得被 `stamp_hashes.py --origin-base` 写回 `originUrl`。
- 直传上传列表不得包含 `MechabellumMods/<file>`。`MergeListedEntry` 必须抄 `parts` 并删除 `originUrl`。
- 不推送、不上传、不删除远程对象。

---

### Task 1: 校验脚本接受分块，并在整包还在时两边都核对

**Files:**
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\scripts\validate_catalog.py`
- Test: `D:\gongzuo\独立工作区\MechabellumMods\scripts\test_validate_catalog.py`

**Interfaces:**
- Consumes: 现有 `check_parts`、`sha256_of`、`LARGE_FILE_BYTES`
- Produces: `check_parts(mod, errors, root: Path = ROOT)`；整包缺失只在「没有可用分块」时报 `file not found`

- [ ] **Step 1: Write the failing test**

```python
import tempfile
import unittest
from pathlib import Path

import validate_catalog as v


def mod(root: Path, *, parts=True, whole=True, part_equals_file=False):
    dll = b"dll-bytes"
    rel = "mods/demo/Demo.dll"
    part_rel = rel if part_equals_file else "mods/demo/parts/0000"
    if whole:
        path = root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(dll)
    if parts:
        path = root / part_rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(dll)
    entry = {
        "id": "demo",
        "file": rel,
        "sha256": v.sha256_of(root / (part_rel if parts else rel)) if (parts or whole) else "ab" * 32,
        "size": len(dll),
    }
    if parts:
        entry["parts"] = [{"file": part_rel, "sha256": v.sha256_of(root / part_rel), "size": len(dll)}]
    return entry


class ValidatePartsTests(unittest.TestCase):
    def test_parts_alone_are_enough(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            errors: list[str] = []
            v.check_parts(mod(root, whole=False), errors, root)
            self.assertEqual(errors, [])

    def test_part_path_must_differ_from_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            errors: list[str] = []
            v.check_parts(mod(root, part_equals_file=True), errors, root)
            self.assertTrue(any("equals file" in e for e in errors))

    def test_whole_file_must_match_parts(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            entry = mod(root)
            (root / "mods/demo/Demo.dll").write_bytes(b"other")
            errors: list[str] = []
            v.check_whole_file(entry, errors, root)
            self.assertTrue(any("sha256 mismatch" in e for e in errors))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python scripts/test_validate_catalog.py` from `D:\gongzuo\独立工作区\MechabellumMods`

Expected: FAIL because `check_parts` does not take `root`, and `check_whole_file` does not exist.

- [ ] **Step 3: Write minimal implementation**

给 `check_parts` 增加 `root: Path = ROOT`。读块文件时用 `root / rel`，不要写死 `ROOT`。每个块路径规范化后若等于 `file`，追加 `id=...: parts[i] equals file`。

新增：

```python
def check_whole_file(mod: dict, errors: list[str], root: Path = ROOT) -> None:
    rel = mod.get("file")
    if not isinstance(rel, str) or not rel.strip():
        return
    path = root / rel.replace("\\", "/")
    has_parts = isinstance(mod.get("parts"), list) and len(mod.get("parts")) > 0
    if not path.is_file():
        if not has_parts:
            errors.append(f"id={mod.get('id')!r}: file not found: {rel}")
        return
    declared = mod.get("sha256")
    size = mod.get("size")
    if isinstance(declared, str) and sha256_of(path) != declared.strip().lower():
        errors.append(
            f"id={mod.get('id')!r}: sha256 mismatch for {rel} "
            f"(catalog={declared.strip().lower()}, file={sha256_of(path)})"
        )
    if isinstance(size, int) and not isinstance(size, bool) and path.stat().st_size != size:
        errors.append(
            f"id={mod.get('id')!r}: size mismatch for {rel} "
            f"(catalog={size}, file={path.stat().st_size})"
        )
```

`main` 里原来「文件不存在就报 file not found，除非 hosted_off_git」的分支改成调用 `check_whole_file`。大于 100MB 且仍有 https `originUrl`、仓库里没有整包的旧规则只留给没有 `parts` 的条目。有 `parts` 的条目走上面两条。

- [ ] **Step 4: Run test to verify it passes**

Run: `python scripts/test_validate_catalog.py` then `python scripts/validate_catalog.py`

Expected: 单元测试 PASS。现有 `catalog.json` 在还没改目录之前，`validate_catalog.py` 仍打印 `OK:`。

### Task 2: stamp_hashes 对分块条目不再写回 originUrl

**Files:**
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\scripts\stamp_hashes.py`
- Test: `D:\gongzuo\独立工作区\MechabellumMods\scripts\test_stamp_hashes.py`

**Interfaces:**
- Consumes: Task 1 的分块规则（块能拼回整包哈希）
- Produces: `joined_digest(root, parts) -> tuple[str, int]`；`origin_line(mod, origin_base, indent, ending) -> str | None`

- [ ] **Step 1: Write the failing test**

```python
import tempfile
import unittest
from pathlib import Path

import stamp_hashes as s


class StampPartsTests(unittest.TestCase):
    def test_joined_parts_match_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            a = root / "mods/demo/parts/0000"
            a.parent.mkdir(parents=True)
            a.write_bytes(b"abc")
            digest, size = s.joined_digest(root, [{"file": "mods/demo/parts/0000"}])
            self.assertEqual(size, 3)
            self.assertEqual(digest, s.sha256_of(a))

    def test_parts_entry_never_emits_origin(self):
        mod = {"id": "demo", "parts": [{"file": "mods/demo/parts/0000"}]}
        self.assertIsNone(s.origin_line(mod, "https://github.com/x/y/releases/download/t", "  ", "\n"))

    def test_plain_entry_still_emits_origin(self):
        mod = {"id": "demo", "file": "mods/demo/Demo.dll"}
        line = s.origin_line(mod, "https://github.com/x/y/releases/download/t", "    ", "\n")
        self.assertIn("originUrl", line)
        self.assertIn("mods__demo__Demo.dll", line)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python scripts/test_stamp_hashes.py` from `D:\gongzuo\独立工作区\MechabellumMods`

Expected: FAIL，`joined_digest` / `origin_line` 不存在。

- [ ] **Step 3: Write minimal implementation**

```python
def joined_digest(root: Path, parts: list) -> tuple[str, int]:
    blob = bytearray()
    for part in parts:
        rel = part["file"].replace("\\", "/")
        blob.extend((root / rel).read_bytes())
    return hashlib.sha256(blob).hexdigest(), len(blob)


def origin_line(mod: dict, origin_base: str | None, indent: str, ending: str) -> str | None:
    parts = mod.get("parts")
    if isinstance(parts, list) and parts:
        return None
    if not origin_base:
        return None
    rel = mod.get("file")
    if not isinstance(rel, str):
        return None
    return f'{indent}"originUrl": "{origin_base}/{asset_name(rel)}",{ending}'
```

`main` 里：条目带非空 `parts` 时，若整包文件在，哈希必须等于 `joined_digest`，不等则打印 ERROR 并返回 1；整包不在则用 `joined_digest` 作为要写入的 `sha256` 和 `size`，不要走「file not found」。带 `parts` 时 `origin_line` 返回 None，这时不要输出 `originUrl`，也不要从旧行里把 `originUrl` 抄回去。没有 `parts` 的条目仍按现在的行为：传了 `--origin-base` 就写新地址，没传就把旧的 `originUrl` 行原样留下。

- [ ] **Step 4: Run test to verify it passes**

Run: `python scripts/test_stamp_hashes.py`

Expected: PASS。不要对真实 `catalog.json` 执行 `stamp_hashes.py`，目录还没分块。

### Task 3: 第一段目录内容（整包先留着）

**Files:**
- Modify: `D:\gongzuo\独立工作区\MechabellumMods\catalog.json`
- Create: `D:\gongzuo\独立工作区\MechabellumMods\mods\<id>\parts\0000`（仅当前没有 `parts` 且 `size` < 100MB 的条目）

**Interfaces:**
- Consumes: Task 1 的校验
- Produces: 每条都有 `parts`、没有 `originUrl`、简介带前缀；整包 DLL 仍在 `file` 路径上

- [ ] **Step 1: 写一次性迁移并运行**

在 MechabellumMods 仓库用下面的脚本改目录。不要 `git rm` 整包，不要调用发布脚本。

```python
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] if False else Path(r"D:\gongzuo\独立工作区\MechabellumMods")
PREFIX = {
    None: "从管理器安装或更新需要 1.3.13 或更高版本。 ",
    "en": "Installing or updating through the manager requires 1.3.13 or newer. ",
    "de": "Installation oder Aktualisierung über den Manager erfordert 1.3.13 oder neuer. ",
    "ja": "マネージャーからのインストールと更新には 1.3.13 以降が必要です。 ",
    "ru": "Установка и обновление через менеджер требуют версию 1.3.13 или новее. ",
}

def prefix(text, lang):
    if not isinstance(text, str) or not text.strip():
        return text
    head = PREFIX[lang]
    return text if text.startswith(head.strip()) else head + text

catalog_path = ROOT / "catalog.json"
data = json.loads(catalog_path.read_text(encoding="utf-8"))
for mod in data["mods"]:
    mod.pop("originUrl", None)
    mod["summary"] = prefix(mod.get("summary"), None)
    locales = mod.get("locales") or {}
    for lang, loc in locales.items():
        if isinstance(loc, dict) and isinstance(loc.get("summary"), str):
            loc["summary"] = prefix(loc["summary"], lang)
    if not mod.get("parts"):
        rel = mod["file"].replace("\\", "/")
        src = ROOT / rel
        dest_rel = f"mods/{mod['id']}/parts/0000"
        dest = ROOT / dest_rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
        mod["parts"] = [{"file": dest_rel, "sha256": mod["sha256"], "size": mod["size"]}]
data["updatedAt"] = "2026-10-06T00:00:00Z"
catalog_path.write_text(json.dumps(data, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
```

`json.dumps` 会打乱手写格式。写完后只核对字段，不要求缩进与旧文件逐字节相同。`cut-guide` 已有 `parts`，脚本不得覆盖那些块，也不得删除 `mods/cut-guide/Mechabellum.Guide.TextHover.dll` 以外的已有文件；该 DLL 若不在仓库里就保持不在。

- [ ] **Step 2: 校验**

Run: `python scripts/validate_catalog.py` from `D:\gongzuo\独立工作区\MechabellumMods`

Expected: `OK:`。抽查一条小 Mod：`file` 仍指向原来的 DLL，该 DLL 还在；`parts[0].file` 是 `mods/<id>/parts/0000`，两边字节相同；条目没有 `originUrl`。`cut-guide` 的五块路径未变，也没有 `originUrl`。

不要推送，不要删 Release 附件，不要删镜像对象。

### Task 4: 直传只上传分块，合并时保留 parts

**Files:**
- Modify: `d:\gongzuo\钢铁指挥官mod管理器开发\src\MechabellumModManager\Services\MirrorCatalogPublisher.cs`
- Test: `d:\gongzuo\钢铁指挥官mod管理器开发\tests\MechabellumModManager.Tests\MirrorCatalogPublisherTests.cs`

**Interfaces:**
- Consumes: 现有 `Plan`、`MergeListedEntry`
- Produces: `Plan(...).Uploads` 含 `MechabellumMods/mods/<id>/parts/0000`，不含 `MechabellumMods/<file>`；`CatalogJson` 与 `EntryJson` 含单块 `parts` 且没有 `originUrl`；`MergeListedEntry` 抄 `parts` 并移除 `originUrl`

- [ ] **Step 1: Write the failing test**

在 `MirrorCatalogPublisherTests` 增加：

```csharp
[Fact]
public void Plan_uploads_one_part_and_drops_origin()
{
    var dll = WriteTemp(new byte[] { 1, 2, 3, 4 });
    try
    {
        var plan = MirrorCatalogPublisher.Plan(
            """{"updatedAt":"old","mods":[{"id":"cam","name":"Old","file":"mods/cam/old.dll","originUrl":"https://keep.example/a.dll"}]}""",
            new MirrorPublishInput
            {
                Id = "cam",
                Name = "Cam",
                DllPath = dll,
                UpdatedAt = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)
            });

        plan.Uploads.Select(u => u.RemoteKey).Should().Equal("MechabellumMods/mods/cam/parts/0000");
        var entry = JsonNode.Parse(plan.EntryJson)!.AsObject();
        entry.ContainsKey("originUrl").Should().BeFalse();
        entry["parts"]![0]!["file"]!.GetValue<string>().Should().Be("mods/cam/parts/0000");
        entry["parts"]![0]!["sha256"]!.GetValue<string>().Should().Be(plan.Sha256);
        entry["file"]!.GetValue<string>().Should().EndWith(".dll");
    }
    finally { File.Delete(dll); }
}

[Fact]
public void Merge_copies_parts_and_removes_origin()
{
    var sha = new string('a', 64);
    var merged = MirrorCatalogPublisher.MergeListedEntry(
        """{"mods":[{"id":"cam","name":"Old","file":"mods/cam/old.dll","originUrl":"https://keep.example/a.dll"}]}""",
        $$"""{"id":"cam","name":"Cam","file":"mods/cam/new.dll","sha256":"{{sha}}","size":4,"parts":[{"file":"mods/cam/parts/0000","sha256":"{{sha}}","size":4}]}""",
        "cam");
    var mod = JsonNode.Parse(merged)!["mods"]![0]!.AsObject();
    mod.ContainsKey("originUrl").Should().BeFalse();
    mod["parts"]![0]!["file"]!.GetValue<string>().Should().Be("mods/cam/parts/0000");
}
```

现有 `Plan_appends_a_new_mod_and_puts_catalog_after_the_files` 断言上传键是 `MechabellumMods/mods/NewMod/<dll 文件名>`。把它改成断言 `MechabellumMods/mods/NewMod/parts/0000`，并断言预览键仍是 `MechabellumMods/mods/NewMod/preview.png`。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter MirrorCatalogPublisherTests`

Expected: 新测试 FAIL。旧的上传键断言也会 FAIL。

- [ ] **Step 3: Write minimal implementation**

`Plan` 在写好 `file`、`sha256`、`size` 之后：

```csharp
entry.Remove("originUrl");
var partRel = $"mods/{id}/parts/0000";
entry["parts"] = new JsonArray
{
    new JsonObject
    {
        ["file"] = partRel,
        ["sha256"] = sha,
        ["size"] = dllInfo.Length
    }
};
var uploads = new List<MirrorUpload> { new(input.DllPath, "MechabellumMods/" + partRel) };
```

不要再把 `input.DllPath` 加到 `MechabellumMods/` + `fileRel`。预览上传保持不变。

`MergeListedEntry` 在写入 `sha256` 和 `size` 之后：若 `incoming["parts"]` 是数组，把它深拷贝到 `entry["parts"]`；然后 `entry.Remove("originUrl")`。块的 `file` 必须以 `mods/<id>/parts/` 开头，且不含 `..`，否则抛 `ValidationFailed`。

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter MirrorCatalogPublisherTests`

Expected: PASS。

### Task 5: 第二段删除整包（本轮不执行）

代码任务完成后停在这里。不要在执行 Task 1–4 的同一次会话里删除远程整包。

开始条件：GitHub raw 与镜像上的 `catalog.json` 都能用不带缓存的 GET 取到，且每条都有 `parts`、都没有 `originUrl`。从那一刻起满 24 小时，再删除：

- 仓库里每条 `file` 指向的整包（`cut-guide` 的整包若不在仓库里就跳过）
- GitHub Release 上的整包附件
- 镜像键 `MechabellumMods/<file>`

删除后三条地址都应为 404。有一处仍是 200 就还没关上。不要用已经发出的 1.3.13 直传覆盖这些 Mod。

