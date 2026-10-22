"""Shared helpers for the skill helper-script tests.

The helper scripts live in hyphenated skill folders, so they are loaded by path.
Their main() is run in process with a patched argv, stdout and working
directory. Temporary repositories use a fixed identity and fixed dates, no
user or system git configuration and no line-ending conversion, so the
commit ids a golden prints repeat on every machine.
"""
from __future__ import annotations

import contextlib
import importlib.util
import io
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[3]
SKILLS = ROOT / ".agents" / "skills"

FIXED_DATE = "1767225600 +0000"
GIT_ENVIRONMENT = {
    "GIT_AUTHOR_NAME": "Fixture Author",
    "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
    "GIT_COMMITTER_NAME": "Fixture Author",
    "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
    "GIT_AUTHOR_DATE": FIXED_DATE,
    "GIT_COMMITTER_DATE": FIXED_DATE,
    "GIT_CONFIG_NOSYSTEM": "1",
}


def load_script(skill: str, name: str):
    """Import .agents/skills/<skill>/scripts/<name>.py without writing bytecode beside it."""
    path = SKILLS / skill / "scripts" / f"{name}.py"
    spec = importlib.util.spec_from_file_location(f"skill_{name}", path)
    module = importlib.util.module_from_spec(spec)
    previous, sys.dont_write_bytecode = sys.dont_write_bytecode, True
    try:
        spec.loader.exec_module(module)
    finally:
        sys.dont_write_bytecode = previous
    return module


@contextlib.contextmanager
def working_directory(path):
    previous = os.getcwd()
    os.chdir(path)
    try:
        yield
    finally:
        os.chdir(previous)


def run_main(module, argv, cwd=None):
    """Run module.main(); return (exit code, stdout, stderr).

    The scripts call sys.stdout.reconfigure, which a StringIO lacks, so stdout
    is a TextIOWrapper over bytes.
    """
    out = io.TextIOWrapper(io.BytesIO(), encoding="utf-8", newline="\n")
    err = io.StringIO()
    code = 0
    with contextlib.ExitStack() as stack:
        stack.enter_context(mock.patch.object(sys, "argv", [module.__name__, *argv]))
        stack.enter_context(mock.patch.object(sys, "stdout", out))
        stack.enter_context(mock.patch.object(sys, "stderr", err))
        if cwd is not None:
            stack.enter_context(working_directory(cwd))
        try:
            code = module.main()
        except SystemExit as stop:
            if stop.code is None:
                code = 0
            elif isinstance(stop.code, int):
                code = stop.code
            else:
                err.write(f"{stop.code}\n")
                code = 1
    out.flush()
    return code, out.buffer.getvalue().decode("utf-8"), err.getvalue()


def isolate_git(root: Path):
    """Start a patch of os.environ that hides user and system git configuration."""
    config = root / "gitconfig"
    config.write_text("", encoding="utf-8")
    patcher = mock.patch.dict(os.environ, {**GIT_ENVIRONMENT, "GIT_CONFIG_GLOBAL": str(config)})
    patcher.start()
    return patcher


def temporary_root() -> tuple[Path, tempfile.TemporaryDirectory]:
    """A temporary directory with its 8.3 short names resolved, as git prints long paths."""
    holder = tempfile.TemporaryDirectory(ignore_cleanup_errors=True)
    return Path(os.path.realpath(holder.name)), holder


class Repository:
    """A temporary git repository driven through the git command line.

    Run it under isolate_git, which leaves line endings unconverted: no
    system or user configuration reaches it.
    """

    def __init__(self, root: Path, *, initialise: bool = True):
        self.root = root
        if initialise:
            root.mkdir(parents=True)
            self.git("init", "-q", "-b", "main")

    def git(self, *args: str, cwd=None) -> str:
        result = subprocess.run(["git", *args], cwd=cwd or self.root, capture_output=True,
                                text=True, encoding="utf-8", errors="replace")
        if result.returncode != 0:
            raise AssertionError(f"git {' '.join(args)} failed: {result.stderr.strip()}")
        return result.stdout.strip()

    def write(self, relative: str, text: str) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(text.encode("utf-8"))

    def commit(self, message: str, files: dict[str, str] | None = None) -> str:
        for relative, text in (files or {}).items():
            self.write(relative, text)
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")

    def branch(self, name: str, start: str) -> None:
        self.git("checkout", "-q", "-b", name, start)


