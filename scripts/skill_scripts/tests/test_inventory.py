"""inventory.py with canned glab and git answers: the blockers, summarize_checks, a Markdown golden and --json-out."""
import hashlib
import json
import re
import subprocess
import types
import unittest
from collections import Counter
from unittest import mock

from skill_scripts_support import IsolatedGitCase, Repository, load_script, run_main, working_directory

inventory = load_script("combine-prs", "inventory")

OWNER, NAME = "acme", "widgets"
PROJECT = "projects/acme%2Fwidgets"
PROJECT_ID = 42
LONG_TITLE = "Add the widget gallery with sortable columns and a saved layout per operator"


def sha(label: str) -> str:
    return hashlib.sha1(label.encode()).hexdigest()


TARGET = sha("dev")


def merge_request(iid, target, source, **overrides):
    value = {
        "iid": iid, "title": f"MR {iid}", "web_url": f"https://gitlab.com/{OWNER}/{NAME}/-/merge_requests/{iid}",
        "state": "opened", "draft": False, "target_branch": target, "source_branch": source,
        "sha": sha(f"head {iid}"), "source_project_id": PROJECT_ID, "target_project_id": PROJECT_ID,
        "has_conflicts": False, "detailed_merge_status": "mergeable", "labels": [], "description": "",
        "author": {"username": "alice"},
    }
    value.update(overrides)
    return value


def note(author, body, **extra):
    return {"author": {"username": author}, "body": body, "system": False, "resolvable": False, "resolved": False,
            **extra}


def discussion(*notes):
    return {"id": "d", "individual_note": len(notes) == 1 and not notes[0]["resolvable"], "notes": list(notes)}


def job(name, status, **extra):
    return {"id": 1, "name": name, "status": status, "allow_failure": False, **extra}


