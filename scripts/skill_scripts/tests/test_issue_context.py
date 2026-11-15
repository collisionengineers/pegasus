"""issue_context.py with canned glab and git answers: the stop codes, a golden and paths that hold spaces."""
import unittest
from unittest import mock

from skill_scripts_support import load_script, run_main

issue_context = load_script("investigate-issue", "issue_context")

BASE_SHA = "b" * 40
SPACED_PATHS = [
    "docs/external-component-documents/brego/New Text Document.txt",
    "reference/case_export/Final Format Example 02.json",
    "reference/rendererref1/Sample - Cash in Lieu Report.pdf",
    "reference/rendererref1/Sample - Contract Repair Report.pdf",
    "reference/rendererref1/Sample - Repairable Report.pdf",
    "reference/rendererref1/Sample - Total Loss Report.pdf",
]
TREE = [
    "docs/notes.md",
    "scripts/Test-Foo.ps1",
    "src/app/Helper.cs",
    "src/app/Widget.cs",
    "src/app/WidgetFactory.cs",
    "src/other/WidgetFactory.cs",
    *SPACED_PATHS,
]
GREP = {"WidgetFactory": ["src/app/WidgetFactory.cs", "src/other/WidgetFactory.cs"]}
PROJECT = "projects/acme%2Fwidgets"
COMMENT = {"system": False, "author": {"username": "helper"}, "created_at": "2026-10-03T10:00:00Z",
           "body": "Seen on the live estate."}
SYSTEM_NOTE = {"system": True, "author": {"username": "reporter"}, "created_at": "2026-10-02T09:00:00Z",
               "body": "changed the description"}


def issue(description, **overrides):
    value = {"iid": 4242, "title": "Widget rendering fails", "state": "opened",
             "web_url": "https://gitlab.com/acme/widgets/-/issues/4242", "author": {"username": "reporter"},
             "created_at": "2026-10-01T09:00:00Z", "updated_at": "2026-10-02T09:00:00Z", "closed_at": None,
             "labels": ["type:bug"], "assignees": [], "description": description}
    value.update(overrides)
    return value


def related_merge_request(iid, state="opened"):
    return {"iid": iid, "title": f"Item {iid}", "state": state,
            "web_url": f"https://gitlab.com/acme/widgets/-/merge_requests/{iid}"}


def mentioned_in_issue(number):
    return {"system": True, "author": {"username": "someone"}, "created_at": "2026-10-04T00:00:00Z",
            "body": f"mentioned in issue #{number}"}


class Canned:
    """glab and git as one issue sees them; records the commands it was asked."""

    def __init__(self, description, related=(), notes=(SYSTEM_NOTE, COMMENT), tree=TREE,
                 log="abc1234 2026-10-05 Change the widget\n", grep=GREP, **overrides):
        self.issue, self.related, self.notes = issue(description, **overrides), list(related), list(notes)
        self.tree, self.log, self.grep = tree, log, grep
        self.commands = []

    def run(self, args, check=True):
        self.commands.append(args)
        if args[:2] == ["git", "rev-parse"]:
            return BASE_SHA + "\n"
        if args[:2] == ["git", "ls-tree"]:
            return "".join(path + "\0" for path in self.tree)
        if args[:2] == ["git", "grep"]:
            # One pass over the tree: for each file in tree order, each match of each pattern, twice
            # over because a file with two matches prints both.
            identifiers = [args[i + 1] for i, arg in enumerate(args) if arg == "-e"]
            return "".join(f"origin/dev:{path}\0{identifier}\n" * 2
                           for path in self.tree for identifier in identifiers if path in self.grep.get(identifier, []))
        if args[:2] == ["git", "log"]:
            return self.log
        raise AssertionError(f"unexpected command {args}")

    def glab_json(self, args):
        self.commands.append(["glab", *args])
        path = args[1]
        if path == PROJECT:
            return {"path_with_namespace": "acme/widgets", "default_branch": "dev"}
        if path == f"{PROJECT}/issues/4242":
            return self.issue
        if path.startswith(f"{PROJECT}/issues/4242/notes?"):
            return self.notes
        if path.startswith(f"{PROJECT}/issues/4242/related_merge_requests?"):
            return self.related
        raise AssertionError(f"unexpected glab call {args}")

    def command(self, name):
        return next(args for args in self.commands if args[:2] == ["git", name])


def run_context(canned, *argv):
    with mock.patch.object(issue_context, "run", canned.run), \
            mock.patch.object(issue_context, "glab_json", canned.glab_json):
        return run_main(issue_context, list(argv) or ["4242", "--repo", "acme/widgets", "--base", "dev", "--no-fetch"])


def glab_paths(canned):
    return [args[2] for args in canned.commands if args[0] == "glab"]


BODY = ("Widget rendering fails.\n\nSee `src/app/Widget.cs` and `docs/gone.md`. The helper `Helper.cs` is involved,\n"
        "and `WidgetFactory` creates it. Also `IdentifierNotInAnyFile`.")


