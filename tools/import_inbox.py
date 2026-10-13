"""Build a catalog entry from an inbox grant by rehashing files already on disk.

import_grant does not write files, call git, call coscli, or use the network.
The command line can download only the grant's object keys with a caller-supplied
coscli config. That config must not be the user-profile .cos.yaml.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath

INBOX_BUCKET = "mmm-inbox-1312774738"
_CHUNK = 1024 * 1024
_FLOOR = re.compile(r"^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})$")


def assert_isolated_config(path: Path) -> None:
    """Reject the user-profile coscli config. Do not open the file."""
    if Path(path) == Path.home() / ".cos.yaml":
        raise ImportError("user profile .cos.yaml is refused")


def import_grant(grant, download_root, catalog, floor, claim) -> dict:
    """Return a catalog entry to merge. Refuse when recomputed bytes disagree."""
    checked_floor = _require_floor(floor)
    mod_id = grant["id"]
    if not claim and _catalog_has_id(catalog, mod_id):
        raise ImportError(f"existing id: {mod_id}")

    root = Path(download_root).resolve()
    verified = [_verify_object(grant, root, obj) for obj in grant.get("objects") or []]
    entry = _entry_from_verified(grant, verified)
    if checked_floor is not None:
        entry["minManagerVersion"] = checked_floor
    return entry


def download_commands(grant, download_root, config) -> list[list[str]]:
    """coscli cp for each listed object. No bucket listing, delete, or sync-mirror."""
    assert_isolated_config(config)
    root = Path(download_root)
    commands = []
    for obj in grant.get("objects") or []:
        relative = _relative(grant, obj)
        commands.append([
            "coscli",
            "-c",
            str(config),
            "cp",
            f"cos://{INBOX_BUCKET}/{obj['key']}",
            str(root / relative),
        ])
    return commands


def _require_floor(floor):
    if floor is None:
        return None
    if not isinstance(floor, str) or _FLOOR.fullmatch(floor) is None:
        raise ImportError(f"bad floor: {floor}")
    if any(int(part) > 65535 for part in floor.split(".")):
        raise ImportError(f"bad floor: {floor}")
    return floor


def _catalog_has_id(catalog, mod_id: str) -> bool:
    if not isinstance(catalog, dict):
        return False
    mods = catalog.get("mods")
    if not isinstance(mods, list):
        return False
    return any(isinstance(mod, dict) and mod.get("id") == mod_id for mod in mods)


def _relative(grant, obj) -> str:
    prefix = f"inbox/{grant['inviteId']}/{grant['id']}/"
    key = obj.get("key")
    if not isinstance(key, str) or not key.startswith(prefix):
        raise ImportError(f"path escape: {key}")
    relative = key[len(prefix):].replace("\\", "/")
    parts = PurePosixPath(relative).parts
    if (
        not relative
        or PurePosixPath(relative).is_absolute()
        or ":" in relative
        or any(part in ("", ".", "..") for part in parts)
    ):
        raise ImportError(f"path escape: {key}")
    return relative


def _locate(root: Path, relative: str) -> Path:
    candidate = root.joinpath(*PurePosixPath(relative).parts).resolve()
    if not candidate.is_relative_to(root):
        raise ImportError(f"path escape: {relative}")
    return candidate


def _verify_object(grant, root: Path, obj) -> dict:
    relative = _relative(grant, obj)
    path = _locate(root, relative)
    if not path.is_file():
        raise ImportError(f"missing file: {relative}")
    digest, size = _hash_file(path)
    declared = obj.get("sha256")
    declared_size = obj.get("size")
    if (
        not isinstance(declared, str)
        or digest != declared.strip().lower()
        or isinstance(declared_size, bool)
        or declared_size != size
    ):
        raise ImportError(f"hash mismatch: {relative}")
    return {
        "role": obj.get("role"),
        "relative": relative,
        "name": obj.get("name") if isinstance(obj.get("name"), str) else PurePosixPath(relative).name,
        "main": obj.get("main") is True,
        "sha256": digest,
        "size": size,
        "path": path,
    }


def _hash_file(path: Path) -> tuple[str, int]:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(_CHUNK), b""):
            digest.update(chunk)
            size += len(chunk)
    return digest.hexdigest(), size


def _hash_joined(paths: list[Path]) -> tuple[str, int]:
    digest = hashlib.sha256()
    size = 0
    for path in paths:
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(_CHUNK), b""):
                digest.update(chunk)
                size += len(chunk)
    return digest.hexdigest(), size


def _entry_from_verified(grant, verified: list[dict]) -> dict:
    mod_id = grant["id"]
    entry = {"id": mod_id, "name": grant.get("name") or ""}
    for field in ("summary", "version"):
        value = grant.get(field)
        if isinstance(value, str):
            entry[field] = value

    kind = grant.get("kind")
    if kind == "parts":
        parts = sorted(
            (item for item in verified if item["role"] == "part"),
            key=lambda item: PurePosixPath(item["relative"]).name,
        )
        joined_hash, joined_size = _hash_joined([item["path"] for item in parts])
        entry["sha256"] = joined_hash
        entry["size"] = joined_size
        entry["parts"] = [
            {
                "file": f"mods/{mod_id}/{item['relative']}",
                "sha256": item["sha256"],
                "size": item["size"],
            }
            for item in parts
        ]
    elif kind == "bundle":
        bundles = [item for item in verified if item["role"] == "bundle"]
        mains = [item for item in bundles if item["main"]]
        if len(mains) != 1:
            raise ImportError("hash mismatch: bundle main")
        main = mains[0]
        entry["file"] = f"mods/{mod_id}/{main['name']}"
        entry["sha256"] = main["sha256"]
        entry["size"] = main["size"]
        entry["bundle"] = [
            {
                "path": item["name"],
                "file": (
                    f"mods/{mod_id}/{item['name']}"
                    if item["main"]
                    else f"mods/{mod_id}/{item['relative']}"
                ),
                "sha256": item["sha256"],
                "size": item["size"],
            }
            for item in bundles
        ]
    else:
        dlls = [item for item in verified if item["role"] == "dll"]
        if len(dlls) != 1:
            raise ImportError("missing file: dll")
        dll = dlls[0]
        entry["file"] = f"mods/{mod_id}/{dll['relative']}"
        entry["sha256"] = dll["sha256"]
        entry["size"] = dll["size"]

    previews = [item for item in verified if item["role"] == "preview"]
    if len(previews) == 1:
        preview = previews[0]
        entry["preview"] = f"mods/{mod_id}/{preview['relative']}"
        entry["previewSha256"] = preview["sha256"]
    return entry


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Rehash an inbox grant and print a catalog entry.")
    parser.add_argument("--grant", required=True, type=Path)
    parser.add_argument("--download-root", required=True, type=Path)
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--floor", default=None)
    parser.add_argument("--claim", action="store_true")
    args = parser.parse_args(argv)
    assert_isolated_config(args.config)
    grant = json.loads(args.grant.read_text(encoding="utf-8"))
    for command in download_commands(grant, args.download_root, args.config):
        subprocess.run(command, check=True)
    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    entry = import_grant(grant, args.download_root, catalog, args.floor, args.claim)
    json.dump(entry, sys.stdout, ensure_ascii=False, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