class World:
    """glab and git as three merge requests see them: 1016 on dev, 1018 stacked on it, 1019 on dev.

    A merge request with a pipeline has parent pipeline iid*10; a child pipeline, when given, is iid*10+1
    behind the trigger job `lanes`. The fetch writes FETCH_HEAD in the directory it is given, one line per
    ref, as git does.
    """

    def __init__(self, directory):
        self.fetch_head = directory / "FETCH_HEAD"
        self.commands = []
        self.default_branch = "dev"
        self.mrs = {
            1016: merge_request(1016, "dev", "task/widget"),
            1018: merge_request(1018, "task/widget", "task/widget-gallery", title=LONG_TITLE),
            1019: merge_request(1019, "dev", "task/other"),
        }
        self.discussions = {
            1016: [
                discussion(note("bob", "Please\n  rename   this", resolvable=True,
                                position={"new_path": "a.txt", "new_line": 7})),
                discussion(note("bob", "done", resolvable=True, resolved=True,
                                position={"new_path": "b.txt", "new_line": 1})),
                discussion(note("carol", "ping")),
                discussion(note("alice", "my own note")),
                discussion(dict(note("alice", "changed the description"), system=True)),
            ],
        }
        self.closes = {1016: [900]}  # what closes_issues answers
        self.pipelines = {  # iid -> parent jobs and, behind a trigger job, child jobs
            1016: {"jobs": [job("unit", "success"), job("lint", "success")], "child": None},
            1018: {"jobs": [job("unit", "success")], "child": [job("sql", "failed")]},
        }
        self.files = {1016: ["a.txt", "shared.txt"], 1018: ["b.txt"], 1019: ["c.txt", "shared.txt"]}
        self.counts = {1016: "2\t3", 1018: "0\t1", 1019: "5\t0"}  # behind, ahead
        self.contained = {(sha("head 1016"), sha("head 1018"))}  # 1018's head contains 1016's
        self.fetched = {}
        self.register_diffs = {}   # number -> the -U0 diff of docs/rulings.md, when the MR changes it
        self.target_register = ""  # docs/rulings.md at the target

    def add_merge_request(self, number):
        """Another merge request on dev that changes its own file and has no pipeline."""
        self.mrs[number] = merge_request(number, "dev", f"task/extra-{number}")
        self.files[number] = [f"extra-{number}.txt"]
        self.counts[number] = "0\t1"

    def head(self, number):
        return self.mrs[number]["sha"]

    def number_of_head(self, head):
        return next(n for n in self.mrs if self.head(n) == head)

    def fetch(self, refs):
        url = f"https://gitlab.com/{OWNER}/{NAME}.git"
        lines = []
        for ref in refs:
            if ref.startswith("refs/merge-requests/"):
                number = int(ref.split("/")[2])
                lines.append(f"{self.fetched.get(number, self.head(number))}\t\t'{ref}' of {url}")
            else:
                lines.append(f"{TARGET}\t\tbranch '{ref.removeprefix('refs/heads/')}' of {url}")
        self.fetch_head.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))

    def run(self, args, check=True):
        self.commands.append(list(args))
        if args[:3] == ["git", "fetch", "--quiet"]:
            self.fetch(args[4:])
            return ""
        if args == ["git", "rev-parse", "--git-path", "FETCH_HEAD"]:
            return f"{self.fetch_head}\n"
        if args[:3] == ["git", "merge-base", "--independent"]:
            heads = args[3:]
            return "".join(f"{head}\n" for head in heads
                           if not any((head, other) in self.contained for other in heads))
        if args[:2] == ["git", "merge-base"]:
            return sha(f"merge-base {args[3]}") + "\n"
        if args[:3] == ["git", "rev-list", "--left-right"]:
            return self.counts[self.number_of_head(args[-1].split("...")[1])] + "\n"
        if args[:3] == ["git", "diff", "-U0"]:
            return self.register_diffs.get(self.number_of_head(args[5]), "")
        if args[:2] == ["git", "diff"]:
            return "\n".join(self.files[self.number_of_head(args[-1])]) + "\n"
        if args[:2] == ["git", "show"]:
            return self.target_register
        raise AssertionError(f"unexpected command {args}")

    def answer(self, path):
        if path == PROJECT:
            return {"path_with_namespace": f"{OWNER}/{NAME}", "default_branch": self.default_branch}
        match = re.fullmatch(rf"{re.escape(PROJECT)}/merge_requests/(\d+)(?:/(\w+))?(?:\?.*)?", path)
        if match:
            iid, part = int(match[1]), match[2]
            if part is None:
                pipeline = {"id": iid * 10, "status": "running"} if iid in self.pipelines else None
                return dict(self.mrs[iid], head_pipeline=pipeline)
            if part == "discussions":
                return self.discussions.get(iid, [])
            if part == "closes_issues":
                return [{"iid": number} for number in self.closes.get(iid, [])]
        match = re.fullmatch(rf"{re.escape(PROJECT)}/pipelines/(\d+)/(jobs|bridges)\?per_page=100", path)
        if match:
            pipeline, part = int(match[1]), match[2]
            iid, is_child = divmod(pipeline, 10)
            child = self.pipelines[iid]["child"]
            if is_child:
                return child if part == "jobs" else []
            if part == "jobs":
                return self.pipelines[iid]["jobs"]
            return [{"name": "lanes", "status": "running", "downstream_pipeline": {"id": pipeline + 1}}] if child else []
        raise AssertionError(f"unexpected glab api {path}")

    def glab_json(self, args):
        self.commands.append(["glab", *args])
        return json.loads(json.dumps(self.answer(args[1])))

    def is_ancestor(self, args):
        self.commands.append(list(args))
        return types.SimpleNamespace(returncode=0 if (args[-2], args[-1]) in self.contained else 1)


def glab_paths(world):
    return [call[2] for call in world.commands if call[0] == "glab"]