class SharedGitCase(unittest.TestCase):
    """One temporary directory and git environment for a whole class, for tests that only read it."""

    @classmethod
    def setUpClass(cls):
        cls.tmp, holder = temporary_root()
        cls.addClassCleanup(holder.cleanup)
        cls.addClassCleanup(isolate_git(cls.tmp).stop)


class IsolatedGitCase(unittest.TestCase):
    """Per-test temporary directory and git environment."""

    def setUp(self):
        self.tmp, holder = temporary_root()
        self.addCleanup(holder.cleanup)
        patcher = isolate_git(self.tmp)
        self.addCleanup(patcher.stop)


def lines(*text: str) -> str:
    """Lines joined with LF and a final LF, for fixture file contents."""
    return "\n".join(text) + "\n"


def short(sha: str) -> str:
    return sha[:12]


# A target and the pull requests that start from its base. Every file is five
# short lines so the pull requests change separate hunks (lines 1 and 5, or
# line 4) and merge cleanly, except the pairs named below.
def build_pull_requests(repo: Repository) -> dict[str, str]:
    """Create the shared fixture; return branch name -> commit id.

    base      the common ancestor
    target    main, which changes a.txt line 1 after the base
    pr1       a.txt line 4, adds one.txt
    pr2       b.txt, shared.txt line 1, 'my file.txt' line 1
    pr3       c.txt, shared.txt line 5, 'my file.txt' line 5
    pr4       shared.txt line 1 differently: conflicts with pr2
    pr5       a.txt line 1 differently: conflicts with the target alone
    pr6       stacked on pr1: a.txt line 4 again, adds six.txt
    """
    def five(stem: str) -> str:
        return lines(*(f"{stem} {n}" for n in range(1, 6)))

    base = repo.commit("base", {
        "a.txt": five("alpha"), "b.txt": five("bravo"), "c.txt": five("charlie"),
        "shared.txt": five("shared"), "my file.txt": five("m"),
    })

    def change(text: str, number: int, replacement: str) -> str:
        parts = text.splitlines()
        parts[number - 1] = replacement
        return lines(*parts)

    commits = {"base": base}
    repo.git("checkout", "-q", "-b", "pr1", base)
    commits["pr1"] = repo.commit("pr1", {"a.txt": change(five("alpha"), 4, "alpha 4 (pr1)"), "one.txt": "one\n"})
    repo.git("checkout", "-q", "-b", "pr6", "pr1")
    commits["pr6"] = repo.commit("pr6", {"a.txt": change(five("alpha"), 4, "alpha 4 (pr6)"), "six.txt": "six\n"})
    repo.git("checkout", "-q", "-b", "pr2", base)
    commits["pr2"] = repo.commit("pr2", {
        "b.txt": change(five("bravo"), 2, "bravo 2 (pr2)"),
        "shared.txt": change(five("shared"), 1, "shared 1 (pr2)"),
        "my file.txt": change(five("m"), 1, "m1 (pr2)"),
    })
    repo.git("checkout", "-q", "-b", "pr3", base)
    commits["pr3"] = repo.commit("pr3", {
        "c.txt": change(five("charlie"), 2, "charlie 2 (pr3)"),
        "shared.txt": change(five("shared"), 5, "shared 5 (pr3)"),
        "my file.txt": change(five("m"), 5, "m5 (pr3)"),
    })
    repo.git("checkout", "-q", "-b", "pr4", base)
    commits["pr4"] = repo.commit("pr4", {"shared.txt": change(five("shared"), 1, "shared 1 (pr4)")})
    repo.git("checkout", "-q", "-b", "pr5", base)
    commits["pr5"] = repo.commit("pr5", {"a.txt": change(five("alpha"), 1, "alpha 1 (pr5)")})
    repo.git("checkout", "-q", "main")
    commits["target"] = repo.commit("target", {"a.txt": change(five("alpha"), 1, "alpha 1 (target)")})
    return commits
