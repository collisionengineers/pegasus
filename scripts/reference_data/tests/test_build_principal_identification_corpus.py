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


def write(root: Path, relative: str, content: bytes) -> Path:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return path


def mail(subject: str, headers: dict[str, str] | None = None) -> bytes:
    lines = ["From: sender@example.test", "To: desk@example.test", f"Subject: {subject}"]
    lines += [f"{name}: {value}" for name, value in (headers or {}).items()]
    return "\r\n".join(lines + ["", "Invented body."]).encode("utf-8")


class CorpusLayoutTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)

    def relative(self, paths):
        return sorted(path.relative_to(self.root).as_posix() for path in paths)

    def test_reads_each_code_folder_and_no_other_folder(self):
        for relative in (
            "KERR/email/new-instruction-received/handrolled/one.eml",
            "KERR/document/instruction/two.docx",
            "KERR/document/instruction/two.msg.md",
            "KERR/image/photo.jpg",
            "SWAN/document/instruction/three.pdf",
            "_reference/four.pdf",
            "_duplicates/five.eml",
            "_unattributed/six.eml",
            "holding/seven.eml",
        ):
            write(self.root, relative, relative.encode("utf-8"))
        self.assertEqual(
            [
                "KERR/document/instruction/two.docx",
                "KERR/email/new-instruction-received/handrolled/one.eml",
                "SWAN/document/instruction/three.pdf",
            ],
            self.relative(corpus.principal_evidence_paths(self.root)),
        )
        self.assertEqual(
            ["SWAN/document/instruction/three.pdf"],
            self.relative(corpus.principal_evidence_paths(self.root, "SWAN")),
        )

    def test_a_name_that_is_not_a_present_code_folder_is_refused(self):
        write(self.root, "holding/one.eml", b"invented")
        write(self.root, "KERR/document/instruction/two.pdf", b"invented")
        for name in ("holding", "kerr", "../KERR", "TEN"):
            with self.subTest(name=name):
                with self.assertRaises(ValueError):
                    corpus.principal_evidence_paths(self.root, name)

    def test_a_thread_keeps_one_bucket_whatever_the_category_folder(self):
        write(
            self.root,
            "KERR/email/new-instruction-received/handrolled/first.eml",
            mail("RE: Our ref AB12 345", {"In-Reply-To": "<root@example.test>"}),
        )
        write(
            self.root,
            "KERR/email/post-report-emails/handrolled/second.eml",
            mail("RE: RE: Our ref AB12 345", {"References": "<root@example.test> <other@example.test>"}),
        )
        files = corpus.principal_buckets(self.root, "KERR")["files"]
        self.assertEqual(2, len(files))
        groupings = [file["grouping"] for file in files]
        self.assertEqual({"thread-root"}, {grouping["basis"] for grouping in groupings})
        self.assertEqual(1, len({grouping["keySha256"] for grouping in groupings}))
        for grouping in groupings:
            self.assertEqual(int(grouping["keySha256"], 16) % 10, grouping["bucket"])
            self.assertEqual("holdout" if grouping["bucket"] in (0, 1) else "development", grouping["cohort"])

    def test_identical_bytes_in_two_categories_are_one_file_with_both_locations(self):
        content = mail("Invented subject")
        write(self.root, "KERR/email/new-instruction-received/handrolled/one.eml", content)
        write(self.root, "KERR/email/post-report-emails/handrolled/one.eml", content)
        files = corpus.principal_buckets(self.root, "KERR")["files"]
        self.assertEqual(1, len(files))
        self.assertEqual(
            [
                "KERR/email/new-instruction-received/handrolled/one.eml",
                "KERR/email/post-report-emails/handrolled/one.eml",
            ],
            files[0]["sourceLocations"],
        )

    def test_a_document_buckets_by_its_own_hash(self):
        path = write(self.root, "SWAN/document/instruction/one.docx", b"invented document")
        (file,) = corpus.principal_buckets(self.root, "SWAN")["files"]
        self.assertEqual("source-hash", file["grouping"]["basis"])
        self.assertEqual(
            corpus.sha256_text(f"source-hash:{corpus.sha256_file(path)}"),
            file["grouping"]["keySha256"],
        )


if __name__ == "__main__":
    unittest.main()
