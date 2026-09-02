---
id: UIIMP-013
type: ticket
title: The test-ui gate costs 50 minutes of every build-affecting PR
status: implementing
area: ui-improvement
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: chore
stageEntered:
  preparing: '2026-09-02T03:09:00.895Z'
taken_at: '2026-09-02T03:21:55.240Z'
branch: task/uiimp-013-test-ui-cost
worktree: ../pegasus-worktrees/uiimp-013-test-ui-cost
claim_expires_at: '2026-09-02T03:51:55.240Z'
claim_controller: claude-code/20260901T215000Z-claude-controller/implementer-a1
lease_id: 11e0006c-506a-4d37-adc3-428d06b2e0a6
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\uiimp-013-test-ui-cost
lease_phase: implementing
lease_heartbeat_at: '2026-09-02T03:21:55.240Z'
labels:
  - ci
  - performance
groups:
  - EPIC-011
links:
  - UIIMP-005
refs:
  - docs/engineering.md
archived: false
created: '2026-08-30T12:51:09.415Z'
updated: '2026-09-02T03:21:55.240Z'
---

## What

Make the `test-ui` CI job cheaper. It currently needs a 75-minute budget and
becomes the critical path for every PR that touches the build.

## The measurements

From run `33310451221` on PR #609, the job's own log:

| Phase | Time |
| --- | --- |
| Capture — the 414-test suite | **40m23s**, 414 passed / 0 failed |
| Verify — a second pass on the committed corpus | started, killed 11s in by the timeout |
| Same capture suite locally | **18m52s** |

So the hosted Windows runner is roughly **2.1× slower** than the local machine
for this work. The capture runs at processor-count parallelism and the runner
has fewer cores.

The timeout was raised twice while landing [[UIIMP-005]] — 30 killed it
mid-capture, 45 killed it 11 seconds into verify — and **both failures were
reported as a stale corpus rather than as a timeout**, which is the worst part:
the gate's failure mode is indistinguishable from the defect it exists to catch.

## Why it is expensive

`Update-TestUiSnapshots.ps1 -Verify` runs the capture suite and then a second
`dotnet test` for the verify, and **rebuilds between them** — the job log shows
`Pegasus.Core -> …` twice. The rebuild is seconds, not minutes, so it is not the
main cost, but it is free to remove.

The real cost is running all 414 capture tests to regenerate a corpus in order
to compare it against the committed one.

## Approach

Ideas, cheapest first — none of them decided:

- Pass `--no-build` to the verify invocation; the capture already built.
- Reuse the `browser` job's Playwright run rather than paying for a second
  Chromium render of every page. `browser` already takes 11-15 minutes doing
  adjacent work.
- Capture once and publish the corpus as an artifact the verify step consumes,
  instead of two full passes in one job.
- Narrow the capture filter: the suite is
  `WebTests|Category=Browser|StaffSignInSecurityTests|TestUiFocusedRenderTests|QdosCustodialWebTests|AutomationConnectorAuthorizationTests|ImageViewingWebTests`.
  Establish which of those actually contribute captured pages.

**Do not simply raise the timeout again.** If the job cannot be made cheaper,
the honest alternative is to decide it should not run on every PR — but that is
a change to what [[UIIMP-005]] delivered and needs its own argument.

## Also fix

The timeout failure mode. A job killed by its budget currently reads as a stale
corpus. It should say it ran out of time, so the next person does not spend an
hour looking for a snapshot drift that never existed.

## Verification

- [ ] `test-ui` completes with real headroom, measured over several runs
- [ ] A genuine stale corpus still fails it (the perturbation and orphan
      injections [[UIIMP-005]] used both still exit 1)
- [ ] A timeout is distinguishable from a stale corpus in the job output