class InventoryCase(IsolatedGitCase):
    maxDiff = None

    def inventory(self, world, *extra, numbers=(1018, 1016, 1019)):
        with mock.patch.object(inventory, "run", world.run), \
                mock.patch.object(inventory, "glab_json", world.glab_json), \
                mock.patch.object(inventory, "subprocess", types.SimpleNamespace(run=world.is_ancestor)):
            return run_main(inventory, ["--target", "dev", "--repo", f"{OWNER}/{NAME}", *extra,
                                        *map(str, numbers)])

    @staticmethod
    def stop_list(out):
        return out.split("## Stop and ask\n\n", 1)[1].rstrip("\n").splitlines() if "## Stop and ask" in out else []


class InventoryOutput(InventoryCase):
    def test_three_merge_requests_with_no_blocker_exit_0_and_print_the_golden(self):
        world = World(self.tmp)
        code, out, _ = self.inventory(world)
        h = {number: world.head(number) for number in world.mrs}
        self.assertEqual(0, code)
        self.assertEqual(
            "# Merge request inventory for `dev`\n"
            "\n"
            f"Target `dev` @ `{TARGET}` in acme/widgets (default branch: `dev`).\n"
            "\n"
            "| Order | MR | Title | Base | Head ref | Head SHA | Ahead/behind | Merge status | Checks | Review | Flags |\n"
            "|---|---|---|---|---|---|---|---|---|---|---|\n"
            f"| 1 | !1016 | MR 1016 | dev | task/widget | `{h[1016][:12]}` | +3/-2 | mergeable"
            " | 2 ok / 0 pending / 0 failed | 1 unresolved | unresolved discussions |\n"
            f"| 2 | !1018 | Add the widget gallery with sortable columns and a saved la… | task/widget"
            f" | task/widget-gallery | `{h[1018][:12]}` | +1/-0 | mergeable | 1 ok / 0 pending / 1 failed"
            " | - | failing checks |\n"
            f"| 3 | !1019 | MR 1019 | dev | task/other | `{h[1019][:12]}` | +0/-5 | mergeable | none | - | - |\n"
            "\n"
            "## Stacks and ancestry\n"
            "\n"
            "- !1018 is stacked on !1016 (base `task/widget`)\n"
            "- !1018 already contains !1016's head\n"
            "\n"
            "## Review feedback to read before merging\n"
            "\n"
            "- !1016: 1 unresolved discussion(s), 1 comment(s) from others\n"
            "  - discussion `a.txt:7` bob: Please rename this\n"
            "  - comment carol: ping\n"
            "- !1018 failing checks: sql\n"
            "- !1018: no review feedback\n"
            "- !1019: no review feedback\n"
            "\n"
            "## Linked issues\n"
            "\n"
            "- !1016: #900\n"
            "- !1018: none linked\n"
            "- !1019: none linked\n"
            "\n"
            "## File overlap\n"
            "\n"
            "- !1016 × !1019: 1 file(s): `shared.txt`\n"
            "\n"
            "## Pinned arguments for simulate_merge.py and audit_combined.py\n"
            "\n"
            "```\n"
            f"--target {TARGET} 1016={h[1016]} 1018={h[1018]} 1019={h[1019]}\n"
            "```\n", out)

    def test_json_out_holds_the_order_the_blockers_and_the_derived_fields(self):
        world = World(self.tmp)
        path = self.tmp / "inventory.json"
        code, _, _ = self.inventory(world, "--json-out", str(path))
        data = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(0, code)
        self.assertEqual({"target": "dev", "targetSha": TARGET, "repo": "acme/widgets", "defaultBranch": "dev",
                          "order": [1016, 1018, 1019], "blockers": []},
                         {key: data[key] for key in ("target", "targetSha", "repo", "defaultBranch", "order",
                                                     "blockers")})
        derived = {mr["iid"]: {key: mr[key] for key in (
            "mergeBase", "ahead", "behind", "changedFiles", "checkSummary", "failedChecks", "unresolvedDiscussions",
            "closingIssues", "stackParent", "flags")}
            for mr in data["mergeRequests"]}
        self.assertEqual([1016, 1018, 1019], [mr["iid"] for mr in data["mergeRequests"]])
        self.assertEqual({
            1016: {"mergeBase": sha(f"merge-base {world.head(1016)}"), "ahead": 3, "behind": 2,
                   "changedFiles": ["a.txt", "shared.txt"], "checkSummary": "2 ok / 0 pending / 0 failed",
                   "failedChecks": [], "unresolvedDiscussions": 1, "closingIssues": [900], "stackParent": None,
                   "flags": ["unresolved discussions"]},
            1018: {"mergeBase": sha(f"merge-base {world.head(1018)}"), "ahead": 1, "behind": 0,
                   "changedFiles": ["b.txt"], "checkSummary": "1 ok / 0 pending / 1 failed",
                   "failedChecks": ["sql"], "unresolvedDiscussions": 0, "closingIssues": [], "stackParent": 1016,
                   "flags": ["failing checks"]},
            1019: {"mergeBase": sha(f"merge-base {world.head(1019)}"), "ahead": 0, "behind": 5,
                   "changedFiles": ["c.txt", "shared.txt"], "checkSummary": "none",
                   "failedChecks": [], "unresolvedDiscussions": 0, "closingIssues": [], "stackParent": None,
                   "flags": []},
        }, derived)
        self.assertEqual("Please\n  rename   this", data["mergeRequests"][0]["discussions"][0]["notes"][0]["body"])

    def test_closing_keywords_in_the_description_are_linked_beside_closes_issues(self):
        # closes_issues answers nothing for a target that is not the default branch; the description still says.
        world = World(self.tmp)
        world.default_branch = "main"
        world.mrs[1016]["description"] = "Fixes #900.\n\nCloses #905, #906 and #907; see #908."
        world.mrs[1019]["description"] = "resolves: #910"
        _, out, _ = self.inventory(world)
        self.assertIn("- !1016: #900, #905, #906, #907\n- !1018: none linked\n- !1019: #910\n", out)
        self.assertIn("\nClosing keywords only fire on `main`; issues for `dev` need closing by hand.\n", out)


