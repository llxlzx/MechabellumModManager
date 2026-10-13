import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import import_inbox


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def write_object(root: Path, relative: str, payload: bytes) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


class ImportInboxTests(unittest.TestCase):
    def test_hash_mismatch_is_refused(self):
        payload = b"dll-bytes"
        digest = sha256_bytes(payload)
        grant = single_grant(digest)
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", b"tampered!")
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(grant, Path(tmp), {"mods": []}, None, claim=False)
        self.assertTrue(str(raised.exception).startswith("hash mismatch"))

    def test_author_floor_and_origin_are_ignored(self):
        payload = b"dll-bytes"
        digest = sha256_bytes(payload)
        grant = single_grant(digest)
        grant["minManagerVersion"] = "9.9.9"
        grant["originUrl"] = "https://evil.example/mod.dll"
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", payload)
            entry = import_inbox.import_grant(grant, Path(tmp), {"mods": []}, "1.3.16", claim=False)
            self.assertNotIn("originUrl", entry)
            self.assertEqual(entry["minManagerVersion"], "1.3.16")
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(grant, Path(tmp), {"mods": []}, "01.3.16", claim=False)
        self.assertTrue(str(raised.exception).startswith("bad floor"))

    def test_existing_catalog_id_without_claim_is_refused(self):
        payload = b"dll-bytes"
        grant = single_grant(sha256_bytes(payload))
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", payload)
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(
                    grant, Path(tmp), {"mods": [{"id": "quick-mark"}]}, None, claim=False)
        self.assertTrue(str(raised.exception).startswith("existing id"))

    def test_parts_hash_is_the_joined_file(self):
        first, second = b"aaaa", b"bbbb"
        grant = {
            "inviteId": "author1",
            "id": "quick-mark",
            "kind": "parts",
            "name": "Quick Mark",
            "objects": [
                {"key": "inbox/author1/quick-mark/parts/0000", "role": "part",
                 "size": 4, "sha256": sha256_bytes(first)},
                {"key": "inbox/author1/quick-mark/parts/0001", "role": "part",
                 "size": 4, "sha256": sha256_bytes(second)},
            ],
        }
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_object(root, "parts/0000", first)
            write_object(root, "parts/0001", second)
            entry = import_inbox.import_grant(grant, root, {"mods": []}, None, claim=False)
        self.assertEqual(entry["sha256"], sha256_bytes(first + second))
        self.assertEqual(entry["size"], 8)
        self.assertEqual(entry["parts"][0]["file"], "mods/quick-mark/parts/0000")
        self.assertNotIn("originUrl", entry)

    def test_bundle_hash_is_the_main_file_only(self):
        main, extra = b"main-dll", b"extra-dll"
        grant = {
            "inviteId": "author1",
            "id": "quick-mark",
            "kind": "bundle",
            "name": "Quick Mark",
            "objects": [
                {"key": "inbox/author1/quick-mark/payload/QuickMark.dll", "role": "bundle",
                 "name": "QuickMark.dll", "size": len(main), "sha256": sha256_bytes(main), "main": True},
                {"key": "inbox/author1/quick-mark/payload/Extra.dll", "role": "bundle",
                 "name": "Extra.dll", "size": len(extra), "sha256": sha256_bytes(extra), "main": False},
            ],
        }
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_object(root, "payload/QuickMark.dll", main)
            write_object(root, "payload/Extra.dll", extra)
            entry = import_inbox.import_grant(grant, root, {"mods": []}, None, claim=False)
        self.assertEqual(entry["sha256"], sha256_bytes(main))
        self.assertEqual(entry["size"], len(main))
        main_member = next(item for item in entry["bundle"] if item["path"] == "QuickMark.dll")
        self.assertEqual(entry["file"], main_member["file"])
        extra_member = next(item for item in entry["bundle"] if item["path"] == "Extra.dll")
        self.assertEqual(extra_member["file"], "mods/quick-mark/payload/Extra.dll")

    def test_user_profile_cos_yaml_is_refused(self):
        home = Path.home() / ".cos.yaml"
        with self.assertRaises(ImportError):
            import_inbox.assert_isolated_config(home)


def single_grant(digest: str) -> dict:
    return {
        "inviteId": "author1",
        "id": "quick-mark",
        "kind": "single",
        "name": "Quick Mark",
        "objects": [{
            "key": "inbox/author1/quick-mark/QuickMark.dll",
            "role": "dll",
            "size": 9,
            "sha256": digest,
        }],
    }
