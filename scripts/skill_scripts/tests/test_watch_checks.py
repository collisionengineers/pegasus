"""watch_checks.py: exit codes 0 to 4 and --once, with gh and the clock replaced."""
import contextlib
import json
import io
import types
import unittest
from unittest import mock

from skill_scripts_support import load_script

watch = load_script("resolve-issue", "watch_checks")

VIEW = {"mergeable": "MERGEABLE", "mergeStateStatus": "CLEAN", "headRefOid": "a" * 40,
        "url": "https://github.com/acme/widgets/pull/7"}


def check(name, bucket, state, completed="", job=""):
    link = f"https://github.com/acme/widgets/actions/runs/1/job/{job}" if job else ""
    return {"name": name, "bucket": bucket, "state": state, "link": link,
            "startedAt": "2026-10-22T08:00:00Z", "completedAt": completed}


class Gh:
    """Answers `gh pr view` once and `gh pr checks` from a queue (the last answer repeats)."""

    def __init__(self, view=(0, json.dumps(VIEW), ""), polls=()):
        self.view = view
        self.polls = list(polls)
        self.calls = []

    def __call__(self, args):
        self.calls.append(args)
        if args[:3] == ["gh", "pr", "view"]:
            return self.view
        answer = self.polls[0] if len(self.polls) == 1 else self.polls.pop(0)
        return answer


def checks_answer(checks, code=0):
    return code, json.dumps(checks), ""


class Clock:
    """time.time() values in order, then the last one; sleep() only records."""

    def __init__(self, *readings):
        self.readings = list(readings) or [0]
        self.sleeps = []

    def namespace(self):
        def now():
            return self.readings.pop(0) if len(self.readings) > 1 else self.readings[0]

        return types.SimpleNamespace(time=now, sleep=self.sleeps.append, strftime=lambda _format: "00:00:00")


def watch_pr(gh, clock=None, *extra):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.ExitStack() as stack:
        stack.enter_context(mock.patch.object(watch, "run", gh))
        stack.enter_context(mock.patch.object(watch, "time", (clock or Clock()).namespace()))
        stack.enter_context(mock.patch.object(watch.sys, "argv", ["watch_checks.py", "7", *extra]))
        stack.enter_context(contextlib.redirect_stdout(out))
        stack.enter_context(contextlib.redirect_stderr(err))
        code = watch.main()
    return code, out.getvalue(), err.getvalue()