class InventoryBlockers(InventoryCase):
    def assert_stops(self, mutate, expected_flag, expected_blocker):
        world = World(self.tmp)
        mutate(world)
        code, out, _ = self.inventory(world)
        self.assertEqual(2, code)
        self.assertEqual([f"- {expected_blocker}"], self.stop_list(out))
        if expected_flag:
            self.assertIn(expected_flag, out)

    def test_a_draft_stops(self):
        self.assert_stops(lambda w: w.mrs[1019].update(draft=True), "| draft |", "!1019: draft")

    def test_a_closed_merge_request_stops(self):
        self.assert_stops(lambda w: w.mrs[1019].update(state="closed"), "| closed |",
                          "!1019: merge request is closed")

    def test_a_hold_label_stops(self):
        self.assert_stops(lambda w: w.mrs[1019].update(labels=["do-not-merge"]), "| hold? |",
                          "!1019: title or label suggests a hold (do-not-merge)")

    def test_a_hold_word_in_the_title_stops(self):
        self.assert_stops(lambda w: w.mrs[1019].update(title="WIP: gallery"), "| hold? |",
                          "!1019: title or label suggests a hold (WIP: gallery)")

    def test_a_base_outside_the_set_stops(self):
        self.assert_stops(lambda w: w.mrs[1019].update(target_branch="release/9"), "base release/9 outside set",
                          "!1019: based on release/9, which is neither dev nor a listed merge request")

    def test_a_head_that_moved_during_the_inventory_stops(self):
        moved = sha("moved")
        self.assert_stops(lambda w: w.fetched.update({1019: moved}), None,
                          f"!1019: head moved during inventory ({sha('head 1019')[:12]} -> {moved[:12]}); rerun")

    def test_a_conflicting_merge_request_is_flagged_but_does_not_stop(self):
        world = World(self.tmp)
        world.mrs[1019].update(has_conflicts=True, detailed_merge_status="conflict")
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertIn("| conflict | none | - | conflicts with target |", out)
        self.assertNotIn("## Stop and ask", out)

    def test_a_fork_is_flagged_but_does_not_stop(self):
        world = World(self.tmp)
        world.mrs[1019]["source_project_id"] = 77
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertIn("| fork:project 77 |", out)

    def test_every_blocker_is_listed(self):
        world = World(self.tmp)
        world.mrs[1016]["draft"] = True
        world.mrs[1019]["state"] = "merged"
        code, out, _ = self.inventory(world)
        self.assertEqual(2, code)
        self.assertEqual(["- !1016: draft", "- !1019: merge request is merged"], self.stop_list(out))


