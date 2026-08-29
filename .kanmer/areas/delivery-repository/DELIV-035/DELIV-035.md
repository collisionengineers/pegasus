---
id: DELIV-035
type: ticket
title: >-
  dev build is broken: ProviderSubmissionTests passes CaseId to
  QueuedIntakeStatus, which INTK-001 removed
status: done
area: delivery-repository
assignee: ''
profile: fix
stageEntered:
  review: '2026-08-29T17:21:18.441Z'
  verifying: '2026-08-29T17:21:28.844Z'
  done: '2026-08-29T17:21:34.792Z'
labels:
  - ci
  - build-break
  - urgent
groups:
  - EPIC-011
links:
  - TICK-058
  - INTK-001
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-29T15:20:22.951Z'
updated: '2026-08-29T17:21:34.792Z'
---

## What

`dotnet build ./Pegasus.slnx --configuration Release` fails on merged `dev` at
`cba29a4f`:

```
tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs(284,13):
error CS1739: The best overload for 'QueuedIntakeStatus' does not have a
parameter named 'CaseId'
```

Only `Pegasus.Core.Tests` fails; Core, Infrastructure, Web, IntegrationTests, Worker
and ArchitectureTests all build clean.

## Why it happened — a merge-order break, not a defect in either lane

Two PRs merged into `dev` within minutes of each other on 2026-08-29, and each was
green on its own CI because neither run had seen the other:

- **INTK-001** (`6c648c59`, PR #620) narrowed the record — *"narrow queued status to
  the durable facts a surface needs"* — removing `CaseId` from
  `src/Pegasus.Core/Intake/DurableIntake.cs:93`. The case id is now resolved the way
  `IntakeReceipt.CurrentCaseId` does it, rather than carried on the status.
- **TICK-058** (`63009b02`, PR #594) added
  `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs`, which constructs
  `QueuedIntakeStatus` with `CaseId: null`.

`CaseId` had been on the record since TICK-051 (`bbb7b6d4`), so the test was correct
when written.

The orchestration error was merging both in one batch without re-running CI on the
second against the first. The lesson belongs in the wave loop, not in either ticket.

## Approach

The test never asserts `CaseId` — it is a constructor argument set to `null`, and the
assertions that follow check `paused.Status`. So the correct fix is to delete the
argument, matching the narrowed record.

Do **not** re-add `CaseId` to `QueuedIntakeStatus`: INTK-001 removed it deliberately
and its case-id resolution is the single owner now. Re-adding it would restore the
duplication that ticket existed to remove.

Check whether any other file constructs `QueuedIntakeStatus` with the old arity:
`git grep -n "QueuedIntakeStatus(" -- src/ tests/`.

## Verification

- [ ] `dotnet build ./Pegasus.slnx --configuration Release` is green on the branch.
- [ ] `ProviderSubmissionTests` passes with its assertions unchanged.
- [ ] No other construction of `QueuedIntakeStatus` uses the removed parameter.
