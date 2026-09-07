# Plan — MAIL-036: Wipe-time email boundary

## Objective

Cleared historical mail cannot re-enter Pegasus after cursor reset or delayed
notification; mail received at/after the wipe cutoff still enters intake.

## Starting state

Base dev 2e50fde474ce35eb32eff8677eb2327cb6aad272.
Evidence: `files/files.md`@`b593f69894566e4a`.
Existing PollApprovedInbox owns receive-time filtering on StartBoundaryUtc.
Wipe preserves poll state but removes occurrence identities; Claim can lower
the boundary and missing poll state has no post-wipe boundary.

## Governing docs

Meets FRD-08 stable mailbox identity and fail-closed intake. With the user's
7 September instruction, modifies its reset contract to retain the later
explicit wipe cutoff without changing onboarding/approval. ADR-0024's
identity and least-privilege boundary stays; no new mechanism or ADR.

## Required changes

Advance or seed existing Inbox poll-state boundaries in the wipe SQL
transaction. Preserve later boundaries across scope refresh; clear stale
cursor/lease state during maintenance. Reject a historical direct notification
before MIME fetch. Require the Worker already stopped before destructive
execution; this task does not stop it or wipe anything.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify/Add | `scripts/Invoke-IntakeDataWipe.ps1` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `scripts/Reset-IntakeMailBoundary.sql` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `tests/Pegasus.IntegrationTests/ApprovedMailboxEstateIntegrationTests.cs` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `docs/runbook.md` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `AGENTS.md` | Reset boundary and focused caller evidence; see files document. |
| Modify/Add | `.agents/skills/pegasus-wipe-intake-data/SKILL.md` | Reset boundary and focused caller evidence; see files document. |

| Modify | `src/Pegasus.Core/Intake/MailboxIntake.cs` | Exact notification completion. |
| Modify | `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | No-scan notification regression. |

## Do not modify

- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `corpus/**`
- `docs/operator-notes.md`

## Constraints

Reuse the existing boundary and Core filter; no schema, dependency, duplicate
domain rule, live reset, mailbox API mutation or copied SQL in tests.

## Ordered steps

1. Add exact reset SQL invoked with one UTC cutoff by the existing wipe
   transaction; require stopped Worker and propagate failed CLI operations.
2. Keep StartBoundaryUtc monotonic when poll scope refreshes; refuse older
   notifications before MIME read.
3. Add focused SQL and Graph tests for pre/post-cutoff, missing state and
   cursor/scope reset; update canonical workflow documentation and wipe skill.

## Acceptance checks

The real reset SQL and IApprovedInboxPollStore claim preserve onboarding,
approval and the cutoff. PollApprovedInbox remains the sole business filter.
Graph notification and expired delta token cannot retrieve old MIME; new mail
is returned. No live wipe is run and no schema/grant changes are needed.

## Commands

From ticket worktree: git diff --check. Root is the only build owner.
After one locked restore/Release build, run integration tests filtered to
ApprovedMailboxEstateIntegrationTests and ProductionGraphSourceTests.
Parse PowerShell with its AST parser without invoking wipe. Reuse this focused
evidence; no repeated full local/CI suite.

## Failure and deviation rules

Preserve and report failing checks; no assertion weakening. Stop before
unplanned source/schema or cloud changes. A live reset requires its own exact
target authorization and maintenance window.

## Stop condition

Push a bounded PR after focused checks and post-implementation report, then
independent review. No self-review or author merge. Root continues the
authorized v1 controller after the phase handoff.

## Operator clarification — 7 September, notification flow

The operator explicitly questioned the redundant per-notification delta scan.
Remove that scan from ExecuteNotificationAsync: complete/release its existing
lease without advancing the recovery cursor or claiming a completed mailbox
scan. Existing timer and lifecycle recovery own catch-up. Add the necessary
completion operation to the existing IApprovedInboxPollStore port and its only
fake, plus focused exact-notification/no-scan/cursor-retention tests. This is
part of the same mailbox replay root cause, not a new intake subsystem.

Additional expected files:

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Core/Intake/MailboxIntake.cs` | Exact notification completion without recovery scan. |
| Modify | `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Same existing fake and focused no-scan regression. |