class InventoryRegisterCollisions(InventoryCase):
    """Two merge requests written from the same target claim the same register id; the inventory says so up front."""

    REGISTER = "docs/rulings.md"

    def claims(self, world, number, diff):
        world.files[number].append(self.REGISTER)
        world.register_diffs[number] = diff

    @staticmethod
    def section(out):
        if "## Register id collisions\n\n" not in out:
            return None
        return out.split("## Register id collisions\n\n", 1)[1].split("\n## Pinned", 1)[0]

    @staticmethod
    def register_calls(world):
        return [call for call in world.commands if call[:3] == ["git", "diff", "-U0"] or call[:2] == ["git", "show"]]

    def test_two_merge_requests_claiming_one_id_are_listed_in_merge_order_and_do_not_stop(self):
        world = World(self.tmp)
        self.claims(world, 1019, "+| R550 | q | a |\n+| R551 | q | a |\n")
        self.claims(world, 1016, "+| R550 | other | b |\n")
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual("- R550: !1016, !1019 (first in order keeps it; renumber the rest after each merge with "
                         "renumber_register.py)\n", self.section(out))
        self.assertIn("- !1016 × !1019: 2 file(s): `docs/rulings.md`, `shared.txt`\n", out)

    def test_a_stacked_child_does_not_collide_with_the_parent_rows_it_contains(self):
        world = World(self.tmp)
        self.claims(world, 1016, "+| R550 | parent | a |\n")
        self.claims(world, 1018, "+| R550 | parent | a |\n+| R551 | child | b |\n")
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual("No two merge requests claim the same `docs/rulings.md` id.\n", self.section(out))

    def test_an_edited_row_is_not_a_claim(self):
        world = World(self.tmp)
        self.claims(world, 1016, "-| R549 | old | a |\n+| R549 | new | a |\n+| R550 | q | a |\n")
        self.claims(world, 1019, "+| R549 | mine | b |\n")
        _, out, _ = self.inventory(world)
        self.assertEqual("No two merge requests claim the same `docs/rulings.md` id.\n", self.section(out))

    def test_an_id_the_target_already_holds_reads_and_the_target(self):
        world = World(self.tmp)
        world.target_register = "| Id | Q | A |\n|---|---|---|\n| R550 | t | t |\n"
        self.claims(world, 1016, "+| R550 | q | a |\n")
        _, out, _ = self.inventory(world)
        self.assertEqual("- R550: !1016 and the target (first in order keeps it; renumber the rest after each merge "
                         "with renumber_register.py)\n", self.section(out))
        self.assertEqual([["git", "diff", "-U0", "--no-renames", sha(f"merge-base {world.head(1016)}"),
                           world.head(1016), "--", self.REGISTER],
                          ["git", "show", f"{TARGET}:{self.REGISTER}"]], self.register_calls(world))

    def test_no_section_and_no_git_call_when_no_merge_request_changes_the_register(self):
        world = World(self.tmp)
        _, out, _ = self.inventory(world)
        self.assertIsNone(self.section(out))
        self.assertEqual([], self.register_calls(world))

    def test_register_claims_sorts_by_number(self):
        with mock.patch.object(inventory, "run", lambda args, check=True: "+| R9 | a | b |\n+| R10 | c | d |\n"):
            self.assertEqual(["R9", "R10"], inventory.register_claims("base", "head", self.REGISTER,
                                                                      re.compile(r"^\|\s*(R[1-9]\d*)\s*\|")))


