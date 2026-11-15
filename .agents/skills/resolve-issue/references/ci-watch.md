# Watching CI on the MR

Each item here cost an earlier run time or credibility.

## Before arming anything

- Read the MR's state: `glab mr view N -F json`. `has_conflicts` and
  `detailed_merge_status` (`conflict`) show a conflict; `head_pipeline.status`
  shows the pipeline. A conflicting MR still gets a pipeline, so a conflict does
  not explain a missing one.
- Run `scripts/watch_checks.py N --once` by hand and read real output before
  running it in the background. A poller that dies on a missing tool looks the
  same as one that is still waiting.
- Never promise "I'll report when CI finishes" via `glab ci status --wait`; it
  waits for every job while an early failure sits unreported.
- CI runs on one self-hosted runner, two jobs at a time. A full code pipeline
  takes roughly 45 to 60 minutes and pipelines queue; a documents-only change
  takes a few minutes. A job that sits `pending` is waiting for the runner.

## On the first failure

- Read the failing job's log while the pipeline is still live:
  `glab ci trace <job-id>` or `glab api projects/:id/jobs/<job-id>/trace`.
  The lanes are jobs in a child pipeline of the MR pipeline's `lanes` trigger.
  If the repo has a status script (Pegasus: `scripts/Get-CiStatus.ps1`), it
  prints the failing test names from that log.
- Decide whose failure it is before fixing: a test the change did not touch,
  that also fails on the integration branch or on a sibling MR, is not this
  MR's defect. Report it with the evidence rather than "fixing" it.
- Fix on the task branch, push, re-run the watcher. Do not rebase or
  force-push a branch that has an MR; add a commit.
- A superseded or stuck pipeline is cancelled with
  `glab ci cancel pipeline <id>`. Do not push an empty commit to retrigger; it
  moves the head away from what was tested.

## The MR body and the pipeline

Editing an MR's description starts no pipeline. After a change to the
`## Migration` section, which CI reads, start a new MR pipeline:

```bash
glab api -X POST projects/:id/merge_requests/<iid>/pipelines
```

## Reading green

- A green pipeline on this MR's exact head is the evidence. Record the head SHA
  with it.
- A skipped job is not evidence. Read which jobs actually ran, including the
  lanes in the child pipeline (`lanes-selected` names them).
- Flaky-looking failures: check whether the same test failed on a recent
  integration-branch pipeline
  (`glab api "projects/:id/pipelines?ref=<base>&per_page=10"`).
