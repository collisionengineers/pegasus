"""inventory.py with canned gh and git answers: the blockers, summarize_checks, a Markdown golden and --json-out."""
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
LONG_TITLE = "Add the widget gallery with sortable columns and a saved layout per operator"


def sha(label: str) -> str:
    return hashlib.sha1(label.encode()).hexdigest()


TARGET = sha("dev")


def pull_request(number, base, head_ref, **overrides):
    value = {
        "number": number, "title": f"PR {number}", "url": f"https://github.com/{OWNER}/{NAME}/pull/{number}",
        "state": "OPEN", "isDraft": False, "baseRefName": base, "headRefName": head_ref,
        "headRefOid": sha(f"head {number}"), "isCrossRepository": False, "headRepositoryOwner": {"login": OWNER},
        "mergeable": "MERGEABLE", "mergeStateStatus": "CLEAN", "reviewDecision": None, "labels": [],
        "closingIssuesReferences": [], "statusCheckRollup": [], "author": {"login": "alice"},
    }
    value.update(overrides)
    return value


def feedback(threads=(), reviews=(), comments=()):
    return {"reviewThreads": {"nodes": list(threads)}, "reviews": {"nodes": list(reviews)},
            "comments": {"nodes": list(comments)}}


def graphql_node(pr, feedback_nodes):
    """A pull request as the aliased query answers it: connections under nodes, checks under the last commit."""
    node = {key: value for key, value in pr.items() if key not in ("labels", "closingIssuesReferences",
                                                                     "statusCheckRollup")}
    node["labels"] = {"nodes": pr["labels"]}
    node["closingIssuesReferences"] = {"nodes": pr["closingIssuesReferences"]}
    rollup = {"contexts": {"nodes": pr["statusCheckRollup"]}} if pr["statusCheckRollup"] else None
    node["commits"] = {"nodes": [{"commit": {"statusCheckRollup": rollup}}]}
    node.update(feedback_nodes)
    return node


class World:
    """gh and git as three pull requests see them: 1016 on dev, 1018 stacked on it, 1019 on dev.

    The fetch writes FETCH_HEAD in the directory it is given, one line per ref, as git does.
    """

    def __init__(self, directory):
        self.fetch_head = directory / "FETCH_HEAD"
        self.commands = []
        passed = {"name": "unit", "status": "COMPLETED", "conclusion": "SUCCESS"}
        self.prs = {
            1016: pull_request(1016, "dev", "task/widget", statusCheckRollup=[passed, dict(passed, name="lint")],
                               closingIssuesReferences=[{"number": 900}]),
            1018: pull_request(1018, "task/widget", "task/widget-gallery", title=LONG_TITLE,
                               reviewDecision="CHANGES_REQUESTED",
                               statusCheckRollup=[passed, {"name": "sql", "status": "COMPLETED",
                                                           "conclusion": "FAILURE"}]),
            1019: pull_request(1019, "dev", "task/other"),
        }
        self.feedback = {
            1016: feedback(
                threads=[{"isResolved": False, "isOutdated": True, "path": "a.txt", "line": 7,
                          "comments": {"nodes": [{"author": {"login": "bob"}, "body": "Please\n  rename   this"}]}},
                         {"isResolved": True, "isOutdated": False, "path": "b.txt", "line": 1,
                          "comments": {"nodes": [{"author": {"login": "bob"}, "body": "done"}]}}],
                reviews=[{"author": {"login": "bob"}, "state": "COMMENTED", "body": "Looks fine"},
                         {"author": {"login": "bob"}, "state": "APPROVED", "body": ""}],
                comments=[{"author": {"login": "carol"}, "body": "ping"},
                          {"author": {"login": "alice"}, "body": "my own note"}]),
            1018: feedback(), 1019: feedback(),
        }
        self.files = {1016: ["a.txt", "shared.txt"], 1018: ["b.txt"], 1019: ["c.txt", "shared.txt"]}
        self.counts = {1016: "2\t3", 1018: "0\t1", 1019: "5\t0"}  # behind, ahead
        self.contained = {(sha("head 1016"), sha("head 1018"))}  # 1018's head contains 1016's
        self.fetched = {}
        self.register_diffs = {}   # number -> the -U0 diff of docs/rulings.md, when the PR changes it
        self.target_register = ""  # docs/rulings.md at the target

    def add_pull_request(self, number):
        """Another pull request on dev that changes its own file."""
        self.prs[number] = pull_request(number, "dev", f"task/extra-{number}")
        self.feedback[number] = feedback()
        self.files[number] = [f"extra-{number}.txt"]
        self.counts[number] = "0\t1"

    def head(self, number):
        return self.prs[number]["headRefOid"]

    def number_of_head(self, head):
        return next(n for n in self.prs if self.head(n) == head)

    def fetch(self, refs):
        url = f"https://github.com/{OWNER}/{NAME}"
        lines = []
        for ref in refs:
            if ref.startswith("refs/pull/"):
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

    def gh_json(self, args):
        self.commands.append(["gh", *args])
        if args[:2] == ["repo", "view"]:
            return {"owner": {"login": OWNER}, "name": NAME, "defaultBranchRef": {"name": "dev"}}
        if args[:2] == ["api", "graphql"]:
            query = next(a for a in args if a.startswith("query=")).removeprefix("query=")
            numbers = [int(n) for n in re.findall(r"pr\d+: pullRequest\(number: (\d+)\)", query)]
            return {"data": {"repository": {f"pr{n}": json.loads(json.dumps(graphql_node(self.prs[n], self.feedback[n])))
                                            for n in numbers}}}
        raise AssertionError(f"unexpected gh {args}")

    def is_ancestor(self, args):
        self.commands.append(list(args))
        return types.SimpleNamespace(returncode=0 if (args[-2], args[-1]) in self.contained else 1)