class WatchChecksExitCodes(unittest.TestCase):
    def test_all_checks_passed_exits_0(self):
        gh = Gh(polls=[checks_answer([check("unit", "pass", "SUCCESS"), check("lint", "pass", "SUCCESS")])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(0, code)
        self.assertIn("2 passed, 0 pending, 0 failed, 0 skipped, 2 total", out)
        self.assertIn("All checks finished and passed.", out)

    def test_skipped_checks_do_not_block_exit_0(self):
        gh = Gh(polls=[checks_answer([check("unit", "pass", "SUCCESS"), check("sql", "skipping", "SKIPPED")])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(0, code)
        self.assertIn("1 passed, 0 pending, 0 failed, 1 skipped, 2 total", out)

    def test_first_failure_is_the_earliest_completed_and_exits_1(self):
        gh = Gh(polls=[checks_answer([
            check("later", "fail", "FAILURE", "2026-10-22T08:09:00Z", job="222"),
            check("earliest", "fail", "TIMED_OUT", "2026-10-22T08:01:00Z", job="111"),
            check("running", "pending", "IN_PROGRESS"),
        ])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: earliest (TIMED_OUT)", out)
        self.assertIn("log: gh api repos/{owner}/{repo}/actions/jobs/111/logs", out)
        self.assertEqual(2, len(gh.calls), "one gh pr view and one gh pr checks, no second poll")

    def test_other_failures_are_listed_after_the_first(self):
        gh = Gh(polls=[checks_answer([
            check("earliest", "fail", "FAILURE", "2026-10-22T08:01:00Z", job="111"),
            check("later", "fail", "FAILURE", "2026-10-22T08:09:00Z", job="222"),
        ])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(1, code)
        self.assertIn("also failed: later", out)

    def test_each_failure_is_named_once_when_the_later_listed_one_completed_first(self):
        gh = Gh(polls=[checks_answer([
            check("later", "fail", "FAILURE", "2026-10-22T08:09:00Z", job="222"),
            check("earliest", "fail", "FAILURE", "2026-10-22T08:01:00Z", job="111"),
        ])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: earliest (FAILURE)", out)
        self.assertIn("also failed: later", out)
        self.assertEqual(1, out.count("earliest"))
        self.assertEqual(1, out.count("later"))

    def test_a_failed_state_without_a_fail_bucket_counts_as_failed(self):
        gh = Gh(polls=[checks_answer([check("deploy", "pending", "ERROR", "2026-10-22T08:01:00Z", job="5")])])
        code, out, _ = watch_pr(gh)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: deploy (ERROR)", out)

    def test_pr_view_failure_exits_2(self):
        gh = Gh(view=(1, "", "gh: Could not resolve to a PullRequest\n"))
        code, out, err = watch_pr(gh)
        self.assertEqual(2, code)
        self.assertIn("Could not resolve", err)
        self.assertEqual(1, len(gh.calls))

    def test_empty_checks_without_the_no_checks_message_exits_2(self):
        gh = Gh(polls=[(1, "", "gh: network unreachable\n")])
        code, _, err = watch_pr(gh)
        self.assertEqual(2, code)
        self.assertIn("network unreachable", err)

    def test_conflicting_pr_exits_3_without_polling(self):
        conflicting = dict(VIEW, mergeable="CONFLICTING", mergeStateStatus="DIRTY")
        gh = Gh(view=(0, json.dumps(conflicting), ""))
        code, out, _ = watch_pr(gh)
        self.assertEqual(3, code)
        self.assertIn("no CI will run", out)
        self.assertEqual([["gh", "pr", "view", "7", "--json", "mergeable,mergeStateStatus,headRefOid,url"]], gh.calls)

    def test_deadline_with_pending_checks_exits_4(self):
        gh = Gh(polls=[checks_answer([check("unit", "pending", "IN_PROGRESS")])])
        clock = Clock(0, 7201)
        code, out, _ = watch_pr(gh, clock, "--timeout", "120")
        self.assertEqual(4, code)
        self.assertIn("Timed out after 120 minutes with 1 check(s) pending.", out)
        self.assertEqual([], clock.sleeps)

    def test_pending_checks_sleep_one_interval_before_the_next_poll(self):
        gh = Gh(polls=[checks_answer([check("unit", "pending", "QUEUED")]),
                       checks_answer([check("unit", "pass", "SUCCESS")])])
        clock = Clock(0, 10, 20)
        code, out, _ = watch_pr(gh, clock, "--interval", "45")
        self.assertEqual(0, code)
        self.assertEqual([45], clock.sleeps)
        self.assertIn("1 passed, 0 pending", out)

    def test_no_checks_loop_reports_and_times_out_with_exit_4(self):
        gh = Gh(polls=[(1, "", "no checks reported on the 'task/x' branch\n")])
        clock = Clock(0, 10, 100000)
        code, out, _ = watch_pr(gh, clock, "--timeout", "1", "--interval", "30")
        self.assertEqual(4, code)
        self.assertIn("no checks reported yet", out)
        self.assertEqual([30], clock.sleeps)
        self.assertIn("with 0 check(s) pending", out)


class WatchChecksOnce(unittest.TestCase):
    def test_once_with_pending_checks_prints_the_table_and_exits_0(self):
        gh = Gh(polls=[checks_answer([
            check("unit", "pending", "IN_PROGRESS"), check("lint", "pass", "SUCCESS", job="9")])])
        clock = Clock()
        code, out, _ = watch_pr(gh, clock, "--once")
        self.assertEqual(0, code)
        self.assertIn("1 passed, 1 pending, 0 failed, 0 skipped, 2 total", out)
        link = "https://github.com/acme/widgets/actions/runs/1/job/9"
        self.assertIn(f"  {'pending':9} {'IN_PROGRESS':12} unit  ", out)
        self.assertIn(f"  {'pass':9} {'SUCCESS':12} lint  {link}", out)
        self.assertEqual([], clock.sleeps)

    def test_once_still_exits_1_on_a_failure(self):
        gh = Gh(polls=[checks_answer([check("unit", "fail", "FAILURE", "2026-10-22T08:01:00Z", job="3")])])
        code, out, _ = watch_pr(gh, None, "--once")
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: unit (FAILURE)", out)

    def test_job_id_comes_from_the_job_segment_of_the_link(self):
        self.assertEqual("123456", watch.job_id("https://github.com/o/r/actions/runs/99/job/123456"))
        self.assertEqual("", watch.job_id("https://github.com/o/r/actions/runs/99"))
        self.assertEqual("", watch.job_id(None))


if __name__ == "__main__":
    unittest.main()