class IssueContextStopCodes(unittest.TestCase):
    def test_an_open_issue_nothing_references_exits_0(self):
        code, out, _ = run_context(Canned(BODY))
        self.assertEqual(0, code)
        self.assertNotIn("## Stop and ask", out)
        self.assertIn("Nothing references this issue.", out)

    def test_a_closed_issue_exits_2(self):
        code, out, _ = run_context(Canned(BODY, state="closed", closed_at="2026-10-06T00:00:00Z"))
        self.assertEqual(2, code)
        self.assertIn("## Stop and ask\n\n- issue is closed (closed 2026-10-06T00:00:00Z)", out)

    def test_an_open_merge_request_that_references_the_issue_exits_2(self):
        code, out, _ = run_context(Canned(BODY, related=[related_merge_request(77)]))
        self.assertEqual(2, code)
        self.assertIn("- open merge request !77 already references this issue: Item 77", out)
        self.assertIn("- merge request !77 (opened): Item 77 https://gitlab.com/acme/widgets/-/merge_requests/77", out)

    def test_merged_and_closed_merge_requests_and_other_issues_do_not_stop(self):
        related = [related_merge_request(78, state="merged"), related_merge_request(79, state="closed")]
        notes = [COMMENT, mentioned_in_issue(80), {"system": True, "body": "mentioned in merge request !78"}]
        code, out, _ = run_context(Canned(BODY, related=related, notes=notes))
        self.assertEqual(0, code)
        self.assertIn("- merge request !78 (merged): Item 78", out)
        self.assertIn("- merge request !79 (closed): Item 79", out)
        self.assertIn("- issue #80 (mentioned 2026-10-04T00:00:00Z)\n", out)
        self.assertNotIn("!78 (mentioned", out)


class IssueContextReads(unittest.TestCase):
    def test_the_issue_its_notes_and_its_merge_requests_are_read_from_the_encoded_project(self):
        canned = Canned(BODY)
        run_context(canned)
        self.assertEqual([f"{PROJECT}/issues/4242",
                          f"{PROJECT}/issues/4242/notes?sort=asc&order_by=created_at&per_page=100",
                          f"{PROJECT}/issues/4242/related_merge_requests?per_page=100"], glab_paths(canned))

    def test_without_base_the_default_branch_is_read_from_the_project(self):
        canned = Canned(BODY)
        code, out, _ = run_context(canned, "4242", "--repo", "acme/widgets", "--no-fetch")
        self.assertEqual(0, code)
        self.assertEqual(PROJECT, glab_paths(canned)[0])
        self.assertIn(f"- Base for investigation: `origin/dev` @ `{BASE_SHA}`", out)


class IssueContextGolden(unittest.TestCase):
    maxDiff = None

    def test_named_paths_bare_names_and_identifiers(self):
        canned = Canned(BODY)
        code, out, _ = run_context(canned)
        self.assertEqual(0, code)
        self.assertEqual(
            "# Issue #4242: Widget rendering fails\n"
            "\n"
            "- URL: https://gitlab.com/acme/widgets/-/issues/4242\n"
            "- State: opened\n"
            "- Author: reporter; opened 2026-10-01T09:00:00Z; updated 2026-10-02T09:00:00Z\n"
            "- Labels: type:bug\n"
            "- Assignees: none\n"
            f"- Base for investigation: `origin/dev` @ `{BASE_SHA}`\n"
            "\n"
            "## Body\n"
            "\n"
            f"{BODY}\n"
            "\n"
            "## Comments\n"
            "\n"
            "### helper at 2026-10-03T10:00:00Z\n"
            "\n"
            "Seen on the live estate.\n"
            "\n"
            "\n"
            "## Cross-references\n"
            "\n"
            "Nothing references this issue.\n"
            "\n"
            "## Paths the issue names, and commits on `origin/dev` since it was opened\n"
            "\n"
            "Named but not present at the base tip (moved, renamed or never existed): `docs/gone.md`\n"
            "- `Helper.cs` resolves to: `src/app/Helper.cs`\n"
            "- `Widget.cs` resolves to: `src/app/Widget.cs`\n"
            "- `WidgetFactory` resolves to: `src/app/WidgetFactory.cs`, `src/other/WidgetFactory.cs`\n"
            "\n"
            "These commits touched the named code after the issue was written, so its file:line claims may be stale:\n"
            "\n"
            "```\n"
            "abc1234 2026-10-05 Change the widget\n"
            "```\n"
            "\n"
            "Named code present at the tip: `src/app/Widget.cs`, `src/app/Helper.cs`, `src/app/WidgetFactory.cs`,"
            " `src/other/WidgetFactory.cs`\n", out)
        git_log = canned.command("log")
        self.assertEqual(["src/app/Widget.cs", "src/app/Helper.cs", "src/app/WidgetFactory.cs",
                          "src/other/WidgetFactory.cs"], git_log[git_log.index("--") + 1:])

    def test_the_tree_and_the_grep_hits_are_read_with_nul_separation(self):
        canned = Canned(BODY)
        run_context(canned)
        self.assertIn("-z", canned.command("ls-tree"))
        self.assertIn("-z", canned.command("grep"))


def git_greps(canned):
    return [args for args in canned.commands if args[:2] == ["git", "grep"]]


