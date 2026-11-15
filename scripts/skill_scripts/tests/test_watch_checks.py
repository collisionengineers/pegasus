"""watch_checks.py: exit codes 0 to 4 and --once, with glab and the clock replaced."""
import contextlib
import json
import io
import types
import unittest
from unittest import mock

from skill_scripts_support import load_script

watch = load_script("resolve-issue", "watch_checks")

MR_PATH = "projects/:id/merge_requests/7"
MR = {"iid": 7, "web_url": "https://gitlab.com/acme/widgets/-/merge_requests/7", "sha": "a" * 40,
      "has_conflicts": False, "detailed_merge_status": "ci_still_running",
      "head_pipeline": {"id": 100, "status": "running"}}


def job(name, status, finished="", job_id=0, **extra):
    link = f"https://gitlab.com/acme/widgets/-/jobs/{job_id}" if job_id else ""
    return {"id": job_id, "name": name, "status": status, "web_url": link, "allow_failure": False,
            "started_at": "2026-10-22T08:00:00Z", "finished_at": finished or None, **extra}


def answer(value, code=0):
    return code, json.dumps(value), ""


class Glab:
    """Answers `glab api` by path: the merge request, the parent pipeline's jobs from a queue
    (the last answer repeats), its trigger jobs, and the jobs of child pipeline 200, which triggers none."""

    def __init__(self, mr=answer(MR), polls=(), bridges=answer([]), child=answer([])):
        self.mr, self.polls, self.bridges, self.child = mr, list(polls), bridges, child
        self.calls = []

    def __call__(self, args):
        self.calls.append(args)
        path = args[2]
        if path.endswith("/merge_requests/7"):
            return self.mr
        if "/pipelines/100/jobs" in path:
            return self.polls[0] if len(self.polls) == 1 else self.polls.pop(0)
        if "/pipelines/100/bridges" in path:
            return self.bridges
        if "/pipelines/200/jobs" in path:
            return self.child
        if "/pipelines/200/bridges" in path:
            return answer([])
        raise AssertionError(f"unexpected glab call {args}")

    def paths(self):
        return [args[2] for args in self.calls]


class Clock:
    """time.time() values in order, then the last one; sleep() only records."""

    def __init__(self, *readings):
        self.readings = list(readings) or [0]
        self.sleeps = []

    def namespace(self):
        def now():
            return self.readings.pop(0) if len(self.readings) > 1 else self.readings[0]

        return types.SimpleNamespace(time=now, sleep=self.sleeps.append, strftime=lambda _format: "00:00:00")


def watch_mr(glab, clock=None, *extra):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.ExitStack() as stack:
        stack.enter_context(mock.patch.object(watch, "run", glab))
        stack.enter_context(mock.patch.object(watch, "time", (clock or Clock()).namespace()))
        stack.enter_context(mock.patch.object(watch.sys, "argv", ["watch_checks.py", "7", *extra]))
        stack.enter_context(contextlib.redirect_stdout(out))
        stack.enter_context(contextlib.redirect_stderr(err))
        code = watch.main()
    return code, out.getvalue(), err.getvalue()