class InventoryCase(IsolatedGitCase):
    maxDiff = None

    def inventory(self, world, *extra, numbers=(1018, 1016, 1019)):
        with mock.patch.object(inventory, "run", world.run), \
                mock.patch.object(inventory, "gh_json", world.gh_json), \
                mock.patch.object(inventory, "subprocess", types.SimpleNamespace(run=world.is_ancestor)):
            return run_main(inventory, ["--target", "dev", "--repo", f"{OWNER}/{NAME}", *extra,
                                        *map(str, numbers)])

    @staticmethod
    def stop_list(out):
        return out.split("## Stop and ask\n\n", 1)[1].rstrip("\n").splitlines() if "## Stop and ask" in out else []


class InventoryOutput(InventoryCase):
    def test_three_prs_with_no_blocker_exit_0_and_print_the_golden(self):
        world = World(self.tmp)
        code, out, _ = self.inventory(world)
        h = {number: world.head(number) for number in world.prs}
        self.assertEqual(0, code)
        self.assertEqual(
            "# PR inventory for `dev`\n"
            "\n"
            f"Target `dev` @ `{TARGET}` in acme/widgets (default branch: `dev`).\n"
            "\n"
            "| Order | PR | Title | Base | Head ref | Head SHA | Ahead/behind | Mergeable | Checks | Review | Flags |\n"
            "|---|---|---|---|---|---|---|---|---|---|---|\n"
            f"| 1 | #1016 | PR 1016 | dev | task/widget | `{h[1016][:12]}` | +3/-2 | MERGEABLE"
            " | 2 ok / 0 pending / 0 failed | - | - |\n"
            f"| 2 | #1018 | Add the widget gallery with sortable columns and a saved la… | task/widget"
            f" | task/widget-gallery | `{h[1018][:12]}` | +1/-0 | MERGEABLE | 1 ok / 0 pending / 1 failed"
            " | CHANGES_REQUESTED | failing checks, changes requested |\n"
            f"| 3 | #1019 | PR 1019 | dev | task/other | `{h[1019][:12]}` | +0/-5 | MERGEABLE | none | - | - |\n"
            "\n"
            "## Stacks and ancestry\n"
            "\n"
            "- #1018 is stacked on #1016 (base `task/widget`)\n"
            "- #1018 already contains #1016's head\n"
            "\n"
            "## Review feedback to read before merging\n"
            "\n"
            "- #1016: 1 unresolved thread(s), 1 review body(ies), 1 comment(s) from others\n"
            "  - thread `a.txt:7` (outdated) bob: Please rename this\n"
            "  - review COMMENTED bob: Looks fine\n"
            "  - comment carol: ping\n"
            "- #1018 failing checks: sql\n"
            "- #1018: no review feedback\n"
            "- #1019: no review feedback\n"
            "\n"
            "## Linked issues\n"
            "\n"
            "- #1016: #900\n"
            "- #1018: none linked (check the body)\n"
            "- #1019: none linked (check the body)\n"
            "\n"
            "## File overlap\n"
            "\n"
            "- #1016 × #1019: 1 file(s): `shared.txt`\n"
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
        derived = {pr["number"]: {key: pr[key] for key in (
            "mergeBase", "ahead", "behind", "changedFiles", "checkSummary", "failedChecks", "stackParent", "flags")}
            for pr in data["prs"]}
        self.assertEqual([1016, 1018, 1019], [pr["number"] for pr in data["prs"]])
        self.assertEqual({
            1016: {"mergeBase": sha(f"merge-base {world.head(1016)}"), "ahead": 3, "behind": 2,
                   "changedFiles": ["a.txt", "shared.txt"], "checkSummary": "2 ok / 0 pending / 0 failed",
                   "failedChecks": [], "stackParent": None, "flags": []},
            1018: {"mergeBase": sha(f"merge-base {world.head(1018)}"), "ahead": 1, "behind": 0,
                   "changedFiles": ["b.txt"], "checkSummary": "1 ok / 0 pending / 1 failed",
                   "failedChecks": ["sql"], "stackParent": 1016, "flags": ["failing checks", "changes requested"]},
            1019: {"mergeBase": sha(f"merge-base {world.head(1019)}"), "ahead": 0, "behind": 5,
                   "changedFiles": ["c.txt", "shared.txt"], "checkSummary": "none",
                   "failedChecks": [], "stackParent": None, "flags": []},
        }, derived)
        self.assertEqual("Please\n  rename   this",
                         data["prs"][0]["feedback"]["reviewThreads"]["nodes"][0]["comments"]["nodes"][0]["body"])


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
        self.assert_stops(lambda w: w.prs[1019].update(isDraft=True), "| draft |", "#1019: draft")

    def test_a_closed_pr_stops(self):
        self.assert_stops(lambda w: w.prs[1019].update(state="CLOSED"), "| closed |", "#1019: PR is CLOSED")

    def test_a_hold_label_stops(self):
        self.assert_stops(lambda w: w.prs[1019].update(labels=[{"name": "do-not-merge"}]), "| hold? |",
                          "#1019: title or label suggests a hold (do-not-merge)")

    def test_a_hold_word_in_the_title_stops(self):
        self.assert_stops(lambda w: w.prs[1019].update(title="WIP: gallery"), "| hold? |",
                          "#1019: title or label suggests a hold (WIP: gallery)")

    def test_a_base_outside_the_set_stops(self):
        self.assert_stops(lambda w: w.prs[1019].update(baseRefName="release/9"), "base release/9 outside set",
                          "#1019: based on release/9, which is neither dev nor a listed PR")

    def test_a_head_that_moved_during_the_inventory_stops(self):
        moved = sha("moved")
        self.assert_stops(lambda w: w.fetched.update({1019: moved}), None,
                          f"#1019: head moved during inventory ({sha('head 1019')[:12]} -> {moved[:12]}); rerun")

    def test_a_conflicting_pr_is_flagged_but_does_not_stop(self):
        world = World(self.tmp)
        world.prs[1019]["mergeable"] = "CONFLICTING"
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertIn("| conflicts-with-base (no CI runs) |", out)
        self.assertNotIn("## Stop and ask", out)

    def test_every_blocker_is_listed(self):
        world = World(self.tmp)
        world.prs[1016]["isDraft"] = True
        world.prs[1019]["state"] = "MERGED"
        code, out, _ = self.inventory(world)
        self.assertEqual(2, code)
        self.assertEqual(["- #1016: draft", "- #1019: PR is MERGED"], self.stop_list(out))


class InventoryRegisterCollisions(InventoryCase):
    """Two PRs written from the same target claim the same register id; the inventory says so up front."""

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

    def test_two_prs_claiming_one_id_are_listed_in_merge_order_and_do_not_stop(self):
        world = World(self.tmp)
        self.claims(world, 1019, "+| R550 | q | a |\n+| R551 | q | a |\n")
        self.claims(world, 1016, "+| R550 | other | b |\n")
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual("- R550: #1016, #1019 (first in order keeps it; renumber the rest after each merge with "
                         "renumber_register.py)\n", self.section(out))
        self.assertIn("- #1016 × #1019: 2 file(s): `docs/rulings.md`, `shared.txt`\n", out)

    def test_a_stacked_child_does_not_collide_with_the_parent_rows_it_contains(self):
        world = World(self.tmp)
        self.claims(world, 1016, "+| R550 | parent | a |\n")
        self.claims(world, 1018, "+| R550 | parent | a |\n+| R551 | child | b |\n")
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual("No two PRs claim the same `docs/rulings.md` id.\n", self.section(out))

    def test_an_edited_row_is_not_a_claim(self):
        world = World(self.tmp)
        self.claims(world, 1016, "-| R549 | old | a |\n+| R549 | new | a |\n+| R550 | q | a |\n")
        self.claims(world, 1019, "+| R549 | mine | b |\n")
        _, out, _ = self.inventory(world)
        self.assertEqual("No two PRs claim the same `docs/rulings.md` id.\n", self.section(out))

    def test_an_id_the_target_already_holds_reads_and_the_target(self):
        world = World(self.tmp)
        world.target_register = "| Id | Q | A |\n|---|---|---|\n| R550 | t | t |\n"
        self.claims(world, 1016, "+| R550 | q | a |\n")
        _, out, _ = self.inventory(world)
        self.assertEqual("- R550: #1016 and the target (first in order keeps it; renumber the rest after each merge "
                         "with renumber_register.py)\n", self.section(out))
        self.assertEqual([["git", "diff", "-U0", "--no-renames", sha(f"merge-base {world.head(1016)}"),
                           world.head(1016), "--", self.REGISTER],
                          ["git", "show", f"{TARGET}:{self.REGISTER}"]], self.register_calls(world))

    def test_no_section_and_no_git_call_when_no_pr_changes_the_register(self):
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
    def network(world):
        return [call[:3] if call[0] == "gh" else call[:2] for call in world.commands
                if call[0] == "gh" or call[:2] in (["git", "fetch"], ["git", "ls-remote"])]

    def test_the_network_calls_stay_three_whatever_the_number_of_pull_requests(self):
        # The repository lookup, one query for every pull request, one fetch of the target and every head;
        # recomputed per pull request it was 3 + 3 per pull request.
        for count in (3, 6):
            with self.subTest(pull_requests=count):
                world = World(self.tmp)
                for number in range(1020, 1020 + count - 3):
                    world.add_pull_request(number)
                code, _, _ = self.inventory(world, numbers=tuple(world.prs))
                self.assertEqual(0, code)
                self.assertEqual([["gh", "repo", "view"], ["gh", "api", "graphql"], ["git", "fetch"]],
                                 self.network(world))

    def test_one_fetch_brings_the_target_and_every_head_after_the_query_has_been_taken(self):
        world = World(self.tmp)
        self.inventory(world)
        fetches = [call for call in world.commands if call[:2] == ["git", "fetch"]]
        self.assertEqual([["git", "fetch", "--quiet", "origin", "refs/heads/dev",
                           "refs/pull/1018/head", "refs/pull/1016/head", "refs/pull/1019/head"]], fetches)
        queried = next(i for i, call in enumerate(world.commands) if call[:3] == ["gh", "api", "graphql"])
        self.assertLess(queried, world.commands.index(fetches[0]))

    def test_the_query_names_each_pull_request_once_and_defines_the_fields_once(self):
        query = inventory.pull_requests_query([1016, 1019, 1016])
        self.assertEqual(["1016", "1019"], re.findall(r"pr(\d+): pullRequest\(number: \d+\) \{ \.\.\.PullRequestFields \}",
                                                      query))
        self.assertEqual(1, query.count("fragment PullRequestFields on PullRequest"))

    def test_a_pull_request_with_no_checks_reads_none(self):
        node = graphql_node(pull_request(7, "dev", "task/x"), feedback())
        self.assertIsNone(node["commits"]["nodes"][0]["commit"]["statusCheckRollup"])
        pr = inventory.flatten(node)
        self.assertEqual([], pr["statusCheckRollup"])
        self.assertEqual(("none", []), inventory.summarize_checks(pr["statusCheckRollup"]))


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
        self.assertIn("- #1018 already contains #1016's head\n", out)

    def test_no_is_ancestor_call_when_every_head_is_returned(self):
        world = World(self.tmp)
        world.contained.clear()
        code, out, _ = self.inventory(world)
        self.assertEqual(0, code)
        self.assertEqual({"--independent": 1}, dict(self.ancestry(world)))
        self.assertEqual("- #1018 is stacked on #1016 (base `task/widget`)\n",
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
        prs = [{"number": number, "headRefOid": self.heads[number]} for number in numbers]
        real, calls = subprocess.run, []

        def counting(args, *rest, **options):
            calls.append(args[2])
            return real(args, *rest, **options)

        with working_directory(self.repo.root), mock.patch.object(inventory.subprocess, "run", counting):
            lines = inventory.containment(prs)
        return lines, dict(Counter(calls))

    def test_the_lines_follow_the_order_of_the_pull_requests_and_the_two_contained_heads_are_asked_about_the_rest(self):
        lines, calls = self.containment(3, 1, 2, 4, 5)
        self.assertEqual(["#3 already contains #1's head", "#2 already contains #1's head",
                          "#3 already contains #2's head"], lines)
        # Heads 3, 4 and 5 are returned; 1 and 2 are asked about the four other heads: 8 calls, where
        # every ordered pair of five pull requests cost 20.
        self.assertEqual({"--independent": 1, "--is-ancestor": 8}, calls)

    def test_no_is_ancestor_call_when_the_independent_call_returns_every_head(self):
        lines, calls = self.containment(4, 5, 3)
        self.assertEqual([], lines)
        self.assertEqual({"--independent": 1}, calls)

    def test_one_pull_request_is_one_independent_call(self):
        lines, calls = self.containment(2)
        self.assertEqual([], lines)
        self.assertEqual({"--independent": 1}, calls)

    def test_two_pull_requests_on_one_head_are_one_head_and_neither_contains_the_other(self):
        self.heads[11] = self.heads[1]
        lines, calls = self.containment(1, 11, 2)
        self.assertEqual(["#2 already contains #1's head", "#2 already contains #11's head"], lines)
        self.assertEqual({"--independent": 1, "--is-ancestor": 1}, calls)


class FetchedOids(InventoryCase):
    def test_every_ref_is_read_by_name_whatever_its_order_or_kind(self):
        path = self.tmp / "FETCH_HEAD"
        url = "https://github.com/acme/widgets"
        path.write_bytes((f"{'a' * 40}\t\tbranch 'dev' of {url}\n"
                          f"{'b' * 40}\tnot-for-merge\t'refs/pull/7/head' of {url}\n"
                          f"{'c' * 40}\t\ttag 'v1' of {url}\n"
                          f"{'d' * 40}\t\tbranch 'task/with space' of {url}\n").encode("utf-8"))
        with mock.patch.object(inventory, "run", lambda args, check=True: f"{path}\n"):
            self.assertEqual({"refs/heads/dev": "a" * 40, "refs/pull/7/head": "b" * 40,
                              "refs/heads/task/with space": "d" * 40}, inventory.fetched_oids())


class SummarizeChecks(unittest.TestCase):
    def test_rows(self):
        rows = {
            "none": (None, ("none", [])),
            "empty": ([], ("none", [])),
            "success": ([{"name": "a", "status": "COMPLETED", "conclusion": "SUCCESS"}],
                        ("1 ok / 0 pending / 0 failed", [])),
            "skipped counts as ok": ([{"name": "a", "status": "COMPLETED", "conclusion": "SKIPPED"}],
                                     ("1 ok / 0 pending / 0 failed", [])),
            "failure": ([{"name": "unit", "status": "COMPLETED", "conclusion": "FAILURE"}],
                        ("0 ok / 0 pending / 1 failed", ["unit"])),
            "timed out and cancelled": ([{"name": "x", "conclusion": "TIMED_OUT"}, {"name": "y", "conclusion": "CANCELLED"}],
                                        ("0 ok / 0 pending / 2 failed", ["x", "y"])),
            "in progress": ([{"name": "a", "status": "IN_PROGRESS", "conclusion": ""}],
                            ("0 ok / 1 pending / 0 failed", [])),
            "queued with no conclusion": ([{"name": "a", "status": "QUEUED"}], ("0 ok / 1 pending / 0 failed", [])),
            "status context pending": ([{"context": "ci/x", "state": "PENDING"}], ("0 ok / 1 pending / 0 failed", [])),
            "status context success": ([{"context": "ci/y", "state": "SUCCESS"}], ("1 ok / 0 pending / 0 failed", [])),
            "status context error": ([{"context": "ci/z", "state": "ERROR"}], ("0 ok / 0 pending / 1 failed", ["ci/z"])),
            "nameless failure": ([{"conclusion": "FAILURE"}], ("0 ok / 0 pending / 1 failed", ["?"])),
            "mixed": ([{"name": "a", "status": "COMPLETED", "conclusion": "SUCCESS"},
                       {"name": "b", "status": "IN_PROGRESS"},
                       {"name": "c", "status": "COMPLETED", "conclusion": "FAILURE"}],
                      ("1 ok / 1 pending / 1 failed", ["c"])),
        }
        for name, (rollup, expected) in rows.items():
            with self.subTest(name):
                self.assertEqual(expected, inventory.summarize_checks(rollup))

    def test_one_line_collapses_whitespace_and_cuts_with_an_ellipsis(self):
        self.assertEqual("a b c", inventory.one_line("  a\n b\t c ", 10))
        self.assertEqual("abcd…", inventory.one_line("abcdefgh", 5))
        self.assertEqual("", inventory.one_line(None, 5))


if __name__ == "__main__":
    unittest.main()