def resolved_lines(out):
    return [line for line in out.splitlines() if "` resolves to: " in line]


class IssueContextOneGrep(unittest.TestCase):
    def test_fifteen_identifiers_cost_one_git_grep_and_a_sixteenth_is_not_searched(self):
        names = [f"Identifier{n:02d}" for n in range(16)]
        canned = Canned("Names: " + " ".join(f"`{name}`" for name in names))
        code, _, _ = run_context(canned)
        greps = git_greps(canned)
        self.assertEqual(0, code)
        self.assertEqual(1, len(greps))
        self.assertEqual(names[:15], [greps[0][i + 1] for i, arg in enumerate(greps[0]) if arg == "-e"])

    def test_no_identifier_means_no_git_grep(self):
        canned = Canned("See `src/app/Widget.cs` and Helper.cs.")
        code, _, _ = run_context(canned)
        self.assertEqual(0, code)
        self.assertEqual([], git_greps(canned))

    def test_each_identifier_lists_the_files_that_hold_it_once_in_tree_order(self):
        grep = {"WidgetFactory": ["src/other/WidgetFactory.cs", "src/app/Widget.cs"],
                "HelperBuilder": ["src/app/Widget.cs", "src/app/Helper.cs"],
                "NeverFound": []}
        code, out, _ = run_context(Canned("`WidgetFactory`, `HelperBuilder` and `NeverFound`", grep=grep))
        self.assertEqual(0, code)
        self.assertEqual(["- `HelperBuilder` resolves to: `src/app/Helper.cs`, `src/app/Widget.cs`",
                          "- `WidgetFactory` resolves to: `src/app/Widget.cs`, `src/other/WidgetFactory.cs`"],
                         resolved_lines(out))

    def test_an_identifier_lists_at_most_eight_files_and_a_bare_name_five(self):
        tree = [f"src/part{n}/Common.cs" for n in range(10)] + [f"src/part{n}/Util.cs" for n in range(7)]
        grep = {"CommonWidget": [path for path in tree if path.endswith("Common.cs")]}
        code, out, _ = run_context(Canned("`CommonWidget` is used by Util.cs.", tree=tree, grep=grep))
        self.assertEqual(0, code)
        self.assertEqual(["- `Util.cs` resolves to: " + ", ".join(f"`src/part{n}/Util.cs`" for n in range(5)),
                          "- `CommonWidget` resolves to: " + ", ".join(f"`src/part{n}/Common.cs`" for n in range(8))],
                         resolved_lines(out))


def paths_section(out):
    return out.split("## Paths the issue names", 1)[1]


class IssueContextPathsWithSpaces(unittest.TestCase):
    def test_all_six_tracked_paths_with_spaces_resolve(self):
        body = "The samples:\n" + "\n".join(f"- `{path}`" for path in SPACED_PATHS)
        canned = Canned(body)
        code, out, _ = run_context(canned)
        section = paths_section(out)
        self.assertEqual(0, code)
        self.assertNotIn("Named but not present", section)
        self.assertIn("Named code present at the tip: " + ", ".join(f"`{path}`" for path in SPACED_PATHS), section)
        log = canned.command("log")
        self.assertEqual(SPACED_PATHS, log[log.index("--") + 1:])

    def test_a_backticked_span_with_no_slash_is_ignored(self):
        canned = Canned("See `Final Format Example 02.json` and `New Text Document.txt`.")
        code, out, _ = run_context(canned)
        self.assertEqual(0, code)
        self.assertIn("The issue names no file paths or known identifiers.", paths_section(out))
        self.assertNotIn("log", [args[1] for args in canned.commands if args[0] == "git"])

    def test_a_spaced_span_the_tree_does_not_track_is_not_listed_as_missing(self):
        code, out, _ = run_context(Canned("The file `reference/gone dir/Missing File.json` is not there."))
        self.assertEqual(0, code)
        self.assertNotIn("Named but not present", out)

    def test_a_backticked_command_still_names_the_path_inside_it(self):
        code, out, _ = run_context(Canned("Run `cat docs/notes.md` and `python scripts/Test-Foo.ps1 -Verbose`."))
        section = paths_section(out)
        self.assertEqual(0, code)
        self.assertNotIn("Named but not present", section)
        self.assertIn("Named code present at the tip: `docs/notes.md`, `scripts/Test-Foo.ps1`", section)

    def test_a_path_without_spaces_that_is_not_tracked_is_still_listed_as_missing(self):
        code, out, _ = run_context(Canned("The file `docs/gone.md` moved."))
        self.assertEqual(0, code)
        self.assertIn("Named but not present at the base tip (moved, renamed or never existed): `docs/gone.md`", out)

    def test_a_tracked_path_is_not_also_listed_by_its_unspaced_tail(self):
        tree = [*TREE, "my dir/sub/file.txt"]
        code, out, _ = run_context(Canned("Look at `my dir/sub/file.txt`.", tree=tree))
        section = paths_section(out)
        self.assertEqual(0, code)
        self.assertNotIn("Named but not present", section)
        self.assertIn("Named code present at the tip: `my dir/sub/file.txt`\n", section)


if __name__ == "__main__":
    unittest.main()