class InventoryNetworkCalls(InventoryCase):
    @staticmethod
    def kinds(world):
        """Each glab read by what it reads, and each git network call."""
        def kind(call):
            if call[0] == "git":
                return call[1]
            path = call[2]
            if path == PROJECT:
                return "project"
            part = re.fullmatch(r".*/(merge_requests/\d+|discussions|closes_issues|jobs|bridges)(\?.*)?", path)[1]
            return "merge_request" if part.startswith("merge_requests/") else part
        return Counter(kind(call) for call in world.commands
                       if call[0] == "glab" or call[:2] in (["git", "fetch"], ["git", "ls-remote"]))

    def test_each_merge_request_is_three_reads_and_its_pipeline_and_there_is_one_fetch(self):
        # The project, then per merge request itself, its discussions and closes_issues, then each
        # pipeline's jobs and trigger jobs: 1016 has one pipeline, 1018 a parent and a child, 1019 none.
        for count in (3, 6):
            with self.subTest(merge_requests=count):
                world = World(self.tmp)
                for number in range(1020, 1020 + count - 3):
                    world.add_merge_request(number)
                code, _, _ = self.inventory(world, numbers=tuple(world.mrs))
                self.assertEqual(0, code)
                self.assertEqual({"project": 1, "merge_request": count, "discussions": count, "closes_issues": count,
                                  "jobs": 3, "bridges": 3, "fetch": 1}, dict(self.kinds(world)))

    def test_one_fetch_brings_the_target_and_every_head_after_every_read(self):
        world = World(self.tmp)
        self.inventory(world)
        fetches = [call for call in world.commands if call[:2] == ["git", "fetch"]]
        self.assertEqual([["git", "fetch", "--quiet", "origin", "refs/heads/dev", "refs/merge-requests/1018/head",
                           "refs/merge-requests/1016/head", "refs/merge-requests/1019/head"]], fetches)
        last_read = max(i for i, call in enumerate(world.commands) if call[0] == "glab")
        self.assertLess(last_read, world.commands.index(fetches[0]))

    def test_a_number_given_twice_is_read_once(self):
        world = World(self.tmp)
        self.inventory(world, numbers=(1016, 1019, 1016))
        self.assertEqual(1, glab_paths(world).count(f"{PROJECT}/merge_requests/1016"))

    def test_a_merge_request_with_no_head_pipeline_reads_none_and_no_jobs(self):
        world = World(self.tmp)
        self.inventory(world)
        self.assertFalse([path for path in glab_paths(world) if "/pipelines/10190/" in path])
        self.assertEqual(("none", []), inventory.summarize_checks([]))


class InventoryAncestryCalls(InventoryCase):
    @staticmethod
    def ancestry(world):
        return Counter(call[2] for call in world.commands if call[:2] == ["git", "merge-base"] and len(call) > 3
                       and call[2] in ("--independent", "--is-ancestor"))

    def test_one_independent_call_over_the_heads_and_is_ancestor_only_for_the_head_it_leaves_out(self):
        # 1016 is contained in 1018, so it is the one head --independent does not return: it is asked
        # about the two other heads, 2 calls where every ordered pair cost 6.
        world = World(self.tmp)
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual({"--independent": 1, "--is-ancestor": 2}, dict(self.ancestry(world)))
        independent = next(call for call in world.commands if call[:3] == ["git", "merge-base", "--independent"])
        self.assertEqual([world.head(1018), world.head(1016), world.head(1019)], independent[3:])
        asked = [call[3:] for call in world.commands if call[:3] == ["git", "merge-base", "--is-ancestor"]]
        self.assertEqual([[world.head(1016), world.head(1018)], [world.head(1016), world.head(1019)]], asked)
        self.assertIn("- !1018 already contains !1016's head\n", out)

    def test_no_is_ancestor_call_when_every_head_is_returned(self):
        world = World(self.tmp)
        world.contained.clear()
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual({"--independent": 1}, dict(self.ancestry(world)))
        self.assertEqual("- !1018 is stacked on !1016 (base `task/widget`)\n",
                         out.split("## Stacks and ancestry\n\n", 1)[1].split("\n## Review", 1)[0])