class WatchChecksExitCodes(unittest.TestCase):
    def test_all_checks_passed_exits_0(self):
        glab = Glab(polls=[answer([job("unit", "success"), job("lint", "success")])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(0, code)
        self.assertIn("2 passed, 0 pending, 0 failed, 0 skipped, 2 total", out)
        self.assertIn("All checks finished and passed.", out)

    def test_skipped_checks_do_not_block_exit_0(self):
        glab = Glab(polls=[answer([job("unit", "success"), job("sql", "skipped")])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(0, code)
        self.assertIn("1 passed, 0 pending, 0 failed, 1 skipped, 2 total", out)

    def test_a_manual_job_that_may_fail_is_skipped_and_one_that_may_not_is_pending(self):
        glab = Glab(polls=[answer([job("unit", "success"), job("optional", "manual", allow_failure=True),
                                        job("gate", "manual")])])
        code, out, _ = watch_mr(glab, None, "--once")
        self.assertEqual(0, code)
        self.assertIn("1 passed, 1 pending, 0 failed, 1 skipped, 3 total", out)

    def test_first_failure_is_the_earliest_finished_and_exits_1(self):
        glab = Glab(polls=[answer([
            job("later", "failed", "2026-10-22T08:09:00Z", job_id=222),
            job("earliest", "failed", "2026-10-22T08:01:00Z", job_id=111),
            job("running", "running"),
        ])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: earliest (failed)", out)
        self.assertIn("  https://gitlab.com/acme/widgets/-/jobs/111\n", out)
        self.assertIn("log: glab api projects/:id/jobs/111/trace", out)
        self.assertEqual([MR_PATH, "projects/:id/pipelines/100/jobs?per_page=100",
                          "projects/:id/pipelines/100/bridges?per_page=100"], glab.paths(),
                         "one merge request read and one read of the pipeline, no second poll")

    def test_other_failures_are_listed_after_the_first(self):
        glab = Glab(polls=[answer([
            job("earliest", "failed", "2026-10-22T08:01:00Z", job_id=111),
            job("later", "failed", "2026-10-22T08:09:00Z", job_id=222),
        ])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(1, code)
        self.assertIn("also failed: later", out)

    def test_each_failure_is_named_once_when_the_later_listed_one_finished_first(self):
        glab = Glab(polls=[answer([
            job("later", "failed", "2026-10-22T08:09:00Z", job_id=222),
            job("earliest", "failed", "2026-10-22T08:01:00Z", job_id=111),
        ])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: earliest (failed)", out)
        self.assertIn("also failed: later", out)
        self.assertEqual(1, out.count("earliest"))
        self.assertEqual(1, out.count("later"))

    def test_a_canceled_job_counts_as_failed(self):
        glab = Glab(polls=[answer([job("deploy", "canceled", "2026-10-22T08:01:00Z", job_id=5)])])
        code, out, _ = watch_mr(glab)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: deploy (canceled)", out)

    def test_the_jobs_of_the_child_pipeline_stand_in_for_its_trigger_job(self):
        bridge = {"id": 300, "name": "lanes", "status": "failed", "downstream_pipeline": {"id": 200, "status": "failed"}}
        glab = Glab(polls=[answer([job("changes", "success", job_id=1)])], bridges=answer([bridge]),
                    child=answer([job("unit", "success", job_id=2),
                                  job("sql-integration 3/6", "failed", "2026-10-22T08:05:00Z", job_id=3)]))
        code, out, _ = watch_mr(glab, None, "--once")
        self.assertEqual(1, code)
        self.assertIn("2 passed, 0 pending, 1 failed, 0 skipped, 3 total", out)
        self.assertNotIn("lanes", out)
        self.assertIn("FIRST FAILURE: sql-integration 3/6 (failed)", out)
        self.assertIn("log: glab api projects/:id/jobs/3/trace", out)
        self.assertIn("projects/:id/pipelines/200/jobs?per_page=100", glab.paths())

    def test_a_trigger_job_that_started_no_child_pipeline_counts_as_itself(self):
        bridge = {"id": 300, "name": "lanes", "status": "failed", "finished_at": "2026-10-22T08:01:00Z",
                  "web_url": "https://gitlab.com/acme/widgets/-/jobs/300", "downstream_pipeline": None}
        glab = Glab(polls=[answer([job("changes", "success", job_id=1)])], bridges=answer([bridge]))
        code, out, _ = watch_mr(glab)
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: lanes (failed)", out)

    def test_merge_request_read_failure_exits_2(self):
        glab = Glab(mr=(1, "", "glab: 404 Not Found\n"))
        code, out, err = watch_mr(glab)
        self.assertEqual(2, code)
        self.assertIn("404 Not Found", err)
        self.assertEqual(1, len(glab.calls))

    def test_a_failing_jobs_read_exits_2(self):
        glab = Glab(polls=[(1, "", "glab: network unreachable\n")])
        code, _, err = watch_mr(glab)
        self.assertEqual(2, code)
        self.assertIn("network unreachable", err)

    def test_conflicting_merge_request_exits_3_without_polling(self):
        for name, overrides in (("has_conflicts", {"has_conflicts": True, "detailed_merge_status": "conflict"}),
                                ("merge status only", {"detailed_merge_status": "conflict"})):
            with self.subTest(name):
                glab = Glab(mr=answer(dict(MR, **overrides)))
                code, out, _ = watch_mr(glab)
                self.assertEqual(3, code)
                self.assertIn("conflicts with its target branch", out)
                self.assertIn("Resolve the conflict first.", out)
                self.assertEqual([["glab", "api", MR_PATH]], glab.calls)

    def test_deadline_with_pending_checks_exits_4(self):
        glab = Glab(polls=[answer([job("unit", "running")])])
        clock = Clock(0, 7201)
        code, out, _ = watch_mr(glab, clock, "--timeout", "120")
        self.assertEqual(4, code)
        self.assertIn("Timed out after 120 minutes with 1 check(s) pending.", out)
        self.assertEqual([], clock.sleeps)

    def test_pending_checks_sleep_one_interval_and_the_next_poll_reads_the_merge_request_again(self):
        glab = Glab(polls=[answer([job("unit", "pending")]),
                           answer([job("unit", "success")])])
        clock = Clock(0, 10, 20)
        code, out, _ = watch_mr(glab, clock, "--interval", "45")
        self.assertEqual(0, code)
        self.assertEqual([45], clock.sleeps)
        self.assertIn("1 passed, 0 pending", out)
        self.assertEqual(2, glab.paths().count(MR_PATH))

    def test_no_head_pipeline_loop_reports_and_times_out_with_exit_4(self):
        glab = Glab(mr=answer(dict(MR, head_pipeline=None)))
        clock = Clock(0, 10, 100000)
        code, out, _ = watch_mr(glab, clock, "--timeout", "1", "--interval", "30")
        self.assertEqual(4, code)
        self.assertIn("no pipeline jobs yet", out)
        self.assertEqual([30], clock.sleeps)
        self.assertIn("with 0 check(s) pending", out)
        self.assertEqual([MR_PATH, MR_PATH], glab.paths())


class WatchChecksOnce(unittest.TestCase):
    def test_once_with_pending_checks_prints_the_table_and_exits_0(self):
        glab = Glab(polls=[answer([job("unit", "running"), job("lint", "success", job_id=9)])])
        clock = Clock()
        code, out, _ = watch_mr(glab, clock, "--once")
        self.assertEqual(0, code)
        self.assertIn("1 passed, 1 pending, 0 failed, 0 skipped, 2 total", out)
        link = "https://gitlab.com/acme/widgets/-/jobs/9"
        self.assertIn(f"  {'pending':9} {'running':12} unit  \n", out)
        self.assertIn(f"  {'pass':9} {'success':12} lint  {link}", out)
        self.assertEqual([], clock.sleeps)

    def test_once_still_exits_1_on_a_failure(self):
        glab = Glab(polls=[answer([job("unit", "failed", "2026-10-22T08:01:00Z", job_id=3)])])
        code, out, _ = watch_mr(glab, None, "--once")
        self.assertEqual(1, code)
        self.assertIn("FIRST FAILURE: unit (failed)", out)

    def test_the_header_names_the_merge_request_its_head_and_merge_status(self):
        glab = Glab(polls=[answer([job("unit", "success")])])
        _, out, _ = watch_mr(glab, None, "--once")
        self.assertIn(f"Merge request !7 {MR['web_url']} head {'a' * 12} has_conflicts=false "
                      "merge_status=ci_still_running\n", out)

    def test_repo_reads_and_the_log_hint_use_the_encoded_project_path(self):
        glab = Glab(polls=[answer([job("unit", "failed", "2026-10-22T08:01:00Z", job_id=3)])])
        code, out, _ = watch_mr(glab, None, "--repo", "acme/widgets")
        self.assertEqual(1, code)
        self.assertEqual("projects/acme%2Fwidgets/merge_requests/7", glab.paths()[0])
        self.assertIn("log: glab api projects/acme%2Fwidgets/jobs/3/trace", out)


if __name__ == "__main__":
    unittest.main()
