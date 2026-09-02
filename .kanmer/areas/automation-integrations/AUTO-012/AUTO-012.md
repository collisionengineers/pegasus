---
id: AUTO-012
type: ticket
title: API-01 provider submission accept path is not atomic across its four writes
status: done
area: automation-integrations
order: 60
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-30T08:04:20.502Z'
  implementing: '2026-08-30T08:04:25.947Z'
  review: '2026-08-30T08:04:58.335Z'
  verifying: '2026-08-30T08:05:04.654Z'
  done: '2026-09-02T16:42:37.855Z'
labels:
  - API-01
  - requires-live-approval
groups:
  - EPIC-011
links:
  - TICK-058
  - TICK-060
commits:
  - 636c4669c08b10ce2820cdd047aab4472450be0c
prs:
  - '635'
deployment: production
delivery_state: integrated
delivery_branch: dev
delivery_sha: b9dcfec95f66d22623ab5ab9be72cfc974c11dc3
delivery_recorded_at: '2026-09-02T16:09:59.546Z'
archived: false
created: '2026-08-29T08:17:39.146Z'
updated: '2026-09-02T16:42:37.855Z'
---

## What

`SubmitProviderInstruction.ExecuteAsync` (`src/Pegasus.Core/ProviderApi/ProviderSubmission.cs`)
performs four independent writes before it answers 201:

1. `IProviderSubmissionStore.CreateAsync` — inserts the `ProviderSubmissions` row.
2. `IIntakeSubmission.ExecuteAsync` — durably retains the request as an intake receipt.
3. `IProviderSubmissionStore.RecordStagedReceiptAsync` — writes the staged receipt id back.
4. `IActionHistoryWriter` — appends the `Accepted` / `Replayed` history row.

Each runs in its own transaction. A process loss between any two leaves a
partial record:

- after (2): the submission row carries no `StagedReceiptId`, so
  `GET /api/provider/v1/submissions/{id}` answers `Received` forever even
  though the intake is being processed and a Case/PO may exist;
- after (3): no `Accepted` row is ever written, so the permanent history has
  no record of the submission FRD-09 says is "the attributable action actor
  in permanent history"; a later replay writes `Replayed`, never `Accepted`.

A provider that retries the same `Idempotency-Key` repairs (2) and (3),
because it never received a 201 — so the window is bounded by whether the
caller retries, not by Pegasus.

## Why this is deferred, not fixed in [[TICK-058]]

Closing it means one transaction across the provider-submission store, the
shared durable-intake path and action history — a design change to the durable
intake path that every intake lane uses, not a local fix to the Provider API.
It is not reachable today: `Features:ProviderApi` is closed, no credential has
been issued, and no provider has called the surface in any environment.

## Approach

- Decide the owner: either the submission row is written inside the durable
  intake transaction, or the staged-receipt back-reference and the history row
  are reconciled by the existing outbox/reconciliation path rather than
  written inline.
- Reuse what exists — the SQL outbox and the staged-artifact reconciliation
  function — before adding anything.

## Verification

- [ ] A killed process between each pair of writes leaves a record a retry or
      a reconciliation pass resolves, proven by a test.
- [ ] `GET` never answers `Received` for a submission whose intake was retained.
- [ ] Activation is still gated on exact-target approval.

## Notes

- Raised by the adversarial verification of [[TICK-058]] (2026-08-29); it is
  the third of that ticket's three confirmed-live P1s. The other two — the
  missing `UPDATE` grant on `ProviderSubmissions` and the pre-authentication
  rate-limit partition — were fixed in TICK-058's own PR (#594).