class ContainmentOnARepository(IsolatedGitCase):
    """containment() with real git on a temporary repository: the lines, and the calls that find them."""

    def setUp(self):
        super().setUp()
        self.repo = Repository(self.tmp / "repo")
        self.repo.commit("base", {"base.txt": "base\n"})
        # A stack 1 <- 2 <- 3, then 4 and 5 each on its own branch from the base.
        self.heads = {}
        for number, start in ((1, "main"), (2, "pr1"), (3, "pr2"), (4, "main"), (5, "main")):
            self.repo.branch(f"pr{number}", start)
            self.heads[number] = self.repo.commit(f"pr{number}", {f"{number}.txt": f"{number}\n"})

    def containment(self, *numbers):
        mrs = [{"iid": number, "sha": self.heads[number]} for number in numbers]
        real, calls = subprocess.run, []

        def counting(args, *rest, **options):
            calls.append(args[2])
            return real(args, *rest, **options)

        with working_directory(self.repo.root), mock.patch.object(inventory.subprocess, "run", counting):
            lines = inventory.containment(mrs)
        return lines, dict(Counter(calls))

    def test_the_lines_follow_the_order_of_the_merge_requests_and_the_two_contained_heads_are_asked_about_the_rest(self):
        lines, calls = self.containment(3, 1, 2, 4, 5)
        self.assertEqual(["!3 already contains !1's head", "!2 already contains !1's head",
                          "!3 already contains !2's head"], lines)
        # Heads 3, 4 and 5 are returned; 1 and 2 are asked about the four other heads: 8 calls, where
        # every ordered pair of five merge requests cost 20.
        self.assertEqual({"--independent": 1, "--is-ancestor": 8}, calls)

    def test_no_is_ancestor_call_when_the_independent_call_returns_every_head(self):
        lines, calls = self.containment(4, 5, 3)
        self.assertEqual([], lines)
        self.assertEqual({"--independent": 1}, calls)

    def test_one_merge_request_is_one_independent_call(self):
        lines, calls = self.containment(2)
        self.assertEqual([], lines)
        self.assertEqual({"--independent": 1}, calls)

    def test_two_merge_requests_on_one_head_are_one_head_and_neither_contains_the_other(self):
        self.heads[11] = self.heads[1]
        lines, calls = self.containment(1, 11, 2)
        self.assertEqual(["!2 already contains !1's head", "!2 already contains !11's head"], lines)
        self.assertEqual({"--independent": 1, "--is-ancestor": 1}, calls)


class FetchedOids(InventoryCase):
    def test_every_ref_is_read_by_name_whatever_its_order_or_kind(self):
        path = self.tmp / "FETCH_HEAD"
        url = "https://gitlab.com/acme/widgets.git"
        path.write_bytes((f"{'a' * 40}\t\tbranch 'dev' of {url}\n"
                          f"{'b' * 40}\tnot-for-merge\t'refs/merge-requests/7/head' of {url}\n"
                          f"{'c' * 40}\t\ttag 'v1' of {url}\n"
                          f"{'d' * 40}\t\tbranch 'task/with space' of {url}\n").encode("utf-8"))
        with mock.patch.object(inventory, "run", lambda args, check=True: f"{path}\n"):
            self.assertEqual({"refs/heads/dev": "a" * 40, "refs/merge-requests/7/head": "b" * 40,
                              "refs/heads/task/with space": "d" * 40}, inventory.fetched_oids())


