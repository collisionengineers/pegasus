import importlib.util
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location(
    "principal_corpus", Path(__file__).resolve().parents[1] / "build_principal_identification_corpus.py"
)
corpus = importlib.util.module_from_spec(spec)
spec.loader.exec_module(corpus)


class SnapshotLineEndingTests(unittest.TestCase):
    def test_normalized_snapshot_is_identical_across_checkout_line_endings(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "source.cs"
            snapshots = []
            for newline in (b"\n", b"\r\n", b"\r"):
                path.write_bytes(newline.join((b"first", b"second", b"")))
                snapshots.append(corpus.snapshot(
                    "source", "pegasus", root, path.name, "source-code", hash_mode="normalized-lf"
                ))
            self.assertEqual(snapshots[0], snapshots[1])
            self.assertEqual(snapshots[0], snapshots[2])
            self.assertEqual(13, snapshots[0]["bytes"])

    def test_raw_snapshot_preserves_exact_bytes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "source.bin"
            path.write_bytes(b"first\r\nsecond\r\n")
            snapshot = corpus.snapshot("source", "pegasus", root, path.name, "binary")
            self.assertEqual(15, snapshot["bytes"])
            self.assertEqual(corpus.sha256_file(path), snapshot["sha256"])
            self.assertNotEqual(corpus.sha256_file(path, "normalized-lf"), snapshot["sha256"])


if __name__ == "__main__":
    unittest.main()