class ReviewFeedback(unittest.TestCase):
    def test_unresolved_discussions_and_comments_from_others(self):
        mr = {"author": {"username": "alice"}, "discussions": [
            discussion(note("bob", "general point", resolvable=True)),
            discussion(note("bob", "fixed?", resolvable=True, resolved=True),
                       note("carol", "reply in a resolved thread", resolvable=True, resolved=True)),
            discussion(note("dave", "thanks"), note("alice", "my own reply")),
            discussion(dict(note("bob", "mentioned in merge request !9"), system=True)),
        ]}
        threads, comments = inventory.review_feedback(mr)
        self.assertEqual([["general point"]], [[n["body"] for n in notes] for notes in threads])
        self.assertEqual(["thanks"], [c["body"] for c in comments])


class SummarizeChecks(unittest.TestCase):
    def test_rows(self):
        rows = {
            "none": (None, ("none", [])),
            "empty": ([], ("none", [])),
            "success": ([{"name": "a", "status": "success"}], ("1 ok / 0 pending / 0 failed", [])),
            "skipped counts as ok": ([{"name": "a", "status": "skipped"}], ("1 ok / 0 pending / 0 failed", [])),
            "failed": ([{"name": "unit", "status": "failed"}], ("0 ok / 0 pending / 1 failed", ["unit"])),
            "canceled counts as failed": ([{"name": "x", "status": "failed"}, {"name": "y", "status": "canceled"}],
                                          ("0 ok / 0 pending / 2 failed", ["x", "y"])),
            "running": ([{"name": "a", "status": "running"}], ("0 ok / 1 pending / 0 failed", [])),
            "created and waiting": ([{"name": "a", "status": "created"}, {"name": "b", "status": "waiting_for_resource"}],
                                    ("0 ok / 2 pending / 0 failed", [])),
            "a blocking manual job is pending": ([{"name": "a", "status": "manual", "allow_failure": False}],
                                                 ("0 ok / 1 pending / 0 failed", [])),
            "a manual job that may fail is ok": ([{"name": "a", "status": "manual", "allow_failure": True}],
                                                 ("1 ok / 0 pending / 0 failed", [])),
            "nameless failure": ([{"status": "failed"}], ("0 ok / 0 pending / 1 failed", ["?"])),
            "mixed": ([{"name": "a", "status": "success"}, {"name": "b", "status": "pending"},
                       {"name": "c", "status": "failed"}],
                      ("1 ok / 1 pending / 1 failed", ["c"])),
        }
        for name, (jobs, expected) in rows.items():
            with self.subTest(name):
                self.assertEqual(expected, inventory.summarize_checks(jobs))

    def test_one_line_collapses_whitespace_and_cuts_with_an_ellipsis(self):
        self.assertEqual("a b c", inventory.one_line("  a\n b\t c ", 10))
        self.assertEqual("abcd…", inventory.one_line("abcdefgh", 5))
        self.assertEqual("", inventory.one_line(None, 5))


class ClosingIssues(unittest.TestCase):
    def test_gitlab_closing_keywords_and_lists(self):
        rows = {
            "Closes #2117": [2117],
            "fixes: #3, #4 and #5": [3, 4, 5],
            "Resolved #6 and implements #7": [6, 7],
            "closing issue #8": [8],
            "disclose #9 and Closes other/project#10": [],
            "_Migrated from GitHub pull request #2139_": [],
            "": [],
        }
        for text, expected in rows.items():
            with self.subTest(text):
                self.assertEqual(expected, inventory.closing_issues(text))


if __name__ == "__main__":
    unittest.main()
